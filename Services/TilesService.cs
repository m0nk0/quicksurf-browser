using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace QuickSurfBrowser.Services
{
    public class Tile
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
    }

    public class TilesService
    {
        private readonly string _filePath;
        public List<Tile> Tiles { get; private set; } = new();

        public TilesService(string dataPath)
        {
            _filePath = Path.Combine(dataPath, "startpage.json");
        }

        public void Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    Tiles = JsonSerializer.Deserialize<List<Tile>>(json) ?? new List<Tile>();
                }
            }
            catch { Tiles = new List<Tile>(); }
        }

        public void AddTile(string title, string url)
        {
            if (!url.StartsWith("http")) url = "https://" + url;
            Tiles.Add(new Tile { Title = title, Url = url });
            Save(); // Синхронно
        }

        public void RemoveTile(Tile tile)
        {
            Tiles.Remove(tile);
            Save(); // Синхронно
        }

        public void Save()
        {
            try { File.WriteAllText(_filePath, JsonSerializer.Serialize(Tiles)); } catch { }
        }
    }
}