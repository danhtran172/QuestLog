using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MasterPlay.Host.Auth;

namespace MasterPlay.Host.Metadata;

public sealed record MetadataCatalogGame(
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
    long Revision);

public sealed record MetadataSearchPage(
    IReadOnlyList<MetadataCatalogGame> Items,
    string? NextCursor,
    int TotalItems,
    bool HasMore);

public sealed class MetadataCatalogException(
    int statusCode,
    string code,
    string message,
    object? details = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;

    /// <summary>
    /// Du lieu kem theo loi, hien nguyen si trong `error.details`.
    ///
    /// Can cho hai truong hop ma bao "that bai" thoi la chua du de nguoi dung hanh
    /// dong: xung dot revision (phai dua ban ghi MOI NHAT de doi chieu) va trung ten
    /// khi tao game (phai dua danh sach ban ghi dang trung de admin tu quyet).
    /// </summary>
    public object? Details { get; } = details;
}

public sealed class SupabaseMetadataCatalog(
    IHttpClientFactory httpClientFactory,
    SupabaseAuthOptions options,
    ILogger<SupabaseMetadataCatalog> logger)
{
    private const int MaxPageSize = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<MetadataSearchPage> SearchAsync(
        string query,
        string? cursor,
        int pageSize,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var trimmedQuery = query.Trim();
        if (trimmedQuery.Length < 2)
            throw new MetadataCatalogException(400, "METADATA_QUERY_TOO_SHORT", "Search text must contain at least 2 characters.");

        var size = Math.Clamp(pageSize, 1, MaxPageSize);
        var decoded = DecodeCursor(cursor);
        var rpc = new
        {
            p_query = trimmedQuery,
            p_limit = Math.Min(MaxPageSize, size + 1),
            p_cursor_name = decoded?.Name,
            p_cursor_id = decoded?.Id,
        };

        using var request = CreateServiceRequest(HttpMethod.Post, "/rest/v1/rpc/search_metadata_games");
        request.Content = JsonContent.Create(rpc, options: JsonOptions);
        var rows = await SendAsync<List<MetadataRow>>(request, cancellationToken);
        var hasMore = rows.Count > size;
        var pageRows = rows.Take(size).ToList();
        var nextCursor = hasMore && pageRows.Count > 0
            ? EncodeCursor(new MetadataCursor(pageRows[^1].Name, pageRows[^1].Id))
            : null;

        return new MetadataSearchPage(
            pageRows.Select(ToDomain).ToList(),
            nextCursor,
            pageRows.FirstOrDefault()?.TotalCount is long total ? checked((int)Math.Min(int.MaxValue, total)) : pageRows.Count,
            hasMore);
    }

    public async Task<IReadOnlyDictionary<long, MetadataCatalogGame>> GetByIdsAsync(
        IReadOnlyCollection<long> ids,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (ids.Count == 0) return new Dictionary<long, MetadataCatalogGame>();

        var values = string.Join(',', ids.Distinct().Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var select = "id,igdb_id,name,developer,publisher,summary,publish_date,is_dlc,base_metadata_game_id,poster_url,backdrop_url,banner_url,meta_score,avg_playtime_hours,series_id,revision";
        // `is_published=is.true` la BAT BUOC: duong nay chay bang service_role nen
        // bypass RLS. Thieu no thi mot ban ghi CHUA xuat ban van import ve thu vien
        // duoc — cua sau di vong qua toan bo tieu chi xuat ban (spec §13).
        var path = $"/rest/v1/metadata_games?select={Uri.EscapeDataString(select)}&id=in.({values})&is_published=is.true";
        using var request = CreateServiceRequest(HttpMethod.Get, path);
        var rows = await SendAsync<List<MetadataRow>>(request, cancellationToken);
        return rows.Select(ToDomain).ToDictionary(game => game.Id);
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
        logger.LogWarning("Metadata catalog request failed with {StatusCode}: {Body}", (int)response.StatusCode, body[..Math.Min(body.Length, 300)]);
        throw new MetadataCatalogException(503, "METADATA_UNAVAILABLE", "Metadata catalog is temporarily unavailable.");
    }

    private void EnsureConfigured()
    {
        if (!options.IsConfigured)
            throw new MetadataCatalogException(503, "METADATA_NOT_CONFIGURED", "Metadata catalog is not configured on this server.");
    }

    private HttpClient HttpClient => httpClientFactory.CreateClient(nameof(SupabaseMetadataCatalog));

    private static MetadataCatalogGame ToDomain(MetadataRow row) => new(
        row.Id,
        row.IgdbId,
        row.Name,
        row.Developer,
        row.Publisher,
        row.Summary,
        row.PublishDate,
        row.IsDlc,
        row.BaseMetadataGameId,
        row.PosterUrl,
        row.BackdropUrl,
        row.BannerUrl,
        row.MetaScore,
        row.AvgPlaytimeHours,
        row.SeriesId,
        row.Revision);

    private static MetadataCursor? DecodeCursor(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded)) return null;
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            return JsonSerializer.Deserialize<MetadataCursor>(json, JsonOptions);
        }
        catch (FormatException) { throw new MetadataCatalogException(400, "INVALID_METADATA_CURSOR", "Metadata cursor is invalid."); }
        catch (JsonException) { throw new MetadataCatalogException(400, "INVALID_METADATA_CURSOR", "Metadata cursor is invalid."); }
    }

    private static string EncodeCursor(MetadataCursor cursor) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cursor, JsonOptions)));

    private sealed record MetadataCursor(string Name, long Id);

    private sealed record MetadataRow(
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
        [property: JsonPropertyName("total_count")] long? TotalCount = null);
}
