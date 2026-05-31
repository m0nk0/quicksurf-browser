using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

        // Путь к истории
        private readonly string _dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickSurf");
        private readonly string _historyPath;
        private List<HistoryItem> _history = new();

        public MainWindow()
        {
            InitializeComponent();
            _historyPath = Path.Combine(_dataPath, "history.json");
            Directory.CreateDirectory(_dataPath);
            LoadHistory();
            _ = InitAsync();
        }

        private async Task InitAsync()
        {
            try
            {
                _env = await CoreWebView2Environment.CreateAsync();
                await WebView.EnsureCoreWebView2Async(_env!);
                
                // Подписываемся на завершение загрузки для обновления заголовка вкладки
                WebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                
                CreateNewTab();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска: {ex.Message}", "QuickSurf", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Обновление заголовка вкладки при загрузке страницы
        private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            try
            {
                if (TabsControl.SelectedIndex < 0 || TabsControl.SelectedIndex >= _tabs.Count) return;

                string title = WebView.CoreWebView2.DocumentTitle;
                string url = WebView.CoreWebView2.Source;

                // Если заголовок пустой, берём домен
                if (string.IsNullOrWhiteSpace(title))
                {
                    try { title = new Uri(url).Host.Replace("www.", ""); }
                    catch { title = $"Вкладка {_counter - 1}"; }
                }

                // Обновляем текст внутри заголовка вкладки
                if (_tabs[TabsControl.SelectedIndex].Header is StackPanel header && header.Children.Count > 0)
                {
                    if (header.Children[0] is TextBlock tb) tb.Text = title;
                }

                // Сохраняем в историю
                AddToHistory(url, title);
            }
            catch { /* Игнорируем ошибки */ }
        }

        // === УПРАВЛЕНИЕ ВКЛАДКАМИ ===

        private void CreateNewTab()
        {
            var tab = new TabItem();
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            
            // Текст заголовка
            var text = new TextBlock 
            { 
                Text = $"Вкладка {_counter}", 
                VerticalAlignment = VerticalAlignment.Center, 
                Margin = new Thickness(0, 0, 10, 0) 
            };
            
            // Кнопка закрытия
            var closeBtn = new Button 
            { 
                Content = "✕", 
                Width = 18, 
                Height = 18, 
                Background = null, 
                BorderThickness = new Thickness(0), 
                Foreground = Brushes.Gray, 
                Cursor = Cursors.Hand 
            };
            
            // Замыкание: кнопка запоминает конкретную вкладку, которую нужно закрыть
            closeBtn.Click += (s, e) => CloseTab(tab);
            
            header.Children.Add(text);
            header.Children.Add(closeBtn);
            tab.Header = header;
            
            _tabs.Add(tab);
            _urls.Add(HomePage);
            TabsControl.Items.Add(tab);
            TabsControl.SelectedIndex = TabsControl.Items.Count - 1;
            
            _counter++;
            LoadUrl(HomePage);
        }

        private void CloseTab(TabItem tabToClose)
        {
            int idx = _tabs.IndexOf(tabToClose);
            if (idx < 0) return;
            
            _tabs.RemoveAt(idx);
            _urls.RemoveAt(idx);
            TabsControl.Items.Remove(tabToClose);
            
            if (_tabs.Count == 0)
            {
                CreateNewTab();
            }
            else if (TabsControl.SelectedItem == tabToClose)
            {
                TabsControl.SelectedIndex = Math.Min(idx, _tabs.Count - 1);
            }
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
            if (WebView.CoreWebView2 != null)
            {
                WebView.CoreWebView2.Navigate(url);
                if (TabsControl.SelectedIndex >= 0 && TabsControl.SelectedIndex < _urls.Count)
                    _urls[TabsControl.SelectedIndex] = url;
            }
        }

        // === НАВИГАЦИЯ ===

        private void BtnBack_Click(object s, RoutedEventArgs e) => WebView.CoreWebView2?.GoBack();
        private void BtnForward_Click(object s, RoutedEventArgs e) => WebView.CoreWebView2?.GoForward();
        private void BtnRefresh_Click(object s, RoutedEventArgs e) => WebView.CoreWebView2?.Reload();
        private void BtnGo_Click(object s, RoutedEventArgs e) => Navigate(UrlBox.Text);
        private void BtnNewTab_Click(object s, RoutedEventArgs e) => CreateNewTab();
        private void UrlBox_KeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter) Navigate(UrlBox.Text); }

        private void Navigate(string input)
        {
            input = input.Trim();
            if (string.IsNullOrEmpty(input)) return;

            string url = input.Contains(".") && !input.Contains(" ")
                ? (input.StartsWith("http") ? input : $"https://{input}")
                : $"https://www.google.com/search?q={Uri.EscapeDataString(input)}";

            UrlBox.Text = url;
            LoadUrl(url);
        }

        // === ИСТОРИЯ (БЕЗОПАСНАЯ) ===

        private void BtnToggleHistory_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryPanel == null) return;

            // Логика переключения с изменением ширины колонки (чтобы не было вылетов)
            if (HistoryPanel.Visibility == Visibility.Visible)
            {
                HistoryPanel.Visibility = Visibility.Collapsed;
                var parentGrid = HistoryPanel.Parent as Grid;
                if (parentGrid != null)
                    parentGrid.ColumnDefinitions[1].Width = new GridLength(0);
            }
            else
            {
                HistoryPanel.Visibility = Visibility.Visible;
                var parentGrid = HistoryPanel.Parent as Grid;
                if (parentGrid != null)
                {
                    parentGrid.ColumnDefinitions[1].Width = new GridLength(320);
                    RefreshHistoryList();
                }
            }
        }

        private void RefreshHistoryList()
        {
            if (HistorySearchBox == null || HistoryList == null || _history == null) return;
            
            var query = (HistorySearchBox.Text ?? "").Trim().ToLower();
            var filtered = string.IsNullOrEmpty(query) ? _history 
                : _history.Where(h => (h.Title ?? "").ToLower().Contains(query) || (h.Url ?? "").ToLower().Contains(query)).ToList();
            
            HistoryList.ItemsSource = filtered;
        }

        private void HistorySearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshHistoryList();

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(_historyPath))
                {
                    var json = File.ReadAllText(_historyPath);
                    var items = JsonSerializer.Deserialize<List<HistoryItem>>(json);
                    _history = items ?? new List<HistoryItem>();
                }
                else { _history = new List<HistoryItem>(); }
            }
            catch { _history = new List<HistoryItem>(); }
            RefreshHistoryList();
        }

        private void SaveHistory()
        {
            try { File.WriteAllText(_historyPath, JsonSerializer.Serialize(_history)); }
            catch { /* Игнорируем ошибки записи */ }
        }

        private void AddToHistory(string url, string title)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url) || url.StartsWith("about:")) return;
                
                // Удаляем дубликат
                _history.RemoveAll(h => h.Url == url);
                
                // Добавляем в начало
                _history.Insert(0, new HistoryItem { Url = url, Title = title ?? url, Time = DateTime.Now });
                
                // Ограничиваем размер (150 записей)
                if (_history.Count > 150) _history.RemoveAt(_history.Count - 1);
                
                SaveHistory();
                RefreshHistoryList();
            }
            catch { /* Игнорируем */ }
        }

        private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Очистить всю историю?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _history.Clear();
                try { File.Delete(_historyPath); } catch { }
                RefreshHistoryList();
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
            catch { /* Игнорируем */ }
        }
    }

    public class HistoryItem { public string Title { get; set; } = ""; public string Url { get; set; } = ""; public DateTime Time { get; set; } }
}