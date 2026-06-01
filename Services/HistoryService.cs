using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace QuickSurfBrowser.Services
{
    public class HistoryItem
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public DateTime Time { get; set; }
    }

    public class HistoryService
    {
        private readonly string _filePath;
        public List<HistoryItem> History { get; private set; } = new();

        public HistoryService(string dataPath)
        {
            _filePath = Path.Combine(dataPath, "history.json");
        }

        public void Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var items = JsonSerializer.Deserialize<List<HistoryItem>>(json);
                    History = items ?? new List<HistoryItem>();
                }
            }
            catch { History = new List<HistoryItem>(); }
        }

        public void Add(string url, string title)
        {
            if (string.IsNullOrWhiteSpace(url) || url.StartsWith("about:")) return;
            History.RemoveAll(h => h.Url == url);
            History.Insert(0, new HistoryItem { Url = url, Title = title, Time = DateTime.Now });
            if (History.Count > 150) History.RemoveAt(History.Count - 1);
            Save();
        }

        public void Save()
        {
            try { File.WriteAllText(_filePath, JsonSerializer.Serialize(History)); } catch { }
        }

        public void Clear()
        {
            History.Clear();
            if (File.Exists(_filePath)) File.Delete(_filePath);
        }

        public List<HistoryItem> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return History;
            var q = query.ToLower();
            return History.Where(h => h.Title.ToLower().Contains(q) || h.Url.ToLower().Contains(q)).ToList();
        }
    }
}