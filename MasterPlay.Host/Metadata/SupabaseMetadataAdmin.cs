using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MasterPlay.Host.Auth;

namespace MasterPlay.Host.Metadata;

public sealed record MetadataSeries(long Id, string Name);

public sealed record MetadataSeriesMergeResult(MetadataSeries Target, int MovedGames);

public sealed record MetadataAdminGame(
    long Id,
    long? IgdbId,
    string Name,
    string? Developer,
    string? Publisher,
    string? Summary,
    DateOnly? PublishDate,
    bool IsDlc,
    long? BaseMetadataGameId,
    string? PosterUrl,
    string? BackdropUrl,
    string? BannerUrl,
    int? MetaScore,
    int? AvgPlaytimeHours,
    long? SeriesId,
    long Revision,
    DateTimeOffset? UpdatedAt,
    /// <summary>Y DINH cua admin: co chu dong GO xuat ban khong. Chi admin ghi.</summary>
    bool ManuallyUnpublished,
    /// <summary>KET QUA suy ra. Chi trigger DB ghi — khong bao gio ghi tay.</summary>
    bool IsPublished,
    /// <summary>Tieu chi dang truot. Rong nghia la dat het.</summary>
    IReadOnlyList<string> GateFailures);

/// <summary>
/// `NextCursor` la jsonb DO DB PHAT RA, Host khong doc va khong dung lai. Giu no
/// mo de sau nay doi cach phan trang ma khong pha client.
///
/// `TotalItems` CHI co o trang dau: dem lai o moi lan "Tai them" la quet ca bang
/// chi de tra ve dung con so vua tra.
/// </summary>
public sealed record MetadataAdminGamePage(
    IReadOnlyList<MetadataAdminGame> Items,
    JsonElement? NextCursor,
    int? TotalItems);

/// <summary>Tham so duyet kho. Gom lai thanh mot record vi da qua 8 tham so roi.</summary>
public sealed record MetadataAdminGameQuery
{
    public string? Text { get; init; }
    public string? Missing { get; init; }
    public bool? IsDlc { get; init; }
    public long? SeriesId { get; init; }
    public bool? NoSeries { get; init; }
    public int? Year { get; init; }
    /// <summary>`true` chi ban ghi DA xuat ban, `false` chi ban ghi chua xuat ban.</summary>
    public bool? Published { get; init; }
    public string Sort { get; init; } = "id";
    public bool Descending { get; init; } = true;
    /// <summary>Con tro tho tu trang truoc. `null` la trang dau.</summary>
    public JsonElement? Cursor { get; init; }
    /// <summary>
    /// So ban ghi bo qua — phan trang SO TRANG cua che do danh sach. Bi bo qua
    /// khi co <see cref="Cursor"/>. Can migration 20261004120000.
    /// </summary>
    public int? Offset { get; init; }
    public int PageSize { get; init; } = 30;
}

public sealed record MetadataGameDeleteResult(int OrphanedDlcCount);

/// <summary>
/// Duong GHI vao kho metadata dung chung.
///
/// Tach khoi <see cref="SupabaseMetadataCatalog"/> co chu y: duong doc muon gom
/// moi su co thanh "tam thoi khong dung duoc", con duong ghi phai giu nguyen
/// tung loi nghiep vu (trung ten, dang duoc dung, khong tim thay) de UI noi
/// dung chuyen gi xay ra.
///
/// Moi thao tac ghi goi MOT ham plpgsql, vi ban ghi va dong audit phai vao cung
/// mot transaction (migration 202609140001).
///
/// Class nay KHONG kiem tra quyen. Guard role admin nam o Program.cs va doc tu
/// JWT da duoc gateway xac thuc — spec §2.
/// </summary>
public sealed class SupabaseMetadataAdmin(
    IHttpClientFactory httpClientFactory,
    SupabaseAuthOptions options,
    ILogger<SupabaseMetadataAdmin> logger)
{
    private const int MaxPageSize = 200;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<MetadataSeries>> ListSeriesAsync(
        string? query,
        int limit,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var size = Math.Clamp(limit, 1, MaxPageSize);
        var path = $"/rest/v1/metadata_series?select=id,name&order=name.asc&limit={size}";

        var safe = SanitizeFilterValue(query);
        if (!string.IsNullOrEmpty(safe))
            path += $"&name=ilike.{Uri.EscapeDataString($"*{safe}*")}";

        using var request = CreateServiceRequest(HttpMethod.Get, path);
        var rows = await SendAsync<List<SeriesRow>>(request, cancellationToken);
        return rows.Select(row => new MetadataSeries(row.Id, row.Name)).ToList();
    }

    public Task<MetadataSeries> CreateSeriesAsync(string name, string actorUserId, CancellationToken cancellationToken) =>
        CallSeriesAsync(
            "admin_create_metadata_series",
            new { p_name = name, p_actor = actorUserId },
            cancellationToken);

    public Task<MetadataSeries> UpdateSeriesAsync(long id, string name, string actorUserId, CancellationToken cancellationToken) =>
        CallSeriesAsync(
            "admin_update_metadata_series",
            new { p_id = id, p_name = name, p_actor = actorUserId },
            cancellationToken);

    public async Task DeleteSeriesAsync(long id, string actorUserId, CancellationToken cancellationToken)
    {
        var result = await CallAsync(
            "admin_delete_metadata_series",
            new { p_id = id, p_actor = actorUserId },
            cancellationToken);
        ThrowIfFailed(result);
    }

    public async Task<MetadataSeriesMergeResult> MergeSeriesAsync(
        long sourceId,
        long targetId,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var result = await CallAsync(
            "admin_merge_metadata_series",
            new { p_source_id = sourceId, p_target_id = targetId, p_actor = actorUserId },
            cancellationToken);
        ThrowIfFailed(result);

        var data = result.Data ?? throw new MetadataCatalogException(
            503, "METADATA_EMPTY_RESPONSE", "Metadata service returned an empty response.");
        var target = data.GetProperty("target");
        return new MetadataSeriesMergeResult(
            new MetadataSeries(target.GetProperty("id").GetInt64(), target.GetProperty("name").GetString() ?? string.Empty),
            data.GetProperty("movedGames").GetInt32());
    }

    // ── Games ───────────────────────────────────────────────────

    private const string GameColumns =
        "id,igdb_id,name,developer,publisher,summary,publish_date,is_dlc," +
        "base_metadata_game_id,poster_url,backdrop_url,banner_url," +
        "meta_score,avg_playtime_hours,series_id,revision,updated_at," +
        "manually_unpublished,is_published,gate_failures";

    /// <summary>
    /// Khoa loc "thieu" hop le — dung de TU CHOI SOM gia tri la, khong phai de
    /// dung cau lenh.
    ///
    /// Dinh nghia "thieu" nghia la gi nam trong `admin_list_metadata_games`, o DB.
    /// Giu ca vi tu o day nua se thanh hai nguon su that cho cung mot quy tac, va
    /// chung se lech nhau vao lan sua thu ba.
    /// </summary>
    private static readonly IReadOnlySet<string> MissingFilters =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "banner", "backdrop", "poster", "developer", "publisher",
            "publishDate", "metaScore", "avgPlaytime", "summary", "igdbId",
            "dlcWithoutBase",
        };

    public static bool IsKnownMissingFilter(string value) => MissingFilters.Contains(value);

    /// <summary>Khoa sap xep hop le. Danh sach that nam o DB, day chi chan som.</summary>
    private static readonly IReadOnlySet<string> SortKeys =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "name", "publishDate", "metaScore", "avgPlaytime", "updatedAt",
        };

    public static bool IsKnownSortKey(string value) => SortKeys.Contains(value);

    /// <summary>
    /// Duyet kho co loc va sap xep.
    ///
    /// Goi HAM chu khong ghep chuoi filter cua PostgREST: sap xep theo cot khac
    /// `id` bien con tro keyset thanh cap (gia tri, id), va nam trong sau khoa sap
    /// xep co the NULL — hai thu do khong viet duoc trong query string.
    ///
    /// `cursor` la jsonb do DB phat ra; Host chuyen tiep nguyen si, khong doc va
    /// khong dung lai. Coi no la mo la cach duy nhat de sau nay doi cach phan
    /// trang ma khong pha client.
    /// </summary>
    public async Task<MetadataAdminGamePage> ListGamesAsync(
        MetadataAdminGameQuery query,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["p_query"] = string.IsNullOrWhiteSpace(query.Text) ? null : query.Text,
            ["p_missing"] = string.IsNullOrWhiteSpace(query.Missing) ? null : query.Missing,
            ["p_is_dlc"] = query.IsDlc,
            ["p_series_id"] = query.SeriesId,
            ["p_no_series"] = query.NoSeries,
            ["p_year"] = query.Year,
            ["p_published"] = query.Published,
            ["p_sort"] = query.Sort,
            ["p_desc"] = query.Descending,
            ["p_cursor"] = query.Cursor,
            ["p_limit"] = Math.Clamp(query.PageSize, 1, 100),
        };
        // CHI gui `p_offset` khi co: PostgREST chon ham theo TEN tham so, nen gui
        // no toi DB chua chay migration 20261004120000 se lam hong ca duong
        // "Tai them" cua che do the — duong do khong can offset.
        if (query.Offset is { } offset)
            parameters["p_offset"] = Math.Max(offset, 0);

        var result = await CallAsync("admin_list_metadata_games", parameters, cancellationToken);
        ThrowIfFailed(result);

        var data = result.Data ?? throw new MetadataCatalogException(
            503, "METADATA_EMPTY_RESPONSE", "Metadata service returned an empty response.");

        var items = data.GetProperty("items")
            .Deserialize<List<GameRow>>(JsonOptions) ?? [];

        JsonElement? next = data.TryGetProperty("nextCursor", out var cursor)
            && cursor.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? cursor.Clone()
            : null;

        int? total = data.TryGetProperty("totalItems", out var totalItems)
            && totalItems.ValueKind == JsonValueKind.Number
            ? totalItems.GetInt32()
            : null;

        return new MetadataAdminGamePage(items.Select(ToDomain).ToList(), next, total);
    }

    public async Task<JsonElement> GetStatsAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var request = CreateServiceRequest(HttpMethod.Post, "/rest/v1/rpc/admin_metadata_stats");
        request.Content = JsonContent.Create(new { }, options: JsonOptions);
        return await SendAsync<JsonElement>(request, cancellationToken);
    }

    public async Task<MetadataAdminGame?> GetGameAsync(long id, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var request = CreateServiceRequest(
            HttpMethod.Get,
            $"/rest/v1/metadata_games?select={GameColumns}&id=eq.{id}&limit=1");
        var rows = await SendAsync<List<GameRow>>(request, cancellationToken);
        return rows.Count == 0 ? null : ToDomain(rows[0]);
    }

    public Task<MetadataAdminGame> CreateGameAsync(
        IReadOnlyDictionary<string, object?> payload,
        string actorUserId,
        bool confirmDuplicate,
        CancellationToken cancellationToken) =>
        CallGameAsync(
            "admin_create_metadata_game",
            new { p_payload = payload, p_actor = actorUserId, p_confirm_duplicate = confirmDuplicate },
            cancellationToken);

    public Task<MetadataAdminGame> UpdateGameAsync(
        long id,
        IReadOnlyDictionary<string, object?> payload,
        long revision,
        string actorUserId,
        CancellationToken cancellationToken) =>
        CallGameAsync(
            "admin_update_metadata_game",
            new { p_id = id, p_payload = payload, p_revision = revision, p_actor = actorUserId },
            cancellationToken);

    /// <summary>
    /// Xuat ban / go xuat ban THU CONG mot ban ghi.
    ///
    /// KHONG nhan `revision`: thao tac nay khong doi noi dung ban ghi, nen no
    /// khong duoc lam hong ban dang sua do cua nguoi khac bang mot xung dot 409.
    /// </summary>
    public Task<MetadataAdminGame> SetGamePublishedAsync(
        long id,
        bool published,
        string actorUserId,
        CancellationToken cancellationToken) =>
        CallGameAsync(
            "admin_set_metadata_game_published",
            new { p_id = id, p_published = published, p_actor = actorUserId },
            cancellationToken);

    public async Task<JsonElement> GetPublishRulesAsync(CancellationToken cancellationToken)
    {
        var result = await CallAsync("admin_get_publish_rules", new { }, cancellationToken);
        ThrowIfFailed(result);
        return result.Data ?? throw new MetadataCatalogException(
            503, "METADATA_EMPTY_RESPONSE", "Metadata service returned an empty response.");
    }

    /// <summary>
    /// Doi dieu kien dau ra. DB quet lai ca bang trong cung transaction, nen khong
    /// co khoang thoi gian nao ma tieu chi da doi con `is_published` thi chua.
    /// </summary>
    public async Task<JsonElement> SetPublishRulesAsync(
        object rules,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var result = await CallAsync(
            "admin_set_publish_rules",
            new { p_rules = rules, p_actor = actorUserId },
            cancellationToken);
        ThrowIfFailed(result);
        return result.Data ?? throw new MetadataCatalogException(
            503, "METADATA_EMPTY_RESPONSE", "Metadata service returned an empty response.");
    }

    // ── Crawler console ─────────────────────────────────────────
    // The crawler schema stays private. These RPCs are the sole bridge used by
    // the Host service role after its admin-role guard has accepted the caller.
    public Task<JsonElement> GetCrawlerStatusAsync(CancellationToken cancellationToken) =>
        CallDataAsync("admin_crawler_status", new { }, cancellationToken);

    public Task<JsonElement> SetCrawlerEnabledAsync(bool enabled, CancellationToken cancellationToken) =>
        CallDataAsync("admin_set_crawler_enabled", new { p_enabled = enabled }, cancellationToken);

    public Task<JsonElement> TriggerCrawlerAsync(CancellationToken cancellationToken) =>
        CallDataAsync("admin_trigger_crawler", new { }, cancellationToken);

    public Task<JsonElement> ListCrawlerRunsAsync(JsonElement? cursor, int pageSize, CancellationToken cancellationToken) =>
        CallDataAsync("admin_list_crawler_runs", new { p_cursor = cursor, p_limit = pageSize }, cancellationToken);

    public Task<JsonElement> ListCrawlerRunItemsAsync(
        Guid runId, JsonElement? cursor, int pageSize, CancellationToken cancellationToken) =>
        CallDataAsync("admin_list_crawler_run_items", new { p_run = runId, p_cursor = cursor, p_limit = pageSize }, cancellationToken);

    public Task<JsonElement> ListCrawlerGamesAsync(JsonElement? cursor, int pageSize, CancellationToken cancellationToken) =>
        CallDataAsync("admin_list_crawler_games", new { p_cursor = cursor, p_limit = pageSize }, cancellationToken);

    // Gemini EXE enrichment has an independent worker and audit boundary.
    public Task<JsonElement> GetGeminiExeStatusAsync(CancellationToken cancellationToken) =>
        CallDataAsync("admin_gemini_exe_status", new { }, cancellationToken);

    public Task<JsonElement> SetGeminiExeEnabledAsync(bool enabled, CancellationToken cancellationToken) =>
        CallDataAsync("admin_set_gemini_exe_enabled", new { p_enabled = enabled }, cancellationToken);

    public Task<JsonElement> TriggerGeminiExeAsync(CancellationToken cancellationToken) =>
        CallDataAsync("admin_trigger_gemini_exe", new { }, cancellationToken);

    public Task<JsonElement> ListGeminiExeRunsAsync(int pageSize, CancellationToken cancellationToken) =>
        CallDataAsync("admin_list_gemini_exe_runs", new { p_limit = pageSize }, cancellationToken);

    public Task<JsonElement> ListGeminiExeGamesAsync(int pageSize, CancellationToken cancellationToken) =>
        CallDataAsync("admin_list_gemini_exe_games", new { p_limit = pageSize }, cancellationToken);

    public async Task<MetadataGameDeleteResult> DeleteGameAsync(
        long id,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var result = await CallAsync(
            "admin_delete_metadata_game",
            new { p_id = id, p_actor = actorUserId },
            cancellationToken);
        ThrowIfFailed(result);

        var orphaned = result.Data?.TryGetProperty("orphanedDlcCount", out var value) == true
            ? value.GetInt32()
            : 0;
        return new MetadataGameDeleteResult(orphaned);
    }

    private async Task<MetadataAdminGame> CallGameAsync(
        string function,
        object payload,
        CancellationToken cancellationToken)
    {
        var result = await CallAsync(function, payload, cancellationToken);
        ThrowIfFailed(result);

        var data = result.Data ?? throw new MetadataCatalogException(
            503, "METADATA_EMPTY_RESPONSE", "Metadata service returned an empty response.");
        return ToDomain(data.Deserialize<GameRow>(JsonOptions)
            ?? throw new MetadataCatalogException(
                503, "METADATA_EMPTY_RESPONSE", "Metadata service returned an empty response."));
    }

    private async Task<JsonElement> CallDataAsync(string function, object payload, CancellationToken cancellationToken)
    {
        var result = await CallAsync(function, payload, cancellationToken);
        ThrowIfFailed(result);
        return result.Data ?? throw new MetadataCatalogException(
            503, "METADATA_EMPTY_RESPONSE", "Metadata service returned an empty response.");
    }

    /// <summary>
    /// `,` va `)` ket thuc mot vi tu trong cu phap filter cua PostgREST, `*` la
    /// wildcard. De nguyen thi mot dau phay trong o tim kiem se be gay query.
    /// </summary>
    private static string? SanitizeFilterValue(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        return trimmed
            .Replace("*", string.Empty)
            .Replace(",", " ")
            .Replace("(", " ")
            .Replace(")", " ");
    }

    private async Task<MetadataSeries> CallSeriesAsync(
        string function,
        object payload,
        CancellationToken cancellationToken)
    {
        var result = await CallAsync(function, payload, cancellationToken);
        ThrowIfFailed(result);

        var data = result.Data ?? throw new MetadataCatalogException(
            503, "METADATA_EMPTY_RESPONSE", "Metadata service returned an empty response.");
        return new MetadataSeries(
            data.GetProperty("id").GetInt64(),
            data.GetProperty("name").GetString() ?? string.Empty);
    }

    private async Task<RpcResult> CallAsync(string function, object payload, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var request = CreateServiceRequest(HttpMethod.Post, $"/rest/v1/rpc/{function}");
        request.Content = JsonContent.Create(payload, options: JsonOptions);
        return await SendAsync<RpcResult>(request, cancellationToken);
    }

    /// <summary>
    /// Ham SQL tra ve `{ ok, code, data }` chu khong raise, nen loi nghiep vu di
    /// qua duong nay chu khong lan vao duong "service khong dung duoc".
    /// </summary>
    private static void ThrowIfFailed(RpcResult result)
    {
        if (result.Ok) return;
        var code = result.Code ?? "METADATA_WRITE_FAILED";
        var (status, message) = code switch
        {
            "SERIES_NAME_REQUIRED" => (422, "Enter a series name."),
            "SERIES_NAME_TAKEN" => (409, "A series with that name already exists."),
            "SERIES_NOT_FOUND" => (404, "That series no longer exists."),
            "SERIES_TARGET_NOT_FOUND" => (404, "The series you are merging into no longer exists."),
            "SERIES_IN_USE" => (409, "This series is still assigned to games. Merge it instead."),
            "SERIES_MERGE_SAME" => (422, "Choose two different series to merge."),

            "GAME_NAME_REQUIRED" => (422, "Enter a game name."),
            "GAME_NAME_TOO_LONG" => (422, "Game name must be 300 characters or fewer."),
            "GAME_META_SCORE_RANGE" => (422, "Meta score must be between 0 and 100."),
            "GAME_AVG_PLAYTIME_RANGE" => (422, "Average playtime cannot be negative."),
            "GAME_NOT_FOUND" => (404, "That metadata record no longer exists."),
            "GAME_IGDB_ID_TAKEN" => (409, "Another metadata record already uses that IGDB id."),
            "GAME_SERIES_NOT_FOUND" => (422, "That series no longer exists."),
            "GAME_BASE_NOT_FOUND" => (422, "The base game you picked no longer exists."),
            "GAME_BASE_SELF_REFERENCE" => (422, "A game cannot be its own base game."),
            // Canh bao, khong phai chan cung: gui lai kem `confirmDuplicate` de luu.
            "GAME_NAME_SIMILAR" => (409, "A metadata record with this name already exists. Confirm to create another."),
            "METADATA_REVISION_CONFLICT" => (409, "Someone else changed this record. Review the latest version before saving."),

            "METADATA_SORT_UNKNOWN" => (422, "That sort key is not supported."),
            "METADATA_FILTER_UNKNOWN" => (422, "That catalog filter is not supported."),
            "METADATA_CURSOR_INVALID" => (422, "That page cursor is not valid. Reload the list."),
            "CRAWLER_ENABLED_REQUIRED" => (422, "Specify whether the crawler should be enabled."),
            "CRAWLER_DISABLED" => (409, "Resume the crawler before starting a run."),
            "CRAWLER_RUN_ACTIVE" => (409, "A crawler run is already active."),
            "CRAWLER_RUN_NOT_FOUND" => (404, "That crawler run no longer exists."),
            "CRAWLER_CURSOR_INVALID" => (422, "That crawler page cursor is not valid. Reload the list."),
            "CRAWLER_SCHEDULER_NOT_READY" => (503, "Crawler scheduling is not configured in Supabase."),

            "ACTOR_REQUIRED" => (401, "Sign in to continue."),
            "PUBLISHED_REQUIRED" => (422, "Specify whether the record should be published."),
            "PUBLISH_LOCAL_ID_REQUIRED" => (422, "A library game was sent without its local id."),

            _ => (503, "Metadata catalog rejected the change."),
        };

        // Hai ma nay khong hanh dong duoc neu chi biet "that bai" — phai dua kem du
        // lieu: ban ghi moi nhat, hoac danh sach ban ghi dang trung ten.
        //
        // Xung dot revision tra ve DONG DB THO (snake_case). Phai doi sang cung kieu
        // voi response thanh cong, neu khong UI se phai xu ly hai shape khac nhau cho
        // cung mot thuc the — dung luc no dang can diff hai ban ghi voi nhau.
        object? details = code switch
        {
            "METADATA_REVISION_CONFLICT" => result.Data is { } row
                ? ToDomain(row.Deserialize<GameRow>(JsonOptions)!)
                : null,
            // `GAME_NAME_SIMILAR` da duoc ham SQL dung san bang jsonb_build_object
            // voi khoa camelCase, khong can doi.
            "GAME_NAME_SIMILAR" => result.Data,
            // Day ca lo: bao "ten qua dai" ma khong noi CUA GAME NAO thi khong
            // sua duoc — ham SQL kem theo { localId, name } cua ban ghi pham loi.
            "GAME_NAME_REQUIRED" or "GAME_NAME_TOO_LONG"
                or "GAME_META_SCORE_RANGE" or "GAME_AVG_PLAYTIME_RANGE" => result.Data,
            _ => null,
        };

        throw new MetadataCatalogException(status, code, message, details);
    }

    private HttpRequestMessage CreateServiceRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, options.Url.TrimEnd('/') + path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("apikey", options.ServiceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ServiceRoleKey);
        return request;
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await HttpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return value ?? throw new MetadataCatalogException(503, "METADATA_EMPTY_RESPONSE", "Metadata service returned an empty response.");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning(
            "Metadata admin request failed with {StatusCode}: {Body}",
            (int)response.StatusCode,
            body[..Math.Min(body.Length, 300)]);

        /*
         * PGRST202 = PostgREST khong tim thay ham voi bo tham so nay. Nghia la
         * SCHEMA DB KHONG KHOP voi ban Host dang chay — gan nhu luon la mot
         * migration chua duoc ap dung.
         *
         * Gop no vao "tam thoi khong dung duoc" la sai ca ve su that lan ve huong
         * dan: no khong tam thoi, va bam Retry se khong bao gio het. Phai noi ro
         * de nguoi doc biet viec can lam.
         */
        if (body.Contains("PGRST202", StringComparison.Ordinal))
        {
            throw new MetadataCatalogException(
                503,
                "METADATA_SCHEMA_OUTDATED",
                "The metadata database is missing a function this app version needs. Apply the pending Supabase migration.");
        }

        throw new MetadataCatalogException(503, "METADATA_UNAVAILABLE", "Metadata catalog is temporarily unavailable.");
    }

    private void EnsureConfigured()
    {
        if (!options.IsConfigured)
            throw new MetadataCatalogException(503, "METADATA_NOT_CONFIGURED", "Metadata catalog is not configured on this server.");
    }

    private HttpClient HttpClient => httpClientFactory.CreateClient(nameof(SupabaseMetadataAdmin));

    private static MetadataAdminGame ToDomain(GameRow row) => new(
        row.Id, row.IgdbId, row.Name, row.Developer, row.Publisher, row.Summary,
        row.PublishDate, row.IsDlc, row.BaseMetadataGameId, row.PosterUrl,
        row.BackdropUrl, row.BannerUrl, row.MetaScore,
        row.AvgPlaytimeHours, row.SeriesId, row.Revision, row.UpdatedAt,
        row.ManuallyUnpublished, row.IsPublished, row.GateFailures ?? []);

    private sealed record SeriesRow(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("name")] string Name);

    private sealed record GameRow(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("igdb_id")] long? IgdbId,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("developer")] string? Developer,
        [property: JsonPropertyName("publisher")] string? Publisher,
        [property: JsonPropertyName("summary")] string? Summary,
        [property: JsonPropertyName("publish_date")] DateOnly? PublishDate,
        [property: JsonPropertyName("is_dlc")] bool IsDlc,
        [property: JsonPropertyName("base_metadata_game_id")] long? BaseMetadataGameId,
        [property: JsonPropertyName("poster_url")] string? PosterUrl,
        [property: JsonPropertyName("backdrop_url")] string? BackdropUrl,
        [property: JsonPropertyName("banner_url")] string? BannerUrl,
        [property: JsonPropertyName("meta_score")] int? MetaScore,
        [property: JsonPropertyName("avg_playtime_hours")] int? AvgPlaytimeHours,
        [property: JsonPropertyName("series_id")] long? SeriesId,
        [property: JsonPropertyName("revision")] long Revision,
        [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt = null,
        [property: JsonPropertyName("manually_unpublished")] bool ManuallyUnpublished = false,
        // Mac dinh `true`: mac dinh cua tinh nang la DA XUAT BAN, va mot ban ghi
        // thieu cot khong duoc bien thanh "chua xuat ban".
        [property: JsonPropertyName("is_published")] bool IsPublished = true,
        [property: JsonPropertyName("gate_failures")] string[]? GateFailures = null);

    private sealed record RpcResult(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("data")] JsonElement? Data);
}
