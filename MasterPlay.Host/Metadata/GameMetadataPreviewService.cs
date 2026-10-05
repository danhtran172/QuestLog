using System.Text.Json;
using MasterPlay.Core.Entities;
using MasterPlay.Infrastructure.Integrations.Igdb;
using MasterPlay.Infrastructure.Integrations.Igdb.Models;
using MasterPlay.Infrastructure.Integrations.Rawg;
using MasterPlay.Infrastructure.Integrations.Rawg.Models;
using MasterPlay.Infrastructure.Integrations.Steam;

namespace MasterPlay.Host.Metadata;

/// <summary>Mot nguon khong dong gop duoc gi va LY DO — hien cho nguoi dung, khong nuot.</summary>
public sealed record MetadataPreviewWarning(string Provider, string Code, string Message);

/// <summary>Game kho metadata giong ten nhat, kem ten series va ten file chay cua no.</summary>
public sealed record MetadataCatalogMatch(MetadataCatalogGame Game, string? SeriesName, string? ExecutableName);

/// <summary>Preview thanh cong, HOAC loi kem ma va HTTP status.</summary>
public sealed record MetadataPreviewResult(
    GameMetadataPreviewDto? Preview,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    int StatusCode = StatusCodes.Status200OK);

/// <summary>
/// Cac nguon ngoai, tach ra de test thay duoc. Ban that: <see cref="LiveGameMetadataProviders"/>.
/// </summary>
public interface IGameMetadataProviders
{
    bool IgdbConfigured { get; }
    Task<MetadataCatalogMatch?> FindCatalogAsync(string name, CancellationToken cancellationToken);
    Task<IgdbMetadata?> IgdbByIdAsync(int igdbId, CancellationToken cancellationToken);
    Task<IgdbMetadata?> IgdbByNameAsync(string name, CancellationToken cancellationToken);
    Task<GameMetadata?> RawgAsync(string name, CancellationToken cancellationToken);
    Task<(string? Banner, string? Hero)> SteamBannerAndHeroAsync(string name, CancellationToken cancellationToken);
    Task<string?> SteamBannerAsync(string name, CancellationToken cancellationToken);
    Task<string?> SteamHeroAsync(string name, CancellationToken cancellationToken);
    Task<string?> SteamPosterAsync(string name, CancellationToken cancellationToken);
    Task<bool> SteamReachableAsync(CancellationToken cancellationToken);
}

public sealed class LiveGameMetadataProviders(SupabaseMetadataCatalog catalog) : IGameMetadataProviders
{
    public bool IgdbConfigured => IgdbService.IsConfigured;

    public async Task<MetadataCatalogMatch?> FindCatalogAsync(string name, CancellationToken cancellationToken)
    {
        var game = await catalog.FindClosestAsync(name, cancellationToken);
        if (game is null) return null;

        var seriesTask = game.SeriesId is long seriesId
            ? catalog.GetSeriesNameAsync(seriesId, cancellationToken)
            : Task.FromResult<string?>(null);
        var executableTask = catalog.GetExecutableNameAsync(game.Id, cancellationToken);
        await Task.WhenAll(seriesTask, executableTask);
        return new MetadataCatalogMatch(game, seriesTask.Result, executableTask.Result);
    }

    public Task<IgdbMetadata?> IgdbByIdAsync(int igdbId, CancellationToken cancellationToken) =>
        IgdbService.GetMetadataByIdAsync(igdbId, cancellationToken);

    public Task<IgdbMetadata?> IgdbByNameAsync(string name, CancellationToken cancellationToken) =>
        IgdbService.GetMetadataAsync(name, cancellationToken);

    public Task<GameMetadata?> RawgAsync(string name, CancellationToken cancellationToken) =>
        RawgService.GetMetadataAsync(name, cancellationToken);

    public Task<(string? Banner, string? Hero)> SteamBannerAndHeroAsync(string name, CancellationToken cancellationToken) =>
        SteamImageService.GetBannerAndHeroAsync(name, cancellationToken);

    public Task<string?> SteamBannerAsync(string name, CancellationToken cancellationToken) =>
        SteamImageService.GetBannerAsync(name, cancellationToken);

    public Task<string?> SteamHeroAsync(string name, CancellationToken cancellationToken) =>
        SteamImageService.GetHeroAsync(name, cancellationToken);

    public Task<string?> SteamPosterAsync(string name, CancellationToken cancellationToken) =>
        SteamImageService.GetPosterAsync(name, cancellationToken);

    public Task<bool> SteamReachableAsync(CancellationToken cancellationToken) =>
        SteamImageService.SearchReachableAsync(cancellationToken);
}

/// <summary>
/// Preview metadata cho nut Sync cua editor (them VA sua game). Khong ghi DB.
///
/// THU TU NGUON — kho metadata truoc, thieu moi goi API:
///   1. Game trong kho metadata co ten giong nhat (<see cref="GameNameMatcher"/>), KE CA ban
///      chua xuat ban vi truot tieu chi; chi bo ban admin chu dong go
///      (<see cref="SupabaseMetadataCatalog.FindClosestAsync"/>). Moi field kho co thi dung luon.
///   2. Chi field CON THIEU moi goi tiep provider, va goi bang TEN CHUAN cua game kho
///      (IGDB thi goi thang theo igdb_id) de moi nguon cung tro mot game.
///        - IGDB: ten, developer, publisher, ngay phat hanh, DLC, poster, series
///        - RAWG: slug, Metascore, gio choi trung binh, backdrop (+ du phong dev/pub/ngay)
///        - Steam: banner, backdrop du phong (library_hero), poster du phong
///   3. Ten file chay chi lay tu kho metadata — khong con doan bang Gemini.
///
/// Mot provider hong KHONG lam hong ca lan sync: loi cua no thanh mot
/// <see cref="MetadataPreviewWarning"/> tra ve cung ket qua.
/// </summary>
public sealed class GameMetadataPreviewService(
    IGameMetadataProviders providers,
    ILogger<GameMetadataPreviewService> logger)
{
    public const string SteamUnreachableMessage =
        "Cannot reach store.steampowered.com from this machine. Some ISP or router DNS "
        + "servers resolve it to 127.0.0.1; switching the DNS server to 1.1.1.1 or 8.8.8.8 fixes it.";

    public async Task<MetadataPreviewResult> PreviewAsync(
        string name,
        string source,
        Func<CancellationToken, Task<IReadOnlyList<Series>>> loadSeries,
        CancellationToken cancellationToken)
    {
        var warnings = new List<MetadataPreviewWarning>();
        var match = await GuardAsync("metadata", () => providers.FindCatalogAsync(name, cancellationToken), warnings, cancellationToken);
        var lookupName = Present(match?.Game.Name) ?? name;

        return source switch
        {
            "metadata" => await FullAsync(name, lookupName, match, warnings, loadSeries, cancellationToken),
            "cover" => await BackdropAsync(name, lookupName, match, warnings, cancellationToken),
            "card-cover" => await BannerAsync(name, lookupName, match, warnings, cancellationToken),
            _ => await PosterAsync(name, lookupName, match, warnings, cancellationToken),
        };
    }

    private async Task<MetadataPreviewResult> FullAsync(
        string name,
        string lookupName,
        MetadataCatalogMatch? match,
        List<MetadataPreviewWarning> warnings,
        Func<CancellationToken, Task<IReadOnlyList<Series>>> loadSeries,
        CancellationToken cancellationToken)
    {
        var game = match?.Game;
        var developer = Present(game?.Developer);
        var publisher = Present(game?.Publisher);
        var publishDate = game?.PublishDate?.ToString("yyyy-MM-dd");
        var poster = Present(game?.PosterUrl);
        var backdrop = Present(game?.BackdropUrl);
        var banner = Present(game?.BannerUrl);
        var metaScore = Positive(game?.MetaScore);
        var avgPlaytime = Positive(game?.AvgPlaytimeHours);

        var needIgdb = game is null || developer is null || publisher is null || publishDate is null || poster is null;
        // RAWG luon duoc goi: kho metadata khong luu slug (dung cho link Metacritic), va
        // RAWG con bu Metascore / gio choi trung binh / backdrop / dev / pub / ngay khi kho thieu.
        var needSteam = banner is null || backdrop is null;

        var igdbTask = needIgdb ? IgdbAsync(lookupName, game, warnings, cancellationToken) : Task.FromResult<IgdbMetadata?>(null);
        var rawgTask = GuardAsync("rawg", () => providers.RawgAsync(lookupName, cancellationToken), warnings, cancellationToken);
        var steamTask = needSteam
            ? GuardAsync("steam", () => providers.SteamBannerAndHeroAsync(lookupName, cancellationToken), warnings, cancellationToken)
            : Task.FromResult<(string? Banner, string? Hero)>((null, null));
        await Task.WhenAll(igdbTask, rawgTask, steamTask);

        var igdb = igdbTask.Result;
        var rawg = rawgTask.Result;
        var steam = steamTask.Result;

        poster ??= Present(igdb?.CoverUrl);
        if (poster is null)
            poster = Present(await GuardAsync("steam", () => providers.SteamPosterAsync(lookupName, cancellationToken), warnings, cancellationToken));

        var resolvedBanner = banner ?? Present(steam.Banner);
        // RAWG background_image la nguon chinh; Steam library_hero (cung sach, khong chu) du phong.
        var resolvedBackdrop = backdrop ?? Present(rawg?.Background_Image) ?? Present(steam.Hero);
        var found = game is not null || igdb is not null || rawg is not null
            || resolvedBanner is not null || resolvedBackdrop is not null || poster is not null;
        if (!found)
            return NotFound("METADATA_NOT_FOUND", $"No metadata found for {name}.", warnings);

        // Series: ten series cua kho truoc, khong co thi collection/franchise cua IGDB.
        // Khop voi Series DA CO; chua co thi chi tra TEN goi y — preview khong ghi DB.
        IReadOnlyList<string> seriesCandidates = Present(match?.SeriesName) is string catalogSeries
            ? [catalogSeries]
            : igdb?.SeriesCandidates ?? [];
        Series? matchedSeries = null;
        string? seriesName = null;
        if (seriesCandidates.Count > 0)
        {
            matchedSeries = IgdbMetadataSync.MatchSeries(await loadSeries(cancellationToken), seriesCandidates);
            seriesName = matchedSeries?.Name ?? IgdbMetadataSync.NewSeriesName(seriesCandidates);
        }

        return new MetadataPreviewResult(new GameMetadataPreviewDto(
            Name: Present(game?.Name) ?? Present(igdb?.Name) ?? Present(rawg?.Name),
            Developer: developer ?? Present(igdb?.Developers) ?? Present(rawg?.Developers),
            Publisher: publisher ?? Present(igdb?.Publishers) ?? Present(rawg?.Publishers),
            SlugName: Present(rawg?.SlugName) ?? Present(igdb?.Slug),
            PublishDate: publishDate ?? igdb?.ReleaseDate?.ToString("yyyy-MM-dd") ?? IsoDate(rawg?.ReleaseDate),
            MetaScore: metaScore ?? Positive(rawg?.MetaScore),
            AvgPlaytimeHours: avgPlaytime ?? Positive(rawg?.AvgPlaytime),
            ExeName: Present(match?.ExecutableName),
            IsDlc: game?.IsDlc ?? igdb?.IsDlc,
            BannerPath: resolvedBanner,
            BackdropPath: resolvedBackdrop,
            PosterPath: poster,
            Series: matchedSeries is null
                ? null
                : new TaxonomyItemDto(matchedSeries.Id, matchedSeries.Name, null, 0, null, null, true, null),
            SeriesName: seriesName,
            MetadataGameId: game?.Id,
            MetadataGameName: game?.Name,
            Warnings: warnings));
    }

    private async Task<MetadataPreviewResult> BackdropAsync(
        string name, string lookupName, MetadataCatalogMatch? match,
        List<MetadataPreviewWarning> warnings, CancellationToken cancellationToken)
    {
        var backdrop = Present(match?.Game.BackdropUrl);
        if (backdrop is null)
        {
            var rawg = await GuardAsync("rawg", () => providers.RawgAsync(lookupName, cancellationToken), warnings, cancellationToken);
            backdrop = Present(rawg?.Background_Image)
                ?? Present(await GuardAsync("steam", () => providers.SteamHeroAsync(lookupName, cancellationToken), warnings, cancellationToken));
        }

        return backdrop is null
            ? NotFound("RAWG_COVER_NOT_FOUND", $"No metadata, RAWG or Steam backdrop found for {name}.", warnings)
            : ImageResult(match, warnings, backdrop: backdrop);
    }

    private async Task<MetadataPreviewResult> BannerAsync(
        string name, string lookupName, MetadataCatalogMatch? match,
        List<MetadataPreviewWarning> warnings, CancellationToken cancellationToken)
    {
        var banner = Present(match?.Game.BannerUrl)
            ?? Present(await GuardAsync("steam", () => providers.SteamBannerAsync(lookupName, cancellationToken), warnings, cancellationToken));
        if (banner is not null) return ImageResult(match, warnings, banner: banner);

        // "Khong goi duoc" va "khong co game" trong giong het nhau tu phia nguoi dung,
        // nhung cach sua khac han (doi DNS vs doi ten game).
        if (!await providers.SteamReachableAsync(cancellationToken))
            return NotFound("STEAM_STORE_UNREACHABLE", SteamUnreachableMessage, warnings);
        return NotFound("STEAM_CARD_COVER_NOT_FOUND", $"No metadata or Steam card cover found for {name}.", warnings);
    }

    /// <summary>
    /// Poster: kho metadata, roi IGDB (box art that `t_cover_big_2x`, nguon DOMAIN_RULES §12
    /// chi dinh), roi Steam `library_600x900_2x`.
    /// </summary>
    private async Task<MetadataPreviewResult> PosterAsync(
        string name, string lookupName, MetadataCatalogMatch? match,
        List<MetadataPreviewWarning> warnings, CancellationToken cancellationToken)
    {
        var poster = Present(match?.Game.PosterUrl);
        bool? isDlc = match?.Game.IsDlc;
        if (poster is null)
        {
            var igdb = await IgdbAsync(lookupName, match?.Game, warnings, cancellationToken);
            poster = Present(igdb?.CoverUrl);
            if (poster is not null) isDlc ??= igdb!.IsDlc;
        }
        poster ??= Present(await GuardAsync("steam", () => providers.SteamPosterAsync(lookupName, cancellationToken), warnings, cancellationToken));
        if (poster is not null) return ImageResult(match, warnings, poster: poster, isDlc: isDlc);

        if (!await providers.SteamReachableAsync(cancellationToken))
            return NotFound("STEAM_STORE_UNREACHABLE", SteamUnreachableMessage, warnings);
        return NotFound("PORTRAIT_NOT_FOUND", $"No portrait cover found for {name} in metadata, IGDB or Steam.", warnings);
    }

    /// <summary>
    /// IGDB theo igdb_id khi game kho da co (dung game do, khong doan lai bang ten),
    /// nguoc lai theo ten. Chua cau hinh thi bao canh bao thay vi im lang.
    /// </summary>
    private Task<IgdbMetadata?> IgdbAsync(
        string lookupName, MetadataCatalogGame? game,
        List<MetadataPreviewWarning> warnings, CancellationToken cancellationToken)
    {
        if (!providers.IgdbConfigured)
        {
            lock (warnings)
                warnings.Add(new MetadataPreviewWarning("igdb", "IGDB_NOT_CONFIGURED",
                    "IGDB credentials are not configured. Add them in Admin Console → Integrations."));
            return Task.FromResult<IgdbMetadata?>(null);
        }

        return game?.IgdbId is long igdbId and > 0 and <= int.MaxValue
            ? GuardAsync("igdb", () => providers.IgdbByIdAsync((int)igdbId, cancellationToken), warnings, cancellationToken)
            : GuardAsync("igdb", () => providers.IgdbByNameAsync(lookupName, cancellationToken), warnings, cancellationToken);
    }

    /// <summary>
    /// Chay mot provider; loi cua no thanh canh bao co ma rieng thay vi lam hong ca
    /// lan sync. Nguoi dung huy (dong dialog) thi van huy that.
    /// </summary>
    private async Task<T?> GuardAsync<T>(
        string provider,
        Func<Task<T?>> call,
        List<MetadataPreviewWarning> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            return await call();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is RawgException or MetadataCatalogException or HttpRequestException
                                              or TaskCanceledException or InvalidOperationException or JsonException)
        {
            var code = exception switch
            {
                RawgException rawg => rawg.Code,
                MetadataCatalogException catalog => catalog.Code,
                TaskCanceledException => provider.ToUpperInvariant() + "_TIMEOUT",
                _ => provider.ToUpperInvariant() + "_REQUEST_FAILED",
            };
            logger.LogWarning("Metadata preview provider {Provider} failed: {Code} {Message}", provider, code, exception.Message);
            lock (warnings)
                warnings.Add(new MetadataPreviewWarning(provider, code, exception.Message));
            return default;
        }
    }

    private static MetadataPreviewResult ImageResult(
        MetadataCatalogMatch? match,
        List<MetadataPreviewWarning> warnings,
        string? banner = null,
        string? backdrop = null,
        string? poster = null,
        bool? isDlc = null) =>
        new(new GameMetadataPreviewDto(null, null, null, null, null, null, null, null, isDlc, banner, backdrop, poster,
            MetadataGameId: match?.Game.Id,
            MetadataGameName: match?.Game.Name,
            Warnings: warnings));

    /// <summary>Khong tim thay: noi kem ly do cua tung nguon de nguoi dung biet sua o dau.</summary>
    private static MetadataPreviewResult NotFound(string code, string message, List<MetadataPreviewWarning> warnings)
    {
        var reasons = warnings.Count == 0
            ? message
            : message + " " + string.Join(" ", warnings.Select(warning => $"[{warning.Provider}] {warning.Message}"));
        return new MetadataPreviewResult(null, code, reasons, StatusCodes.Status502BadGateway);
    }

    private static string? Present(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int? Positive(int? value) => value is > 0 ? value : null;

    private static string? IsoDate(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", out var date) ? date.ToString("yyyy-MM-dd") : null;
}
