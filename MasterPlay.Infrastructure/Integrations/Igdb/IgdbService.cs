using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MasterPlay.Core.Entities;
using MasterPlay.Infrastructure.Configuration;
using MasterPlay.Infrastructure.Integrations;
using MasterPlay.Infrastructure.Integrations.Igdb.Models;

namespace MasterPlay.Infrastructure.Integrations.Igdb;

/// <summary>
/// Client IGDB API v4.
///
/// Khac RAWG o ba diem phai nho:
///   1. Xac thuc bang Twitch OAuth2 (client_credentials), KHONG phai api key tren query string.
///   2. Moi request la POST, body viet bang APIcalypse ("fields ...; where ...; limit N;").
///   3. Rate limit 4 req/giay — <see cref="ThrottleAsync"/> giu khoang cach toi thieu 250ms.
///
/// Credential doc theo thu tu: bien moi truong -> config.json. KHONG hardcode key trong source.
/// </summary>
public static class IgdbService
{
    private const string TokenEndpoint = "https://id.twitch.tv/oauth2/token";
    private const string GamesEndpoint = "https://api.igdb.com/v4/games";
    private const string ImageBase = "https://images.igdb.com/igdb/image/upload";

    /// <summary>Cover DOC 528x748. Doi size o day thi doi ca DOMAIN_RULES §12.</summary>
    private const string CoverSize = "t_cover_big_2x";

    private const int MinRequestSpacingMs = 250;

    /// <summary>
    /// game_type coi la DLC (noi dung can game goc moi choi duoc).
    /// standalone_expansion CO TINH chu y bi loai: no choi doc lap duoc, nhung van co parent_game
    /// nen BaseGameId van duoc gan.
    /// </summary>
    private static readonly HashSet<string> DlcTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "dlc_addon", "dlc / addon", "dlc", "expansion", "episode", "season", "pack"
    };

    /// <summary>Enum category cu, dung khi game_type khong duoc tra ve. Xem api-docs.igdb.com §game.</summary>
    private static readonly HashSet<int> DlcCategoryIds = new() { 1, 2, 6, 7, 13 };

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly SemaphoreSlim TokenGate = new(1, 1);
    private static readonly SemaphoreSlim ThrottleGate = new(1, 1);

    private static string? _cachedToken;
    private static DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;
    private static DateTimeOffset _lastRequestAt = DateTimeOffset.MinValue;

    /// <summary>
    /// True khi da co ca Client-ID lan Client-Secret. Tang tren PHAI kiem tra truoc khi goi
    /// de tra loi ro rang thay vi de request that bai voi 401.
    /// </summary>
    public static bool IsConfigured
    {
        get
        {
            var (clientId, clientSecret) = ReadCredentials();
            return !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret);
        }
    }

    /// <summary>
    /// Tra cuu mot game theo ten. Tra null khi IGDB khong co ket qua nao.
    /// Nem <see cref="InvalidOperationException"/> khi thieu credential.
    /// </summary>
    public static async Task<IgdbMetadata?> GetMetadataAsync(
        string gameName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gameName))
            return null;

        // Mot request duy nhat lay ca game lan quan he — expander cua IGDB lo phan join.
        // RAWG can 2 request (search roi detail) cho cung luong nay.
        var query = new StringBuilder()
            .Append("fields id, name, slug, first_release_date, category, ")
            .Append("game_type.type, ")
            .Append("cover.image_id, ")
            .Append("parent_game.id, parent_game.name, ")
            .Append("collections.name, franchises.name, ")
            .Append("involved_companies.developer, involved_companies.publisher, ")
            .Append("involved_companies.company.name; ")
            .Append("search \"").Append(EscapeApicalypse(gameName)).Append("\"; ")
            // version_parent != null la cac ban "GOTY Edition"/"Deluxe" tach rieng — khong phai game goc.
            .Append("where version_parent = null; ")
            .Append("limit 5;")
            .ToString();

        var games = await PostAsync(query, cancellationToken).ConfigureAwait(false);
        var match = games.FirstOrDefault(game => !string.IsNullOrWhiteSpace(game.Name));
        return match is null ? null : Project(match);
    }

    /// <summary>
    /// Tra cuu theo IGDB id — dung khi da biet id (vd: resolve parent_game).
    /// </summary>
    public static async Task<IgdbMetadata?> GetMetadataByIdAsync(
        int igdbId,
        CancellationToken cancellationToken = default)
    {
        if (igdbId <= 0)
            return null;

        var query = new StringBuilder()
            .Append("fields id, name, slug, first_release_date, category, ")
            .Append("game_type.type, ")
            .Append("cover.image_id, ")
            .Append("parent_game.id, parent_game.name, ")
            .Append("collections.name, franchises.name, ")
            .Append("involved_companies.developer, involved_companies.publisher, ")
            .Append("involved_companies.company.name; ")
            .Append("where id = ").Append(igdbId.ToString(CultureInfo.InvariantCulture)).Append("; ")
            .Append("limit 1;")
            .ToString();

        var games = await PostAsync(query, cancellationToken).ConfigureAwait(false);
        var match = games.FirstOrDefault();
        return match is null ? null : Project(match);
    }

    private static IgdbMetadata Project(IgdbGame game)
    {
        var companies = game.InvolvedCompanies ?? new List<IgdbInvolvedCompany>();

        static string? JoinCompanies(IEnumerable<IgdbInvolvedCompany> source)
        {
            var names = source
                .Select(item => item.Company?.Name?.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return names.Length == 0 ? null : string.Join(", ", names);
        }

        // Collection = series hep (God of War), franchise = thuong hieu rong (Marvel).
        // Uu tien collection; tang tren se chon ung vien nao khop Series da co.
        var seriesCandidates = (game.Collections ?? new List<IgdbNamed>())
            .Concat(game.Franchises ?? new List<IgdbNamed>())
            .Select(item => item.Name?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new IgdbMetadata
        {
            IgdbId = game.Id,
            Name = game.Name!.Trim(),
            Slug = string.IsNullOrWhiteSpace(game.Slug) ? null : game.Slug.Trim(),
            ReleaseDate = ToDateOnly(game.FirstReleaseDate),
            CoverUrl = string.IsNullOrWhiteSpace(game.Cover?.ImageId)
                ? null
                : $"{ImageBase}/{CoverSize}/{game.Cover.ImageId}.jpg",
            Developers = JoinCompanies(companies.Where(item => item.Developer)),
            Publishers = JoinCompanies(companies.Where(item => item.Publisher)),
            SeriesCandidates = seriesCandidates,
            IsDlc = IsDlcType(game),
            ParentGameIgdbId = game.ParentGame?.Id > 0 ? game.ParentGame.Id : null,
            ParentGameName = string.IsNullOrWhiteSpace(game.ParentGame?.Name)
                ? null
                : game.ParentGame!.Name!.Trim()
        };
    }

    private static bool IsDlcType(IgdbGame game)
    {
        var typeName = game.GameType?.Type?.Trim();
        if (!string.IsNullOrWhiteSpace(typeName))
            return DlcTypeNames.Contains(typeName.Replace(" ", string.Empty).Replace("/", "_"))
                   || DlcTypeNames.Contains(typeName);

        return game.Category.HasValue && DlcCategoryIds.Contains(game.Category.Value);
    }

    /// <summary>first_release_date la Unix seconds UTC.</summary>
    private static DateOnly? ToDateOnly(long? unixSeconds) =>
        unixSeconds is null or <= 0
            ? null
            : DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(unixSeconds.Value).UtcDateTime);

    private static async Task<List<IgdbGame>> PostAsync(string apicalypseQuery, CancellationToken cancellationToken)
    {
        var (clientId, _) = ReadCredentials();
        var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

        await ThrottleAsync(cancellationToken).ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Post, GamesEndpoint)
        {
            Content = new StringContent(apicalypseQuery, Encoding.UTF8, "text/plain")
        };
        request.Headers.Add("Client-ID", clientId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // Token het han som hon expires_in (vd: bi thu hoi) — xoa cache de lan sau lay lai.
            InvalidateToken();
            throw new HttpRequestException("IGDB rejected the access token (401). Token cache was cleared; retry the sync.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"IGDB request failed ({(int)response.StatusCode}).");
        }

        var games = await response.Content
            .ReadFromJsonAsync<List<IgdbGame>>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return games ?? new List<IgdbGame>();
    }

    private static async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
            return _cachedToken;

        await TokenGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
                return _cachedToken;

            var (clientId, clientSecret) = ReadCredentials();
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new InvalidOperationException(
                    "IGDB is not configured. Set IGDB_CLIENT_ID and IGDB_CLIENT_SECRET, " +
                    "or fill IgdbClientId / IgdbClientSecret in config.json.");
            }

            var url = $"{TokenEndpoint}?client_id={Uri.EscapeDataString(clientId)}" +
                      $"&client_secret={Uri.EscapeDataString(clientSecret)}" +
                      "&grant_type=client_credentials";

            using var response = await Client.PostAsync(url, content: null, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Twitch token request failed ({(int)response.StatusCode}).");
            }

            var payload = await response.Content
                .ReadFromJsonAsync<TwitchTokenResponse>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(payload?.AccessToken))
                throw new HttpRequestException("Twitch returned an empty access token.");

            _cachedToken = payload.AccessToken;
            // Tru 5 phut de khong dung token sap het han giua chung.
            var lifetime = Math.Max(60, payload.ExpiresIn - 300);
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(lifetime);
            return _cachedToken;
        }
        finally
        {
            TokenGate.Release();
        }
    }

    private static void InvalidateToken()
    {
        _cachedToken = null;
        _tokenExpiresAt = DateTimeOffset.MinValue;
    }

    /// <summary>Giu khoang cach toi thieu giua hai request de khong dinh 429 (IGDB: 4 req/s).</summary>
    private static async Task ThrottleAsync(CancellationToken cancellationToken)
    {
        await ThrottleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var elapsed = DateTimeOffset.UtcNow - _lastRequestAt;
            var wait = TimeSpan.FromMilliseconds(MinRequestSpacingMs) - elapsed;
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            _lastRequestAt = DateTimeOffset.UtcNow;
        }
        finally
        {
            ThrottleGate.Release();
        }
    }

    private static (string? ClientId, string? ClientSecret) ReadCredentials()
    {
        if (RuntimeIntegrationCredentials.TryGet("igdb", "clientId", out var vaultClientId) &&
            RuntimeIntegrationCredentials.TryGet("igdb", "clientSecret", out var vaultClientSecret))
        {
            return (vaultClientId, vaultClientSecret);
        }

        var clientId = Environment.GetEnvironmentVariable("IGDB_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("IGDB_CLIENT_SECRET");

        if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
            return (clientId.Trim(), clientSecret.Trim());

        Config config;
        try
        {
            config = JsonService.LoadConfig();
        }
        catch (Exception)
        {
            return (clientId?.Trim(), clientSecret?.Trim());
        }

        if (string.IsNullOrWhiteSpace(clientId))
            clientId = config.IgdbClientId;
        if (string.IsNullOrWhiteSpace(clientSecret))
            clientSecret = config.IgdbClientSecret;

        return (
            string.IsNullOrWhiteSpace(clientId) ? null : clientId.Trim(),
            string.IsNullOrWhiteSpace(clientSecret) ? null : clientSecret.Trim());
    }

    /// <summary>APIcalypse boc chuoi search bang dau nhay kep — phai escape.</summary>
    private static string EscapeApicalypse(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"").Trim();

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
