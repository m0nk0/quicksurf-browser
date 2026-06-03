using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickSurfBrowser.Models;
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
        private readonly ObservableCollection<TabItemModel> _tabsCollection = new();

        public ObservableCollection<TabItemModel> TabsCollection => _tabsCollection;

        public MainWindow()
        {
            InitializeComponent();
            
            _dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickSurf");
            Directory.CreateDirectory(_dataPath);

            _history = new HistoryService(_dataPath);
            _tiles = new TilesService(_dataPath);
            _tabs = new TabService(_tabsCollection);
            _browser = new BrowserService(WebView, OnNavigationCompleted);
            _chatContext = new ChatContextService(_dataPath);
            
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
            SetupModelTiles();
            SetupMediaTiles();  // ✅ Обработчики для панели "AI Генераторы"
            UpdateContextStatus();

            _ = _browser.InitializeAsync();
            _ = _aiWorker.InitializeAsync();

            _tabs.CreateNewTab("Старт");
            ShowStartPage();
        }

        // === НАВИГАЦИЯ И ВКЛАДКИ ===

        private void OnNavigationCompleted(string url, string title)
        {
            if (_tabs.SelectedTab != null)
            {
                _tabs.SetCurrentUrl(url);
                if (!string.IsNullOrWhiteSpace(title))
                    _tabs.SetCurrentTitle(title);
            }
            _history.Add(url, title);
        }

        private void OnTabSwitched(object sender, int index)
        {
            if (_tabs.SelectedTab != null)
            {
                UrlBox.Text = _tabs.GetCurrentUrl();
                
                if (_tabs.SelectedTab.Title == "Старт" && string.IsNullOrEmpty(_tabs.SelectedTab.Url))
                    ShowStartPage();
                else
                    ShowBrowser();
            }
        }

        // === ОБРАБОТЧИКИ ВКЛАДОК ===

        private void Tab_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is TabItemModel tab)
                _tabs.SelectTab(tab);
        }

        private void Tab_CloseClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TabItemModel tab)
            {
                e.Handled = true;
                _tabs.CloseTab(tab);
            }
        }

        // === КНОПКИ НАВИГАЦИИ ===

        private void BtnBack_Click(object s, RoutedEventArgs e) => _browser.GoBack();
        private void BtnForward_Click(object s, RoutedEventArgs e) => _browser.GoForward();
        private void BtnRefresh_Click(object s, RoutedEventArgs e) => _browser.Refresh();
        private void BtnGo_Click(object s, RoutedEventArgs e) => Navigate(UrlBox.Text);
        private void UrlBox_KeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter) Navigate(UrlBox.Text); }

        private void UrlBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null && !textBox.IsKeyboardFocused)
            {
                textBox.Focus();
                textBox.SelectAll();
                e.Handled = true;
            }
            else if (textBox != null)
            {
                textBox.SelectAll();
                e.Handled = true;
            }
        }

        private void UrlBox_GotFocus(object sender, RoutedEventArgs e)
        {
            UrlBox.SelectAll();
        }

        private void BtnCreateTab_Click(object s, RoutedEventArgs e)
        {
            _tabs.CreateNewTab("Новая вкладка");
            ShowBrowser();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
            {
                BtnCreateTab_Click(this, e);
                e.Handled = true;
            }
            else if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (_tabs.SelectedTab != null)
                    _tabs.CloseTab(_tabs.SelectedTab);
                e.Handled = true;
            }
            else if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)
            {
                UrlBox.Focus();
                UrlBox.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.Tab && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                if (_tabs.Count > 0)
                {
                    int prevIndex = (_tabs.SelectedIndex - 1 + _tabs.Count) % _tabs.Count;
                    _tabs.SelectTabByIndex(prevIndex);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (_tabs.Count > 0)
                {
                    int nextIndex = (_tabs.SelectedIndex + 1) % _tabs.Count;
                    _tabs.SelectTabByIndex(nextIndex);
                }
                e.Handled = true;
            }
        }

        private void Navigate(string input)
        {
            ShowBrowser();
            input = input.Trim(); 
            if (string.IsNullOrEmpty(input)) return;
            string url = input.Contains(".") && !input.Contains(" ") 
                ? (input.StartsWith("http") ? input : $"https://{input}") 
                : $"https://www.google.com/search?q={Uri.EscapeDataString(input)}";
            UrlBox.Text = url;
            _browser.Navigate(url);
            _tabs.SetCurrentUrl(url);
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
                RootGrid.ColumnDefinitions[1].Width = 
                    (AISidebar.Visibility == Visibility.Visible || HistoryPanel.Visibility == Visibility.Visible) 
                        ? new GridLength(400) 
                        : new GridLength(0);
            }
        }

        private void BtnHome_Click(object sender, RoutedEventArgs e) => ShowStartPage();

        // === ПЛИТКИ ===

        private void RenderTiles()
        {
            TilesWrapPanel.Children.Clear();
            
            foreach (var tile in _tiles.Tiles)
                TilesWrapPanel.Children.Add(CreateTile(tile.Title, tile.Url, canDelete: true));
        }

        private Border CreateTile(string title, string url, bool canDelete)
        {
            var border = new Border 
            { 
                Width = 80, 
                Height = 80, 
                Margin = new Thickness(5), 
                Background = Brushes.White, 
                BorderBrush = (Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12), 
                Cursor = Cursors.Hand, 
                ToolTip = url, 
                SnapsToDevicePixels = true 
            };
            
            var stack = new StackPanel 
            { 
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center, 
                VerticalAlignment = VerticalAlignment.Center 
            };
            
            var img = new Image { Width = 32, Height = 32, Margin = new Thickness(0, 0, 0, 4) };
            try { img.Source = new BitmapImage(new Uri($"https://www.google.com/s2/favicons?domain={new Uri(url).Host}&sz=64")); } catch { }
            
            var text = new TextBlock 
            { 
                Text = title, 
                TextAlignment = TextAlignment.Center, 
                FontSize = 10, 
                FontWeight = FontWeights.Medium 
            };
            
            stack.Children.Add(img); 
            stack.Children.Add(text); 
            border.Child = stack;
            
            border.MouseLeftButtonUp += (s, e) => { ShowBrowser(); Navigate(url); };
            
            if (canDelete)
            {
                border.MouseRightButtonUp += (s, e) => 
                { 
                    if (MessageBox.Show($"Удалить \"{title}\"?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes) 
                    { 
                        var tileToRemove = _tiles.Tiles.FirstOrDefault(t => t.Url == url);
                        if (tileToRemove != null)
                        {
                            _tiles.RemoveTile(tileToRemove);
                            RenderTiles();
                        }
                    } 
                    e.Handled = true;
                };
            }
            
            return border;
        }

        // ✅ Обработчики для плиток ИИ-помощников + удаление через ПКМ
        private void SetupAITiles()
        {
            foreach (var border in FindVisualChildren<Border>(StartPageContainer))
            {
                if (border.Tag is string tagData && tagData.Contains("|") && !tagData.StartsWith("model|") && !tagData.StartsWith("media|"))
                {
                    var parts = tagData.Split('|');
                    if (parts.Length == 2)
                    {
                        string url = parts[1];
                        border.MouseLeftButtonUp += (s, e) => { ShowBrowser(); Navigate(url); };
                        
                        // ✅ Удаление через ПКМ
                        border.MouseRightButtonUp += (s, e) => 
                        { 
                            if (MessageBox.Show($"Скрыть \"{parts[0]}\"?\n(Вернётся при следующем запуске)", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) 
                            { 
                                border.Visibility = Visibility.Collapsed;
                            } 
                            e.Handled = true;
                        };
                    }
                }
            }
        }

        // ✅ Обработчики для плиток панели "ИИ модели" + удаление через ПКМ
        private void SetupModelTiles()
        {
            foreach (var border in FindVisualChildren<Border>(StartPageContainer))
            {
                if (border.Tag is string tagData && tagData.StartsWith("model|"))
                {
                    var parts = tagData.Split('|');
                    if (parts.Length == 2)
                    {
                        string url = parts[1];
                        border.MouseLeftButtonUp += (s, e) => { ShowBrowser(); Navigate(url); };
                        
                        // ✅ Удаление через ПКМ
                        border.MouseRightButtonUp += (s, e) => 
                        { 
                            var textBlock = FindVisualChild<TextBlock>(border);
                            string name = textBlock?.Text ?? "Эту карточку";
                            if (MessageBox.Show($"Скрыть \"{name}\"?\n(Вернётся при следующем запуске)", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) 
                            { 
                                border.Visibility = Visibility.Collapsed;
                            } 
                            e.Handled = true;
                        };
                    }
                }
            }
        }

        // ✅ Обработчики для плиток панели "AI Генераторы" + удаление через ПКМ
        private void SetupMediaTiles()
        {
            foreach (var border in FindVisualChildren<Border>(StartPageContainer))
            {
                if (border.Tag is string tagData && tagData.StartsWith("media|"))
                {
                    var parts = tagData.Split('|');
                    if (parts.Length == 2)
                    {
                        string url = parts[1];
                        border.MouseLeftButtonUp += (s, e) => { ShowBrowser(); Navigate(url); };
                        
                        // ✅ Удаление через ПКМ
                        border.MouseRightButtonUp += (s, e) => 
                        { 
                            var textBlock = FindVisualChild<TextBlock>(border);
                            string name = textBlock?.Text ?? "Эту карточку";
                            if (MessageBox.Show($"Скрыть \"{name}\"?\n(Вернётся при следующем запуске)", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) 
                            { 
                                border.Visibility = Visibility.Collapsed;
                            } 
                            e.Handled = true;
                        };
                    }
                }
            }
        }

        // ✅ Вспомогательный метод для поиска дочернего элемента
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }

        // === ИСТОРИЯ ===

        private void BtnToggleHistory_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryPanel.Visibility == Visibility.Visible) 
            { 
                HistoryPanel.Visibility = Visibility.Collapsed; 
            }
            else 
            { 
                AISidebar.Visibility = Visibility.Collapsed; 
                HistoryPanel.Visibility = Visibility.Visible; 
                RefreshHistoryList(); 
            }
            UpdateSidebarWidth();
        }

        private void BtnCloseHistory_Click(object s, RoutedEventArgs e) 
        { 
            HistoryPanel.Visibility = Visibility.Collapsed; 
            UpdateSidebarWidth(); 
        }

        private void RefreshHistoryList() => HistoryList.ItemsSource = _history.Search(HistorySearchBox?.Text ?? "");
        private void HistorySearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshHistoryList();
        
        private void BtnClearHistory_Click(object sender, RoutedEventArgs e) 
        { 
            if (MessageBox.Show("Очистить историю?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes) 
            { 
                _history.Clear(); 
                RefreshHistoryList(); 
            } 
        }

        private void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e) 
        { 
            if (HistoryList.SelectedItem is HistoryItem item) 
            { 
                Navigate(item.Url); 
                HistoryPanel.Visibility = Visibility.Collapsed; 
                UpdateSidebarWidth(); 
            } 
        }

        // === AI САЙДБАР & ЧАТ ===

        private void BtnToggleAI_Click(object s, RoutedEventArgs e)
        {
            if (AISidebar.Visibility == Visibility.Visible) 
                AISidebar.Visibility = Visibility.Collapsed;
            else 
            { 
                HistoryPanel.Visibility = Visibility.Collapsed; 
                AISidebar.Visibility = Visibility.Visible; 
                ChatInput.Focus(); 
            }
            UpdateSidebarWidth();
        }

        private void BtnCloseSidebar_Click(object s, RoutedEventArgs e) 
        { 
            AISidebar.Visibility = Visibility.Collapsed; 
            UpdateSidebarWidth(); 
        }
        
        private async void BtnSendChat_Click(object s, RoutedEventArgs e) => await SendRealChat();
        
        private async void ChatInput_KeyDown(object s, KeyEventArgs e) 
        { 
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) 
                await SendRealChat(); 
        }

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
            var container = new Border 
            { 
                Margin = new Thickness(0, 0, 0, 15), 
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left 
            };
            var bubble = new Border 
            { 
                Background = isUser ? (Brush)FindResource("PrimaryBrush") : Brushes.White, 
                CornerRadius = new CornerRadius(12), 
                Padding = new Thickness(12, 10, 12, 10), 
                MaxWidth = 320 
            };
            var tb = new TextBlock 
            { 
                Text = text, 
                TextWrapping = TextWrapping.Wrap, 
                Foreground = isUser ? Brushes.White : (Brush)FindResource("TextBrush") 
            };
            bubble.Child = tb; 
            container.Child = bubble; 
            ChatMessages.Children.Add(container); 
            ChatScrollViewer.ScrollToEnd();
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
                ChatMessages.Children.Clear();
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

        private async void CtxAITranslate_Click(object s, RoutedEventArgs e) 
        { 
            var t = await WebView.CoreWebView2.ExecuteScriptAsync("window.getSelection().toString()"); 
            if (!string.IsNullOrWhiteSpace(t)) OpenAISidebar($"Переведи: {t}"); 
        }

        private async void CtxAIExplain_Click(object s, RoutedEventArgs e) 
        { 
            var t = await WebView.CoreWebView2.ExecuteScriptAsync("window.getSelection().toString()"); 
            if (!string.IsNullOrWhiteSpace(t)) OpenAISidebar($"Объясни: {t}"); 
        }

        private void CtxAISummarize_Click(object s, RoutedEventArgs e) 
        { 
            OpenAISidebar($"Краткое содержание: {WebView.CoreWebView2.DocumentTitle}"); 
        }

        private async void CtxAICheckCode_Click(object s, RoutedEventArgs e) 
        { 
            var t = await WebView.CoreWebView2.ExecuteScriptAsync("window.getSelection().toString()"); 
            if (!string.IsNullOrWhiteSpace(t)) OpenAISidebar($"Найди ошибки в коде: {t}"); 
        }

        private void OpenAISidebar(string prompt = null) 
        { 
            AISidebar.Visibility = Visibility.Visible; 
            HistoryPanel.Visibility = Visibility.Collapsed; 
            UpdateSidebarWidth();
            if (!string.IsNullOrWhiteSpace(prompt)) 
            { 
                ChatInput.Text = prompt; 
                ChatInput.Focus(); 
            } 
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

            var selected = (SearchEngineComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "🔍 Google";
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

        private void BtnAddTile_Click(object sender, RoutedEventArgs e) 
        { 
            AddTileForm.Visibility = Visibility.Visible; 
            NewTileTitle.Focus(); 
        }

        private void BtnConfirmAddTile_Click(object sender, RoutedEventArgs e)
        {
            var title = NewTileTitle.Text.Trim(); 
            var url = NewTileUrl.Text.Trim();
            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(url)) 
            { 
                _tiles.AddTile(title, url); 
                RenderTiles(); 
            }
            AddTileForm.Visibility = Visibility.Collapsed; 
            NewTileTitle.Text = ""; 
            NewTileUrl.Text = "";
        }

        private void BtnCancelAddTile_Click(object sender, RoutedEventArgs e) 
        { 
            AddTileForm.Visibility = Visibility.Collapsed; 
        }

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