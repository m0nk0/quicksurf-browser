using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;

namespace QuickSurfBrowser.Services
{
    public class TabService
    {
        private readonly TabControl _tabsControl;
        private readonly List<TabItem> _tabs = new();
        private readonly List<string> _urls = new();
        private int _counter = 1;

        // Событие, когда вкладка переключилась
        public event EventHandler<int> TabSwitched = null!;

        public TabService(TabControl tabsControl)
        {
            _tabsControl = tabsControl;
            _tabsControl.SelectionChanged += (s, e) =>
            {
                if (_tabsControl.SelectedIndex >= 0)
                    TabSwitched?.Invoke(this, _tabsControl.SelectedIndex);
            };
        }

        public int Count => _tabs.Count;
        public string GetCurrentUrl() => _urls[_tabsControl.SelectedIndex];
        
        public void SetCurrentUrl(string url)
        {
            if (_tabsControl.SelectedIndex >= 0)
                _urls[_tabsControl.SelectedIndex] = url;
        }

        public void CreateNewTab(string title = "Новая вкладка")
        {
            var tab = new TabItem();
            var header = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            
            var text = new TextBlock { Text = title, VerticalAlignment = System.Windows.VerticalAlignment.Center, Margin = new System.Windows.Thickness(0, 0, 10, 0) };
            var closeBtn = new System.Windows.Controls.Button 
            { 
                Content = "✕", Width = 18, Height = 18, Background = null, 
                BorderThickness = new System.Windows.Thickness(0), Foreground = Brushes.Gray, Cursor = System.Windows.Input.Cursors.Hand 
            };
            
            closeBtn.Click += (s, e) => CloseTab(tab);
            
            header.Children.Add(text);
            header.Children.Add(closeBtn);
            tab.Header = header;

            _tabs.Add(tab);
            _urls.Add(""); // Пустой URL для новой
            _tabsControl.Items.Add(tab);
            _tabsControl.SelectedIndex = _tabsControl.Items.Count - 1;
            _counter++;
        }

        private void CloseTab(TabItem tabToClose)
        {
            int idx = _tabs.IndexOf(tabToClose);
            if (idx < 0) return;

            _tabs.RemoveAt(idx);
            _urls.RemoveAt(idx);
            _tabsControl.Items.Remove(tabToClose);

            if (_tabs.Count == 0)
                CreateNewTab("Старт");
            else if (_tabsControl.SelectedItem == tabToClose)
                _tabsControl.SelectedIndex = Math.Min(idx, _tabs.Count - 1);
        }
    }
}