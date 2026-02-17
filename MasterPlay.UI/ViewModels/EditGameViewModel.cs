using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MasterPlay.Models;
using MasterPlay.Helpers;
using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Linq;

namespace MasterPlay.UI.ViewModels
{
    public partial class EditGameViewModel : ObservableObject
    {
        [ObservableProperty]
        private Game game;

        [ObservableProperty]
        private string backupFolderPath = string.Empty;

        [ObservableProperty]
        private BitmapImage? previewImage;

        [ObservableProperty]
        private bool isReadOnly = true;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private bool saveFolderAccessible;

        [ObservableProperty]
        private bool backupFolderAccessible;

        // Play time properties - PascalCase để binding đúng
        [ObservableProperty]
        private int playTimeHours;

        [ObservableProperty]
        private int playTimeMinutes;

        [ObservableProperty]
        private int adjustMinutes;

        public event Action<bool>? RequestClose;

        public EditGameViewModel(Game selectedGame)
        {
            Game = selectedGame ?? new Game();
            ReloadGameFromConfig(); // Load ban đầu
        }

        // --- Folder handling ---
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

        [RelayCommand]
        private void OpenSaveFolder() => HandleFolderOpening((Game.SaveFolderPath, "Save Folder"));

        [RelayCommand]
        private void OpenBackupFolder() => HandleFolderOpening((Game.BackupFolderPath, "Backup Folder"));

        // --- Edit & Validate ---
        [RelayCommand]
        private void ToggleEditMode()
        {
            IsReadOnly = !IsReadOnly;
            ReloadGameFromConfig();
            if (IsReadOnly) StatusMessage = string.Empty;
        }

        private void InputFormater()
        {
            if (Game == null) return;

            if (string.IsNullOrWhiteSpace(Game.Name))
            {
                var original = Data.LoadGames().FirstOrDefault(g => g.Id == Game.Id);
                Game.Name = original?.Name ?? "Unknown Game";
            }

            if (!string.IsNullOrWhiteSpace(Game.ExeName))
            {
                string trimmed = Game.ExeName.Trim();
                if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    Game.ExeName = trimmed[..^4].Trim();
                }
            }

            Game.SaveFolderPath = Game.SaveFolderPath?.Trim('"').Trim() ?? string.Empty;
            Game.BackupFolderPath = Game.BackupFolderPath?.Trim('"').Trim() ?? string.Empty;
            Game.ImagePath = Game.ImagePath?.Trim('"').Trim() ?? string.Empty;
        }

        [RelayCommand]
        private void Save()
        {
            try
            {
                var games = Data.LoadGames();
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

                    InputFormater();
                    games[index] = Game;
                    Data.SaveGamesData(games);
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
                    var games = Data.LoadGames();
                    var gameToDisable = games.FirstOrDefault(g => g.Id == Game.Id);
                    if (gameToDisable != null)
                    {
                        gameToDisable.IsDisabled = true;
                        Data.SaveGamesData(games);
                        StatusMessage = $"{gameToDisable.Name} is Disabled!";
                        RequestClose?.Invoke(true);
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
            var games = Data.LoadGames();
            var originalGame = games.FirstOrDefault(g => g.Id == Game.Id);
            if (originalGame != null)
            {
                Game = originalGame;
                BackupFolderPath = Game.BackupFolderPath;
                LoadPreviewImage(Game.ImagePath);
                ReloadGameFromConfig();
            }
            IsReadOnly = true;
            StatusMessage = string.Empty;
        }

        [RelayCommand]
        private void Close() => RequestClose?.Invoke(false);

        private void LoadPreviewImage(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) { PreviewImage = null; return; }
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(url, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                PreviewImage = bitmap;
            }
            catch { PreviewImage = null; }
        }

        partial void OnGameChanged(Game value)
        {
            LoadPreviewImage(value?.ImagePath);
        }

        private void ReloadGameFromConfig()
        {
            var games = Data.LoadGames();
            var reloaded = games.FirstOrDefault(g => g.Id == Game.Id);
            if (reloaded != null)
            {
                Game = reloaded;
                BackupFolderPath = Game.BackupFolderPath;
                LoadPreviewImage(Game.ImagePath);

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
                LoadPreviewImage(Game.ImagePath);
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

        // Dùng cho view mode hiển thị
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