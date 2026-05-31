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
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace QuickSurfBrowser
{
    public partial class MainWindow : Window
    {
        private const string HomePage = "https://www.google.com/search?igu=1";
        private readonly List<TabItem> _tabs = new();
        private readonly List<string> _urls = new();
        private int _counter = 1;
        private CoreWebView2Environment? _env;

        private readonly string _dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickSurf");
        private readonly string _historyPath;
        private List<HistoryItem> _history = new();
        private Timer? _searchTimer;

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
                        border.MouseLeftButtonUp += (s, e) =>
                        {
                            ShowBrowser();
                            Navigate(tile.Url);
                        };
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
                await WebView.EnsureCoreWebView2Async(_env!);
                WebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                CreateNewTab();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска: {ex.Message}", "QuickSurf", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
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

        private void CreateNewTab()
        {
            var tab = new TabItem();
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            var text = new TextBlock { Text = $"Вкладка {_counter}", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
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
            if (_tabs.Count == 0) CreateNewTab();
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

        // ✅ ИСПРАВЛЕНО: проверка видимости и наличия истории
        private void BtnBack_Click(object s, RoutedEventArgs e)
        {
            if (WebView.Visibility == Visibility.Visible && WebView.CoreWebView2?.CanGoBack == true)
                WebView.CoreWebView2.GoBack();
        }

        private void BtnForward_Click(object s, RoutedEventArgs e)
        {
            if (WebView.Visibility == Visibility.Visible && WebView.CoreWebView2?.CanGoForward == true)
                WebView.CoreWebView2.GoForward();
        }

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

        private void BtnToggleHistory_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryPanel == null) return;
            if (HistoryPanel.Visibility == Visibility.Visible)
            {
                HistoryPanel.Visibility = Visibility.Collapsed;
                var parentGrid = HistoryPanel.Parent as Grid;
                if (parentGrid != null) parentGrid.ColumnDefinitions[1].Width = new GridLength(0);
            }
            else
            {
                HistoryPanel.Visibility = Visibility.Visible;
                var parentGrid = HistoryPanel.Parent as Grid;
                if (parentGrid != null) { parentGrid.ColumnDefinitions[1].Width = new GridLength(320); RefreshHistoryList(); }
            }
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
            var filtered = string.IsNullOrEmpty(query) ? _history
                : _history.Where(h => (h.Title ?? "").ToLower().Contains(query) || (h.Url ?? "").ToLower().Contains(query)).ToList();
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
                    if (HistoryPanel != null) HistoryPanel.Visibility = Visibility.Collapsed;
                }
            }
            catch { }
        }

        private void BtnHome_Click(object sender, RoutedEventArgs e) => ShowStartPage();
        
        // ✅ ИСПРАВЛЕНО: обновление заголовка вкладки и очистка URL при возврате на главную
        private void ShowStartPage()
        {
            UpdateCurrentTabTitle("Главная");
            UrlBox.Text = "";

            StartPageContainer.Visibility = Visibility.Visible;
            WebView.Visibility = Visibility.Collapsed;
            if (HistoryPanel.Visibility == Visibility.Visible)
            {
                HistoryPanel.Visibility = Visibility.Collapsed;
                var parentGrid = HistoryPanel.Parent as Grid;
                if (parentGrid != null) parentGrid.ColumnDefinitions[1].Width = new GridLength(0);
            }
        }

        private void ShowBrowser()
        {
            StartPageContainer.Visibility = Visibility.Collapsed;
            WebView.Visibility = Visibility.Visible;
        }

        // Вспомогательный метод для безопасного обновления заголовка
        private void UpdateCurrentTabTitle(string title)
        {
            if (TabsControl.SelectedIndex < 0 || TabsControl.SelectedIndex >= _tabs.Count) return;
            if (_tabs[TabsControl.SelectedIndex].Header is StackPanel header && header.Children.Count > 0 && header.Children[0] is TextBlock tb)
            {
                tb.Text = title;
            }
        }

        private void LoadTiles()
        {
            try
            {
                if (File.Exists(_tilesPath))
                {
                    var json = File.ReadAllText(_tilesPath);
                    _tiles = JsonSerializer.Deserialize<List<Tile>>(json) ?? new List<Tile>();
                }
            }
            catch { _tiles = new List<Tile>(); }
            RenderTiles();
        }

        private void SaveTiles()
        {
            try { File.WriteAllText(_tilesPath, JsonSerializer.Serialize(_tiles)); }
            catch { }
        }

        private void RenderTiles()
        {
            TilesWrapPanel.Children.Clear();
            foreach (var tile in _tiles)
            {
                var border = new Border
                {
                    Width = 110, Height = 110, Margin = new Thickness(10),
                    Background = Brushes.White, BorderBrush = (Brush)FindResource("BorderBrush"),
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                    Cursor = Cursors.Hand, ToolTip = tile.Url
                };
                
                var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8) };
                var img = new Image { Width = 40, Height = 40, Margin = new Thickness(0,0,0,8) };
                try
                {
                    var uri = new Uri(tile.Url);
                    img.Source = new BitmapImage(new Uri($"https://www.google.com/s2/favicons?domain={uri.Host}&sz=64"));
                }
                catch { img.Source = null; }
                
                var text = new TextBlock { Text = tile.Title, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 12, Foreground = (Brush)FindResource("TextBrush") };
                stack.Children.Add(img); stack.Children.Add(text);
                border.Child = stack;
                
                border.MouseLeftButtonUp += (s, e) =>
                {
                    ShowBrowser();
                    Navigate(tile.Url);
                };
                
                border.MouseRightButtonUp += (s, e) =>
                {
                    if (MessageBox.Show($"Удалить \"{tile.Title}\"?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        _tiles.Remove(tile);
                        SaveTiles();
                        RenderTiles();
                    }
                };

                TilesWrapPanel.Children.Add(border);
            }
        }

        private void BtnAddTile_Click(object sender, RoutedEventArgs e)
        {
            NewTileTitle.Text = "";
            NewTileUrl.Text = "https://";
            AddTileForm.Visibility = Visibility.Visible;
            NewTileTitle.Focus();
        }

        private void BtnConfirmAddTile_Click(object sender, RoutedEventArgs e)
        {
            var title = NewTileTitle.Text.Trim();
            var url = NewTileUrl.Text.Trim();
            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(url) || url == "https://")
            {
                MessageBox.Show("Заполните название и ссылку.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!url.StartsWith("http")) url = "https://" + url;
            
            _tiles.Add(new Tile { Title = title, Url = url });
            SaveTiles();
            RenderTiles();
            AddTileForm.Visibility = Visibility.Collapsed;
        }

        private void BtnCancelAddTile_Click(object sender, RoutedEventArgs e)
        {
            NewTileTitle.Text = "";
            NewTileUrl.Text = "https://";
            AddTileForm.Visibility = Visibility.Collapsed;
        }
    }

    public class HistoryItem { public string Title { get; set; } = ""; public string Url { get; set; } = ""; public DateTime Time { get; set; } }
    public class Tile { public string Title { get; set; } = ""; public string Url { get; set; } = ""; }
}