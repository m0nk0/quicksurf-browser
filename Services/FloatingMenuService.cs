using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;

namespace QuickSurfBrowser.Services
{
    public class FloatingMenuService
    {
        private readonly WebView2 _webView;
        private readonly Action<string> _onAskAI;

        public FloatingMenuService(WebView2 webView, Action<string> onAskAI)
        {
            _webView = webView;
            _onAskAI = onAskAI;
        }

        public async Task InitializeAsync()
        {
            try
            {
                if (_webView.CoreWebView2 == null)
                {
                    await Task.Delay(1000);
                    if (_webView.CoreWebView2 == null) return;
                }
                
                // Подписываемся на сообщения
                _webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                
                // Разрешаем скриптам отправлять сообщения
                _webView.CoreWebView2.Settings.IsScriptEnabled = true;
                
                // Читаем JavaScript из файла
                string jsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "floating-menu.js");
                if (File.Exists(jsPath))
                {
                    string script = File.ReadAllText(jsPath);
                    await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
                    await _webView.CoreWebView2.ExecuteScriptAsync(script);
                    System.Windows.MessageBox.Show("Floating menu injected successfully!");
                }
                else
                {
                    System.Windows.MessageBox.Show($"JS file not found: {jsPath}");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }
        
        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var message = e.TryGetWebMessageAsString();
                System.Windows.MessageBox.Show($"Message received: {message}");
                
                if (message != null && message.StartsWith("AI_SELECTION:"))
                {
                    var selectedText = message.Substring("AI_SELECTION:".Length);
                    System.Windows.MessageBox.Show($"Selected text: {selectedText}");
                    _onAskAI?.Invoke(selectedText);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }
    }
}