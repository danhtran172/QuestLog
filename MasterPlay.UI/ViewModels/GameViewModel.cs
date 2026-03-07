using CommunityToolkit.Mvvm.ComponentModel;
using MasterPlay.Helpers;
using MasterPlay.Models;
using MasterPlay.Models.Models;
using System;
using System.Collections.Generic;
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
        [NotifyPropertyChangedFor(nameof(DisplayGenres))]
        [NotifyPropertyChangedFor(nameof(DisplayThemes))]
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
        [ObservableProperty]
        private string folderWarningInfo = "";

        // Thêm các property lưu danh sách genre và theme
        private List<Genre> _gameGenres;
        private List<Theme> _gameThemes;

        // =====================================================================
        // Constructor
        // =====================================================================

        public GameViewModel(Game game)
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            HasPlayableExe = game.Ready;

            _gameGenres = DataService.GetGameGenres(game.Id);
            _gameThemes = DataService.GetGameThemes(game.Id);
        }

        // =====================================================================
        // Public Display Properties
        // =====================================================================

        public string DisplayName =>
            string.IsNullOrWhiteSpace((string?)(Game?.Name)) ? "--" : Game.Name;

        public string DisplayPlayTime
        {
            get
            {
                if ((Game?.TotalPlayTimeSeconds ?? 0) <= 0)
                {
                    return "--";
                }

                var ts = TimeSpan.FromSeconds((double)Game.TotalPlayTimeSeconds);
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
            FormatText(Game?.Series?.Name);

        // Genre và Theme từ danh sách đã load
        public string DisplayGenres
        {
            get
            {
                if (_gameGenres == null || _gameGenres.Count == 0)
                    return "--";
                return string.Join(", ", _gameGenres.Select(g => g.Name));
            }
        }

        public string DisplayThemes
        {
            get
            {
                if (_gameThemes == null || _gameThemes.Count == 0)
                    return "--";
                return string.Join(", ", _gameThemes.Select(t => t.Name));
            }
        }

        public string DisplayProgress =>
            FormatText(Game?.Progress?.Name);

        public string DisplayOwnership =>
            FormatText(Game?.Ownership?.Name);

        public string DisplayScore => Game.Score switch
        {
            null => "--",
            <= 0 => "--",
            999 => "10🔥",
            _ => Game.Score.Value.ToString()
        };

        public string DisplayExcited => Game.Excited switch
        {
            null => "--",
            <= 0 => "--",
            999 => "10🔥",
            _ => Game.Excited.Value.ToString()
        };

        public string DisplayMetaScore =>
            (Game?.MetaScore ?? 0) > 0 ? Game.MetaScore.ToString() : "--";

        public string DisplayAvgPlayTime =>
            (Game?.AvgPlayTime ?? 0) > 0 ? $"{Game.AvgPlayTime}h" : "--";

        public string DisplayFinishedIn =>
            string.IsNullOrWhiteSpace((string?)(Game?.FinishedIn)) ? "--" : Game.FinishedIn;

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

        public bool ShowFolderWarning => CheckFolderWarning();

        public bool IsGameRunning => CheckRunningGame();

        // =====================================================================
        // Public Methods
        // =====================================================================

        public string GetLaunchProcessPath()
        {
            var detectedProcesses = DataService.LoadDetectedProcesses();

            try
            {
                string processName = $"{Game.ExeName}.exe";

                if (!string.IsNullOrEmpty(Game.InitExeName))
                {
                    processName = $"{Game.InitExeName}.exe";
                }

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
            if (!HasPlayableExe)
            {
                FolderWarningInfo = string.Empty;
                return false;
            }

            bool saveMissing = !FileHelper.FileCheck(Game.SaveFolderPath);
            bool backupMissing = !FileHelper.FileCheck(Game.BackupFolderPath);
            bool exeMissing = string.IsNullOrEmpty(Game.ExeName);

            bool showWarning = saveMissing || backupMissing || exeMissing;

            if (showWarning)
            {
                var missingItems = new System.Collections.Generic.List<string>();
                if (saveMissing) missingItems.Add("Save Folder");
                if (backupMissing) missingItems.Add("Backup Folder");
                if (exeMissing) missingItems.Add("Process Name");
                FolderWarningInfo = "Cannot Backup: " + string.Join(", ", missingItems) + " not available!";
            }
            else
            {
                FolderWarningInfo = string.Empty;
            }

            return showWarning;
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
            // Khi game thay đổi (nếu có), reload genre và theme
            if (value != null)
            {
                _gameGenres = DataService.GetGameGenres(value.Id);
                _gameThemes = DataService.GetGameThemes(value.Id);
                // Notify các property liên quan
                OnPropertyChanged(nameof(DisplayGenres));
                OnPropertyChanged(nameof(DisplayThemes));
            }
        }
    }
}