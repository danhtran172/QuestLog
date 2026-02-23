using System;

namespace MasterPlay.Models.Models
{
    public class Game
    {
        // Identity / Core
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string SlugName { get; set; } = string.Empty;

        // Executable / Launch
        public string ExeName { get; set; } = string.Empty;
        public string InitExeName { get; set; } = string.Empty;
        public bool Ready { get; set; } = false;
        public bool IsDisabled { get; set; } = false;

        // Storage / Paths
        public string SaveFolderPath { get; set; } = string.Empty;
        public string BackupFolderPath { get; set; } = string.Empty;

        // Tracking / Runtime Data
        public long TotalPlayTimeSeconds { get; set; } = 0;

        // Media
        public string ImagePath { get; set; } = string.Empty;
        public string GImagePath { get; set; } = string.Empty;

        // Game Metadata (Publishing)
        public DateOnly? PublishDate { get; set; }
        public string Publisher { get; set; } = string.Empty;
        public string Developer { get; set; } = string.Empty;
        public string Series { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public string Theme { get; set; } = string.Empty;

        // Ratings / Estimation
        public float Score { get; set; }
        public int MetaScore { get; set; }
        public float Excited { get; set; }
        public long AvgPlayTime { get; set; }

        // Ownership / Progress
        public string Ownership { get; set; } = string.Empty;
        public string Progress { get; set; }

        // Completion
        public string FinishedIn { get; set; } = string.Empty;
    }
}