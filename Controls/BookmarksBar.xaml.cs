using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickSurfBrowser.Services;

namespace QuickSurfBrowser.Controls
{
    public partial class BookmarksBar : UserControl
    {
        public event Action<string>? BookmarkClicked;
        public event Action? AddCurrentPageRequested;
        public event Action<Bookmark>? BookmarkRemoved;
        
        private ObservableCollection<Bookmark>? _bookmarks;
        
        public BookmarksBar()
        {
            InitializeComponent();
        }
        
        public void SetBookmarks(ObservableCollection<Bookmark> bookmarks)
        {
            _bookmarks = bookmarks;
            RefreshBookmarks();
        }
        
        public void RefreshBookmarks()
        {
            if (_bookmarks == null) return;
            
            BookmarksPanel.Children.Clear();
            foreach (var bookmark in _bookmarks)
            {
                BookmarksPanel.Children.Add(CreateBookmarkButton(bookmark));
            }
        }
        
        public void AddBookmark(Bookmark bookmark)
        {
            if (_bookmarks == null) return;
            BookmarksPanel.Children.Add(CreateBookmarkButton(bookmark));
        }
        
        public void RemoveBookmark(string url)
        {
            foreach (UIElement element in BookmarksPanel.Children)
            {
                if (element is Button btn && btn.Tag is Bookmark b && b.Url == url)
                {
                    BookmarksPanel.Children.Remove(btn);
                    break;
                }
            }
        }
        
        private Button CreateBookmarkButton(Bookmark bookmark)
        {
            var btn = new Button
            {
                Style = (Style)FindResource("BookmarkButtonStyle"),
                Tag = bookmark,
                ToolTip = bookmark.Title  // Всплывающая подсказка с названием
            };
            
            var img = new Image 
            { 
                Width = 18, 
                Height = 18
            };
            
            try 
            { 
                img.Source = new BitmapImage(new Uri(bookmark.IconUrl)); 
            } 
            catch { }
            
            btn.Content = img;
            
            btn.Click += (s, e) => BookmarkClicked?.Invoke(bookmark.Url);
            
            btn.MouseRightButtonUp += (s, e) =>
            {
                if (MessageBox.Show($"Удалить \"{bookmark.Title}\" из избранного?", "Подтверждение", 
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    BookmarkRemoved?.Invoke(bookmark);
                }
                e.Handled = true;
            };
            
            return btn;
        }
        
        private void AddCurrentPage_Click(object sender, MouseButtonEventArgs e)
        {
            AddCurrentPageRequested?.Invoke();
        }
    }
}