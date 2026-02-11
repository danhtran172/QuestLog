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

        public event Action<bool>? RequestClose;

        public EditGameViewModel(Game selectedGame)
        {
            Game = selectedGame ?? new Game();
            LoadPreviewImage(Game.ImagePath);
        }

        // --- LOGIC XỬ LÝ THƯ MỤC ---
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
        private void OpenSaveFolder() => HandleFolderOpening((Game.SaveFilePath, "Save Folder"));

        [RelayCommand]
        private void OpenBackupFolder() => HandleFolderOpening((Game.BackupFolderPath, "Backup Folder"));

        // --- LOGIC CHỈNH SỬA & VALIDATE ---
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
            //MessageBox.Show("Formated called");

            // 1. Xử lý Tên Game: Nếu trống thì lấy từ Config cũ
            if (string.IsNullOrWhiteSpace(Game.Name))
            {
                var original = ConfigManager.LoadGames().FirstOrDefault(g => g.Id == Game.Id);
                Game.Name = original?.Name ?? "Unknown Game";
            }

            // 2. ExeName: Tự xóa đuôi .exe
            if (!string.IsNullOrWhiteSpace(Game.ExeName))
            {
                string trimmed = Game.ExeName.Trim();
                if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    Game.ExeName = trimmed[..^4].Trim();
                }
            }

            // 3. Clean Paths: Xóa dấu ngoặc kép và khoảng trắng thừa
            Game.SaveFilePath = Game.SaveFilePath?.Trim('"').Trim() ?? string.Empty;
            Game.BackupFolderPath = Game.BackupFolderPath?.Trim('"').Trim() ?? string.Empty;
            Game.ImagePath = Game.ImagePath?.Trim('"').Trim() ?? string.Empty;
        }

        [RelayCommand]
        private void Save()
        {

            try
            {
                var games = ConfigManager.LoadGames();
                int index = games.FindIndex(g => g.Id == Game.Id);

                if (index >= 0)
                {
                    // Xử lý tạo thư mục backup và file info
                    if (!string.IsNullOrWhiteSpace(Game.BackupFolderPath))
                    {
                        if (!Directory.Exists(Game.BackupFolderPath))
                        {
                            Directory.CreateDirectory(Game.BackupFolderPath);
                        }

                        string infoFilePath = Path.Combine(Game.BackupFolderPath, "save_location.txt");
                        File.WriteAllText(infoFilePath, Game.SaveFilePath);
                    }


                    InputFormater();
                    games[index] = Game;
                    ConfigManager.SaveGamesData(games);

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
            var result = MessageBox.Show($"Are you sure to Disable '{Game.Name}'?\n(Game will be hide but stil remaining data.)",
                "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var games = ConfigManager.LoadGames();
                    var gameToDisable = games.FirstOrDefault(g => g.Id == Game.Id);

                    if (gameToDisable != null)
                    {
                        gameToDisable.IsDisabled = true;
                        ConfigManager.SaveGamesData(games);  // Lưu thay đổi

                        StatusMessage = gameToDisable.Name + " is Disabled!";
                        RequestClose?.Invoke(true);  // Đóng window và refresh list ở Main
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
            var games = ConfigManager.LoadGames();
            var originalGame = games.FirstOrDefault(g => g.Id == Game.Id);
            if (originalGame != null)
            {
                Game = originalGame;
                BackupFolderPath = Game.BackupFolderPath;
                LoadPreviewImage(Game.ImagePath);
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
            var games = ConfigManager.LoadGames();
            var reloaded = games.FirstOrDefault(g => g.Id == Game.Id);
            if (reloaded != null)
            {
                Game = reloaded;  // Gán object mới từ JSON
                BackupFolderPath = Game.BackupFolderPath;
                LoadPreviewImage(Game.ImagePath);
            }
        }
    }
}