using GenerativeAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Linq;


namespace MasterPlay.Models
{
    public class ApiHelper
    {
        private static readonly HttpClient client = new HttpClient();
        public static async Task<GameInfo?> GetGameMetadataAsync(string inputGameName)
        {
            // 1. Khởi tạo Model
            var apiKey = "AIzaSyCy6u-vvezcdQPGr7D8ojiKAUvpjs5pIoE";
            var client = new GenerativeModel(apiKey: apiKey, model: "gemini-3.0-flash");

            // 2. Thiết lập Prompt chi tiết
            string prompt = $@"
        Find information for the game with name is or most similar to: '{inputGameName}'.
        Return a JSON object with:
        - 'Name': The official game name formatted for RAWG database.
        - 'ExeName': The common process executable name (without .exe).
        - 'SaveFolderPath': The standard Windows save game location (use placeholders like %USERPROFILE% or C:\Users\UserName).
        
        Example format:
        {{
            ""Name"": ""Cyberpunk 2077"",
            ""ExeName"": ""Cyberpunk2077"",
            ""SaveFolderPath"": ""%USERPROFILE%\\Saved Games\\CD Projekt Red\\Cyberpunk 2077""
        }}
        
        Return ONLY the JSON. No markdown backticks.";

            try
            {
                // 3. Gọi API
                var response = await client.GenerateContentAsync(prompt);
                string jsonResponse = response.Text.Trim();

                // 4. Parse JSON sang Object
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<GameInfo>(jsonResponse, options);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return null;
            }
        }

        public static async Task<string> GetGameImageUrlAsync(string gameName)
        {
            string rawgApiKey = "cbf5c4560d9d4e388d1294afe77d574d";

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
            string rawgApiKey = "cbf5c4560d9d4e388d1294afe77d574d";

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

        public static async Task<string> GetProcessNameAsync(string gameName)
        {
            try
            {
                // Endpoint API của PCGamingWiki
                // Chúng ta dùng action=cargoquery để truy vấn bảng dữ liệu kỹ thuật
                string url = $"https://www.pcgamingwiki.com/w/api.php?action=cargoquery" +
                             $"&tables=Infobox_game" +
                             $"&fields=Executable_file_path" +
                             $"&where=Name_alt='{gameName}' OR _pageName='{gameName}'" +
                             $"&format=json";

                client.DefaultRequestHeaders.UserAgent.ParseAdd("MasterPlayApp/1.0 (Contact: your@email.com)");

                var response = await client.GetStringAsync(url);
                var json = JObject.Parse(response);

                // Parse kết quả từ cấu trúc phức tạp của MediaWiki Cargo
                var results = json["cargoquery"];
                if (results != null && results.HasValues)
                {
                    // Lấy bản ghi đầu tiên
                    var exePath = results[0]["title"]?["Executable file path"]?.ToString();
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        // Thường kết quả trả về có thể là đường dẫn hoặc nhiều file, 
                        // ta làm sạch để lấy tên file .exe
                        return CleanExeName(exePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching from PCGamingWiki: {ex.Message}");
            }

            return null;
        }

        private static string CleanExeName(string path)
        {
            // PCGamingWiki đôi khi để nhiều file exe ngăn cách bởi dấu phẩy
            // Ví dụ: "game.exe, game_x64.exe"
            var primaryExe = path.Split(',').FirstOrDefault()?.Trim();

            // Loại bỏ đường dẫn thư mục nếu có
            if (primaryExe != null && primaryExe.Contains("\\"))
            {
                primaryExe = System.IO.Path.GetFileName(primaryExe);
            }

            return primaryExe;
        }

    }
}
