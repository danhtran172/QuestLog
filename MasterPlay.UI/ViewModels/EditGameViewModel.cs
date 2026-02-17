using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MasterPlay.Helpers;
using MasterPlay.Models;
using MasterPlay.Models.Models;
using MasterPlay.UI.ViewModels.Generral;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MasterPlay.UI.ViewModels
{
    public partial class EditGameViewModel : BaseViewModel
    {
        // --- Properties ---
        [ObservableProperty]
        private Game game;
        [ObservableProperty]
        private string backupFolderPath = string.Empty;
        [ObservableProperty]
        private BitmapImage? previewImage;
        [ObservableProperty]
        private bool isReadOnly = true;
        [ObservableProperty]
        private bool saveFolderAccessible;
        [ObservableProperty]
        private bool backupFolderAccessible;
        // Play time properties
        [ObservableProperty]
        private int playTimeHours;
        [ObservableProperty]
        private int playTimeMinutes;
        [ObservableProperty]
        private int adjustMinutes;

        // --- Constructors and Initialization ---
        public EditGameViewModel(Game selectedGame)
        {
            Game = selectedGame ?? new Game();
            ReloadGameFromConfig();
        }

        // --- Commands: Mode Toggling and Saving ---
        [RelayCommand]
        private void ToggleEditMode()
        {
            IsReadOnly = !IsReadOnly;
            ReloadGameFromConfig();
            if (IsReadOnly) StatusMessage = string.Empty;
        }

        [RelayCommand]
        private void Save()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(Game.Name))
                {
                    UpdateBackupFolderPath(Game.Name);
                }
                var games = DataHelper.LoadGames();
                int index = games.FindIndex(g => g.Id == Game.Id);
                if (index >= 0)
                {
                    if (!string.IsNullOrWhiteSpace(Game.BackupFolderPath))
                    {
                        if (!Directory.Exists(Game.BackupFolderPath))
                        {
                            Directory.CreateDirectory(Game.BackupFolderPath);
                        }
                        string infoFilePath = Path.Combine(Game.BackupFolderPath, "save_location.txt");
                        File.WriteAllText(infoFilePath, Game.SaveFolderPath);
                    }
                    ViewModelHelpers.InputFormatter(Game);
                    games[index] = Game;
                    DataHelper.SaveGamesData(games);
                    ReloadGameFromConfig();
                    StatusMessage = "Data Saved Successfully!";
                    IsReadOnly = true;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show(StatusMessage, "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void DeleteGame()
        {
            var result = MessageBox.Show($"Are you sure to Disable '{Game.Name}'?\n(Game will be hidden but data remains.)",
                "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var games = DataHelper.LoadGames();
                    var gameToDisable = games.FirstOrDefault(g => g.Id == Game.Id);
                    if (gameToDisable != null)
                    {
                        gameToDisable.IsDisabled = true;
                        DataHelper.SaveGamesData(games);
                        StatusMessage = $"{gameToDisable.Name} is Disabled!";
                        RaiseRequestClose(true);
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error: {ex.Message}";
                    MessageBox.Show(StatusMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            var games = DataHelper.LoadGames();
            var originalGame = games.FirstOrDefault(g => g.Id == Game.Id);
            if (originalGame != null)
            {
                Game = originalGame;
                BackupFolderPath = Game.BackupFolderPath;
                PreviewImage = ViewModelHelpers.LoadPreviewImage(Game.ImagePath);
                ReloadGameFromConfig();
            }
            IsReadOnly = true;
            StatusMessage = string.Empty;
        }

        [RelayCommand]
        private void Close() => RaiseRequestClose(false);

        // --- Commands: Folder Handling ---
        [RelayCommand]
        private void OpenSaveFolder() => HandleFolderOpening((Game.SaveFolderPath, "Save Folder"));

        [RelayCommand]
        private void OpenBackupFolder() => HandleFolderOpening((Game.BackupFolderPath, "Backup Folder"));

        // --- Commands: Sync and Adjustment ---
        [RelayCommand]
        private void SyncImage()
        {
            if (string.IsNullOrWhiteSpace(Game.ImagePath))
            {
                PreviewImage = null;
                StatusMessage = "No image URL provided.";
                return;
            }
            try
            {
                PreviewImage = ViewModelHelpers.LoadPreviewImage(Game.ImagePath);
                StatusMessage = "Image preview updated successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load image: {ex.Message}";
            }
        }

        [RelayCommand]
        private void AdjustPlayTimeUp()
        {
            if (AdjustMinutes <= 0)
            {
                StatusMessage = "Please enter a positive number of minutes.";
                return;
            }
            Game.TotalPlayTimeSeconds += AdjustMinutes * 60L;
            UpdatePlayTimeFieldsFromSeconds();
            StatusMessage = $"Added {AdjustMinutes} minutes to play time.";
            AdjustMinutes = 0;
        }

        [RelayCommand]
        private void AdjustPlayTimeDown()
        {
            if (AdjustMinutes <= 0)
            {
                StatusMessage = "Please enter a positive number of minutes.";
                return;
            }
            long newSeconds = Game.TotalPlayTimeSeconds - (AdjustMinutes * 60L);
            if (newSeconds < 0)
            {
                Game.TotalPlayTimeSeconds = 0;
                StatusMessage = "Play time cannot be negative. Reset to 0.";
            }
            else
            {
                Game.TotalPlayTimeSeconds = newSeconds;
                StatusMessage = $"Subtracted {AdjustMinutes} minutes from play time.";
            }
            UpdatePlayTimeFieldsFromSeconds();
            AdjustMinutes = 0;
        }

        // --- Private Methods: Folder Handling ---
        private void HandleFolderOpening(params (string? Path, string Desc)[] locations)
        {
            if (!FileHelper.TryOpenLocations(out var errors, locations))
            {
                MessageBox.Show(
                    "Cannot Open Folder:\n" + string.Join("\n", errors),
                    "Access Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        // --- Private Methods: Data Loading and Updating ---
        private void ReloadGameFromConfig()
        {
            var games = DataHelper.LoadGames();
            var reloaded = games.FirstOrDefault(g => g.Id == Game.Id);
            if (reloaded != null)
            {
                Game = reloaded;
                BackupFolderPath = Game.BackupFolderPath;
                PreviewImage = ViewModelHelpers.LoadPreviewImage(Game.ImagePath);
                if (IsReadOnly)
                {
                    SaveFolderAccessible = FileHelper.FileCheck(Game.SaveFolderPath);
                    BackupFolderAccessible = FileHelper.FileCheck(Game.BackupFolderPath);
                }
                else
                {
                    SaveFolderAccessible = true;
                    BackupFolderAccessible = true;
                    UpdatePlayTimeFieldsFromSeconds();
                }
            }
        }

        private void UpdateBackupFolderPath(string newGameName)
        {
            if (string.IsNullOrWhiteSpace(newGameName)) return;
            string backupRoot = DataHelper.GetBackupRootPath();
            string validFolderName = ViewModelHelpers.ValidFolderName(newGameName);
            string newBackupFolder = Path.Combine(backupRoot, validFolderName);
            if (!string.IsNullOrWhiteSpace(Game.BackupFolderPath) &&
                Directory.Exists(Game.BackupFolderPath) &&
                Game.BackupFolderPath != newBackupFolder)
            {
                try
                {
                    Directory.Move(Game.BackupFolderPath, newBackupFolder);
                    StatusMessage = $"Backup folder renamed to: {validFolderName}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Failed to rename backup folder: {ex.Message}";
                }
            }
            else if (!Directory.Exists(newBackupFolder))
            {
                Directory.CreateDirectory(newBackupFolder);
            }
            Game.BackupFolderPath = newBackupFolder;
        }

        // --- Private Methods: Play Time Handling ---
        private void UpdatePlayTimeFieldsFromSeconds()
        {
            var ts = TimeSpan.FromSeconds(Game.TotalPlayTimeSeconds);
            PlayTimeHours = (int)ts.TotalHours;
            PlayTimeMinutes = ts.Minutes;
            OnPropertyChanged(nameof(PlayTimeHours));
            OnPropertyChanged(nameof(PlayTimeMinutes));
        }

        private void UpdateTotalPlayTimeFromFields()
        {
            Game.TotalPlayTimeSeconds = (PlayTimeHours * 3600L) + (PlayTimeMinutes * 60L);
        }

        // --- Partial Methods: Property Changed Handlers ---
        partial void OnGameChanged(Game value)
        {
            PreviewImage = ViewModelHelpers.LoadPreviewImage(value?.ImagePath);
            if (!IsReadOnly && !string.IsNullOrWhiteSpace(value?.Name))
            {
                UpdateBackupFolderPath(value.Name);
            }
        }

        partial void OnIsReadOnlyChanged(bool value)
        {
            if (!value) // Vào edit mode
            {
                UpdatePlayTimeFieldsFromSeconds();
            }
        }

        partial void OnPlayTimeHoursChanged(int value)
        {
            if (value < 0) PlayTimeHours = 0;
            UpdateTotalPlayTimeFromFields();
        }

        partial void OnPlayTimeMinutesChanged(int value)
        {
            if (value < 0) PlayTimeMinutes = 0;
            if (value >= 60)
            {
                PlayTimeHours += value / 60;
                PlayTimeMinutes = value % 60;
            }
            UpdateTotalPlayTimeFromFields();
        }

        // --- Public Properties: Display Helpers ---
        public string DisplayPlayTime
        {
            get
            {
                var ts = TimeSpan.FromSeconds(Game.TotalPlayTimeSeconds);
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            }
        }
    }
}