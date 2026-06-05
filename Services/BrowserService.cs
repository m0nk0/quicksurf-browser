using System;
using System.Threading.Tasks;
using System.Windows;
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
                
                // 1. Обработка новых окон
                _webView.CoreWebView2.NewWindowRequested += (s, e) =>
                {
                    e.Handled = true;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (Application.Current.MainWindow is MainWindow mainWindow)
                        {
                            mainWindow.NavigateInNewTab(e.Uri);
                        }
                    });
                };
                
                // 2. JavaScript для перехвата всех кликов по ссылкам
                string script = @"
                    (function() {
                        document.addEventListener('click', function(e) {
                            let target = e.target.closest('a');
                            if (target && target.href) {
                                let url = target.href;
                                let isExternal = target.target === '_blank' || 
                                                (url.indexOf(window.location.hostname) === -1);
                                
                                if (isExternal || target.target === '_blank') {
                                    e.preventDefault();
                                    e.stopPropagation();
                                    window.chrome.webview.postMessage('OPEN_IN_NEW_TAB:' + url);
                                }
                            }
                        });
                    })();
                ";
                
                await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
                await _webView.CoreWebView2.ExecuteScriptAsync(script);
                
                // 3. Обработка сообщений от JavaScript
                _webView.CoreWebView2.WebMessageReceived += (s, e) =>
                {
                    var message = e.TryGetWebMessageAsString();
                    if (message != null && message.StartsWith("OPEN_IN_NEW_TAB:"))
                    {
                        var url = message.Substring("OPEN_IN_NEW_TAB:".Length);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Application.Current.MainWindow is MainWindow mainWindow)
                            {
                                mainWindow.NavigateInNewTab(url);
                            }
                        });
                    }
                };
                
                _webView.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    string title = _webView.CoreWebView2.DocumentTitle;
                    string url = _webView.CoreWebView2.Source;
                    if (string.IsNullOrWhiteSpace(title)) title = url;
                    _onNavigationCompleted?.Invoke(url, title);
                    
                    // Заново инжектируем скрипт после навигации
                    _webView.CoreWebView2.ExecuteScriptAsync(script);
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