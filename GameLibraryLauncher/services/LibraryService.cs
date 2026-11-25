using MinimalGameLauncher.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MinimalGameLauncher.Services
{
    public class LibraryService
    {
        private readonly string _storageFilePath;
        private readonly string _settingsFilePath;
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public LibraryService(string storageFilePath, string settingsFilePath)
        {
            _storageFilePath = storageFilePath ?? throw new ArgumentNullException(nameof(storageFilePath));
            _settingsFilePath = settingsFilePath ?? throw new ArgumentNullException(nameof(settingsFilePath));
        }

        public List<GameItem> LoadGames()
        {
            if (!File.Exists(_storageFilePath))
                return new List<GameItem>();

            string json = File.ReadAllText(_storageFilePath);
            if (string.IsNullOrWhiteSpace(json))
                return new List<GameItem>();

            var games = JsonSerializer.Deserialize<List<GameItem>>(json) ?? new List<GameItem>();
            foreach (var game in games)
            {
                game.IsSelected = false;
                game.IsDownloading = false;
            }
            return games;
        }

        public void SaveGames(IEnumerable<GameItem> games)
        {
            EnsureParentDirectory(_storageFilePath);
            string json = JsonSerializer.Serialize(games, _jsonOptions);
            File.WriteAllText(_storageFilePath, json);
        }

        public void AddGame(List<GameItem> games, GameItem newGame)
        {
            if (newGame == null) throw new ArgumentNullException(nameof(newGame));
            games.Add(newGame);
            SaveGames(games);
        }

        public bool RemoveGameAt(List<GameItem> games, int index)
        {
            if (index < 0 || index >= games.Count)
                return false;

            games.RemoveAt(index);
            SaveGames(games);
            return true;
        }

        public string LoadApiKey()
        {
            if (!File.Exists(_settingsFilePath))
                return string.Empty;

            string json = File.ReadAllText(_settingsFilePath);
            if (string.IsNullOrWhiteSpace(json))
                return string.Empty;

            var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return settings != null && settings.TryGetValue("ApiKey", out var key) ? key : string.Empty;
        }

        public void SaveApiKey(string apiKey)
        {
            EnsureParentDirectory(_settingsFilePath);
            var settings = new Dictionary<string, string> { { "ApiKey", apiKey.Trim() } };
            File.WriteAllText(_settingsFilePath, JsonSerializer.Serialize(settings, _jsonOptions));
        }

        private static void EnsureParentDirectory(string filePath)
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }
}