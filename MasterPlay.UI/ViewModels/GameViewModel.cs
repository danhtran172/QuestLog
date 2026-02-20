using CommunityToolkit.Mvvm.ComponentModel;
using MasterPlay.Helpers;
using MasterPlay.Models;
using MasterPlay.Models.Models;
using System;
using System.Diagnostics;
using System.Linq;

namespace MasterPlay.UI.ViewModels
{
    public partial class GameViewModel : ObservableObject
    {
        // --- Properties ---
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayPlayTime))]
        private Game _game;
        [ObservableProperty]
        private bool hasPlayableExe;

        // --- Constructors and Initialization ---
        public GameViewModel(Game game)
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            HasPlayableExe = game.Ready;
        }

        // --- Public Properties: Display and Status Helpers ---
        public bool ShowFolderWarning => !CheckFolderWarning();

        public bool IsGameRunning => CheckRunningGame();

        public string DisplayPlayTime
        {
            get
            {
                var ts = TimeSpan.FromSeconds(Game.TotalPlayTimeSeconds);
                return $"{(int)ts.TotalHours:D2}h{ts.Minutes:D2}m";
            }
        }

        // --- Private Methods: Path and Status Checks ---
        public string GetProcessPath()
        {
            var available = DataService.LoadAvailableGames();
            try
            {
                var processName = Game.ExeName + ".exe";
                string fullPath = available.FirstOrDefault(a =>
                    string.Equals(a.ExeName, processName, StringComparison.OrdinalIgnoreCase))?.FullPath;
                return fullPath ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private bool CheckFolderWarning()
        {
            return FileHelper.FileCheck(Game.SaveFolderPath) && FileHelper.FileCheck(Game.BackupFolderPath);
        }

        private bool CheckRunningGame()
        {
            if (!Game.Ready) return false;
            var processName = Game?.ExeName;
            if (string.IsNullOrEmpty(processName)) return false;
            // Loại bỏ đuôi .exe nếu có
            if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                processName = processName.Substring(0, processName.Length - 4);
            }
            Process[] processes = Process.GetProcessesByName(processName);
            return processes.Length > 0;
        }
    }
}