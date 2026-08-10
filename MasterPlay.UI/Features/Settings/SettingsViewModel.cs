using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MasterPlay.Core.Localization;
using MasterPlay.Infrastructure.Configuration;
using MasterPlay.UI.Services;
using MasterPlay.UI.Shared.ViewModels;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace MasterPlay.UI.Features.Settings
{
    public partial class LibraryPathItem : ObservableObject
    {

        [ObservableProperty] private string value = string.Empty;
        [ObservableProperty] private bool isReadOnly = true;


        [RelayCommand]
        private void EditLibrary()
        {
            var dialog = new OpenFolderDialog { Title = LocalizationService.Get("SelectGameLibraryFolder") };
            if (dialog.ShowDialog() == true)
            {
                Value = dialog.FolderName;
                IsReadOnly = true;
            }
            else IsReadOnly = false;
        }
    }

    public partial class SettingsViewModel : BaseViewModel
    {

        [ObservableProperty] private string backupRootPath = string.Empty;
        [ObservableProperty] private string configPath = string.Empty;
        [ObservableProperty] private string databasePath = string.Empty;
        [ObservableProperty] private ObservableCollection<LibraryPathItem> libraryPaths = new();
        [ObservableProperty] private bool isBackupReadOnly = true;
        [ObservableProperty] private bool isConfigReadOnly = true;
        [ObservableProperty] private bool isDatabaseReadOnly = true;
        [ObservableProperty] private LanguageOption? selectedLanguage;
        [ObservableProperty] private bool isDarkMode;
        [ObservableProperty] private bool isCatalogPage;
        public CatalogSettingsViewModel Catalog { get; } = new();

        public IReadOnlyList<LanguageOption> AvailableLanguages => LocalizationService.Languages;
        private string _originalLanguage = LocalizationService.CurrentLanguage;
        private bool _originalDarkMode;

        public SettingsViewModel()
        {
            Catalog.BackRequested += () => IsCatalogPage = false;
            LoadSettings();
        }


        [RelayCommand]
        private void EditBackup()
        {
            var d = new OpenFolderDialog { Title = LocalizationService.Get("SelectBackupRootFolder") };
            if (d.ShowDialog() == true) BackupRootPath = d.FolderName;
        }

        [RelayCommand]
        private void EditConfig()
        {
            var d = new OpenFileDialog
            {
                Title = LocalizationService.Get("SelectConfigFile"),
                Filter = $"{LocalizationService.Get("JsonFiles")}|*.json|{LocalizationService.Get("AllFiles")}|*.*"
            };
            if (d.ShowDialog() == true) ConfigPath = d.FileName;
        }

        [RelayCommand]
        private void EditDatabase()
        {
            var d = new OpenFileDialog
            {
                Title = LocalizationService.Get("SelectDatabaseFile"),
                Filter = $"{LocalizationService.Get("DatabaseFiles")}|*.db|{LocalizationService.Get("AllFiles")}|*.*"
            };
            if (d.ShowDialog() == true) DatabasePath = d.FileName;
        }


        [RelayCommand]
        private void AddLibrary() => LibraryPaths.Add(new LibraryPathItem { Value = "", IsReadOnly = false });

        [RelayCommand]
        private void RemoveLibrary(LibraryPathItem item) { if (item != null) LibraryPaths.Remove(item); }

        [RelayCommand]
        private void ManageLabelsAndIcons()
        {
            IsCatalogPage = true;
        }


        [RelayCommand]
        private void Save()
        {
            try
            {
                var config = JsonService.LoadConfig();
                config.BackupRootPath = BackupRootPath;
                config.DatabasePath = DatabasePath;
                config.GameLibraryPath = LibraryPaths.Select(x => x.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
                config.Language = SelectedLanguage?.Code ?? "en-US";
                config.isDarkMode = IsDarkMode;
                JsonService.SaveConfig(config);
                _originalLanguage = config.Language;
                _originalDarkMode = config.isDarkMode;
                StatusMessage = LocalizationService.Get("SettingsSaved");
                IsBackupReadOnly = IsConfigReadOnly = IsDatabaseReadOnly = true;
                foreach (var item in LibraryPaths) item.IsReadOnly = true;
            }
            catch (Exception ex) { StatusMessage = LocalizationService.Get("ErrorWithDetails", ex.Message); }
        }

        [RelayCommand]
        private void Cancel()
        {
            RestoreLanguage();
            RestoreTheme();
            RaiseRequestClose(false);
        }

        public void RestoreLanguage() => LocalizationService.SetLanguage(_originalLanguage);
        public void RestoreTheme() => ThemeService.Apply(_originalDarkMode);


        private void LoadSettings()
        {
            var config = JsonService.LoadConfig();

            BackupRootPath = config.BackupRootPath ?? string.Empty;
            DatabasePath = config.DatabasePath ?? string.Empty;
            ConfigPath = JsonService.GetConfigPath();
            _originalLanguage = LocalizationService.CurrentLanguage;
            _originalDarkMode = config.isDarkMode;
            IsDarkMode = _originalDarkMode;
            SelectedLanguage = AvailableLanguages.First(x => x.Code == _originalLanguage);

            LibraryPaths = new ObservableCollection<LibraryPathItem>(
                (config.GameLibraryPath ?? new System.Collections.Generic.List<string>())
                    .Select(path => new LibraryPathItem { Value = path, IsReadOnly = true })
            );
        }

        partial void OnSelectedLanguageChanged(LanguageOption? value)
        {
            if (value != null)
                LocalizationService.SetLanguage(value.Code);
        }

        partial void OnIsDarkModeChanged(bool value) => ThemeService.Apply(value);
    }
}
