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
        public event Action<int, int>? BookmarkMoved;
        
        private ObservableCollection<Bookmark>? _bookmarks;
        private Point _dragStartPoint;
        private Button? _draggedItem;
        private int _dragStartIndex = -1;
        
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
            for (int i = 0; i < _bookmarks.Count; i++)
            {
                var btn = CreateBookmarkButton(_bookmarks[i]);
                btn.Tag = i; // сохраняем индекс
                BookmarksPanel.Children.Add(btn);
            }
        }
        
        private Button CreateBookmarkButton(Bookmark bookmark)
        {
            var btn = new Button
            {
                Style = (Style)FindResource("BookmarkButtonStyle"),
                Tag = bookmark,
                ToolTip = bookmark.Title
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
            
            btn.PreviewMouseLeftButtonDown += OnBookmarkPreviewMouseDown;
            btn.PreviewMouseMove += OnBookmarkPreviewMouseMove;
            btn.Click += (s, e) => 
            {
                if (_draggedItem == null)
                    BookmarkClicked?.Invoke(bookmark.Url);
            };
            
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
        
        private void OnBookmarkPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button btn && e.LeftButton == MouseButtonState.Pressed)
            {
                _dragStartPoint = e.GetPosition(null);
                _draggedItem = btn;
                _dragStartIndex = BookmarksPanel.Children.IndexOf(btn);
                _draggedItem.CaptureMouse();
            }
        }
        
        private void OnBookmarkPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedItem == null || e.LeftButton != MouseButtonState.Pressed) return;
            
            var currentPos = e.GetPosition(null);
            var diff = _dragStartPoint - currentPos;
            
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance)
            {
                DragDrop.DoDragDrop(_draggedItem, _draggedItem, DragDropEffects.Move);
                _draggedItem = null;
            }
        }
        
        private void BookmarksPanel_DragOver(object sender, DragEventArgs e)
        {
            if (_draggedItem != null)
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }
        
        private void BookmarksPanel_Drop(object sender, DragEventArgs e)
        {
            if (_draggedItem == null || _bookmarks == null) return;
            
            try
            {
                // Находим целевой элемент под курсором
                Point dropPosition = e.GetPosition(BookmarksPanel);
                int targetIndex = GetTargetIndex(dropPosition);
                
                if (targetIndex >= 0 && targetIndex != _dragStartIndex && targetIndex <= _bookmarks.Count)
                {
                    BookmarkMoved?.Invoke(_dragStartIndex, targetIndex);
                }
            }
            finally
            {
                if (_draggedItem != null)
                {
                    _draggedItem.ReleaseMouseCapture();
                    _draggedItem = null;
                }
                _dragStartIndex = -1;
            }
        }
        
        private int GetTargetIndex(Point position)
        {
            for (int i = 0; i < BookmarksPanel.Children.Count; i++)
            {
                var child = BookmarksPanel.Children[i] as UIElement;
                if (child != null)
                {
                    var pos = child.TransformToAncestor(BookmarksPanel).Transform(new Point(0, 0));
                    var width = child.RenderSize.Width;
                    
                    if (position.X < pos.X + width / 2)
                    {
                        return i;
                    }
                }
            }
            return BookmarksPanel.Children.Count;
        }
        
        private void AddCurrentPage_Click(object sender, MouseButtonEventArgs e)
        {
            AddCurrentPageRequested?.Invoke();
        }
    }
}