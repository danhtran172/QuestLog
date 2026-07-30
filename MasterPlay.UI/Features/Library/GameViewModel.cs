using CommunityToolkit.Mvvm.ComponentModel;
using MasterPlay.Core.Localization;
using MasterPlay.Infrastructure.FileSystem;
using MasterPlay.Infrastructure.Data.Services;
using MasterPlay.Core.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MasterPlay.UI.Features.Library
{
    public partial class GameViewModel : ObservableObject
    {




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
        private Game _game;

        [ObservableProperty]
        private bool hasPlayableExe;
        [ObservableProperty]
        private bool isGameRunning;
        [ObservableProperty]
        private string folderWarningInfo = "";

        private List<Genre> _gameGenres = [];
        private List<Theme> _gameThemes = [];
        private string _searchName = string.Empty;
        private string _searchSeries = string.Empty;
        private string _searchPublisher = string.Empty;
        private string _searchDeveloper = string.Empty;
        private string _searchAll = string.Empty;
        private string _nameInitials = string.Empty;
        private string _seriesInitials = string.Empty;





        public GameViewModel(Game game)
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
        }





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
            Game?.PublishDate?.ToString("MMM dd, yyyy", CultureInfo.CurrentCulture) ?? "--";

        public string DisplayPublisher =>
            FormatText(Game?.Publisher);

        public string DisplayDeveloper =>
            FormatText(Game?.Developer);

        public string DisplaySeries =>
            FormatText(Game?.Series?.Name);


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
            LocalizationService.GetDataValue(Game?.Progress?.Name);

        public string DisplayOwnership =>
            LocalizationService.GetDataValue(Game?.Ownership?.Name);

        public string DisplayScore => Game.Score switch
        {
            null => "--",
            <= 0 => "--",
            999 => "10",
            _ => Game.Score.Value.ToString()
        };

        public string DisplayExcited => Game.Excited switch
        {
            null => "--",
            <= 0 => "--",
            999 => "10",
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





        public bool ShowFolderWarning => CheckFolderWarning();

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
                if (saveMissing) missingItems.Add(LocalizationService.Get("SaveFolder"));
                if (backupMissing) missingItems.Add(LocalizationService.Get("BackupFolder"));
                if (exeMissing) missingItems.Add(LocalizationService.Get("ProcessName"));
                FolderWarningInfo = LocalizationService.Get("CannotBackup", string.Join(", ", missingItems));
            }
            else
            {
                FolderWarningInfo = string.Empty;
            }

            return showWarning;
        }


        internal bool MatchesSearch(string searchBy, string search)
        {
            if (search.Length == 0)
                return true;

            return searchBy switch
            {
                "Name" => Contains(_searchName, search) || Contains(_nameInitials, search),
                "Series" => Contains(_searchSeries, search) || Contains(_seriesInitials, search),
                "Publisher" => Contains(_searchPublisher, search),
                "Developer" => Contains(_searchDeveloper, search),
                "Release" => int.TryParse(search, out var year) && Game.PublishDate?.Year == year,
                _ => Contains(_searchAll, search) || Contains(_nameInitials, search) || Contains(_seriesInitials, search)
            };
        }

        internal void UpdateRunningState(IReadOnlySet<string> runningProcessNames)
        {
            var processName = Path.GetFileNameWithoutExtension(Game.ExeName?.Trim() ?? string.Empty);
            IsGameRunning = Game.Ready
                && processName.Length > 0
                && runningProcessNames.Contains(processName);
        }

        private void RebuildSearchIndex()
        {
            _searchName = Game.Name ?? string.Empty;
            _searchSeries = Game.Series?.Name ?? string.Empty;
            _searchPublisher = Game.Publisher ?? string.Empty;
            _searchDeveloper = Game.Developer ?? string.Empty;
            _nameInitials = BuildInitials(_searchName);
            _seriesInitials = BuildInitials(_searchSeries);
            _searchAll = string.Join(
                '\u001F',
                _searchName,
                _searchSeries,
                string.Join('\u001E', _gameGenres.Where(x => x != null).Select(x => x.Name ?? string.Empty)),
                string.Join('\u001E', _gameThemes.Where(x => x != null).Select(x => x.Name ?? string.Empty)));
        }

        private static string BuildInitials(string value)
        {
            return string.Concat(
                value.Split([' ', '-', '_', '.'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(word => char.ToLowerInvariant(word[0])));
        }

        private static bool Contains(string value, string search)
        {
            return value.Contains(search, StringComparison.OrdinalIgnoreCase);
        }





        partial void OnGameChanged(Game value)
        {
            HasPlayableExe = value?.Ready ?? false;

            if (value != null)
            {
                _gameGenres = DataService.GetGameGenres(value.Id);
                _gameThemes = DataService.GetGameThemes(value.Id);
                RebuildSearchIndex();

                OnPropertyChanged(nameof(DisplayGenres));
                OnPropertyChanged(nameof(DisplayThemes));
            }
        }
    }
}
