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

        [ObservableProperty] private string value = string.Empty;
        [ObservableProperty] private bool isReadOnly = true;


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

        [ObservableProperty] private string backupRootPath = string.Empty;
        [ObservableProperty] private string configPath = string.Empty;
        [ObservableProperty] private string databasePath = string.Empty;
        [ObservableProperty] private ObservableCollection<LibraryPathItem> libraryPaths = new();
        [ObservableProperty] private bool isBackupReadOnly = true;
        [ObservableProperty] private bool isConfigReadOnly = true;
        [ObservableProperty] private bool isDatabaseReadOnly = true;


        public SettingsViewModel()
        {
            LoadSettings();
        }


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


        [RelayCommand]
        private void AddLibrary() => LibraryPaths.Add(new LibraryPathItem { Value = "", IsReadOnly = false });

        [RelayCommand]
        private void RemoveLibrary(LibraryPathItem item) { if (item != null) LibraryPaths.Remove(item); }


        [RelayCommand]
        private void Save()
        {
            try
            {
                var config = JsonService.LoadConfig();
                config.BackupRootPath = BackupRootPath;
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


        private void LoadSettings()
        {
            var config = JsonService.LoadConfig();

            BackupRootPath = config.BackupRootPath ?? string.Empty;
            DatabasePath = config.DatabasePath ?? string.Empty;
            ConfigPath = JsonService.GetConfigPath();

            LibraryPaths = new ObservableCollection<LibraryPathItem>(
                (config.GameLibraryPath ?? new System.Collections.Generic.List<string>())
                    .Select(path => new LibraryPathItem { Value = path, IsReadOnly = true })
            );
        }
    }
}