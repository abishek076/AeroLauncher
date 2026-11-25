using MinimalGameLauncher.Services;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AeroLauncher.Tests
{
    public class SteamGridServiceTests : IDisposable
    {
        private readonly string _tempCoversDir;

        public SteamGridServiceTests()
        {
            _tempCoversDir = Path.Combine(Path.GetTempPath(), $"AeroCoversTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempCoversDir);
        }

        [Fact]
        public void SanitizeFileName_RemovesInvalidPathCharacters()
        {
            string sanitized = SteamGridService.SanitizeFileName("Cyberpunk: 2077 / Phantom*Liberty?");

            Assert.DoesNotContain(":", sanitized);
            Assert.DoesNotContain("/", sanitized);
            Assert.DoesNotContain("*", sanitized);
            Assert.DoesNotContain("?", sanitized);
        }

        [Fact]
        public async Task DownloadCoverArtAsync_WhenApiKeyIsEmpty_ReturnsEmptyStringsWithoutCallingApi()
        {
            bool requestSent = false;
            var handler = new StubHttpMessageHandler(_ =>
            {
                requestSent = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            using var httpClient = new HttpClient(handler);
            var service = new SteamGridService(httpClient, _tempCoversDir);

            var (posterPath, heroPath) = await service.DownloadCoverArtAsync("Halo", "");

            Assert.False(requestSent);
            Assert.Equal(string.Empty, posterPath);
            Assert.Equal(string.Empty, heroPath);
        }

        [Fact]
        public async Task DownloadCoverArtAsync_WhenApiReturns401Unauthorized_DegradesGracefully()
        {
            var handler = new StubHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.Unauthorized));

            using var httpClient = new HttpClient(handler);
            var service = new SteamGridService(httpClient, _tempCoversDir);

            var (posterPath, heroPath) = await service.DownloadCoverArtAsync("Need for Speed", "invalid_key");

            Assert.Equal(string.Empty, posterPath);
            Assert.Equal(string.Empty, heroPath);
        }

        [Fact]
        public async Task DownloadCoverArtAsync_WhenValidResponsesReturned_SavesPosterAndHeroFiles()
        {
            byte[] fakeJpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };

            var handler = new StubHttpMessageHandler(request =>
            {
                string url = request.RequestUri!.ToString();

                if (url.Contains("/search/autocomplete/"))
                {
                    Assert.Equal("Bearer valid_test_key", request.Headers.Authorization?.ToString());
                    string json = """{ "success": true, "data": [ { "id": 9942, "name": "Hades" } ] }""";
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };
                }

                if (url.Contains("/grids/game/9942"))
                {
                    string json = """{ "success": true, "data": [ { "url": "https://cdn.example.com/hades_poster.jpg" } ] }""";
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };
                }

                if (url.Contains("/heroes/game/9942"))
                {
                    string json = """{ "success": true, "data": [ { "url": "https://cdn.example.com/hades_hero.jpg" } ] }""";
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };
                }

                if (url.StartsWith("https://cdn.example.com/"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(fakeJpegBytes)
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            using var httpClient = new HttpClient(handler);
            var service = new SteamGridService(httpClient, _tempCoversDir);

            var (posterPath, heroPath) = await service.DownloadCoverArtAsync("Hades", "valid_test_key");

            Assert.True(File.Exists(posterPath));
            Assert.True(File.Exists(heroPath));
            Assert.EndsWith("Hades_poster.jpg", posterPath);
            Assert.EndsWith("Hades_hero.jpg", heroPath);
            Assert.Equal(fakeJpegBytes, await File.ReadAllBytesAsync(posterPath));
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempCoversDir))
            {
                Directory.Delete(_tempCoversDir, recursive: true);
            }
        }

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

            public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            {
                _responder = responder;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_responder(request));
            }
        }
    }
}