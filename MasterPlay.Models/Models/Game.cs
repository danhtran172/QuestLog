using System;

namespace MasterPlay.Models.Models
{
    public class Game
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string ExeName { get; set; } = string.Empty;
        public string SaveFolderPath { get; set; } = string.Empty;
        public string BackupFolderPath { get; set; } = string.Empty;
        public long TotalPlayTimeSeconds { get; set; } = 0;
        public string ImagePath { get; set; } = string.Empty;
        public bool IsDisabled { get; set; } = false;
        public bool Ready { get; set; } = false;
        public string Publisher { get; set; } = string.Empty;
        public string Developer { get; set; } = string.Empty;
        public DateOnly? PublishDate { get; set; }

        public string Series { get; set; } = string.Empty;

        public string Genre { get; set; } = string.Empty ;
        public string Theme { get; set; } = string.Empty;

        public int Score { get; set; } 
        public int MetaScore { get; set; } 
        public float Excited { get; set; }
        public string Ownership { get; set; } = string.Empty;

        public string Progress { get; set; }

        public long AvgPlayTime { get; set; }

        public string FinishedIn { get; set; } = string.Empty;

    }
}