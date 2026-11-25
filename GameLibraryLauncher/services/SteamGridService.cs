using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace MinimalGameLauncher.Services
{
    public class SteamGridService
    {
        private readonly HttpClient _httpClient;
        private readonly string _coversFolderPath;

        public SteamGridService(HttpClient httpClient, string coversFolderPath)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _coversFolderPath = coversFolderPath ?? throw new ArgumentNullException(nameof(coversFolderPath));
        }

        public static string SanitizeFileName(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "Unknown_Game";

            return string.Join("_", title.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        }

        public async Task<(string posterPath, string heroPath)> DownloadCoverArtAsync(string gameTitle, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(gameTitle))
                return (string.Empty, string.Empty);

            try
            {
                Directory.CreateDirectory(_coversFolderPath);

                // 1. Search for the SteamGridDB Game ID
                string searchUrl = $"https://www.steamgriddb.com/api/v2/search/autocomplete/{Uri.EscapeDataString(gameTitle)}";
                string searchResponse = await GetAuthorizedStringAsync(searchUrl, apiKey);
                using JsonDocument searchDoc = JsonDocument.Parse(searchResponse);

                var dataArray = searchDoc.RootElement.GetProperty("data");
                if (dataArray.GetArrayLength() == 0)
                    return (string.Empty, string.Empty);

                string gameId = dataArray[0].GetProperty("id").GetInt32().ToString();
                string cleanTitle = SanitizeFileName(gameTitle);

                // 2. Fetch Vertical Poster (600x900)
                string posterUrl = $"https://www.steamgriddb.com/api/v2/grids/game/{gameId}?dimensions=600x900";
                string finalPosterPath = await DownloadFirstAssetAsync(posterUrl, apiKey, $"{cleanTitle}_poster.jpg");

                // 3. Fetch Wide Hero Banner
                string heroUrl = $"https://www.steamgriddb.com/api/v2/heroes/game/{gameId}";
                string finalHeroPath = await DownloadFirstAssetAsync(heroUrl, apiKey, $"{cleanTitle}_hero.jpg");

                return (finalPosterPath, finalHeroPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SteamGridDB API Download Failed: {ex.Message}");
                return (string.Empty, string.Empty);
            }
        }

        private async Task<string> DownloadFirstAssetAsync(string apiUrl, string apiKey, string outputFileName)
        {
            string jsonResponse = await GetAuthorizedStringAsync(apiUrl, apiKey);
            using JsonDocument doc = JsonDocument.Parse(jsonResponse);

            var dataArray = doc.RootElement.GetProperty("data");
            if (dataArray.GetArrayLength() == 0)
                return string.Empty;

            string assetUrl = dataArray[0].GetProperty("url").GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(assetUrl))
                return string.Empty;

            byte[] imageBytes = await _httpClient.GetByteArrayAsync(assetUrl);
            string localPath = Path.Combine(_coversFolderPath, outputFileName);
            await File.WriteAllBytesAsync(localPath, imageBytes);

            return localPath;
        }

        private async Task<string> GetAuthorizedStringAsync(string url, string apiKey)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}