using MinimalGameLauncher.Models;
using MinimalGameLauncher.Services;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace AeroLauncher.Tests
{
    public class LibraryServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _gamesFile;
        private readonly string _settingsFile;
        private readonly LibraryService _service;

        public LibraryServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"AeroLauncherTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);

            _gamesFile = Path.Combine(_tempDir, "games.json");
            _settingsFile = Path.Combine(_tempDir, "settings.json");
            _service = new LibraryService(_gamesFile, _settingsFile);
        }

        [Fact]
        public void LoadGames_WhenFileDoesNotExist_ReturnsEmptyList()
        {
            List<GameItem> games = _service.LoadGames();

            Assert.NotNull(games);
            Assert.Empty(games);
        }

        [Fact]
        public void AddGame_PersistsToDisk_AndResetsTransientUiStateOnReload()
        {
            var games = new List<GameItem>();
            var newGame = new GameItem
            {
                Title = "Rise of the Tomb Raider",
                ExecutablePath = @"C:\Games\ROTTR\ROTTR.exe",
                ImagePath = @"C:\Covers\rottr_poster.jpg",
                IsSelected = true,
                IsDownloading = true
            };

            _service.AddGame(games, newGame);
            List<GameItem> reloaded = _service.LoadGames();

            Assert.Single(reloaded);
            Assert.Equal("Rise of the Tomb Raider", reloaded[0].Title);
            Assert.Equal(@"C:\Games\ROTTR\ROTTR.exe", reloaded[0].ExecutablePath);
            Assert.Equal(@"C:\Covers\rottr_poster.jpg", reloaded[0].HeroImagePath); // Fallback check
            Assert.False(reloaded[0].IsSelected);
            Assert.False(reloaded[0].IsDownloading);
        }

        [Fact]
        public void RemoveGameAt_WithValidIndex_RemovesItemAndUpdatesFile()
        {
            var games = new List<GameItem>
            {
                new() { Title = "Game One", ExecutablePath = @"C:\g1.exe" },
                new() { Title = "Game Two", ExecutablePath = @"C:\g2.exe" }
            };
            _service.SaveGames(games);

            bool removed = _service.RemoveGameAt(games, 0);
            List<GameItem> reloaded = _service.LoadGames();

            Assert.True(removed);
            Assert.Single(reloaded);
            Assert.Equal("Game Two", reloaded[0].Title);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(5)]
        public void RemoveGameAt_WithInvalidIndex_ReturnsFalse(int invalidIndex)
        {
            var games = new List<GameItem>
            {
                new() { Title = "Only Game", ExecutablePath = @"C:\g1.exe" }
            };

            bool removed = _service.RemoveGameAt(games, invalidIndex);

            Assert.False(removed);
            Assert.Single(games);
        }

        [Fact]
        public void SaveApiKey_AndLoadApiKey_TrimsWhitespaceAndPersists()
        {
            _service.SaveApiKey("  sgdb_live_token_12345  ");
            string loadedKey = _service.LoadApiKey();

            Assert.Equal("sgdb_live_token_12345", loadedKey);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
    }
}