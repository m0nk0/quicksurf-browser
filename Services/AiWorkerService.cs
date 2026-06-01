using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

#nullable disable

namespace QuickSurfBrowser.Services
{
    public class AiWorkerService
    {
        private readonly WebView2 _worker;
        private TaskCompletionSource<string> _responseTcs;
        private readonly string _userDataPath;
        private readonly string _url = "https://alice.yandex.ru/";

        public AiWorkerService(WebView2 workerView, string dataPath)
        {
            _worker = workerView;
            _userDataPath = dataPath;
            Directory.CreateDirectory(_userDataPath);
        }

        public async Task InitializeAsync()
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(null, _userDataPath);
                await _worker.EnsureCoreWebView2Async(env);

                _worker.CoreWebView2.WebMessageReceived += (s, e) =>
                {
                    var msg = e.TryGetWebMessageAsString();
                    if (msg.StartsWith("AI_RESPONSE:"))
                        _responseTcs?.TrySetResult(FormatResponse(msg.Substring("AI_RESPONSE:".Length)));
                    else if (msg.StartsWith("AI_ERROR:"))
                        _responseTcs?.TrySetException(new InvalidOperationException(msg.Substring("AI_ERROR:".Length)));
                };

                _worker.CoreWebView2.Navigate(_url);
                await WaitForPageLoad();
                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}", "QuickSurf", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ✅ ИСПРАВЛЕННОЕ ФОРМАТИРОВАНИЕ: умная обрезка без потери смысла
        private string FormatResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response)) return response;

            // 1. Форматируем погоду
            if (response.Contains("Сегодня") && response.Contains("°"))
            {
                var lines = response.Split('\n', '\r')
                    .Where(l => !string.IsNullOrWhiteSpace(l) && !l.Trim().All(char.IsDigit))
                    .ToList();
                var relevantLines = lines.TakeWhile((line, index) => 
                    index < 3 || (!line.Contains("00:00") && !line.Contains("01:00")))
                    .Take(5);
                return string.Join("\n", relevantLines).Trim();
            }

            // 2. Умная обрезка длинных ответов (лимит 1000 символов)
            if (response.Length > 1000)
            {
                var truncated = response.Substring(0, 1000);
                var lastPunctuation = Math.Max(
                    truncated.LastIndexOf('.'),
                    Math.Max(truncated.LastIndexOf('!'), truncated.LastIndexOf('?'))
                );

                // Обрезаем на последнем полном предложении
                if (lastPunctuation > 100)
                    return truncated.Substring(0, lastPunctuation + 1).Trim();
                else
                    return truncated.Trim() + "...";
            }

            // 3. Короткие ответы возвращаем как есть
            return response.Trim();
        }

        public async Task<string> AskAsync(string prompt)
        {
            if (_worker.CoreWebView2 == null) throw new InvalidOperationException("Алиса не готова");
            _responseTcs = new TaskCompletionSource<string>();

            var safePrompt = prompt.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\"", "\\\"");

            var script = $@"
(async function() {{
    console.log('[AI] Start:', '{safePrompt}');
    try {{
        let input = document.querySelector('textarea');
        if(!input) {{
            window.chrome.webview.postMessage('AI_ERROR:Поле ввода не найдено');
            return;
        }}

        let getLastBotMessage = function() {{
            // Level 1: Specific selectors
            var specific = ['.ChatMessage_role_bot', '[data-testid=""bot-message""]', '.message_bot', '[role=""article""]', '.AliceChat-Message', '.chat-message'];
            for(var i = 0; i < specific.length; i++) {{
                var els = document.querySelectorAll(specific[i]);
                if(els.length > 0) {{
                    var txt = els[els.length-1].innerText.trim();
                    if(txt && txt.length > 10) return txt;
                }}
            }}
            
            // Level 2: General class matchers
            var general = document.querySelectorAll('div[class*=""message""], div[class*=""Message""], div[class*=""chat""], div[class*=""Chat""]');
            if(general.length > 0) {{
                var candidates = Array.prototype.slice.call(general).slice(-3);
                var best = null;
                var maxLen = 0;
                for(var j = 0; j < candidates.length; j++) {{
                    var t = candidates[j].innerText ? candidates[j].innerText.trim() : '';
                    if(t.length > maxLen) {{ maxLen = t.length; best = candidates[j]; }}
                }}
                if(best) {{
                    var txt = best.innerText.trim();
                    if(txt && txt.length > 10) return txt;
                }}
            }}
            
            // Level 3: Fallback - elements in lower half of screen
            var allText = document.querySelectorAll('p, div, span');
            var recent = [];
            for(var k = 0; k < allText.length; k++) {{
                var el = allText[k];
                var rect = el.getBoundingClientRect();
                var txt = el.innerText ? el.innerText.trim() : '';
                if(rect.top > window.innerHeight * 0.5 && txt.length > 20 && !el.querySelector('input, textarea, button')) {{
                    recent.push(txt);
                }}
            }}
            if(recent.length > 0) return recent[recent.length-1];
            
            return '';
        }};
        
        var oldText = getLastBotMessage();
        console.log('[AI] Before:', oldText ? oldText.substring(0, 40) : '(empty)');
        
        // Input and send
        input.value = '';
        input.dispatchEvent(new Event('input', {{bubbles:true}}));
        input.value = '{safePrompt}';
        input.dispatchEvent(new Event('input', {{bubbles:true}}));
        input.dispatchEvent(new Event('change', {{bubbles:true}}));
        
        await new Promise(function(r) {{ setTimeout(r, 400); }});
        
        var btn = document.querySelector('button[type=""submit""], [aria-label=""Отправить""], button[class*=""send""]');
        if(btn && !btn.disabled) btn.click();
        else input.dispatchEvent(new KeyboardEvent('keydown', {{key:'Enter', code:'Enter', bubbles:true}}));
        
        // Wait for field clear
        for(var w = 0; w < 20; w++) {{
            await new Promise(function(r) {{ setTimeout(r, 200); }});
            if(input.value.trim() === '') break;
        }}
        
        console.log('[AI] Waiting for response...');
        
        // Wait for new answer
        for(var i = 0; i < 70; i++) {{
            await new Promise(function(r) {{ setTimeout(r, 500); }});
            var newText = getLastBotMessage();
            
            if(newText && newText !== oldText && newText.length > 20 && newText.indexOf('{safePrompt}') === -1) {{
                // Stability check
                await new Promise(function(r) {{ setTimeout(r, 800); }});
                var finalText = getLastBotMessage();
                if(finalText === newText) {{
                    console.log('[AI] Got answer:', finalText.substring(0, 80));
                    window.chrome.webview.postMessage('AI_RESPONSE:' + finalText);
                    return;
                }}
            }}
        }}
        
        window.chrome.webview.postMessage('AI_ERROR:Timeout waiting for response');
    }} catch(e) {{
        console.log('[AI] Exception:', e.message);
        window.chrome.webview.postMessage('AI_ERROR:' + e.message);
    }}
}})();
            ";

            await _worker.CoreWebView2.ExecuteScriptAsync(script);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(50));
            try
            {
                using var reg = cts.Token.Register(() => _responseTcs?.TrySetCanceled());
                return await _responseTcs.Task;
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("Алиса не ответила вовремя.");
            }
        }

        private async Task WaitForPageLoad()
        {
            if (_worker.CoreWebView2 == null) return;
            for (int i = 0; i < 40; i++)
            {
                var ready = await _worker.CoreWebView2.ExecuteScriptAsync("document.readyState === 'complete' ? 'yes' : 'no'");
                if (ready.Contains("yes")) return;
                await Task.Delay(250);
            }
        }
    }
}