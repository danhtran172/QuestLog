using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MasterPlay.Infrastructure.Integrations.Rawg.Models;

namespace MasterPlay.Infrastructure.Integrations.Rawg
{
    /// <summary>
    /// Loi RAWG co ma rieng de tang tren bao DUNG nguyen nhan. Truoc day moi loi deu
    /// thanh `null` im lang, nen "thieu API key" trong giong het "khong tim thay game".
    /// </summary>
    public sealed class RawgException(string code, string message, Exception? inner = null)
        : Exception(message, inner)
    {
        public string Code { get; } = code;
    }

    public static class RawgService
    {
        private const string ApiBase = "https://api.rawg.io/api";

        private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private static string? ApiKey =>
            RuntimeIntegrationCredentials.TryGet("rawg", "apiKey", out var configured)
                ? configured
                : Environment.GetEnvironmentVariable("RAWG_API_KEY")?.Trim();

        public static bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

        /// <summary>
        /// Tra metadata cua game RAWG GIONG TEN NHAT, hoac null khi RAWG khong co game
        /// nao du giong. Nem <see cref="RawgException"/> khi thieu key, key sai, bi gioi
        /// han toc do, hoac RAWG loi — de nguoi dung thay va sua dung cho.
        /// </summary>
        public static async Task<GameMetadata?> GetMetadataAsync(string gameName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(gameName)) return null;

            var apiKey = ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new RawgException("RAWG_NOT_CONFIGURED",
                    "RAWG API key is not configured. Add it in Admin Console → Integrations (or set RAWG_API_KEY).");

            var key = Uri.EscapeDataString(apiKey);
            // Lay KET QUA DAU TIEN theo thu tu lien quan cua chinh RAWG — giu nguyen cach
            // tim cu (da dung dung hang tram lan); khong cham diem lai ten o day.
            var search = await GetAsync<RawgResponse>(
                $"{ApiBase}/games?key={key}&search={Uri.EscapeDataString(gameName.Trim())}&page_size=1",
                cancellationToken).ConfigureAwait(false);

            var best = search?.Results?.FirstOrDefault();
            if (best is null) return null;

            var raw = await GetAsync<RawgData>($"{ApiBase}/games/{best.Id}?key={key}", cancellationToken).ConfigureAwait(false);
            if (raw is null) return null;

            return new GameMetadata
            {
                Name = raw.Name,
                SlugName = raw.Slug,
                Background_Image = raw.Background_Image,
                ReleaseDate = raw.Released,
                // RAWG tra null khi Metacritic chua cham diem; 0 la "khong co", khong phai diem 0.
                MetaScore = raw.Metacritic is > 0 ? raw.Metacritic : null,
                // `playtime` la so gio choi trung binh cua nguoi dung RAWG; 0 nghia la chua co du lieu.
                AvgPlaytime = raw.Playtime is > 0 ? (int)raw.Playtime : null,
                Genres = string.Join(", ", raw.Genres?.Select(g => g.Name) ?? []),
                Developers = string.Join(", ", raw.Developers?.Select(d => d.Name) ?? []),
                Publishers = string.Join(", ", raw.Publishers?.Select(p => p.Name) ?? [])
            };
        }

        private static async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken)
        {
            HttpResponseMessage response;
            try
            {
                response = await Client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new RawgException("RAWG_TIMEOUT", "RAWG did not respond in time.", exception);
            }
            catch (HttpRequestException exception)
            {
                throw new RawgException("RAWG_UNREACHABLE", $"Cannot reach api.rawg.io: {exception.Message}", exception);
            }

            using (response)
            {
                // Khong bao gio dua URL vao thong bao loi: URL chua API key.
                switch (response.StatusCode)
                {
                    case HttpStatusCode.NotFound:
                        return default;
                    case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                        throw new RawgException("RAWG_KEY_INVALID",
                            "RAWG rejected the API key. Check it in Admin Console → Integrations.");
                    case HttpStatusCode.TooManyRequests:
                        throw new RawgException("RAWG_RATE_LIMITED", "RAWG rate limit reached. Try again later.");
                }

                if (!response.IsSuccessStatusCode)
                    throw new RawgException("RAWG_REQUEST_FAILED", $"RAWG returned HTTP {(int)response.StatusCode}.");

                try
                {
                    return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
                }
                catch (JsonException exception)
                {
                    throw new RawgException("RAWG_INVALID_RESPONSE", "RAWG returned a response QuestLog cannot read.", exception);
                }
            }
        }
    }
}
