using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickSurfBrowser.Services;

#nullable disable

namespace QuickSurfBrowser
{
    public partial class MainWindow : Window
    {
        private readonly BrowserService _browser;
        private readonly TabService _tabs;
        private readonly HistoryService _history;
        private readonly TilesService _tiles;
        private readonly ChatContextService _chatContext;
        private AiWorkerService _aiWorker;
        private readonly string _dataPath;

        public MainWindow()
        {
            InitializeComponent();
            
            _dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickSurf");
            Directory.CreateDirectory(_dataPath);

            _history = new HistoryService(_dataPath);
            _tiles = new TilesService(_dataPath);
            _tabs = new TabService(TabsControl);
            _browser = new BrowserService(WebView, OnNavigationCompleted);
            _chatContext = new ChatContextService(_dataPath);
            
            // Инициализация AI-воркера (скрытый WebView2)
            _aiWorker = new AiWorkerService(AiWorkerView, _dataPath);

            _tabs.TabSwitched += OnTabSwitched;
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _history.Load();
            _tiles.Load();
            _chatContext.Load();

            RenderTiles();
            SetupAITiles();
            UpdateContextStatus();

            // Асинхронная инициализация браузеров в фоне
            _ = _browser.InitializeAsync();
            _ = _aiWorker.InitializeAsync();

            _tabs.CreateNewTab("Старт");
            ShowStartPage();
        }

        // === НАВИГАЦИЯ И ВКЛАДКИ ===

        private void OnNavigationCompleted(string url, string title)
        {
            var idx = TabsControl.SelectedIndex;
            if (idx >= 0 && idx < _tabs.Count)
            {
                var tabItem = TabsControl.Items[idx] as TabItem;
                if (tabItem?.Header is StackPanel sp && sp.Children[0] is TextBlock tb)
                    tb.Text = title;
            }
            _tabs.SetCurrentUrl(url);
            _history.Add(url, title);
        }

        private void OnTabSwitched(object sender, int index)
        {
            if (index >= 0 && index < _tabs.Count)
                UrlBox.Text = _tabs.GetCurrentUrl();
        }

        private void TabsControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabsControl.SelectedIndex >= 0 && TabsControl.SelectedIndex < _tabs.Count)
                UrlBox.Text = _tabs.GetCurrentUrl();
        }

        // === КНОПКИ НАВИГАЦИИ ===

        private void BtnBack_Click(object s, RoutedEventArgs e) => _browser.GoBack();
        private void BtnForward_Click(object s, RoutedEventArgs e) => _browser.GoForward();
        private void BtnRefresh_Click(object s, RoutedEventArgs e) => _browser.Refresh();
        private void BtnGo_Click(object s, RoutedEventArgs e) => Navigate(UrlBox.Text);
        private void BtnNewTab_Click(object s, RoutedEventArgs e) { _tabs.CreateNewTab("Новая вкладка"); ShowBrowser(); }
        private void UrlBox_KeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter) Navigate(UrlBox.Text); }

        private void Navigate(string input)
        {
            ShowBrowser();
            input = input.Trim(); if (string.IsNullOrEmpty(input)) return;
            string url = input.Contains(".") && !input.Contains(" ") ? (input.StartsWith("http") ? input : $"https://{input}") : $"https://www.google.com/search?q={Uri.EscapeDataString(input)}";
            UrlBox.Text = url; _browser.Navigate(url);
        }

        // === СТАРТОВАЯ СТРАНИЦА ===

        private void ShowStartPage()
        {
            UrlBox.Text = "";
            StartPageContainer.Visibility = Visibility.Visible;
            WebView.Visibility = Visibility.Collapsed;
            CloseSidebars();
        }

        private void ShowBrowser()
        {
            StartPageContainer.Visibility = Visibility.Collapsed;
            WebView.Visibility = Visibility.Visible;
            CloseSidebars();
        }

        private void CloseSidebars()
        {
            HistoryPanel.Visibility = Visibility.Collapsed;
            AISidebar.Visibility = Visibility.Collapsed;
            UpdateSidebarWidth();
        }

        private void UpdateSidebarWidth()
        {
            if (RootGrid.ColumnDefinitions.Count > 1)
            {
                RootGrid.ColumnDefinitions[1].Width = (AISidebar.Visibility == Visibility.Visible || HistoryPanel.Visibility == Visibility.Visible) 
                    ? new GridLength(400) 
                    : new GridLength(0);
            }
        }

        private void BtnHome_Click(object sender, RoutedEventArgs e) => ShowStartPage();

        private void RenderTiles()
        {
            TilesWrapPanel.Children.Clear();
            foreach (var tile in _tiles.Tiles)
                TilesWrapPanel.Children.Add(CreateTileControl(tile));
        }

        private Border CreateTileControl(Tile tile)
        {
            var border = new Border { Width = 140, Height = 64, Margin = new Thickness(8), Background = Brushes.White, CornerRadius = new CornerRadius(8), Cursor = Cursors.Hand, ToolTip = tile.Url, SnapsToDevicePixels = true };
            var stack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var img = new Image { Width = 28, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
            try { img.Source = new BitmapImage(new Uri($"https://www.google.com/s2/favicons?domain={new Uri(tile.Url).Host}&sz=64")); } catch { }
            var text = new TextBlock { Text = tile.Title, TextAlignment = TextAlignment.Left, FontSize = 12, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 95 };
            stack.Children.Add(img); stack.Children.Add(text); border.Child = stack;
            border.MouseLeftButtonUp += (s, e) => { ShowBrowser(); Navigate(tile.Url); };
            border.MouseRightButtonUp += (s, e) => { if (MessageBox.Show($"Удалить \"{tile.Title}\"?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { _tiles.RemoveTile(tile); RenderTiles(); } };
            return border;
        }

        private void SetupAITiles()
        {
            foreach (var border in FindVisualChildren<Border>(StartPageContainer))
            {
                if (border.Tag is string tagData && tagData.Contains("|"))
                {
                    var parts = tagData.Split('|');
                    if (parts.Length == 2) border.MouseLeftButtonUp += (s, e) => { ShowBrowser(); Navigate(parts[1]); };
                }
            }
        }

        // === ИСТОРИЯ ===

        private void BtnToggleHistory_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryPanel.Visibility == Visibility.Visible) HistoryPanel.Visibility = Visibility.Collapsed;
            else { AISidebar.Visibility = Visibility.Collapsed; HistoryPanel.Visibility = Visibility.Visible; RefreshHistoryList(); }
            UpdateSidebarWidth();
        }

        private void BtnCloseHistory_Click(object s, RoutedEventArgs e) { HistoryPanel.Visibility = Visibility.Collapsed; UpdateSidebarWidth(); }
        private void RefreshHistoryList() => HistoryList.ItemsSource = _history.Search(HistorySearchBox?.Text ?? "");
        private void HistorySearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshHistoryList();
        private void BtnClearHistory_Click(object sender, RoutedEventArgs e) { if (MessageBox.Show("Очистить историю?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { _history.Clear(); RefreshHistoryList(); } }
        private void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e) { if (HistoryList.SelectedItem is HistoryItem item) { Navigate(item.Url); HistoryPanel.Visibility = Visibility.Collapsed; UpdateSidebarWidth(); } }

        // === AI САЙДБАР & ЧАТ ===

        private void BtnToggleAI_Click(object s, RoutedEventArgs e)
        {
            if (AISidebar.Visibility == Visibility.Visible) AISidebar.Visibility = Visibility.Collapsed;
            else { HistoryPanel.Visibility = Visibility.Collapsed; AISidebar.Visibility = Visibility.Visible; ChatInput.Focus(); }
            UpdateSidebarWidth();
        }

        private void BtnCloseSidebar_Click(object s, RoutedEventArgs e) { AISidebar.Visibility = Visibility.Collapsed; UpdateSidebarWidth(); }
        
        private async void BtnSendChat_Click(object s, RoutedEventArgs e) => await SendRealChat();
        private async void ChatInput_KeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter) await SendRealChat(); }

        private async Task SendRealChat()
{
    var msg = ChatInput.Text.Trim();
    if (string.IsNullOrEmpty(msg)) return;

    AddChatBubble(msg, true);
    _chatContext.AddMessage("user", msg);
    ChatInput.Text = "";
    var loading = AddChatBubble("Думаю...", false, true);
    UpdateContextStatus();

    try
    {
        // Работаем только с Алисой
        var response = await _aiWorker.AskAsync(msg);
        
        ChatMessages.Children.Remove(loading);
        AddChatBubble(response, false);
        _chatContext.AddMessage("assistant", response);
        UpdateContextStatus();
    }
    catch (Exception ex)
    {
        ChatMessages.Children.Remove(loading);
        AddChatBubble($"⚠️ {ex.Message}\n\n💡 Убедитесь, что вы вошли в Яндекс в основной вкладке.", false);
    }
}
        private Border AddChatBubble(string text, bool isUser, bool isLoading = false)
{
    var container = new Border { Margin = new Thickness(0, 0, 0, 15), HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left };
    var bubble = new Border { Background = isUser ? (Brush)FindResource("PrimaryBrush") : Brushes.White, CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10, 12, 10), MaxWidth = 320 };
    var tb = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = isUser ? Brushes.White : (Brush)FindResource("TextBrush") };
    bubble.Child = tb; 
    container.Child = bubble; 
    ChatMessages.Children.Add(container); 
    ChatScrollViewer.ScrollToEnd(); // ✅ Прокрутка вниз после каждого сообщения
    return container;
}

        // === УПРАВЛЕНИЕ КОНТЕКСТОМ ===

        private void BtnSaveContext_Click(object s, RoutedEventArgs e)
        {
            var provider = (AIProviderComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Perplexity";
            _chatContext.SetProvider(provider);
            UpdateContextStatus();
            MessageBox.Show($"Контекст сохранён для {provider}", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnClearContext_Click(object s, RoutedEventArgs e)
{
    if (MessageBox.Show("Очистить историю диалога?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
    {
        _chatContext.Clear();
        ChatMessages.Children.Clear(); // ✅ Очищаем визуальный чат
        UpdateContextStatus();
    }
}

        private void UpdateContextStatus()
        {
            if (ContextStatusText != null)
                ContextStatusText.Text = $"Контекст: {_chatContext.MessageCount} сообщений";
        }

        // === КОНТЕКСТНОЕ МЕНЮ (ПКМ) ===

        private void CtxBack_Click(object s, RoutedEventArgs e) => _browser.GoBack();
        private void CtxForward_Click(object s, RoutedEventArgs e) => _browser.GoForward();
        private void CtxRefresh_Click(object s, RoutedEventArgs e) => _browser.Refresh();
        private void CtxCopy_Click(object s, RoutedEventArgs e) => WebView.CoreWebView2?.ExecuteScriptAsync("document.execCommand('copy')");
        private void CtxSearch_Click(object s, RoutedEventArgs e) => Navigate("https://www.google.com/search");

        private async void CtxAITranslate_Click(object s, RoutedEventArgs e) { var t = await WebView.CoreWebView2.ExecuteScriptAsync("window.getSelection().toString()"); if (!string.IsNullOrWhiteSpace(t)) OpenAISidebar($"Переведи: {t}"); }
        private async void CtxAIExplain_Click(object s, RoutedEventArgs e) { var t = await WebView.CoreWebView2.ExecuteScriptAsync("window.getSelection().toString()"); if (!string.IsNullOrWhiteSpace(t)) OpenAISidebar($"Объясни: {t}"); }
        private void CtxAISummarize_Click(object s, RoutedEventArgs e) { OpenAISidebar($"Краткое содержание: {WebView.CoreWebView2.DocumentTitle}"); }
        private async void CtxAICheckCode_Click(object s, RoutedEventArgs e) { var t = await WebView.CoreWebView2.ExecuteScriptAsync("window.getSelection().toString()"); if (!string.IsNullOrWhiteSpace(t)) OpenAISidebar($"Найди ошибки в коде: {t}"); }

        private void OpenAISidebar(string prompt = null) 
        { 
            AISidebar.Visibility = Visibility.Visible; 
            HistoryPanel.Visibility = Visibility.Collapsed; 
            UpdateSidebarWidth();
            if (!string.IsNullOrWhiteSpace(prompt)) { ChatInput.Text = prompt; ChatInput.Focus(); } 
        }

        // === ПОИСК НА СТАРТОВОЙ ===

        private void SearchBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) PerformSearch(); }
        private void SearchBox_GotFocus(object sender, RoutedEventArgs e) { if (SearchBox.Text == "Введите запрос...") SearchBox.Text = ""; }
        private void SearchBox_LostFocus(object sender, RoutedEventArgs e) { if (string.IsNullOrWhiteSpace(SearchBox.Text)) SearchBox.Text = "Введите запрос..."; }
        private void BtnSearch_Click(object sender, RoutedEventArgs e) => PerformSearch();

        private void PerformSearch()
        {
            var query = SearchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(query) || query == "Введите запрос...") return;

            var selected = (SearchEngineComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? " Google";
            string engine = selected.Split(' ').Last();

            string url = engine switch
            {
                "Google" => $"https://www.google.com/search?q={Uri.EscapeDataString(query)}",
                "Perplexity" => $"https://www.perplexity.ai/search?q={Uri.EscapeDataString(query)}",
                "Yandex" => $"https://yandex.ru/search/?text={Uri.EscapeDataString(query)}",
                "DuckDuckGo" => $"https://duckduckgo.com/?q={Uri.EscapeDataString(query)}",
                "Bing" => $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}",
                _ => $"https://www.google.com/search?q={Uri.EscapeDataString(query)}"
            };

            ShowBrowser();
            Navigate(url);
        }

        // === УПРАВЛЕНИЕ ПЛИТКАМИ (ФОРМА) ===

        private void BtnAddTile_Click(object sender, RoutedEventArgs e) { AddTileForm.Visibility = Visibility.Visible; NewTileTitle.Focus(); }
        private void BtnConfirmAddTile_Click(object sender, RoutedEventArgs e)
        {
            var title = NewTileTitle.Text.Trim(); var url = NewTileUrl.Text.Trim();
            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(url)) { _tiles.AddTile(title, url); RenderTiles(); }
            AddTileForm.Visibility = Visibility.Collapsed; NewTileTitle.Text = ""; NewTileUrl.Text = "";
        }
        private void BtnCancelAddTile_Click(object sender, RoutedEventArgs e) { AddTileForm.Visibility = Visibility.Collapsed; }

        // === ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ===

        private IEnumerable<T> FindVisualChildren<T>(DependencyObject obj) where T : DependencyObject
        {
            if (obj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
                {
                    var child = VisualTreeHelper.GetChild(obj, i);
                    if (child is T t) yield return t;
                    foreach (var c in FindVisualChildren<T>(child)) yield return c;
                }
            }
        }
    }
}