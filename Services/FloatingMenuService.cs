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
                if (_webView.CoreWebView2 == null) return;
                
                _webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                _webView.CoreWebView2.Settings.IsScriptEnabled = true;
                
                string jsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "floating-menu.js");
                if (File.Exists(jsPath))
                {
                    string script = File.ReadAllText(jsPath);
                    await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
                    await _webView.CoreWebView2.ExecuteScriptAsync(script);
                }
            }
            catch (Exception)
            {
                // Тихо
            }
        }
        
        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var message = e.TryGetWebMessageAsString();
                if (message != null && message.StartsWith("AI_SELECTION:"))
                {
                    var selectedText = message.Substring("AI_SELECTION:".Length);
                    _onAskAI?.Invoke(selectedText);
                }
            }
            catch (Exception)
            {
                // Тихо
            }
        }
    }
}