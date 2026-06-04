using System;
using System.Collections.ObjectModel;
using System.Linq;
using QuickSurfBrowser.Models;

namespace QuickSurfBrowser.Services
{
    public class TabService
    {
        private readonly ObservableCollection<TabItemModel> _tabs;
        private int _counter = 1;

        public event EventHandler<int> TabSwitched = null!;
        public event EventHandler<TabItemModel> TabClosed = null!;

        public ReadOnlyObservableCollection<TabItemModel> Tabs { get; }
        public int Count => _tabs.Count;
        public TabItemModel? SelectedTab => _tabs.FirstOrDefault(t => t.IsSelected);
        public int SelectedIndex => _tabs.IndexOf(SelectedTab!);

        public TabService(ObservableCollection<TabItemModel> tabs)
        {
            _tabs = tabs;
            Tabs = new ReadOnlyObservableCollection<TabItemModel>(_tabs);
        }

        public string GetCurrentUrl()
        {
            return SelectedTab?.Url ?? "";
        }

        public void SetCurrentUrl(string url)
        {
            if (SelectedTab != null)
                SelectedTab.Url = url;
        }

        public void SetCurrentTitle(string title)
        {
            if (SelectedTab != null)
                SelectedTab.Title = title;
        }

        // Существующий метод (без URL)
        public void CreateNewTab(string title = "Новая вкладка")
        {
            var tab = new TabItemModel(_counter++, title);
            
            foreach (var t in _tabs)
                t.IsSelected = false;
            
            tab.IsSelected = true;
            _tabs.Add(tab);
            
            TabSwitched?.Invoke(this, _tabs.Count - 1);
        }

        // НОВЫЙ МЕТОД: создание вкладки с URL
        public void CreateNewTab(string title, string url)
        {
            var tab = new TabItemModel(_counter++, title, url);
            
            foreach (var t in _tabs)
                t.IsSelected = false;
            
            tab.IsSelected = true;
            _tabs.Add(tab);
            
            TabSwitched?.Invoke(this, _tabs.Count - 1);
        }

        public void SelectTab(TabItemModel tab)
        {
            if (tab == null || tab == SelectedTab) return;
            
            foreach (var t in _tabs)
                t.IsSelected = false;
            
            tab.IsSelected = true;
            TabSwitched?.Invoke(this, _tabs.IndexOf(tab));
        }

        public void SelectTabByIndex(int index)
        {
            if (index >= 0 && index < _tabs.Count)
                SelectTab(_tabs[index]);
        }

        public void CloseTab(TabItemModel tabToClose)
        {
            if (tabToClose == null) return;
            
            int idx = _tabs.IndexOf(tabToClose);
            if (idx < 0) return;

            bool wasSelected = tabToClose.IsSelected;
            _tabs.RemoveAt(idx);
            TabClosed?.Invoke(this, tabToClose);

            if (_tabs.Count == 0)
            {
                CreateNewTab("Старт");
            }
            else if (wasSelected)
            {
                int newIndex = Math.Min(idx, _tabs.Count - 1);
                SelectTab(_tabs[newIndex]);
            }
        }
    }
}