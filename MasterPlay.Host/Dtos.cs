namespace MasterPlay.Host;

/// <summary>
/// DTO khop CHINH XAC voi docs/openapi.yaml.
/// Doi field o day thi PHAI doi openapi.yaml roi chay `npm run gen:api` ben MasterPlay.Web.
/// </summary>
public record CatalogItemDto(
    int Id,
    string SystemKey,
    string Name,
    string DisplayName,
    string ColorCode,
    string? IconData,
    int SortOrder);

/// <summary>
/// Key/Scope/Editable/GroupId do server dien (spec 2026-10-04-theme-groups-design §5.1);
/// khi la body cua request thi bi bo qua — client khong quyet dinh quyen hay chu so huu.
/// </summary>
public record TaxonomyItemDto(
    int Id,
    string Name,
    string? IconData,
    int SortOrder,
    string? Key = null,
    string? Scope = null,
    bool Editable = false,
    int? GroupId = null);

public sealed record BaseGameCandidateDto(string Id, string Name);

public sealed record MetadataGameCandidateDto(
    long Id,
    long? IgdbId,
    string Name,
    string? Developer,
    string? Publisher,
    string? Summary,
    string? PublishDate,
    bool IsDlc,
    bool IsUpcoming,
    long? BaseMetadataGameId,
    string? PosterUrl,
    string? BackdropUrl,
    string? BannerUrl,
    int? MetaScore,
    int? AvgPlaytimeHours,
    long? SeriesId,
    long Revision,
    string LibraryStatus);

public sealed record MetadataSearchResponseDto(
    IReadOnlyList<MetadataGameCandidateDto> Items,
    string? NextCursor,
    int PageSize,
    bool HasMore,
    int TotalItems);

// ── Admin Console ──────────────────────────────────────────────
// Spec 2026-09-14 §7. Khong co DTO nao cho metadata genres/themes: hai bang do
// thuoc tien trinh sync, khong co duong ghi (§4.4).

public sealed record AdminMetadataSeriesDto(long Id, string Name);

public sealed record AdminMetadataSeriesWriteDto(string? Name);

public sealed record AdminMetadataSeriesMergeDto(long? SourceId, long? TargetId);

public sealed record AdminMetadataSeriesMergeResponseDto(
    AdminMetadataSeriesDto Target,
    int MovedGames);

public sealed record AdminMetadataGameDto(
    long Id,
    long? IgdbId,
    string Name,
    string? Developer,
    string? Publisher,
    string? Summary,
    string? PublishDate,
    bool IsDlc,
    long? BaseMetadataGameId,
    string? PosterUrl,
    string? BackdropUrl,
    string? BannerUrl,
    int? MetaScore,
    int? AvgPlaytimeHours,
    long? SeriesId,
    long Revision,
    string? UpdatedAt,
    /// <summary>Ý định của admin: có chủ động gỡ xuất bản không. Ghi qua `PUT .../published`.</summary>
    bool ManuallyUnpublished,
    /// <summary>Kết quả suy ra — người dùng có tìm thấy bản ghi này không. Read-only.</summary>
    bool IsPublished,
    /// <summary>Tiêu chí đang trượt. Rỗng nghĩa là đạt hết.</summary>
    IReadOnlyList<string> GateFailures);

public sealed record AdminMetadataGameListResponseDto(
    IReadOnlyList<AdminMetadataGameDto> Items,
    /// <summary>
    /// Con tro tho cho trang sau, gui lai nguyen si o `?cursor=`. `null` la het.
    /// Client KHONG duoc doc cau truc ben trong: no thuoc ve DB va se doi.
    /// </summary>
    string? NextCursor,
    int PageSize,
    /// <summary>Tong so ban ghi khop. CHI co o trang dau, cac trang sau la `null`.</summary>
    int? TotalItems);

/// <summary>
/// PUT la THAY THE toan bo: field vang mat duoc hieu la null, khong phai "giu nguyen".
///
/// Khong co `genreIds` / `themeIds`: genre va theme thuoc tien trinh sync, read-only
/// o moi duong ghi (spec §4.4).
/// </summary>
public sealed record AdminMetadataGameWriteDto(
    long? IgdbId,
    string? Name,
    string? Developer,
    string? Publisher,
    string? Summary,
    string? PublishDate,
    bool? IsDlc,
    long? BaseMetadataGameId,
    string? PosterUrl,
    string? BackdropUrl,
    string? BannerUrl,
    int? MetaScore,
    int? AvgPlaytimeHours,
    long? SeriesId,
    long? Revision,
    bool? ConfirmDuplicate);

public sealed record AdminMetadataGameDeleteResponseDto(int OrphanedDlcCount);

public sealed record AdminMetadataGamePublishedRequestDto(bool? Published);

/// <summary>
/// Bật/tạm dừng worker crawler. Cron, quota và Vault không là input từ UI: chúng
/// được vận hành ở Supabase để một admin không vô tình làm vượt ngân sách.
/// </summary>
public sealed record AdminCrawlerEnabledRequestDto(bool? Enabled);

/// <summary>
/// Tiêu chí xuất bản. `null` ở một field nghĩa là **giữ nguyên**, không phải `false`
/// — đổi một công tắc mà vô tình tắt hết các công tắc khác là kiểu mất dữ liệu
/// không ai ngờ.
/// </summary>
public sealed record AdminPublishRulesDto(
    bool? RequireBanner,
    bool? RequirePoster,
    bool? RequireBackdrop,
    bool? RequireDeveloper,
    bool? RequirePublisher,
    bool? RequirePublishDate,
    bool? RequireMetaScore,
    bool? RequireAvgPlaytime,
    bool? RequireSummary,
    bool? RequireIgdbId,
    bool? RequireDlcBase);

public sealed record LibraryGameImportUserDataDto(
    double? Score,
    double? Excited,
    int? TotalPlaytimeSeconds,
    int? ProgressId,
    int? OwnershipId,
    string? ExeName,
    LaunchTargetDto? LaunchTarget,
    string? SaveFolderPath,
    string? BackupFolderPath,
    OwnershipInfoDto? OwnershipInfo);

public sealed record LibraryGameImportItemDto(long MetadataGameId, LibraryGameImportUserDataDto? UserData);

public sealed record LibraryGameImportRequestDto(IReadOnlyList<LibraryGameImportItemDto> Items);

public sealed record LibraryGameImportResultItemDto(long MetadataGameId, string Status, string? GameId, string? Message);

public sealed record LibraryGameImportResponseDto(
    IReadOnlyList<LibraryGameImportResultItemDto> Items,
    int ImportedCount,
    int DuplicateCount,
    int RestoreRequiredCount);

public sealed record LibraryMetadataRefreshRequestDto(
    /// <summary>Tinh ra thay doi ma khong ghi. Mac dinh false.</summary>
    bool? DryRun,
    /// <summary>Bo qua cua chan 10 phut va goi kho ngay.</summary>
    bool? Force);

/// <summary>Mot dong thu vien co thay doi sau khi hop nhat voi kho.</summary>
public sealed record LibraryMetadataRefreshGameDto(
    string GameId,
    string Name,
    long MetadataGameId,
    /// <summary>Ten field da lay gia tri moi tu kho.</summary>
    IReadOnlyList<string> Updated,
    /// <summary>Ten field kho dang noi khac nhung giu lai vi nguoi dung da sua tay.</summary>
    IReadOnlyList<string> Kept);

public sealed record LibraryMetadataRefreshResponseDto(
    bool DryRun,
    /// <summary>Khong goi kho vi moi dong deu vua dong bo xong. Xem `force`.</summary>
    bool Skipped,
    /// <summary>So dong thu vien co `metadataGameId`.</summary>
    int Linked,
    /// <summary>So dong kho tra ve du lieu, tuc la thuc su duoc doi chieu.</summary>
    int Checked,
    /// <summary>So GAME co it nhat mot field doi.</summary>
    int Updated,
    int FieldsUpdated,
    int FieldsKept,
    /// <summary>Dong da noi nhung kho khong tra ve — bi go xuat ban hoac da xoa. Khong dung toi.</summary>
    int Missing,
    IReadOnlyList<LibraryMetadataRefreshGameDto> Games)
{
    public static LibraryMetadataRefreshResponseDto Nothing(bool dryRun) =>
        new(dryRun, false, 0, 0, 0, 0, 0, 0, []);
}

public sealed record UserDataPatchDto(
    double? Score,
    double? Excited,
    int? TotalPlaytimeSeconds,
    int? ProgressId,
    int? OwnershipId,
    string? ExeName,
    LaunchTargetDto? LaunchTarget,
    string? SaveFolderPath,
    string? BackupFolderPath,
    OwnershipInfoDto? OwnershipInfo);

public record GameListItemDto(
    string Id,
    long? MetadataGameId,
    long? MetadataRevision,
    DateTime? MetadataSyncedAt,
    bool IsMetadataDerived,
    string Name,
    string? Developer,
    string? Publisher,
    TaxonomyItemDto? Series,
    string? BannerPath,
    string? PosterPath,
    string? BackdropPath,
    CatalogItemDto? Progress,
    CatalogItemDto? Ownership,
    int? MetaScore,
    string? MetaScoreBand,
    double? Excited,
    double? Score,
    string? ScoreMaterialBand,
    int? TotalPlaytimeSeconds,
    int? AvgPlaytimeHours,
    string? AvgPlaytimeBand,
    string? PublishDate,
    DateTime? LastFinishedAt,
    DateTime? LastPlayedAt,
    bool IsDlc,
    bool IsUpcoming,
    bool Ready,
    bool IsRunning,
    bool CanLaunch,
    string? FolderWarning,
    // DLC: id + ten base game de card thay ngay phat hanh bang chip base game.
    string? BaseGameId = null,
    string? BaseGameName = null);

public record LaunchTargetDto(string Kind, string Path);

/// <param name="HasMonth">
/// False khi nguoi choi chi nho NAM. `FinishedAt` van la mot ngay day du
/// (1 thang 1 cua nam do) de sort/filter khong phai biet den chuyen nay, nhung
/// UI phai hien "2019" chu khong phai "01/2019". DOMAIN_RULES 9.1.
/// </param>
public record GameFinishEntryDto(int Id, DateTime FinishedAt, bool HasMonth, double? Score);

/// <param name="FinishedYear">
/// Bo trong = dung thoi diem hien tai cua backend (hanh vi cu).
/// </param>
/// <param name="FinishedMonth">Bo trong = chi nho nam.</param>
public sealed record FinishGameRequestDto(double? Score, int? FinishedYear, int? FinishedMonth);

public sealed record UpdateFinishTimeRequestDto(int? FinishedYear, int? FinishedMonth);

public sealed record AdjustPlaytimeRequestDto(int DeltaMinutes);

public sealed record RevealPathRequestDto(string? Path);

public sealed record PickFileRequestDto(string? Filter);

public sealed record SelectedPathDto(string? Path);

public sealed record BackupFolderDto(string BackupFolderPath);

public sealed record ExternalLinksDto(string HltbUrl, string MetacriticUrl);

public record OwnershipInfoDto(
    string? SteamAccount,
    string? SteamPassword,
    string? UbisoftAccount,
    string? UbisoftPassword,
    string? SupportDocumentUrl,
    string? DownloadUrl,
    string? Note);

public record GameDetailDto(
    string Id,
    long? MetadataGameId,
    long? MetadataRevision,
    DateTime? MetadataSyncedAt,
    bool IsMetadataDerived,
    string Name,
    string? Developer,
    string? Publisher,
    TaxonomyItemDto? Series,
    string? BannerPath,
    string? PosterPath,
    string? BackdropPath,
    CatalogItemDto? Progress,
    CatalogItemDto? Ownership,
    int? MetaScore,
    string? MetaScoreBand,
    double? Excited,
    double? Score,
    string? ScoreMaterialBand,
    int? TotalPlaytimeSeconds,
    int? AvgPlaytimeHours,
    string? AvgPlaytimeBand,
    string? PublishDate,
    DateTime? LastFinishedAt,
    DateTime? LastPlayedAt,
    bool IsDlc,
    bool IsUpcoming,
    bool Ready,
    bool IsRunning,
    bool CanLaunch,
    string? FolderWarning,
    string? SlugName,
    string? ExeName,
    LaunchTargetDto? LaunchTarget,
    string? SaveFolderPath,
    string? BackupFolderPath,
    IReadOnlyList<TaxonomyItemDto> Genres,
    IReadOnlyList<TaxonomyItemDto> Themes,
    string? BaseGameId,
    string? BaseGameName,
    IReadOnlyList<GameFinishEntryDto> Finishes,
    OwnershipInfoDto? OwnershipInfo,
    DateTime? CreateAt)
    : GameListItemDto(
        Id, MetadataGameId, MetadataRevision, MetadataSyncedAt, IsMetadataDerived, Name, Developer, Publisher, Series, BannerPath, PosterPath, BackdropPath, Progress, Ownership,
        MetaScore, MetaScoreBand, Excited, Score, ScoreMaterialBand, TotalPlaytimeSeconds, AvgPlaytimeHours,
        AvgPlaytimeBand, PublishDate, LastFinishedAt, LastPlayedAt, IsDlc, IsUpcoming, Ready, IsRunning, CanLaunch,
        FolderWarning, BaseGameId, BaseGameName);

public record CardHintsDto(
    bool ShowDeveloper,
    bool ShowPublisher,
    bool ShowFinishTime,
    bool ShowPlaytime,
    bool ShowExcited,
    bool ShowScore,
    bool ShowOwnership,
    bool ShowMetaScore,
    bool ShowAvgPlaytime,
    bool ShowPublishDate,
    bool ShowSeries,
    bool ShowProgress);

public record GameListResponseDto(
    IReadOnlyList<GameListItemDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    int ActiveFilterCount,
    CardHintsDto CardHints);

public record QueueOrderRequestDto(IReadOnlyList<string> GameIds);

public sealed record SortRuleDto(string Key, string Direction);

public sealed record ViewSortProfileDto(
    string View,
    IReadOnlyList<SortRuleDto> Rules,
    bool Customized);

public sealed record ViewSortProfilesDto(IReadOnlyList<ViewSortProfileDto> Profiles);

public sealed record UpdateViewSortProfileDto(IReadOnlyList<SortRuleDto>? Rules);

/// <summary>
/// Metadata resolved from external providers for an editor draft. This result is
/// deliberately not a Game entity and must never be persisted by the preview endpoint.
/// </summary>
public sealed record GameMetadataPreviewRequestDto(string? Name, string? Source);

public sealed record GameMetadataPreviewDto(
    string? Name,
    string? Developer,
    string? Publisher,
    string? SlugName,
    string? PublishDate,
    int? MetaScore,
    int? AvgPlaytimeHours,
    string? ExeName,
    bool? IsDlc,
    string? BannerPath,
    string? BackdropPath,
    string? PosterPath,
    // Series co san trong thu vien khop voi collection/franchise cua IGDB.
    TaxonomyItemDto? Series = null,
    // Ten series IGDB goi y; co ma Series = null nghia la thu vien chua co series nay.
    string? SeriesName = null);

// Input rieng cho mutation: cung cac field editable cua GameDetail trong openapi,
// nhung khong cho client ghi de cac field runtime/read-only nhu Ready/CanLaunch.
public sealed class GameUpsertDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Developer { get; init; }
    public string? Publisher { get; init; }
    public TaxonomyItemDto? Series { get; init; }
    public string? BannerPath { get; init; }
    public string? BackdropPath { get; init; }
    public string? PosterPath { get; init; }
    public CatalogItemDto? Progress { get; init; }
    public CatalogItemDto? Ownership { get; init; }
    public int? MetaScore { get; init; }
    public double? Excited { get; init; }
    public double? Score { get; init; }
    public int? TotalPlaytimeSeconds { get; init; }
    public int? AvgPlaytimeHours { get; init; }
    public string? PublishDate { get; init; }
    public bool IsDlc { get; init; }
    public string? SlugName { get; init; }
    public string? ExeName { get; init; }
    public LaunchTargetDto? LaunchTarget { get; init; }
    public string? SaveFolderPath { get; init; }
    public string? BackupFolderPath { get; init; }
    public IReadOnlyList<TaxonomyItemDto>? Genres { get; init; }
    public IReadOnlyList<TaxonomyItemDto>? Themes { get; init; }
    public string? BaseGameId { get; init; }
    public OwnershipInfoDto? OwnershipInfo { get; init; }
}

public record CapabilityFeaturesDto(
    bool Launch,
    bool FilePicker,
    bool RevealFolder,
    bool Tray,
    bool Scanner,
    bool Controller,
    bool ImageSync,
    bool Backup);

public record CapabilitiesDto(string Mode, CapabilityFeaturesDto Features);

public sealed record ImageSyncRoleCountsDto(int Uploaded, int AlreadySynced, int Missing);

public sealed record ImageSyncFailureDto(string GameId, string GameName, string Role, string Reason);

/// <summary>Bao cao cua `POST /api/tray/sync-images`.</summary>
public sealed record ImageSyncReportDto(
    int TotalGames,
    ImageSyncRoleCountsDto Banner,
    ImageSyncRoleCountsDto Backdrop,
    ImageSyncRoleCountsDto Poster,
    int Failed,
    IReadOnlyList<ImageSyncFailureDto> Failures);

public sealed record LibraryRuntimeSettingsDto(string Path, bool Valid, int ExeCount);

public sealed record LibrarySettingsDto(
    IReadOnlyList<string> Paths,
    IReadOnlyList<LibraryRuntimeSettingsDto> Runtime);

public sealed record CloudinarySettingsDto(string CloudName, bool Configured);

public sealed record RawgSettingsDto(bool Configured);

/// <summary>
/// Secret-bearing request body for Admin Integrations. It is intentionally write-only:
/// no response DTO contains these values.
/// </summary>
public sealed record IntegrationCredentialsWriteDto(IReadOnlyDictionary<string, string>? Values);

public sealed record SettingsDto(
    string Language,
    string ThemeMode,
    string JungleFx,
    string JungleWeather,
    string BackupRootPath,
    string DatabasePath,
    LibrarySettingsDto Libraries,
    CloudinarySettingsDto Cloudinary,
    RawgSettingsDto Rawg);

public sealed record LibrarySettingsUpdateDto(IReadOnlyList<string>? Paths);

public sealed record CloudinarySettingsUpdateDto(string? CloudName);

public sealed record UpdateSettingsDto(
    string? Language,
    string? ThemeMode,
    string? JungleFx,
    string? JungleWeather,
    string? BackupRootPath,
    string? DatabasePath,
    LibrarySettingsUpdateDto? Libraries,
    CloudinarySettingsUpdateDto? Cloudinary);
