using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MasterPlay.Infrastructure.Integrations.Igdb.Models;

/// <summary>
/// IGDB dang chuyen cac enum sang bang tra cuu. Trong giai doan chuyen tiep, mot field tham chieu
/// co the ve duoi dang SO (id tho) hoac OBJECT (da expand). Converter nay nuot ca hai
/// de mot thay doi phia IGDB khong lam vo toan bo luong sync.
/// </summary>
public sealed class FlexibleGameTypeConverter : JsonConverter<IgdbGameType?>
{
    public override IgdbGameType? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.Number:
                return new IgdbGameType { Id = reader.GetInt32() };

            case JsonTokenType.StartObject:
                using (var document = JsonDocument.ParseValue(ref reader))
                {
                    var root = document.RootElement;
                    return new IgdbGameType
                    {
                        Id = root.TryGetProperty("id", out var id) && id.TryGetInt32(out var idValue) ? idValue : 0,
                        Type = root.TryGetProperty("type", out var type) ? type.GetString() : null
                    };
                }

            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, IgdbGameType? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteNumberValue(value.Id);
    }
}

/// <summary>Response cua Twitch OAuth2 (client_credentials) — IGDB dung token nay.</summary>
internal sealed class TwitchTokenResponse
{
    [JsonPropertyName("access_token")] public string? AccessToken { get; set; }

    [JsonPropertyName("expires_in")] public long ExpiresIn { get; set; }

    [JsonPropertyName("token_type")] public string? TokenType { get; set; }
}

/// <summary>Shape chung cho moi entity IGDB chi can id + name (company, collection, franchise, parent_game).</summary>
public sealed class IgdbNamed
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("name")] public string? Name { get; set; }
}

public sealed class IgdbCover
{
    [JsonPropertyName("id")] public int Id { get; set; }

    /// <summary>Hash de ghep URL anh: https://images.igdb.com/igdb/image/upload/t_{size}/{image_id}.jpg</summary>
    [JsonPropertyName("image_id")] public string? ImageId { get; set; }
}

/// <summary>
/// Bang GameType moi (IGDB dang migrate enum -> table).
/// <c>category</c> la enum cu, van duoc tra ve; giu ca hai de khong vo khi IGDB go enum.
/// </summary>
public sealed class IgdbGameType
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("type")] public string? Type { get; set; }
}

public sealed class IgdbInvolvedCompany
{
    [JsonPropertyName("developer")] public bool Developer { get; set; }

    [JsonPropertyName("publisher")] public bool Publisher { get; set; }

    [JsonPropertyName("porting")] public bool Porting { get; set; }

    [JsonPropertyName("supporting")] public bool Supporting { get; set; }

    [JsonPropertyName("company")] public IgdbNamed? Company { get; set; }
}

/// <summary>Raw response cua POST /v4/games. Chi khai bao field thuc su duoc dung.</summary>
public sealed class IgdbGame
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("slug")] public string? Slug { get; set; }

    [JsonPropertyName("first_release_date")] public long? FirstReleaseDate { get; set; }

    [JsonPropertyName("cover")] public IgdbCover? Cover { get; set; }

    [JsonPropertyName("game_type")]
    [JsonConverter(typeof(FlexibleGameTypeConverter))]
    public IgdbGameType? GameType { get; set; }

    /// <summary>Enum cu (DEPRECATED phia IGDB). Chi dung khi <see cref="GameType"/> khong co.</summary>
    [JsonPropertyName("category")] public int? Category { get; set; }

    [JsonPropertyName("parent_game")] public IgdbNamed? ParentGame { get; set; }

    [JsonPropertyName("collections")] public List<IgdbNamed>? Collections { get; set; }

    [JsonPropertyName("franchises")] public List<IgdbNamed>? Franchises { get; set; }

    [JsonPropertyName("involved_companies")] public List<IgdbInvolvedCompany>? InvolvedCompanies { get; set; }
}

/// <summary>
/// Ket qua da chuan hoa cua mot lan tra cuu IGDB — day la thu ma tang tren tieu thu.
/// Khong lo gi ve DB: viec map Series / BaseGame nam o <c>IgdbMetadataSync</c>.
/// </summary>
public sealed class IgdbMetadata
{
    public required int IgdbId { get; init; }

    public required string Name { get; init; }

    public string? Slug { get; init; }

    public DateOnly? ReleaseDate { get; init; }

    /// <summary>URL cover DOC da ghep san (t_cover_big_2x, 528x748).</summary>
    public string? CoverUrl { get; init; }

    /// <summary>Da gop bang ", " — khop dinh dang cot Game.Developer hien tai.</summary>
    public string? Developers { get; init; }

    /// <summary>Da gop bang ", " — khop dinh dang cot Game.Publisher hien tai.</summary>
    public string? Publishers { get; init; }

    /// <summary>
    /// Ung vien Series theo thu tu uu tien: collections truoc, roi den franchises.
    /// Tang tren chon cai nao khop voi Series da co trong DB (xem IgdbMetadataSync).
    /// </summary>
    public IReadOnlyList<string> SeriesCandidates { get; init; } = Array.Empty<string>();

    public bool IsDlc { get; init; }

    public int? ParentGameIgdbId { get; init; }

    public string? ParentGameName { get; init; }
}
