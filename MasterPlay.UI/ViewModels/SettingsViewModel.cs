using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MasterPlay.Helpers;
using MasterPlay.Models;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;

namespace MasterPlay.UI.ViewModels
{
    public partial class LibraryPathItem : ObservableObject
    {
        [ObservableProperty] private string value = string.Empty;
        [ObservableProperty] private bool isReadOnly = true;

        [RelayCommand]
        private void Edit()
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

    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty] private string backupRootPath = string.Empty;
        [ObservableProperty] private string configPath = string.Empty;
        [ObservableProperty] private string gameDataPath = string.Empty;
        [ObservableProperty] private string metaDataPath = string.Empty;
        [ObservableProperty] private ObservableCollection<LibraryPathItem> libraryPaths = new();

        [ObservableProperty] private bool isBackupReadOnly = true;
        [ObservableProperty] private bool isConfigReadOnly = true;
        [ObservableProperty] private bool isGameDataReadOnly = true;
        [ObservableProperty] private bool isMetaDataReadOnly = true;

        [ObservableProperty] private string statusMessage = string.Empty;
        private readonly DispatcherTimer _statusTimer;

        public SettingsViewModel()
        {
            // Khởi tạo timer cho Status Message
            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _statusTimer.Tick += (s, e) => { StatusMessage = string.Empty; _statusTimer.Stop(); };

            LoadSettings();
        }

        // Tự động chạy timer khi StatusMessage có nội dung mới
        partial void OnStatusMessageChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _statusTimer.Stop();
                _statusTimer.Start();
            }
        }

        private void LoadSettings()
        {
            var config = Data.LoadConfig();
            if (config != null)
            {
                BackupRootPath = config.BackupRootPath ?? "";
                ConfigPath = config.ConfigFilePath ?? "";
                GameDataPath = config.DataFilePath ?? "";
                MetaDataPath = config.MetaDataPath ?? "";

                LibraryPaths.Clear();
                if (config.GameLibraryPath != null)
                {
                    foreach (var path in config.GameLibraryPath)
                        LibraryPaths.Add(new LibraryPathItem { Value = path, IsReadOnly = true });
                }
            }
        }

        [RelayCommand]
        private void EditBackup() { var d = new OpenFolderDialog(); if (d.ShowDialog() == true) BackupRootPath = d.FolderName; }
        [RelayCommand]
        private void EditConfig() { var d = new OpenFileDialog { Filter = "JSON|*.json" }; if (d.ShowDialog() == true) ConfigPath = d.FileName; }
        [RelayCommand]
        private void EditGameData() { var d = new OpenFileDialog { Filter = "JSON|*.json" }; if (d.ShowDialog() == true) GameDataPath = d.FileName; }
        [RelayCommand]
        private void EditMetaData() { var d = new OpenFileDialog { Filter = "JSON|*.json" }; if (d.ShowDialog() == true) MetaDataPath = d.FileName; }

        [RelayCommand]
        private void AddLibrary() => LibraryPaths.Add(new LibraryPathItem { Value = "", IsReadOnly = false });

        [RelayCommand]
        private void RemoveLibrary(LibraryPathItem item) { if (item != null) LibraryPaths.Remove(item); }

        [RelayCommand]
        private void Save()
        {
            try
            {
                var config = Data.LoadConfig();
                config.BackupRootPath = BackupRootPath;
                config.ConfigFilePath = ConfigPath;
                config.DataFilePath = GameDataPath;
                config.MetaDataPath = MetaDataPath;
                config.GameLibraryPath = LibraryPaths.Select(x => x.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

                Data.SaveConfig(config);
                StatusMessage = "Settings saved successfully!";
                IsBackupReadOnly = IsConfigReadOnly = IsGameDataReadOnly = IsMetaDataReadOnly = true;
                foreach (var item in LibraryPaths) item.IsReadOnly = true;
            }
            catch (Exception ex) { StatusMessage = "Error: " + ex.Message; }
        }

        [RelayCommand]
        private void Cancel() => RequestClose?.Invoke(false);
        public event Action<bool>? RequestClose;
    }
}