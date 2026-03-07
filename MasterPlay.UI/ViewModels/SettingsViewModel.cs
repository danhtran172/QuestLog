using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MasterPlay.Helpers;
using MasterPlay.Models;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace MasterPlay.UI.ViewModels
{
    public partial class LibraryPathItem : ObservableObject
    {
        // --- Properties ---
        [ObservableProperty] private string value = string.Empty;
        [ObservableProperty] private bool isReadOnly = true;

        // --- Commands ---
        [RelayCommand]
        private void EditLibrary()
        {
            var dialog = new OpenFolderDialog { Title = "Select Game Library Folder" };
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
        // --- Properties ---
        [ObservableProperty] private string backupRootPath = string.Empty;
        [ObservableProperty] private string configPath = string.Empty;
        [ObservableProperty] private string databasePath = string.Empty;
        [ObservableProperty] private ObservableCollection<LibraryPathItem> libraryPaths = new();
        [ObservableProperty] private bool isBackupReadOnly = true;
        [ObservableProperty] private bool isConfigReadOnly = true;
        [ObservableProperty] private bool isDatabaseReadOnly = true;

        // --- Constructors and Initialization ---
        public SettingsViewModel()
        {
            LoadSettings();
        }

        // --- Commands: Editing Paths ---
        [RelayCommand]
        private void EditBackup()
        {
            var d = new OpenFolderDialog { Title = "Select Backup Root Folder" };
            if (d.ShowDialog() == true) BackupRootPath = d.FolderName;
        }

        [RelayCommand]
        private void EditConfig()
        {
            var d = new OpenFileDialog
            {
                Title = "Select Config File",
                Filter = "JSON files|*.json|All files|*.*"
            };
            if (d.ShowDialog() == true) ConfigPath = d.FileName;
        }

        [RelayCommand]
        private void EditDatabase()
        {
            var d = new OpenFileDialog
            {
                Title = "Select Database File",
                Filter = "Database files|*.db|All files|*.*"
            };
            if (d.ShowDialog() == true) DatabasePath = d.FileName;
        }

        // --- Commands: Library Management ---
        [RelayCommand]
        private void AddLibrary() => LibraryPaths.Add(new LibraryPathItem { Value = "", IsReadOnly = false });

        [RelayCommand]
        private void RemoveLibrary(LibraryPathItem item) { if (item != null) LibraryPaths.Remove(item); }

        // --- Commands: Saving and Canceling ---
        [RelayCommand]
        private void Save()
        {
            try
            {
                var config = JsonService.LoadConfig();
                config.BackupRootPath = BackupRootPath;
                config.ConfigFilePath = ConfigPath;
                config.DatabasePath = DatabasePath;
                config.GameLibraryPath = LibraryPaths.Select(x => x.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
                JsonService.SaveConfig(config);
                StatusMessage = "Settings saved successfully!";
                IsBackupReadOnly = IsConfigReadOnly = IsDatabaseReadOnly = true;
                foreach (var item in LibraryPaths) item.IsReadOnly = true;
            }
            catch (Exception ex) { StatusMessage = "Error: " + ex.Message; }
        }

        [RelayCommand]
        private void Cancel() => RaiseRequestClose(false);

        // --- Private Methods: Data Loading ---
        private void LoadSettings()
        {
            var config = JsonService.LoadConfig();
            if (config != null)
            {
                BackupRootPath = config.BackupRootPath ?? "";
                ConfigPath = config.ConfigFilePath ?? "";
                DatabasePath = config.DatabasePath ?? "";
                LibraryPaths.Clear();
                if (config.GameLibraryPath != null)
                {
                    foreach (var path in config.GameLibraryPath)
                        LibraryPaths.Add(new LibraryPathItem { Value = path, IsReadOnly = true });
                }
            }
        }
    }
}