using System.Net.Http.Json;
using System.Text.Json;
using MasterPlay.Infrastructure.Integrations;

namespace MasterPlay.Infrastructure.Integrations.Gemini;

public static class GeminiService
{
    private const string Model = "gemini-2.5-flash";
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public static async Task<string?> GetExecutableNameAsync(
        string gameName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gameName))
            return null;

        var apiKey = RuntimeIntegrationCredentials.TryGet("gemini", "apiKey", out var configured)
            ? configured
            : Environment.GetEnvironmentVariable("GEMINI_API_KEY")?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        try
        {
            var request = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new
                            {
                                text = $"Return the primary Windows executable filename for the PC game '{gameName}'. " +
                                       "Do not guess. If you are not confident, return an empty string. " +
                                       "Exclude launchers, installers, anti-cheat bootstrap executables, and file paths."
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    responseSchema = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            executableName = new { type = "STRING" }
                        },
                        required = new[] { "executableName" }
                    }
                }
            };

            using var response = await Client.PostAsJsonAsync(
                $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={Uri.EscapeDataString(apiKey)}",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            if (!document.RootElement.TryGetProperty("candidates", out var candidates) ||
                candidates.GetArrayLength() == 0)
            {
                return null;
            }

            var text = candidates[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(text))
                return null;

            using var result = JsonDocument.Parse(text);
            if (!result.RootElement.TryGetProperty("executableName", out var executable))
                return null;

            return NormalizeExecutableName(executable.GetString());
        }
        catch (Exception)
        {
            Console.WriteLine("[Gemini] Executable lookup skipped.");
            return null;
        }
    }

    private static string? NormalizeExecutableName(string? executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName))
            return null;

        var normalized = Path.GetFileName(executableName.Trim());
        if (!normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            normalized.Length <= ".exe".Length ||
            normalized.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            normalized.Contains('/') || normalized.Contains('\\'))
        {
            return null;
        }

        return Path.GetFileNameWithoutExtension(normalized);
    }
}
