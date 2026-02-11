using System;

namespace MasterPlay.Models
{
    public class Game
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string ExeName { get; set; } = string.Empty;
        public string SaveFilePath { get; set; } = string.Empty;
        public string BackupFolderPath { get; set; } = string.Empty;
        public long TotalPlayTimeSeconds { get; set; } = 0;
        public string ImagePath { get; set; } = string.Empty;
        public bool IsDisabled { get; set; } = false;
    }

    public class AvailableGame
    {
        public string ExeName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string AddedTime { get; set; } = string.Empty;
    }
}