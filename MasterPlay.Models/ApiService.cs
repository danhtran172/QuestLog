using GenerativeAI;
using Google.GenAI;
using Google.GenAI.Types;
using MasterPlay.Models.Models;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MasterPlay.Models
{
    public class ApiService
    {
        private const string rawgApiKey = "cbf5c4560d9d4e388d1294afe77d574d";
        private const string geminiApiKey = "AIzaSyAtWwdGUXOIBNQhTjhm74DJV4wlEbP7Pb8";
        private static readonly HttpClient client = new HttpClient();


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
                    Background_Image = raw.Background_Image,
                    ReleaseDate = raw.Released,
                    MetaScore = (raw.Metacritic != null) ? int.Parse(raw.Metacritic.ToString()) : 0,
                    AvgPlaytime = raw.Playtime,
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
        public static DateOnly? ParseDate(string dateString)
        {
            if (string.IsNullOrWhiteSpace(dateString))
                return null;

            string[] formats = {
            "dd/MM/yyyy", "d/m/yyyy",
            "dd-MM-yyyy", "d-m-yyyy",
            "yyyy-MM-dd", "yyyy/MM/dd"
        };

            if (DateOnly.TryParseExact(dateString,
                                       formats,
                                       CultureInfo.InvariantCulture,
                                       DateTimeStyles.None,
                                       out DateOnly result))
            {
                return result;
            }

            return null;
        }

    }
}
public static class DateExtensions
{
    public static string DateDisplay(this DateOnly date)
    {
        return date.ToString("dd-MM-yyyy");
    }
}