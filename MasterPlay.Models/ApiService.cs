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

        //public static async Task<string> GetGameImageUrl(string gameName)
        //{
        //    // RAWG yêu cầu slug hoặc tìm kiếm qua tham số 'search'
        //    string url = $"https://api.rawg.io/api/games?key={rawgApiKey}&search={Uri.EscapeDataString(gameName)}&page_size=1";

        //    using HttpClient client = new HttpClient();
        //    try
        //    {
        //        var response = await client.GetFromJsonAsync<RawgSearchResponse>(url);

        //        // Lấy ảnh của kết quả đầu tiên nếu có
        //        if (response?.Results != null && response.Results.Count > 0)
        //        {
        //            return response.Results[0].Background_Image;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"RAWG API Error: {ex.Message}");
        //    }

        //    return "https://via.placeholder.com/600x400?text=No+Image+Found";
        //}
        //public static async Task<string> GetGameFormatedName(string gameName)
        //{

        //    // RAWG yêu cầu slug hoặc tìm kiếm qua tham số 'search'
        //    string url = $"https://api.rawg.io/api/games?key={rawgApiKey}&search={Uri.EscapeDataString(gameName)}&page_size=1";

        //    using HttpClient client = new HttpClient();
        //    try
        //    {
        //        var response = await client.GetFromJsonAsync<RawgSearchResponse>(url);

        //        if (response?.Results != null && response.Results.Count > 0)
        //        {
        //            return response.Results[0].Name;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"RAWG API Error: {ex.Message}");
        //    }

        //    return gameName;
        //}
        //public static async Task<string> GetGameGenre(string gameName)
        //{
        //    string url = $"https://api.rawg.io/api/games?key={rawgApiKey}&search={Uri.EscapeDataString(gameName)}&page_size=1";
        //    using HttpClient client = new HttpClient();
        //    try
        //    {
        //        var response = await client.GetFromJsonAsync<RawgSearchResponse>(url);
        //        var game = response?.Results?.FirstOrDefault();
        //        if (game?.Genres != null)
        //        {
        //            return string.Join(", ", game.Genres.Select(g => g.Name));
        //        }
        //    }
        //    catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }
        //    return "Unknown Genre";
        //}

        //public static async Task<string> GetGameReleaseDate(string gameName)
        //{
        //    string url = $"https://api.rawg.io/api/games?key={rawgApiKey}&search={Uri.EscapeDataString(gameName)}&page_size=1";
        //    using HttpClient client = new HttpClient();
        //    try
        //    {
        //        var response = await client.GetFromJsonAsync<RawgSearchResponse>(url);
        //        return response?.Results?.FirstOrDefault()?.Released ?? "Unknown Date";
        //    }
        //    catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }
        //    return "N/A";
        //}

        //public static async Task<string> GetGameDeveloper(string gameName)
        //{
        //    string url = $"https://api.rawg.io/api/games?key={rawgApiKey}&search={Uri.EscapeDataString(gameName)}&page_size=1";
        //    using HttpClient client = new HttpClient();
        //    try
        //    {
        //        var response = await client.GetFromJsonAsync<RawgSearchResponse>(url);

        //        return "Contacting API Detail recommended for Developer info";
        //    }
        //    catch { return "Unknown Developer"; }
        //}
        //public static async Task<string> GetGamePublisher(string gameName)
        //{
        //    // Tương tự Developer, Publisher thường nằm trong endpoint: /games/{id}
        //    // Dưới đây là cách gọi nếu bạn đã có ID từ hàm Search trước đó
        //    return "Publisher info usually requires Game ID";
        //}

        public static async Task<RawgGame> GetGameDetails(string gameName)
        {
            using HttpClient client = new HttpClient();
            try
            {
                // Bước 1: Tìm kiếm game để lấy ID/Slug chính xác nhất
                string searchUrl = $"https://api.rawg.io/api/games?key={rawgApiKey}&search={Uri.EscapeDataString(gameName)}&page_size=1";
                var searchResponse = await client.GetFromJsonAsync<RawgResponse>(searchUrl);

                if (searchResponse?.Results == null || searchResponse.Results.Count == 0)
                    return null;

                var briefInfo = searchResponse.Results[0];

                // Bước 2: Gọi API chi tiết để lấy đầy đủ Developers và Publishers
                string detailUrl = $"https://api.rawg.io/api/games/{briefInfo.Id}?key={rawgApiKey}";
                var detail = await client.GetFromJsonAsync<RawgGameDetail>(detailUrl);

                if (detail == null) return null;

                // Trả về object theo format bạn yêu cầu
                return new RawgGame
                {
                    Name = detail.Name,
                    Background_Image = detail.Background_Image,
                    ReleaseDate = detail.Released,
                    // Kết hợp các list thành chuỗi phân cách bằng dấu phẩy
                    Genres = string.Join(", ", detail.Genres.Select(g => g.Name)),
                    Developers = string.Join(", ", detail.Developers?.Select(d => d.Name) ?? Enumerable.Empty<string>()),
                    Publishers = string.Join(", ", detail.Publishers?.Select(p => p.Name) ?? Enumerable.Empty<string>())
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
