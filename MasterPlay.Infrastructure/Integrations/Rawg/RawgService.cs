using MasterPlay.Infrastructure.Integrations.Rawg.Models;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MasterPlay.Infrastructure.Integrations.Rawg
{
    public class RawgService
    {
        private const string rawgApiKey = "cbf5c4560d9d4e388d1294afe77d574d";
        private static readonly HttpClient client = new HttpClient();
        private static readonly string _dbPath = Path.Combine(
                            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                            @"Playnite\library\games.db");

        public static async Task<string> GetGameFormatedName(string gameName)
        {

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
            catch (Exception ex)
            {
                Console.WriteLine($"RAWG API Error: {ex.Message}");
            }

            return gameName;
        }
        public static async Task<string> GetGameCover(string gameName, string oldCover)
        {

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
            catch (Exception ex)
            {
                Console.WriteLine($"RAWG API Error: {ex.Message}");
            }

            return oldCover;
        }

        public static async Task<GameMetadata> GetMetadata_Rawg(string gameName)
        {
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
            catch (Exception ex)
            {
                Console.WriteLine($"[RAWG Error]: {ex.Message}");
                return null;
            }
        }

        public static async Task<string> GetGalleryImage(string gameName)
        {
            var appId = await GetSteamAppId(gameName);
            return appId.HasValue
                ? $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg"
                : null;
        }

        public static async Task<string> GetSteamLibraryImage(string gameName)
        {
            var appId = await GetSteamAppId(gameName);
            if (!appId.HasValue)
                return null;

            var inputJson = JsonSerializer.Serialize(new
            {
                ids = new[] { new { appid = appId.Value } },
                context = new { country_code = "US" },
                data_request = new { include_assets = true }
            });
            var url = "https://api.steampowered.com/IStoreBrowseService/GetItems/v1/?input_json=" +
                      Uri.EscapeDataString(inputJson);

            using var steamClient = new HttpClient();
            try
            {
                using var response = await steamClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return null;

                using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
                var storeItems = document.RootElement
                    .GetProperty("response")
                    .GetProperty("store_items");

                if (storeItems.GetArrayLength() == 0)
                    return null;

                var assets = storeItems[0].GetProperty("assets");
                if (!assets.TryGetProperty("asset_url_format", out var formatElement) ||
                    !assets.TryGetProperty("library_capsule_2x", out var fileNameElement))
                    return null;

                var assetPath = formatElement.GetString()?.Replace("${FILENAME}", fileNameElement.GetString());
                return string.IsNullOrWhiteSpace(assetPath)
                    ? null
                    : $"https://shared.cloudflare.steamstatic.com/store_item_assets/{assetPath}";
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static async Task<int?> GetSteamAppId(string gameName)
        {
            if (string.IsNullOrWhiteSpace(gameName))
            {
                return null;
            }

            var query = Uri.EscapeDataString(gameName);
            var url = $"https://store.steampowered.com/search/?term={query}";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

            string html;
            try
            {
                html = await client.GetStringAsync(url);
            }
            catch (Exception)
            {
                return null;
            }
            var regex = new Regex(@"data-ds-appid=""(\d+)""", RegexOptions.IgnoreCase);
            var match = regex.Match(html);

            return match.Success && int.TryParse(match.Groups[1].Value, out var appId)
                ? appId
                : null;
        }
    }
}
