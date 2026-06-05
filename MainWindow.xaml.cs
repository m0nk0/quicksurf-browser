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

namespace QuickSurfBrowser
{
    public partial class MainWindow : Window
    {
        private readonly BrowserService _browser;
        private readonly TabService _tabs;
        private readonly HistoryService _history;
        private readonly TilesService _tiles;
        private readonly ChatContextService _chatContext;
        private readonly GitHubService _gitHub;
        private AiWorkerService _aiWorker;
        private AISelectionService _aiSelectionService;
        private FloatingMenuService _floatingMenuService;
        private readonly string _dataPath;
        private readonly ObservableCollection<TabItemModel> _tabsCollection = new();
        private System.Timers.Timer _gitHubTimer;
        
        // Drag-and-drop поля
        private Point _dragStartPoint;
        private Border? _draggedTile;
        private int _dragStartTileIndex = -1;
        
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
            _gitHub = new GitHubService();
            _tabs.TabSwitched += OnTabSwitched;
            
            _aiSelectionService = new AISelectionService(
                _aiWorker,
                _chatContext,
                (text, isUser, isLoading) => AddChatMessageForSelection(text, isUser, isLoading),
                (text) => ChatInput.Text = text,
                () => { AISidebar.Visibility = Visibility.Visible; HistoryPanel.Visibility = Visibility.Collapsed; UpdateSidebarWidth(); },
                () => ChatInput.Focus()
            );
            
            _floatingMenuService = new FloatingMenuService(WebView, OnFloatingMenuAskAI);
            
            _gitHubTimer = new System.Timers.Timer(6 * 60 * 60 * 1000);
            _gitHubTimer.Elapsed += async (s, e) => await LoadGitHubTrendingAsync();
            _gitHubTimer.AutoReset = true;
            
            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _history.Load();
            _tiles.Load();
            _chatContext.Load();
            RenderTiles();
            SetupAITiles();
            SetupModelTiles();
            SetupMediaTiles();
            UpdateContextStatus();
            await _browser.InitializeAsync();
            
            _ = _aiWorker.InitializeAsync();
            
            if (WebView.CoreWebView2 != null)
            {
                await _floatingMenuService.InitializeAsync();
            }
            else
            {
                WebView.CoreWebView2InitializationCompleted += async (s, ev) =>
                {
                    if (ev.IsSuccess)
                        await _floatingMenuService.InitializeAsync();
                };
            }
            
            BookmarksBarControl.SetBookmarks(_tiles.Bookmarks);
            BookmarksBarControl.BookmarkClicked += NavigateToBookmark;
            BookmarksBarControl.AddCurrentPageRequested += AddCurrentPageToBookmarks;
            BookmarksBarControl.BookmarkRemoved += RemoveBookmark;
            BookmarksBarControl.BookmarkMoved += (oldIndex, newIndex) =>
            {
                _tiles.MoveBookmark(oldIndex, newIndex);
                BookmarksBarControl.RefreshBookmarks();
                RenderTiles();
            };
            
            _tabs.CreateNewTab("Старт", "");
            ShowStartPage();
            
            await LoadGitHubTrendingAsync();
            _gitHubTimer.Start();
        }

        private void OnFloatingMenuAskAI(string selectedText)
        {
            _aiSelectionService.ProcessSelection(selectedText, "ask");
        }

        private void OnNavigationCompleted(string url, string title)
        {
            if (_tabs.SelectedTab != null)
            {
                _tabs.SetCurrentUrl(url);
                if (!string.IsNullOrWhiteSpace(title))
                    _tabs.SetCurrentTitle(title);
                
                _tiles.IncrementVisitCount(url);
                _tiles.SortTilesByVisitCount();
                RenderTiles();
            }
            _history.Add(url, title);
        }

        private void OnTabSwitched(object? sender, int index)
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

        private void BtnBack_Click(object s, RoutedEventArgs e) => _browser.GoBack();
        private void BtnForward_Click(object s, RoutedEventArgs e) => _browser.GoForward();
        private void BtnRefresh_Click(object s, RoutedEventArgs e) => _browser.Refresh();
        private void BtnGo_Click(object s, RoutedEventArgs e) => Navigate(UrlBox.Text);

        private void BtnCloseOtherTabs_Click(object sender, RoutedEventArgs e)
        {
            if (_tabs.SelectedTab == null) return;
            var currentTab = _tabs.SelectedTab;
            var tabsToClose = _tabsCollection.Where(t => t != currentTab).ToList();
            foreach (var tab in tabsToClose)
            {
                _tabs.CloseTab(tab);
            }
        }

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
            _tabs.CreateNewTab("Старт", "");
            ShowStartPage();
        }

        private void AddCurrentPageToBookmarks()
        {
            if (_tabs.SelectedTab != null && !string.IsNullOrEmpty(_tabs.SelectedTab.Url))
            {
                var url = _tabs.SelectedTab.Url;
                var title = _tabs.SelectedTab.Title;
                
                if (url != "about:blank" && !url.StartsWith("https://www.google.com/search"))
                {
                    _tiles.AddBookmark(title, url);
                    BookmarksBarControl.RefreshBookmarks();
                    RenderTiles();
                }
            }
        }
        
        private void NavigateToBookmark(string url)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                ShowBrowser();
                Navigate(url);
            }
        }
        
        private void RemoveBookmark(Bookmark bookmark)
        {
            _tiles.RemoveBookmark(bookmark);
            BookmarksBarControl.RefreshBookmarks();
            
            var tile = _tiles.Tiles.FirstOrDefault(t => t.Url == bookmark.Url);
            if (tile != null)
            {
                _tiles.RemoveTile(tile);
                RenderTiles();
            }
        }

        public void NavigateInNewTab(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            
            Dispatcher.Invoke(() =>
            {
                _tabs.CreateNewTab("Новая вкладка", url);
                ShowBrowser();
                _browser.Navigate(url);
                UrlBox.Text = url;
            });
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

        private void RenderTiles()
        {
            TilesWrapPanel.Children.Clear();
            var sortedTiles = _tiles.Tiles.OrderBy(t => t.Order).ToList();
            foreach (var tile in sortedTiles)
                TilesWrapPanel.Children.Add(CreateTile(tile.Title, tile.Url, true));
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
                Tag = url,
                AllowDrop = true
            };
            
            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            var img = new Image { Width = 32, Height = 32, Margin = new Thickness(0, 0, 0, 4) };
            try 
            { 
                img.Source = new BitmapImage(new Uri($"https://www.google.com/s2/favicons?domain={new Uri(url).Host}&sz=64")); 
            } 
            catch { }
            
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
            
            // Drag-and-drop для крупных плиток
            border.PreviewMouseLeftButtonDown += (s, e) =>
            {
                _dragStartPoint = e.GetPosition(null);
                _draggedTile = border;
                _dragStartTileIndex = TilesWrapPanel.Children.IndexOf(border);
                border.CaptureMouse();
            };
            
            border.PreviewMouseMove += (s, e) =>
            {
                if (_draggedTile == null || e.LeftButton != MouseButtonState.Pressed) return;
                var diff = _dragStartPoint - e.GetPosition(null);
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance)
                {
                    DragDrop.DoDragDrop(_draggedTile, _draggedTile, DragDropEffects.Move);
                    _draggedTile = null;
                }
            };
            
            border.DragOver += (s, e) => e.Effects = DragDropEffects.Move;
            
            border.Drop += (s, e) =>
            {
                if (_draggedTile == null) return;
                
                int oldIndex = _dragStartTileIndex;
                int newIndex = TilesWrapPanel.Children.IndexOf(border);
                
                if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex)
                {
                    _tiles.MoveTile(oldIndex, newIndex);
                    RenderTiles();
                    BookmarksBarControl.RefreshBookmarks();
                }
                
                _draggedTile.ReleaseMouseCapture();
                _draggedTile = null;
            };
            
            border.MouseLeftButtonUp += (s, e) => { ShowBrowser(); Navigate(url); };
            
            if (canDelete)
            {
                border.MouseRightButtonUp += (s, e) =>
                {
                    if (MessageBox.Show($"Удалить \"{title}\" из избранного?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        var tileToRemove = _tiles.Tiles.FirstOrDefault(t => t.Url == url);
                        if (tileToRemove != null)
                        {
                            _tiles.RemoveTile(tileToRemove);
                            var bookmark = _tiles.Bookmarks.FirstOrDefault(b => b.Url == url);
                            if (bookmark != null)
                            {
                                _tiles.RemoveBookmark(bookmark);
                                BookmarksBarControl.RefreshBookmarks();
                            }
                            RenderTiles();
                        }
                    }
                    e.Handled = true;
                };
            }
            
            return border;
        }

        private void SetupAITiles()
        {
            AITilesPanel.Children.Clear();
            var tiles = new (string name, string url)[]
            {
                ("Qwen", "https://chat.qwen.ai"),
                ("DeepSeek", "https://chat.deepseek.com"),
                ("Алиса", "https://alice.yandex.ru"),
                ("ChatGPT", "https://chatgpt.com"),
                ("GigaChat", "https://giga.chat"),
                ("Claude", "https://claude.ai"),
                ("Gemini", "https://gemini.google.com"),
                ("Copilot", "https://copilot.microsoft.com")
            };
            foreach (var tile in tiles)
                AITilesPanel.Children.Add(CreateStaticTile(tile.name, tile.url));
        }

        private void SetupModelTiles()
        {
            ModelTilesPanel.Children.Clear();
            var tiles = new (string name, string url)[]
            {
                ("Hugging Face", "https://huggingface.co"),
                ("Civitai", "https://civitai.com"),
                ("OpenModelDB", "https://openmodeldb.info"),
                ("Kaggle", "https://www.kaggle.com/models"),
                ("Ollama", "https://ollama.com/library"),
                ("Replicate", "https://replicate.com"),
                ("TensorFlow", "https://tfhub.dev"),
                ("PyTorch", "https://pytorch.org/hub")
            };
            foreach (var tile in tiles)
                ModelTilesPanel.Children.Add(CreateStaticTile(tile.name, tile.url));
        }

        private void SetupMediaTiles()
        {
            MediaTilesPanel.Children.Clear();
            var tiles = new (string name, string url)[]
            {
                ("Kandinsky", "https://www.sberbank.ru/ru/person/kandinsky"),
                ("Leonardo", "https://leonardo.ai"),
                ("Suno", "https://suno.com"),
                ("Craiyon", "https://www.craiyon.com"),
                ("Playground", "https://playgroundai.com"),
                ("Udio", "https://www.udio.com"),
                ("Runway", "https://runwayml.com"),
                ("Pika", "https://pika.art")
            };
            foreach (var tile in tiles)
                MediaTilesPanel.Children.Add(CreateStaticTile(tile.name, tile.url));
        }

        private Border CreateStaticTile(string title, string url)
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
                Tag = url
            };
            
            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            var img = new Image { Width = 32, Height = 32, Margin = new Thickness(0, 0, 0, 4) };
            try 
            { 
                img.Source = new BitmapImage(new Uri($"https://www.google.com/s2/favicons?domain={new Uri(url).Host}&sz=64")); 
            } 
            catch { }
            
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
            
            return border;
        }

        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
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

        private void BtnToggleHistory_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryPanel.Visibility == Visibility.Visible)
                HistoryPanel.Visibility = Visibility.Collapsed;
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
                AddChatBubble($"⚠️ {ex.Message}\n💡 Убедитесь, что вы вошли в Яндекс в основной вкладке.", false);
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

        private void AddChatMessageForSelection(string text, bool isUser, bool isLoading = false)
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
        }

        private void BtnSaveContext_Click(object s, RoutedEventArgs e)
        {
            var provider = (AIProviderComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Алиса (Яндекс)";
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
                _tiles.AddBookmark(title, url);
                RenderTiles();
                BookmarksBarControl.RefreshBookmarks();
            }
            AddTileForm.Visibility = Visibility.Collapsed;
            NewTileTitle.Text = "";
            NewTileUrl.Text = "";
        }

        private void BtnCancelAddTile_Click(object sender, RoutedEventArgs e)
        {
            AddTileForm.Visibility = Visibility.Collapsed;
        }

        private async Task LoadGitHubTrendingAsync()
        {
            try
            {
                var repos = await _gitHub.GetTrendingAIReposAsync(8);
                await Dispatcher.InvokeAsync(() =>
                {
                    GitHubTilesPanel.Children.Clear();
                    foreach (var repo in repos)
                    {
                        GitHubTilesPanel.Children.Add(CreateGitHubTile(repo));
                    }
                    GitHubUpdateTime.Text = $"Обновлено: {DateTime.Now:HH:mm}";
                });
            }
            catch (Exception)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    GitHubUpdateTime.Text = "Ошибка загрузки";
                });
            }
        }

        private Border CreateGitHubTile(GitHubRepo repo)
        {
            var border = new Border
            {
                Width = 240,
                MinHeight = 65,
                Margin = new Thickness(8, 4, 8, 4),
                Background = Brushes.White,
                BorderBrush = (Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                Tag = repo.HtmlUrl,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            var stack = new StackPanel
            {
                Margin = new Thickness(12, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            
            var nameBlock = new TextBlock
            {
                Text = repo.FullName,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.Black,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 216
            };
            
            var statsBlock = new TextBlock
            {
                Text = $"⭐ {FormatNumber(repo.StargazersCount)}    🍴 {FormatNumber(repo.ForksCount)}",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.Black,
                Opacity = 0.75,
                Margin = new Thickness(0, 2, 0, 0)
            };
            
            var langBlock = new TextBlock
            {
                Text = repo.Language ?? "N/A",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(0, 1, 0, 0)
            };
            
            stack.Children.Add(nameBlock);
            stack.Children.Add(statsBlock);
            stack.Children.Add(langBlock);
            border.Child = stack;
            
            border.MouseLeftButtonUp += (s, e) => 
            { 
                string url = !string.IsNullOrWhiteSpace(repo.HtmlUrl) 
                    ? repo.HtmlUrl 
                    : $"https://github.com/{repo.FullName}";
                ShowBrowser();
                Navigate(url);
            };
            
            return border;
        }

        private string FormatNumber(int num)
        {
            if (num >= 1000)
                return (num / 1000.0).ToString("0.0") + "k";
            return num.ToString();
        }

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