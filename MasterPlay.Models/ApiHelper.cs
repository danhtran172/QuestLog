using GenerativeAI;
using Google.GenAI;
using Google.GenAI.Types;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MasterPlay.Models
{
    public class ApiHelper
    {
        private const string rawgApiKey = "cbf5c4560d9d4e388d1294afe77d574d";
        private const string geminiApiKey = "AIzaSyAtWwdGUXOIBNQhTjhm74DJV4wlEbP7Pb8";
        private static readonly HttpClient client = new HttpClient();

        public static async Task<string> GetGameImageUrlAsync(string gameName)
        {
            // RAWG yêu cầu slug hoặc tìm kiếm qua tham số 'search'
            string url = $"https://api.rawg.io/api/games?key={rawgApiKey}&search={Uri.EscapeDataString(gameName)}&page_size=1";

            using HttpClient client = new HttpClient();
            try
            {
                var response = await client.GetFromJsonAsync<RawgResponse>(url);

                // Lấy ảnh của kết quả đầu tiên nếu có
                if (response?.Results != null && response.Results.Count > 0)
                {
                    return response.Results[0].Background_Image;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RAWG API Error: {ex.Message}");
            }

            return "https://via.placeholder.com/600x400?text=No+Image+Found";
        }
        public static async Task<string> GetGameFormatedNameAsync(string gameName)
        {

            // RAWG yêu cầu slug hoặc tìm kiếm qua tham số 'search'
            string url = $"https://api.rawg.io/api/games?key={rawgApiKey}&search={Uri.EscapeDataString(gameName)}&page_size=1";

            using HttpClient client = new HttpClient();
            try
            {
                var response = await client.GetFromJsonAsync<RawgResponse>(url);

                // Lấy ảnh của kết quả đầu tiên nếu có
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
    }
}
