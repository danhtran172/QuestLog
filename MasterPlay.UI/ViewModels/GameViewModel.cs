using CommunityToolkit.Mvvm.ComponentModel;
using MasterPlay.Helpers;
using MasterPlay.Models;
using MasterPlay.Models.Models;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace MasterPlay.UI.ViewModels
{
    public partial class GameViewModel : ObservableObject
    {
        // =====================================================================
        // Fields
        // =====================================================================

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayName))]
        [NotifyPropertyChangedFor(nameof(DisplayPlayTime))]
        [NotifyPropertyChangedFor(nameof(DisplayPublishYear))]
        [NotifyPropertyChangedFor(nameof(DisplayPublishDate))]
        [NotifyPropertyChangedFor(nameof(DisplayPublisher))]
        [NotifyPropertyChangedFor(nameof(DisplayDeveloper))]
        [NotifyPropertyChangedFor(nameof(DisplaySeries))]
        [NotifyPropertyChangedFor(nameof(DisplayGenre))]
        [NotifyPropertyChangedFor(nameof(DisplayTheme))]
        [NotifyPropertyChangedFor(nameof(DisplayProgress))]
        [NotifyPropertyChangedFor(nameof(DisplayOwnership))]
        [NotifyPropertyChangedFor(nameof(DisplayScore))]
        [NotifyPropertyChangedFor(nameof(DisplayExcited))]
        [NotifyPropertyChangedFor(nameof(DisplayMetaScore))]
        [NotifyPropertyChangedFor(nameof(DisplayAvgPlayTime))]
        [NotifyPropertyChangedFor(nameof(DisplayFinishedIn))]
        [NotifyPropertyChangedFor(nameof(DisplayProcessName))]
        [NotifyPropertyChangedFor(nameof(DisplayInitProcessName))]
        [NotifyPropertyChangedFor(nameof(DisplaySaveFolderPath))]
        [NotifyPropertyChangedFor(nameof(DisplayBackupFolderPath))]
        [NotifyPropertyChangedFor(nameof(DisplayImagePath))]
        [NotifyPropertyChangedFor(nameof(DisplayGalleryImagePath))]
        [NotifyPropertyChangedFor(nameof(ShowFolderWarning))]
        [NotifyPropertyChangedFor(nameof(IsGameRunning))]
        private Game _game;

        [ObservableProperty]
        private bool hasPlayableExe;

        // =====================================================================
        // Constructor
        // =====================================================================

        public GameViewModel(Game game)
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            HasPlayableExe = game.Ready;
        }

        // =====================================================================
        // Public Display Properties
        // =====================================================================

        public string DisplayName =>
            string.IsNullOrWhiteSpace(Game?.Name) ? "--" : Game.Name;

        public string DisplayPlayTime
        {
            get
            {
                if ((Game?.TotalPlayTimeSeconds ?? 0) <= 0)
                {
                    return "--";
                }

                var ts = TimeSpan.FromSeconds(Game.TotalPlayTimeSeconds);
                return $"{(int)ts.TotalHours:D2}h{ts.Minutes:D2}m";
            }
        }

        public string DisplayPublishYear =>
            Game?.PublishDate?.ToString("yyyy") ?? "--";

        public string DisplayPublishDate =>
            Game?.PublishDate?.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture) ?? "--";

        public string DisplayPublisher =>
            FormatText(Game?.Publisher);

        public string DisplayDeveloper =>
            FormatText(Game?.Developer);

        public string DisplaySeries =>
            FormatText(Game?.Series);

        public string DisplayGenre =>
            FormatText(Game?.Genre);

        public string DisplayTheme =>
            FormatText(Game?.Theme);

        public string DisplayProgress =>
            FormatText(Game?.Progress);

        public string DisplayOwnership =>
            FormatText(Game?.Ownership);

        public string DisplayScore =>
            (Game?.Score ?? 0) > 0 ? Game.Score.ToString("F1") : "--";

        public string DisplayExcited =>
            (Game?.Excited ?? 0) > 0 ? Game.Excited.ToString("F0") : "--";

        public string DisplayMetaScore =>
            (Game?.MetaScore ?? 0) > 0 ? Game.MetaScore.ToString("F0") : "--";

        public string DisplayAvgPlayTime =>
            (Game?.AvgPlayTime ?? 0) > 0 ? $"{Game.AvgPlayTime}h" : "--";

        public string DisplayFinishedIn =>
            string.IsNullOrWhiteSpace(Game?.FinishedIn) ? "Not Finished" : Game.FinishedIn;

        public string DisplayProcessName =>
            FormatExeDisplayName(Game?.ExeName);

        public string DisplayInitProcessName =>
            FormatExeDisplayName(Game?.InitExeName);

        public string DisplaySaveFolderPath =>
            FormatText(Game?.SaveFolderPath);

        public string DisplayBackupFolderPath =>
            FormatText(Game?.BackupFolderPath);

        public string DisplayImagePath =>
            FormatText(Game?.ImagePath);

        public string DisplayGalleryImagePath =>
            FormatText(Game?.GImagePath);

        // =====================================================================
        // Public Status Properties
        // =====================================================================

        public bool ShowFolderWarning => !CheckFolderWarning();

        public bool IsGameRunning => CheckRunningGame();

        // =====================================================================
        // Public Methods
        // =====================================================================

        public string GetProcessPath()
        {
            var detectedProcesses = DataService.LoadDetectedProcesses();

            try
            {
                string processName = $"{Game.InitExeName}.exe";

                string fullPath = detectedProcesses.FirstOrDefault(a =>
                    string.Equals(a.ExeName, processName, StringComparison.OrdinalIgnoreCase))?.FullPath;

                return fullPath ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        // =====================================================================
        // Private Methods
        // =====================================================================

        private static string FormatText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "--" : value;
        }

        private static string FormatExeDisplayName(string? exeName)
        {
            if (string.IsNullOrWhiteSpace(exeName))
            {
                return "--";
            }

            return exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? exeName
                : $"{exeName}.exe";
        }

        private bool CheckFolderWarning()
        {
            return FileHelper.FileCheck(Game.SaveFolderPath) &&
                   FileHelper.FileCheck(Game.BackupFolderPath);
        }

        private bool CheckRunningGame()
        {
            if (!Game.Ready)
            {
                return false;
            }

            string processName = Game.ExeName;
            if (string.IsNullOrWhiteSpace(processName))
            {
                return false;
            }

            if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                processName = processName[..^4];
            }

            Process[] processes = Process.GetProcessesByName(processName);
            return processes.Length > 0;
        }

        // =====================================================================
        // Partial Property Changed Handlers
        // =====================================================================

        partial void OnGameChanged(Game value)
        {
            HasPlayableExe = value?.Ready ?? false;
        }
    }
}