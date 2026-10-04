using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MasterPlay.Infrastructure.Integrations.Steam
{
    /// <summary>
    /// Anh lay tu CDN cua Steam. Hai vai tro — DOMAIN_RULES §12:
    ///
    ///   banner  `header.jpg`                460x215, CO ten game in san
    ///   poster  `library_600x900_2x.jpg`    600x900, box art, cung co ten game
    ///   hero    `library_hero.jpg`          1920x620, anh SACH (logo la file rieng)
    ///           — chi la nguon du phong cho backdrop khi RAWG khong tra anh.
    ///
    /// Ban `library_600x900.jpg` (khong hau to `_2x`) tra ve 600x900 tren host
    /// cloudflare nhung chi 300x450 tren akamai — do la ly do o day dung `_2x`:
    /// mot ten file cho ket qua khac nhau tuy host la cai bay khong nhin thay.
    ///
    /// Khong phai app nao cung co anh thu vien: Steam tra 404. Vi vay moi URL deu
    /// duoc KIEM CHUNG truoc khi tra ve — ghi mot URL chet vao DB thi loi chi lo
    /// ra luc render, xa noi gay ra no.
    /// </summary>
    public static class SteamImageService
    {
        private const string Cdn = "https://cdn.akamai.steamstatic.com/steam/apps";

        private const string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        /// <summary>
        /// Mot HttpClient dung chung: `new HttpClient()` moi lan goi lam can kiet
        /// socket khi quet ca thu vien (moi instance giu rieng mot connection pool
        /// va socket con o TIME_WAIT rat lau sau khi Dispose).
        /// </summary>
        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            })
            {
                Timeout = TimeSpan.FromSeconds(25),
            };
            client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
            return client;
        }

        private static readonly Regex AppIdPattern =
            new(@"data-ds-appid=""(\d+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string PosterUrl(int appId) => $"{Cdn}/{appId}/library_600x900_2x.jpg";

        public static string BannerUrl(int appId) => $"{Cdn}/{appId}/header.jpg";

        public static string HeroUrl(int appId) => $"{Cdn}/{appId}/library_hero.jpg";

        /// <summary>
        /// API tim kiem cua Steam client. Nam tren `api.steampowered.com` — host KHAC
        /// `store.steampowered.com`, va van thong o nhung mang chan trang store
        /// (thuc te 2026-10-04: store bi reset ket noi TLS, API va CDN van chay).
        /// </summary>
        private const string SearchApi = "https://api.steampowered.com/IStoreQueryService/SearchSuggestions/v1/";

        /// <summary>
        /// Tim appid theo ten game.
        ///
        /// CANH BAO: lay KET QUA DAU TIEN cua trang tim kiem. Voi ten mo ho hoac
        /// game co nhieu ban (Remastered, Definitive Edition, ban demo) ket qua co
        /// the khong phai game dang can. Day la gioi han cua chinh nguon du lieu,
        /// khong phai cua doan ma nay — nen tang tren phai coi ket qua la GOI Y va
        /// cho nguoi dung nhin thay anh truoc khi luu.
        ///
        /// Hoi API tim kiem truoc, trang tim kiem cua store sau.
        ///
        /// Tra `null` khi khong tim thay HOAC khong goi duoc ca hai. Hai truong hop
        /// nay phan biet duoc qua <see cref="SearchReachableAsync"/>.
        /// </summary>
        public static async Task<int?> GetAppIdAsync(string gameName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(gameName)) return null;

            var term = gameName.Trim();
            return await SearchApiAppIdAsync(term, cancellationToken).ConfigureAwait(false)
                ?? await SearchPageAppIdAsync(term, cancellationToken).ConfigureAwait(false);
        }

        private static string SearchApiUrl(string gameName)
        {
            var input = "{\"search_term\":\"" + JsonEncodedText.Encode(gameName) + "\","
                + "\"max_results\":1,\"context\":{\"language\":\"english\",\"country_code\":\"US\"}}";
            return SearchApi + "?input_json=" + Uri.EscapeDataString(input);
        }

        private static async Task<int?> SearchApiAppIdAsync(string gameName, CancellationToken cancellationToken)
        {
            try
            {
                var json = await Http.GetStringAsync(SearchApiUrl(gameName), cancellationToken).ConfigureAwait(false);
                using var document = JsonDocument.Parse(json);
                if (!document.RootElement.TryGetProperty("response", out var response)
                    || !response.TryGetProperty("ids", out var ids)
                    || ids.ValueKind != JsonValueKind.Array)
                    return null;

                foreach (var item in ids.EnumerateArray())
                {
                    if (item.TryGetProperty("appid", out var appId) && appId.TryGetInt32(out var value) && value > 0)
                        return value;
                }
                return null;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
            {
                Console.WriteLine($"[Steam] Khong goi duoc API tim kiem ({gameName}): {exception.Message}");
                return null;
            }
        }

        /// <summary>Cach cu: doc `data-ds-appid` dau tien tu trang tim kiem cua store.</summary>
        private static async Task<int?> SearchPageAppIdAsync(string gameName, CancellationToken cancellationToken)
        {
            var url = "https://store.steampowered.com/search/?term=" + Uri.EscapeDataString(gameName);

            string html;
            try
            {
                html = await Http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                Console.WriteLine($"[Steam] Khong goi duoc trang tim kiem ({gameName}): {exception.Message}");
                return null;
            }

            var match = AppIdPattern.Match(html);
            return match.Success && int.TryParse(match.Groups[1].Value, out var appId) ? appId : null;
        }

        /// <summary>
        /// Co it nhat MOT duong tim kiem appid (API hoac trang store) goi duoc tu
        /// may nay khong.
        ///
        /// Ton tai rieng vi mot so mang (ISP, DNS cua router) chan hoac tra `127.0.0.1`
        /// cho `store.steampowered.com` trong khi CDN anh van thong. Khi do moi lan sync
        /// deu that bai giong het nhu "khong tim thay game" — hai chuyen khac han
        /// nhau, va gop chung lai thi nguoi dung se di sua nham cho.
        /// </summary>
        public static async Task<bool> SearchReachableAsync(CancellationToken cancellationToken = default) =>
            await HostReachableAsync(SearchApiUrl("steam"), cancellationToken).ConfigureAwait(false)
            || await HostReachableAsync("https://store.steampowered.com/", cancellationToken).ConfigureAwait(false);

        private static async Task<bool> HostReachableAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                return response.IsSuccessStatusCode || (int)response.StatusCode < 500;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                return false;
            }
        }

        /// <summary>URL co that su tra ve mot tam anh khong.</summary>
        public static async Task<bool> ImageExistsAsync(string url, CancellationToken cancellationToken = default)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                return response.IsSuccessStatusCode
                    && response.Content.Headers.ContentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                return false;
            }
        }

        /// <summary>Anh doc 600x900 cho mot ten game, `null` neu khong co.</summary>
        public static Task<string?> GetPosterAsync(string gameName, CancellationToken cancellationToken = default) =>
            ResolveAsync(gameName, PosterUrl, cancellationToken);

        /// <summary>Anh ngang 460x215 cho mot ten game, `null` neu khong co.</summary>
        public static Task<string?> GetBannerAsync(string gameName, CancellationToken cancellationToken = default) =>
            ResolveAsync(gameName, BannerUrl, cancellationToken);

        /// <summary>Anh hero sach 1920x620 cho mot ten game, `null` neu khong co.</summary>
        public static Task<string?> GetHeroAsync(string gameName, CancellationToken cancellationToken = default) =>
            ResolveAsync(gameName, HeroUrl, cancellationToken);

        /// <summary>
        /// Banner va hero cua CUNG mot game, chi tim appid mot lan: sync metadata can
        /// ca hai, tim hai lan vua cham vua co the ra hai game khac nhau.
        /// </summary>
        public static async Task<(string? Banner, string? Hero)> GetBannerAndHeroAsync(
            string gameName,
            CancellationToken cancellationToken = default)
        {
            var appId = await GetAppIdAsync(gameName, cancellationToken).ConfigureAwait(false);
            if (appId is null) return (null, null);

            var banner = VerifiedAsync(BannerUrl(appId.Value), cancellationToken);
            var hero = VerifiedAsync(HeroUrl(appId.Value), cancellationToken);
            return (await banner.ConfigureAwait(false), await hero.ConfigureAwait(false));
        }

        private static async Task<string?> VerifiedAsync(string url, CancellationToken cancellationToken) =>
            await ImageExistsAsync(url, cancellationToken).ConfigureAwait(false) ? url : null;

        private static async Task<string?> ResolveAsync(
            string gameName,
            Func<int, string> build,
            CancellationToken cancellationToken)
        {
            var appId = await GetAppIdAsync(gameName, cancellationToken).ConfigureAwait(false);
            if (appId is null) return null;

            return await VerifiedAsync(build(appId.Value), cancellationToken).ConfigureAwait(false);
        }
    }
}
