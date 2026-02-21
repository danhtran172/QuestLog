using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class HowLongToBeatService
{
    private static readonly HttpClient _httpClient = new HttpClient();

    static HowLongToBeatService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Referrer = new Uri("https://howlongtobeat.com/");
        _httpClient.DefaultRequestHeaders.Add("Origin", "https://howlongtobeat.com");
    }

    public static async Task<int> GetMainExtraPlaytimeAsync(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
        {
            throw new ArgumentException("Game name cannot be empty or whitespace", nameof(gameName));
        }

        var payload = new
        {
            searchType = "games",
            searchTerms = gameName.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            searchPage = 1,
            size = 20,
            searchOptions = new
            {
                games = new
                {
                    userId = 0,
                    platform = "",
                    sortCategory = "popular",
                    rangeCategory = "main",
                    rangeTime = new { min = 0, max = 0 },
                    gameplay = new { style = "comp", amount = 0 },
                    modifier = ""
                },
                users = new { sortCategory = "postcount" },
                lists = new { sortCategory = "followcount" },
                groups = new { }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("https://howlongtobeat.com/api/search", content);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"HTTP error: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
        }

        var jsonResponse = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonResponse);

        var games = doc.RootElement.GetProperty("data").EnumerateArray();

        if (!games.MoveNext())
        {
            throw new Exception("Game not found");
        }

        var firstGame = games.Current;
        var mainExtraElement = firstGame.GetProperty("comp_main_plus").GetProperty("value"); // hoặc "comp_plus" tùy field

        if (mainExtraElement.ValueKind == JsonValueKind.Null || mainExtraElement.GetInt32() == 0)
        {
            throw new Exception("Main + Extras playtime not available");
        }

        return mainExtraElement.GetInt32() / 60 / 60; // HLTB API trả giây → convert sang hours
    }
}