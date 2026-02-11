using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MasterPlay.Models;
using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;

namespace MasterPlay.UI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private string backupRootPath;

        [ObservableProperty]
        private string configPath;

        [ObservableProperty]
        private string gameDataPath;  // MỚI: Game Data File Path

        [ObservableProperty]
        private bool isBackupReadOnly = true;

        [ObservableProperty]
        private bool isConfigReadOnly = true;

        [ObservableProperty]
        private bool isGameDataReadOnly = true;  // MỚI

        [ObservableProperty]
        private string statusMessage = string.Empty;

        // Lưu giá trị gốc để Cancel
        private readonly string _originalBackupPath;
        private readonly string _originalConfigPath;
        private readonly string _originalGameDataPath;  

        public SettingsViewModel()
        {
            var config = ConfigManager.LoadConfig();

            BackupRootPath = config.BackupRootPath;
            ConfigPath = config.ConfigFilePath;
            gameDataPath = config.DataFilePath;  

            _originalBackupPath = BackupRootPath;
            _originalConfigPath = ConfigPath;
            _originalGameDataPath = gameDataPath;  

            // Nếu DataFilePath trống, fallback ngay
            if (string.IsNullOrEmpty(gameDataPath))
            {
                gameDataPath = Path.Combine(
                    Path.GetDirectoryName(ConfigPath) ?? ConfigManager.GetConfigDirectory(),
                    "gameData.json");
            }
        }

        [RelayCommand]
        private void EditBackup() => IsBackupReadOnly = false;

        [RelayCommand]
        private void EditConfig() => IsConfigReadOnly = false;

        [RelayCommand]
        private void EditGameData() => IsGameDataReadOnly = false; 

        [RelayCommand]
        private void BrowseBackup()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                InitialDirectory = !string.IsNullOrWhiteSpace(BackupRootPath)
                    ? Path.GetDirectoryName(BackupRootPath)
                    : @"D:\\",
                FolderName = !string.IsNullOrWhiteSpace(BackupRootPath)
                    ? Path.GetFileName(BackupRootPath)
                    : string.Empty,
                Title = "Select Backup Root Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                BackupRootPath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private void BrowseConfig()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Select Config File Location",
                FileName = "config.json"
            };

            if (!string.IsNullOrEmpty(ConfigPath))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(ConfigPath);
            }

            if (dialog.ShowDialog() == true)
            {
                ConfigPath = dialog.FileName;
            }
        }

        [RelayCommand]
        private void BrowseGameData()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Select Game Data File Location",
                FileName = "gameData.json"
            };

            if (!string.IsNullOrEmpty(gameDataPath))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(gameDataPath);
            }

            if (dialog.ShowDialog() == true)
            {
                gameDataPath = dialog.FileName;
            }
        }

        [RelayCommand]
        private void Save()
        {
            try
            {
                var config = ConfigManager.LoadConfig();

                config.BackupRootPath = BackupRootPath;
                config.ConfigFilePath = ConfigPath;
                config.DataFilePath = gameDataPath;

                ConfigManager.SaveConfig(config);

                StatusMessage = "Save Succesfully!";
                MessageBox.Show("Save success!", "Notification", MessageBoxButton.OK, MessageBoxImage.Information);

                // Reset read-only
                IsBackupReadOnly = true;
                IsConfigReadOnly = true;
                IsGameDataReadOnly = true;

                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Save error: {ex.Message}";
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            BackupRootPath = _originalBackupPath;
            ConfigPath = _originalConfigPath;
            gameDataPath = _originalGameDataPath;  // MỚI

            IsBackupReadOnly = true;
            IsConfigReadOnly = true;
            IsGameDataReadOnly = true;

            StatusMessage = string.Empty;
            RequestClose?.Invoke(false);
        }

        public event Action<bool>? RequestClose;
    }
}