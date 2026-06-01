using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace QuickSurfBrowser.Services
{
    public class BrowserService
    {
        private readonly WebView2 _webView;
        private readonly Action<string, string> _onNavigationCompleted;

        public BrowserService(WebView2 webView, Action<string, string> onNavigationCompleted)
        {
            _webView = webView;
            _onNavigationCompleted = onNavigationCompleted;
        }

        public async Task InitializeAsync()
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync();
                await _webView.EnsureCoreWebView2Async(env);
                
                _webView.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    string title = _webView.CoreWebView2.DocumentTitle;
                    string url = _webView.CoreWebView2.Source;
                    if (string.IsNullOrWhiteSpace(title)) title = url; // Fallback
                    _onNavigationCompleted?.Invoke(url, title);
                };
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка инициализации браузера: {ex.Message}");
            }
        }

        public void Navigate(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (_webView.CoreWebView2 != null) _webView.CoreWebView2.Navigate(url);
        }

        public void GoBack() => _webView.CoreWebView2?.GoBack();
        public void GoForward() => _webView.CoreWebView2?.GoForward();
        public void Refresh() => _webView.CoreWebView2?.Reload();
        public bool CanGoBack => _webView.CoreWebView2?.CanGoBack == true;
        public bool CanGoForward => _webView.CoreWebView2?.CanGoForward == true;
    }
}