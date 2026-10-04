using System;
using System.Collections.Generic;

namespace MasterPlay.Core.Entities;

public partial class Game
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = string.Empty;

    public string? SlugName { get; set; }

    /// <summary>
    /// Id cua game ben IGDB. Khoa ngoai on dinh de re-sync va de noi DLC voi game goc.
    /// KHONG dung SlugName cho viec nay: cot do dang giu slug RAWG va duoc dung de dung URL Metacritic.
    /// </summary>
    public int? IgdbId { get; set; }

    /// <summary>
    /// Owner of this local library row. Derived by the host from the authenticated
    /// account or the desktop guest session; never accepted from the client.
    /// </summary>
    public string LibraryOwnerKey { get; set; } = "legacy:local";

    /// <summary>
    /// Shared metadata catalog identity. Null means this is a manual game.
    /// </summary>
    public long? MetadataGameId { get; set; }

    public long? MetadataRevision { get; set; }

    public DateTime? MetadataSyncedAt { get; set; }

    /// <summary>
    /// JSON: gia tri kho da tra ve o lan dong bo gan nhat. Moc so sanh de biet
    /// field nao nguoi dung da sua tay, khong phai du lieu hien thi — khong UI
    /// nao doc cot nay. Xem <c>LibraryMetadataMerge</c>.
    /// </summary>
    public string? MetadataBaseline { get; set; }

    public string? ExeName { get; set; }

    public string? InitExeName { get; set; }

    public string? LaunchTargetKind { get; set; }

    public string? LaunchTargetPath { get; set; }

    public bool Ready { get; set; }

    public bool IsDisabled { get; set; }

    public bool IsDlc { get; set; }

    public string? BaseGameId { get; set; }

    public string? SaveFolderPath { get; set; }

    public string? BackupFolderPath { get; set; }

    public int? TotalPlayTimeSeconds { get; set; }

    /// <summary>
    /// UTC timestamp of the most recent play session detected by the Tray playtime service.
    /// This runtime field is read-only to API clients.
    /// </summary>
    public DateTime? LastPlayedAt { get; set; }

    /// <summary>Steam header image used by cards, rows, and the play queue.</summary>
    public string? BannerPath { get; set; }

    /// <summary>RAWG landscape image used by the game-detail hero.</summary>
    public string? BackdropPath { get; set; }

    /// <summary>IGDB portrait cover used by portrait-oriented surfaces.</summary>
    public string? PosterPath { get; set; }

    /// <summary>
    /// JSON <c>{"banner":{"source":..., "url":...}, ...}</c>: anh Cloudinary
    /// <c>url</c> la ban copy app upload tu link goc <c>source</c>. Merge metadata
    /// doi chieu bang link goc, khong phai bang ban copy. Chi Host/Tray ghi.
    /// </summary>
    public string? ImageSources { get; set; }

    public DateOnly? PublishDate { get; set; }

    public string? Publisher { get; set; }

    public string? Developer { get; set; }

    public int? SeriesId { get; set; }

    public double? Score { get; set; }

    public int? MetaScore { get; set; }

    public double? Excited { get; set; }

    public int? AvgPlayTime { get; set; }

    public string? FinishedIn { get; set; }

    public int? OwnershipId { get; set; }

    public int? ProgressId { get; set; }

    public DateTime? CreateAt { get; set; } = DateTime.Now;

    public virtual ICollection<GameGenre> GameGenres { get; set; } = new List<GameGenre>();
    public virtual ICollection<GameTheme> GameThemes { get; set; } = new List<GameTheme>();

    public virtual ICollection<GameFinish> Finishes { get; set; } = new List<GameFinish>();

    public virtual GamePosition? GamePosition { get; set; }

    public virtual GameOwnershipInfo? OwnershipInfo { get; set; }

    public virtual OwnershipStatus? Ownership { get; set; }

    public virtual ProgressStatus? Progress { get; set; }

    public virtual Series? Series { get; set; }

    public virtual ICollection<Genre> Genres { get; set; } = new List<Genre>();

    public virtual ICollection<Theme> Themes { get; set; } = new List<Theme>();

}
