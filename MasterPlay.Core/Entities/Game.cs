using System;
using System.Collections.Generic;

namespace MasterPlay.Models.Models;

public partial class Game
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = string.Empty;

    public string? SlugName { get; set; }

    public string? ExeName { get; set; }

    public string? InitExeName { get; set; }

    public bool Ready { get; set; }

    public bool IsDisabled { get; set; }

    public string? SaveFolderPath { get; set; }

    public string? BackupFolderPath { get; set; }

    public int? TotalPlayTimeSeconds { get; set; }

    public string? ImagePath { get; set; }

    public string? GImagePath { get; set; }

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

    public virtual GamePosition? GamePosition { get; set; }

    public virtual OwnershipStatus? Ownership { get; set; }

    public virtual ProgressStatus? Progress { get; set; }

    public virtual Series? Series { get; set; }

    public virtual ICollection<Genre> Genres { get; set; } = new List<Genre>();

    public virtual ICollection<Theme> Themes { get; set; } = new List<Theme>();

}
