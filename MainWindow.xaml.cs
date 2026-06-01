using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

#nullable disable

namespace QuickSurfBrowser
{
    public partial class MainWindow : Window
    {
        private const string HomePage = "https://www.google.com/search?igu=1";
        private readonly List<TabItem> _tabs = new();
        private readonly List<string> _urls = new();
        private int _counter = 1;
        private CoreWebView2Environment _env;
        private Timer _searchTimer;

        private readonly string _dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickSurf");
        private readonly string _historyPath;
        private List<HistoryItem> _history = new();

        private readonly string _tilesPath;
        private List<Tile> _tiles = new();

        public MainWindow()
        {
            InitializeComponent();
            
            _historyPath = Path.Combine(_dataPath, "history.json");
            _tilesPath = Path.Combine(_dataPath, "startpage.json");
            Directory.CreateDirectory(_dataPath);
            
            LoadHistory();
            LoadTiles();
            
            WebView.Visibility = Visibility.Collapsed;
            StartPageContainer.Visibility = Visibility.Visible;
            
            this.Loaded += (s, e) => SetupAIHelpersClickHandlers();
            _ = InitAsync();
        }

        private void SetupAIHelpersClickHandlers()
        {
            foreach (var border in FindVisualChildren<Border>(StartPageContainer))
            {
                if (border.Tag is string tagData && tagData.Contains("|"))
                {
                    var parts = tagData.Split('|');
                    if (parts.Length == 2)
                    {
                        var tile = new Tile { Title = parts[0], Url = parts[1] };
                        border.MouseLeftButtonUp += (s, e) => { ShowBrowser(); Navigate(tile.Url); };
                    }
                }
            }
        }

        private IEnumerable<T> FindVisualChildren<T>(DependencyObject obj) where T : DependencyObject
        {
            if (obj != null)
            {
                int count = VisualTreeHelper.GetChildrenCount(obj);
                for (int i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(obj, i);
                    if (child is T t) yield return t;
                    foreach (var childOfChild in FindVisualChildren<T>(child)) yield return childOfChild;
                }
            }
        }

        private async Task InitAsync()
        {
            try
            {
                _env = await CoreWebView2Environment.CreateAsync();
                await WebView.EnsureCoreWebView2Async(_env);
                WebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                CreateNewTab("Старт");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска: {ex.Message}", "QuickSurf", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            try
            {
                if (TabsControl.SelectedIndex < 0 || TabsControl.SelectedIndex >= _tabs.Count) return;

                string title = WebView.CoreWebView2.DocumentTitle;
                string url = WebView.CoreWebView2.Source;

                if (string.IsNullOrWhiteSpace(title))
                {
                    try { title = new Uri(url).Host.Replace("www.", ""); }
                    catch { title = $"Вкладка {_counter - 1}"; }
                }

                if (_tabs[TabsControl.SelectedIndex].Header is StackPanel header && header.Children.Count > 0)
                {
                    if (header.Children[0] is TextBlock tb) tb.Text = title;
                }

                AddToHistory(url, title);
            }
            catch { }
        }

        private void CreateNewTab(string title = null)
        {
            var tab = new TabItem();
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            var text = new TextBlock { Text = title ?? $"Вкладка {_counter}", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            var closeBtn = new Button { Content = "✕", Width = 18, Height = 18, Background = null, BorderThickness = new Thickness(0), Foreground = Brushes.Gray, Cursor = Cursors.Hand };
            closeBtn.Click += (s, e) => CloseTab(tab);
            header.Children.Add(text); header.Children.Add(closeBtn); tab.Header = header;
            
            _tabs.Add(tab);
            _urls.Add("");
            TabsControl.Items.Add(tab);
            TabsControl.SelectedIndex = TabsControl.Items.Count - 1;
            _counter++;
        }

        private void CloseTab(TabItem tabToClose)
        {
            int idx = _tabs.IndexOf(tabToClose); if (idx < 0) return;
            _tabs.RemoveAt(idx); _urls.RemoveAt(idx); TabsControl.Items.Remove(tabToClose);
            if (_tabs.Count == 0) CreateNewTab("Старт");
            else if (TabsControl.SelectedItem == tabToClose) TabsControl.SelectedIndex = Math.Min(idx, _tabs.Count - 1);
        }

        private void TabsControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabsControl.SelectedIndex >= 0 && TabsControl.SelectedIndex < _urls.Count)
            {
                UrlBox.Text = _urls[TabsControl.SelectedIndex];
                LoadUrl(_urls[TabsControl.SelectedIndex]);
            }
        }

        private void LoadUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (WebView.CoreWebView2 != null)
            {
                WebView.CoreWebView2.Navigate(url);
                if (TabsControl.SelectedIndex >= 0 && TabsControl.SelectedIndex < _urls.Count)
                    _urls[TabsControl.SelectedIndex] = url;
            }
        }

        private void BtnBack_Click(object s, RoutedEventArgs e) { if (WebView.Visibility == Visibility.Visible && WebView.CoreWebView2?.CanGoBack == true) WebView.CoreWebView2.GoBack(); }
        private void BtnForward_Click(object s, RoutedEventArgs e) { if (WebView.Visibility == Visibility.Visible && WebView.CoreWebView2?.CanGoForward == true) WebView.CoreWebView2.GoForward(); }
        private void BtnRefresh_Click(object s, RoutedEventArgs e) => WebView.CoreWebView2?.Reload();
        private void BtnGo_Click(object s, RoutedEventArgs e) => Navigate(UrlBox.Text);
        private void BtnNewTab_Click(object s, RoutedEventArgs e) => CreateNewTab();
        private void UrlBox_KeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter) Navigate(UrlBox.Text); }

        private void Navigate(string input)
        {
            ShowBrowser();
            input = input.Trim(); if (string.IsNullOrEmpty(input)) return;
            string url = input.Contains(".") && !input.Contains(" ") ? (input.StartsWith("http") ? input : $"https://{input}") : $"https://www.google.com/search?q={Uri.EscapeDataString(input)}";
            UrlBox.Text = url; LoadUrl(url);
        }

        private void BtnToggleAI_Click(object s, RoutedEventArgs e)
        {
            if (AISidebar.Visibility == Visibility.Visible) AISidebar.Visibility = Visibility.Collapsed;
            else
            {
                if (HistoryPanel.Visibility == Visibility.Visible) HistoryPanel.Visibility = Visibility.Collapsed;
                AISidebar.Visibility = Visibility.Visible;
                ChatInput.Focus();
            }
            UpdateSidebarWidth();
        }

        private void BtnToggleHistory_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryPanel.Visibility == Visibility.Visible) HistoryPanel.Visibility = Visibility.Collapsed;
            else
            {
                if (AISidebar.Visibility == Visibility.Visible) AISidebar.Visibility = Visibility.Collapsed;
                HistoryPanel.Visibility = Visibility.Visible;
                RefreshHistoryList();
            }
            UpdateSidebarWidth();
        }

        private void BtnCloseSidebar_Click(object s, RoutedEventArgs e)
        {
            AISidebar.Visibility = Visibility.Collapsed;
            UpdateSidebarWidth();
        }

        private void BtnCloseHistory_Click(object s, RoutedEventArgs e)
        {
            HistoryPanel.Visibility = Visibility.Collapsed;
            UpdateSidebarWidth();
        }

        private void UpdateSidebarWidth()
        {
            if (AISidebar.Visibility == Visibility.Visible || HistoryPanel.Visibility == Visibility.Visible)
                RootGrid.ColumnDefinitions[1].Width = new GridLength(400);
            else
                RootGrid.ColumnDefinitions[1].Width = new GridLength(0);
        }

        private void HistorySearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer?.Dispose();
            _searchTimer = new Timer(_ => Application.Current.Dispatcher.Invoke(() => RefreshHistoryList()), null, 200, Timeout.Infinite);
        }

        private void RefreshHistoryList()
        {
            if (HistoryList == null || _history == null) return;
            var query = (HistorySearchBox?.Text ?? "").Trim().ToLower();
            var filtered = string.IsNullOrEmpty(query) ? _history : _history.Where(h => (h.Title ?? "").ToLower().Contains(query) || (h.Url ?? "").ToLower().Contains(query)).ToList();
            HistoryList.ItemsSource = null; HistoryList.ItemsSource = filtered;
        }

        private void LoadHistory()
        {
            try { if (File.Exists(_historyPath)) { var json = File.ReadAllText(_historyPath); var items = JsonSerializer.Deserialize<List<HistoryItem>>(json); _history = items ?? new List<HistoryItem>(); } else _history = new List<HistoryItem>(); }
            catch { _history = new List<HistoryItem>(); }
            RefreshHistoryList();
        }

        private async void SaveHistory() { try { await File.WriteAllTextAsync(_historyPath, JsonSerializer.Serialize(_history)); } catch { } }

        private void AddToHistory(string url, string title)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url) || url.StartsWith("about:")) return;
                _history.RemoveAll(h => h.Url == url);
                _history.Insert(0, new HistoryItem { Url = url, Title = title ?? url, Time = DateTime.Now });
                if (_history.Count > 150) _history.RemoveAt(_history.Count - 1);
                SaveHistory(); RefreshHistoryList();
            }
            catch { }
        }

        private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Удалить всю историю посещений?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try { _history.Clear(); if (File.Exists(_historyPath)) File.Delete(_historyPath); RefreshHistoryList(); }
                catch (Exception ex) { MessageBox.Show($"Не удалось очистить историю: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        private void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (HistoryList.SelectedItem is HistoryItem item && !string.IsNullOrEmpty(item.Url))
                {
                    Navigate(item.Url);
                    HistoryPanel.Visibility = Visibility.Collapsed;
                    UpdateSidebarWidth();
                }
            }
            catch { }
        }

        private void BtnHome_Click(object sender, RoutedEventArgs e) => ShowStartPage();
        
        private void ShowStartPage()
        {
            UpdateCurrentTabTitle("Старт");
            UrlBox.Text = "";
            StartPageContainer.Visibility = Visibility.Visible;
            WebView.Visibility = Visibility.Collapsed;
            if (HistoryPanel.Visibility == Visibility.Visible) HistoryPanel.Visibility = Visibility.Collapsed;
            if (AISidebar.Visibility == Visibility.Visible) AISidebar.Visibility = Visibility.Collapsed;
            UpdateSidebarWidth();
        }

        private void ShowBrowser() 
        { 
            StartPageContainer.Visibility = Visibility.Collapsed; 
            WebView.Visibility = Visibility.Visible; 
            
            if (HistoryPanel.Visibility == Visibility.Visible) HistoryPanel.Visibility = Visibility.Collapsed;
            if (AISidebar.Visibility == Visibility.Visible) AISidebar.Visibility = Visibility.Collapsed;
            
            UpdateSidebarWidth();
        }

        private void UpdateCurrentTabTitle(string title)
        {
            if (TabsControl.SelectedIndex < 0 || TabsControl.SelectedIndex >= _tabs.Count) return;
            if (_tabs[TabsControl.SelectedIndex].Header is StackPanel header && header.Children.Count > 0 && header.Children[0] is TextBlock tb) tb.Text = title;
        }

        private void LoadTiles()
        {
            try { if (File.Exists(_tilesPath)) { var json = File.ReadAllText(_tilesPath); _tiles = JsonSerializer.Deserialize<List<Tile>>(json) ?? new List<Tile>(); } } catch { _tiles = new List<Tile>(); }
            RenderTiles();
        }

        private void SaveTiles() { try { File.WriteAllText(_tilesPath, JsonSerializer.Serialize(_tiles)); } catch { } }

        private void RenderTiles()
        {
            TilesWrapPanel.Children.Clear();
            foreach (var tile in _tiles)
            {
                var border = new Border
                {
                    Width = 140, Height = 64, Margin = new Thickness(8, 8, 8, 8), Background = Brushes.White,
                    BorderBrush = (Brush)FindResource("BorderBrush"), BorderThickness = new Thickness(1, 1, 1, 1),
                    CornerRadius = new CornerRadius(8), Cursor = Cursors.Hand, ToolTip = tile.Url, SnapsToDevicePixels = true
                };
                
                var stack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var img = new Image { Width = 28, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
                
                try { var uri = new Uri(tile.Url); img.Source = new BitmapImage(new Uri($"https://www.google.com/s2/favicons?domain={uri.Host}&sz=64")); } catch { img.Source = null; }
                
                var text = new TextBlock { Text = tile.Title, TextAlignment = TextAlignment.Left, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 12, FontWeight = FontWeights.Medium, Foreground = (Brush)FindResource("TextBrush"), VerticalAlignment = VerticalAlignment.Center, MaxWidth = 95 };
                stack.Children.Add(img); stack.Children.Add(text); border.Child = stack;
                
                border.MouseLeftButtonUp += (s, e) => { ShowBrowser(); Navigate(tile.Url); };
                border.MouseRightButtonUp += (s, e) => { if (MessageBox.Show($"Удалить \"{tile.Title}\"?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) { _tiles.Remove(tile); SaveTiles(); RenderTiles(); } };
                TilesWrapPanel.Children.Add(border);
            }
        }

        private void BtnAddTile_Click(object sender, RoutedEventArgs e) { NewTileTitle.Text = ""; NewTileUrl.Text = "https://"; AddTileForm.Visibility = Visibility.Visible; NewTileTitle.Focus(); }
        private void BtnConfirmAddTile_Click(object sender, RoutedEventArgs e)
        {
            var title = NewTileTitle.Text.Trim(); var url = NewTileUrl.Text.Trim();
            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(url) || url == "https://") { MessageBox.Show("Заполните название и ссылку.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!url.StartsWith("http")) url = "https://" + url;
            _tiles.Add(new Tile { Title = title, Url = url }); SaveTiles(); RenderTiles(); AddTileForm.Visibility = Visibility.Collapsed;
        }
        private void BtnCancelAddTile_Click(object sender, RoutedEventArgs e) { NewTileTitle.Text = ""; NewTileUrl.Text = "https://"; AddTileForm.Visibility = Visibility.Collapsed; }

        private async void BtnSendChat_Click(object s, RoutedEventArgs e) => await SendChatMessage();
        private async void ChatInput_KeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None) { e.Handled = true; await SendChatMessage(); } }
        
        private async Task SendChatMessage()
        {
            var message = ChatInput.Text.Trim(); if (string.IsNullOrWhiteSpace(message)) return;
            var provider = (AIProviderComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ChatGPT";
            AddChatMessage(message, true); ChatInput.Text = "";
            var loading = AddChatMessage("Думаю...", false, true);
            string response = await Task.Run(() => SimulateAIResponse(provider, message));
            ChatMessages.Children.Remove(loading); AddChatMessage(response, false);
        }

        private Border AddChatMessage(string text, bool isUser, bool isLoading = false)
        {
            var container = new Border { Margin = new Thickness(0, 0, 0, 15), HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left };
            var bubble = new Border { Background = isUser ? (Brush)FindResource("PrimaryBrush") : Brushes.White, CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10, 12, 10), MaxWidth = 320 };
            if (!isLoading) bubble.Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Opacity = 0.1 };
            var tb = new TextBlock { Text = text, Foreground = isUser ? Brushes.White : (Brush)FindResource("TextBrush"), FontSize = 13, TextWrapping = TextWrapping.Wrap, LineHeight = 18 };
            if (isLoading) { tb.FontStyle = FontStyles.Italic; tb.Foreground = Brushes.Gray; }
            bubble.Child = tb; container.Child = bubble; ChatMessages.Children.Add(container); ChatScrollViewer.ScrollToEnd();
            return container;
        }

        private string SimulateAIResponse(string provider, string message)
        {
            Thread.Sleep(1200);
            return provider switch
            {
                "ChatGPT" => $"[ChatGPT] Принято: \"{message.Substring(0, Math.Min(40, message.Length))}...\"\n\nПрототип. Скоро подключим OpenAI API.",
                "Claude" => $"[Claude] Анализирую...\n\nПрототип активен. Anthropic API скоро.",
                "Gemini" => $"[Gemini] Обрабатываю...\n\nGoogle Gemini API в очереди.",
                "Qwen" => $"[Qwen] Понял!\n\nQwen API подключение в разработке.",
                "DeepSeek" => $"[DeepSeek] Думаю...\n\nПрототип работает. Скоро реальные ответы.",
                "GigaChat" => $"[GigaChat] Принимаю.\n\nSber GigaChat интеграция скоро.",
                "Алиса" => $"[Алиса] Хорошо!\n\nYandex API следующим этапом.",
                "Copilot" => $"[Copilot] Анализирую...\n\nMicrosoft Copilot API скоро.",
                _ => "Выберите ИИ из списка."
            };
        }

        private void CtxBack_Click(object s, RoutedEventArgs e) => WebView.CoreWebView2?.GoBack();
        private void CtxForward_Click(object s, RoutedEventArgs e) => WebView.CoreWebView2?.GoForward();
        private void CtxRefresh_Click(object s, RoutedEventArgs e) => WebView.CoreWebView2?.Reload();
        private void CtxCopy_Click(object s, RoutedEventArgs e) => WebView.CoreWebView2?.ExecuteScriptAsync("document.execCommand('copy')");
        private async void CtxSearch_Click(object s, RoutedEventArgs e) { var t = await WebView.CoreWebView2.ExecuteScriptAsync("window.getSelection().toString()"); if (!string.IsNullOrWhiteSpace(t) && t != "\"\"") Navigate($"https://www.google.com/search?q={Uri.EscapeDataString(t)}"); }
        private async void CtxAITranslate_Click(object s, RoutedEventArgs e) { var t = await WebView.CoreWebView2.ExecuteScriptAsync("window.getSelection().toString()"); if (string.IsNullOrWhiteSpace(t) || t == "\"\"") { MessageBox.Show("Выделите текст", "AI", MessageBoxButton.OK, MessageBoxImage.Information); return; } OpenAISidebar($"Переведи на русский:\n\n{t}"); }
        private async void CtxAIExplain_Click(object s, RoutedEventArgs e) { var t = await WebView.CoreWebView2.ExecuteScriptAsync("window.getSelection().toString()"); if (string.IsNullOrWhiteSpace(t) || t == "\"\"") { MessageBox.Show("Выделите текст", "AI", MessageBoxButton.OK, MessageBoxImage.Information); return; } OpenAISidebar($"Объясни простыми словами:\n\n{t}"); }
        private async void CtxAISummarize_Click(object s, RoutedEventArgs e) { var title = WebView.CoreWebView2.DocumentTitle; var content = await WebView.CoreWebView2.ExecuteScriptAsync("document.body.innerText"); if (content.Length > 5000) content = content.Substring(0, 5000) + "..."; OpenAISidebar($"Краткое содержание \"{title}\":\n\n{content}"); }
        private async void CtxAICheckCode_Click(object s, RoutedEventArgs e) { var t = await WebView.CoreWebView2.ExecuteScriptAsync("window.getSelection().toString()"); if (string.IsNullOrWhiteSpace(t) || t == "\"\"") { MessageBox.Show("Выделите код", "AI", MessageBoxButton.OK, MessageBoxImage.Information); return; } OpenAISidebar($"Найди ошибки и улучши код:\n\n{t}"); }
        
        private void OpenAISidebar(string prompt = null) 
        { 
            AISidebar.Visibility = Visibility.Visible; 
            UpdateSidebarWidth();
            if (!string.IsNullOrWhiteSpace(prompt)) { ChatInput.Text = prompt; ChatInput.Focus(); } 
        }

        // ✅ ОБРАБОТЧИКИ PLACEHOLDER ДЛЯ ПОИСКА
        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchBox.Text == "Введите запрос...")
            {
                SearchBox.Text = "";
                SearchBox.Foreground = (Brush)FindResource("TextBrush");
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                SearchBox.Text = "Введите запрос...";
                SearchBox.Foreground = Brushes.Gray;
            }
        }

        // ✅ МЕТОДЫ ПОИСКА
        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) PerformSearch();
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            PerformSearch();
        }

        private void PerformSearch()
        {
            var query = SearchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(query) || query == "Введите запрос...") return;
            
            string selectedEngine = "🔍 Google";
            if (SearchEngineComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                selectedEngine = selectedItem.Content?.ToString() ?? "🔍 Google";
            }
            
            string searchUrl = selectedEngine switch
            {
                "🔍 Google" => $"https://www.google.com/search?q={Uri.EscapeDataString(query)}",
                "🤖 Perplexity" => $"https://www.perplexity.ai/search?q={Uri.EscapeDataString(query)}",
                "🇷🇺 Yandex" => $"https://yandex.ru/search/?text={Uri.EscapeDataString(query)}",
                "🦆 DuckDuckGo" => $"https://duckduckgo.com/?q={Uri.EscapeDataString(query)}",
                "🤖 Bing AI" => $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}&showconv=1",
                _ => $"https://www.google.com/search?q={Uri.EscapeDataString(query)}"
            };
            
            ShowBrowser();
            Navigate(searchUrl);
        }
    }

    public class HistoryItem { public string Title { get; set; } = ""; public string Url { get; set; } = ""; public DateTime Time { get; set; } }
    public class Tile { public string Title { get; set; } = ""; public string Url { get; set; } = ""; }
}