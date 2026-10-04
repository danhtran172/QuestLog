namespace MasterPlay.Host.Taxonomy;

/// <summary>Spec 2026-10-04-theme-groups-design §5.1.</summary>
public sealed record ThemeGroupDto(
    int Id,
    string Scope,
    string NameEn,
    string NameVi,
    string DisplayName,
    int SortOrder,
    bool Editable);

/// <summary>Group null = muc "chua phan nhom".</summary>
public sealed record ThemeGroupBucketDto(
    ThemeGroupDto? Group,
    IReadOnlyList<TaxonomyItemDto> Themes);

public sealed record GroupedThemesDto(IReadOnlyList<ThemeGroupBucketDto> Groups);

public sealed record ThemeOrderRequest(int? GroupId, IReadOnlyList<int>? Ids);

public sealed record ThemeGroupWriteRequest(string? NameEn, string? NameVi, int? SortOrder);

public sealed record IdOrderRequest(IReadOnlyList<int>? Ids);
