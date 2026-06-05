using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace QuickSurfBrowser.Services
{
    public class Tile
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public int Order { get; set; } = 0;
        public int VisitCount { get; set; } = 0;
        public DateTime LastVisit { get; set; } = DateTime.Now;
        public string IconUrl { get; set; } = "";
    }
    
    public class Bookmark
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string IconUrl { get; set; } = "";
        public int Order { get; set; } = 0;
    }

    public class TilesService
    {
        private readonly string _filePath;
        private readonly string _bookmarksPath;
        public List<Tile> Tiles { get; private set; } = new();
        public ObservableCollection<Bookmark> Bookmarks { get; private set; } = new();

        public TilesService(string dataPath)
        {
            _filePath = Path.Combine(dataPath, "startpage.json");
            _bookmarksPath = Path.Combine(dataPath, "bookmarks.json");
        }

        public void Load()
        {
            LoadTiles();
            LoadBookmarks();
        }
        
        private void LoadTiles()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    Tiles = JsonSerializer.Deserialize<List<Tile>>(json) ?? new List<Tile>();
                    
                    int maxOrder = Tiles.Count > 0 ? Tiles.Max(t => t.Order) : 0;
                    foreach (var tile in Tiles)
                    {
                        if (tile.Order == 0 && tile.Title != null)
                        {
                            tile.Order = ++maxOrder;
                        }
                        if (string.IsNullOrEmpty(tile.IconUrl))
                        {
                            try
                            {
                                tile.IconUrl = $"https://www.google.com/s2/favicons?domain={new Uri(tile.Url).Host}&sz=64";
                            }
                            catch { }
                        }
                    }
                    SortTilesByOrder();
                    SaveTiles();
                }
            }
            catch { Tiles = new List<Tile>(); }
        }
        
        private void LoadBookmarks()
        {
            try
            {
                if (File.Exists(_bookmarksPath))
                {
                    var json = File.ReadAllText(_bookmarksPath);
                    var items = JsonSerializer.Deserialize<List<Bookmark>>(json);
                    if (items != null)
                    {
                        Bookmarks.Clear();
                        foreach (var item in items.OrderBy(b => b.Order))
                            Bookmarks.Add(item);
                    }
                }
            }
            catch { }
            
            if (Bookmarks.Count == 0 && Tiles.Count > 0)
            {
                foreach (var tile in Tiles.OrderBy(t => t.Order))
                {
                    Bookmarks.Add(new Bookmark
                    {
                        Title = tile.Title,
                        Url = tile.Url,
                        IconUrl = tile.IconUrl,
                        Order = tile.Order
                    });
                }
                SaveBookmarks();
            }
        }
        
        public void AddBookmark(string title, string url)
        {
            if (!url.StartsWith("http")) url = "https://" + url;
            
            if (Bookmarks.Any(b => b.Url == url)) return;
            
            int maxOrder = Bookmarks.Count > 0 ? Bookmarks.Max(b => b.Order) + 1 : 0;
            string iconUrl = "";
            try { iconUrl = $"https://www.google.com/s2/favicons?domain={new Uri(url).Host}&sz=64"; } catch { }
            
            Bookmarks.Add(new Bookmark
            {
                Title = title,
                Url = url,
                IconUrl = iconUrl,
                Order = maxOrder
            });
            SaveBookmarks();
            SyncBookmarksToTiles();
        }
        
        public void RemoveBookmark(Bookmark bookmark)
        {
            Bookmarks.Remove(bookmark);
            SaveBookmarks();
            
            var tile = Tiles.FirstOrDefault(t => t.Url == bookmark.Url);
            if (tile != null)
            {
                Tiles.Remove(tile);
                SaveTiles();
            }
            ReorderAll();
        }
        
        public void AddTile(string title, string url)
        {
            if (!url.StartsWith("http")) url = "https://" + url;
            
            if (Tiles.Any(t => t.Url == url)) return;
            
            int maxOrder = Tiles.Count > 0 ? Tiles.Max(t => t.Order) + 1 : 0;
            
            string iconUrl = "";
            try { iconUrl = $"https://www.google.com/s2/favicons?domain={new Uri(url).Host}&sz=64"; } catch { }
            
            Tiles.Add(new Tile 
            { 
                Title = title, 
                Url = url, 
                Order = maxOrder,
                VisitCount = 0,
                LastVisit = DateTime.Now,
                IconUrl = iconUrl
            });
            SaveTiles();
            
            AddBookmark(title, url);
        }

        public void RemoveTile(Tile tile)
        {
            Tiles.Remove(tile);
            SaveTiles();
            
            var bookmark = Bookmarks.FirstOrDefault(b => b.Url == tile.Url);
            if (bookmark != null)
            {
                Bookmarks.Remove(bookmark);
                SaveBookmarks();
            }
            ReorderAll();
        }
        
        public void IncrementVisitCount(string url)
        {
            var tile = Tiles.FirstOrDefault(t => t.Url == url);
            if (tile != null)
            {
                tile.VisitCount++;
                tile.LastVisit = DateTime.Now;
                SaveTiles();
            }
        }
        
        public void MoveBookmark(int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= Bookmarks.Count) return;
            if (newIndex < 0) newIndex = 0;
            if (newIndex >= Bookmarks.Count) newIndex = Bookmarks.Count - 1;
            if (oldIndex == newIndex) return;
            
            var item = Bookmarks[oldIndex];
            Bookmarks.RemoveAt(oldIndex);
            Bookmarks.Insert(newIndex, item);
            
            for (int i = 0; i < Bookmarks.Count; i++)
            {
                Bookmarks[i].Order = i;
            }
            
            SaveBookmarks();
            SyncBookmarksToTiles();
        }
        
        public void MoveTile(int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= Tiles.Count) return;
            if (newIndex < 0) newIndex = 0;
            if (newIndex >= Tiles.Count) newIndex = Tiles.Count - 1;
            if (oldIndex == newIndex) return;
            
            var item = Tiles[oldIndex];
            Tiles.RemoveAt(oldIndex);
            Tiles.Insert(newIndex, item);
            
            for (int i = 0; i < Tiles.Count; i++)
            {
                Tiles[i].Order = i;
            }
            
            SaveTiles();
            SyncTilesToBookmarks();
        }
        
        private void SyncBookmarksToTiles()
        {
            for (int i = 0; i < Bookmarks.Count; i++)
            {
                var existingTile = Tiles.FirstOrDefault(t => t.Url == Bookmarks[i].Url);
                if (existingTile != null)
                {
                    existingTile.Order = i;
                }
            }
            Tiles = Tiles.OrderBy(t => t.Order).ToList();
            SaveTiles();
        }
        
        private void SyncTilesToBookmarks()
        {
            for (int i = 0; i < Tiles.Count; i++)
            {
                var existingBookmark = Bookmarks.FirstOrDefault(b => b.Url == Tiles[i].Url);
                if (existingBookmark != null)
                {
                    existingBookmark.Order = i;
                }
            }
            var sortedBookmarks = Bookmarks.OrderBy(b => b.Order).ToList();
            Bookmarks.Clear();
            foreach (var b in sortedBookmarks)
            {
                Bookmarks.Add(b);
            }
            SaveBookmarks();
        }
        
        private void ReorderAll()
        {
            for (int i = 0; i < Tiles.Count; i++)
            {
                Tiles[i].Order = i;
            }
            for (int i = 0; i < Bookmarks.Count; i++)
            {
                Bookmarks[i].Order = i;
            }
            SaveTiles();
            SaveBookmarks();
        }
        
        public void SortTilesByOrder()
        {
            Tiles = Tiles.OrderBy(t => t.Order).ToList();
        }
        
        public void SortTilesByVisitCount()
        {
            Tiles = Tiles.OrderByDescending(t => t.VisitCount).ThenByDescending(t => t.LastVisit).ToList();
            for (int i = 0; i < Tiles.Count; i++)
            {
                Tiles[i].Order = i;
            }
            SaveTiles();
            SyncTilesToBookmarks();
        }
        
        private void SaveTiles()
        {
            try 
            { 
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(Tiles, options);
                File.WriteAllText(_filePath, json); 
            } 
            catch { }
        }
        
        private void SaveBookmarks()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(Bookmarks, options);
                File.WriteAllText(_bookmarksPath, json);
            }
            catch { }
        }
        
        public void RefreshBookmarksOrder()
        {
            for (int i = 0; i < Bookmarks.Count; i++)
            {
                Bookmarks[i].Order = i;
            }
            SaveBookmarks();
        }
    }
}