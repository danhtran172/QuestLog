using MasterPlay.Infrastructure.Integrations.Rawg.Models;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using MasterPlay.Infrastructure.Integrations;

namespace MasterPlay.Infrastructure.Integrations.Rawg
{
    public class RawgService
    {
        private static readonly HttpClient client = new HttpClient();
        private static readonly string _dbPath = Path.Combine(
                            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                            @"Playnite\library\games.db");

        private static string? ApiKey =>
            RuntimeIntegrationCredentials.TryGet("rawg", "apiKey", out var configured)
                ? configured
                : Environment.GetEnvironmentVariable("RAWG_API_KEY")?.Trim();

        public static async Task<string> GetGameFormatedName(string gameName)
        {
            var rawgApiKey = ApiKey;
            if (string.IsNullOrWhiteSpace(rawgApiKey)) return gameName;

            string url = $"https://api.rawg.io/api/games?key={rawgApiKey}&search={Uri.EscapeDataString(gameName)}&page_size=1";

            using HttpClient client = new HttpClient();
            try
            {
                var response = await client.GetFromJsonAsync<RawgResponse>(url);

                if (response?.Results != null && response.Results.Count > 0)
                {
                    return response.Results[0].Name;
                }
            }
            catch (Exception)
            {
                Console.WriteLine("[RAWG] Request failed.");
            }

            return gameName;
        }
        public static async Task<string> GetGameCover(string gameName, string oldCover)
        {
            var rawgApiKey = ApiKey;
            if (string.IsNullOrWhiteSpace(rawgApiKey)) return oldCover;

            string url = $"https://api.rawg.io/api/games?key={rawgApiKey}&search={Uri.EscapeDataString(gameName)}&page_size=1";

            using HttpClient client = new HttpClient();
            try
            {
                var response = await client.GetFromJsonAsync<RawgResponse>(url);

                if (response?.Results != null && response.Results.Count > 0)
                {
                    return response.Results[0].Background_Image;
                }
            }
            catch (Exception)
            {
                Console.WriteLine("[RAWG] Request failed.");
            }

            return oldCover;
        }

        public static async Task<GameMetadata> GetMetadata_Rawg(string gameName)
        {
            var rawgApiKey = ApiKey;
            if (string.IsNullOrWhiteSpace(rawgApiKey)) return null;
            using HttpClient client = new HttpClient();
            try
            {
                string searchUrl = $"https://api.rawg.io/api/games?key={rawgApiKey}&search={Uri.EscapeDataString(gameName)}&page_size=1";
                var searchResponse = await client.GetFromJsonAsync<RawgResponse>(searchUrl);

                if (searchResponse?.Results == null || searchResponse.Results.Count == 0)
                    return null;

                var gameId = searchResponse.Results[0].Id;

                string detailUrl = $"https://api.rawg.io/api/games/{gameId}?key={rawgApiKey}";
                var raw = await client.GetFromJsonAsync<RawgData>(detailUrl);

                if (raw == null) return null;

                return new GameMetadata
                {
                    Name = raw.Name,
                    SlugName = raw.Slug,
                    Background_Image = raw.Background_Image,
                    ReleaseDate = raw.Released,
                    MetaScore = raw.Metacritic ?? 0,
                    AvgPlaytime = (int?)raw.Playtime,
                    Genres = string.Join(", ", raw.Genres.Select(g => g.Name)),
                    Developers = string.Join(", ", raw.Developers?.Select(d => d.Name) ?? Enumerable.Empty<string>()),
                    Publishers = string.Join(", ", raw.Publishers?.Select(p => p.Name) ?? Enumerable.Empty<string>())
                };
            }
            catch (Exception)
            {
                Console.WriteLine("[RAWG] Metadata request failed.");
                return null;
            }
        }

    }
}
