using MasterPlay.Models;
using MasterPlay.UI.ViewModels;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace MasterPlay.UI
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<GameViewModel> Games { get; } = new ObservableCollection<GameViewModel>();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this; // Bind ItemsSource="{Binding Games}" với property Games
            LoadGamesFromConfig();
        }

        private void LoadGamesFromConfig()
        {
            Games.Clear();
            var allGames = ConfigManager.GetAllGames();
            var activeGames = allGames.Where(g => !g.IsDisabled).ToList();

            foreach (var game in activeGames)
            {
                var vm = new GameViewModel(game);
                vm.CheckPlayableGames();
                Games.Add(vm);
            }

            if (activeGames.Count == 0 && allGames.Count > 0)
            {
                MessageBox.Show("There are no Actived Game.\nAll Games are Disabled.",
                                "Notification", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (activeGames.Count == 0)
            {
                MessageBox.Show("There are no Game added.",
                                "Welcome to MasterPlay", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void AddGameButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var addWindow = new AddGameWindow();
                addWindow.Owner = this;
                addWindow.ShowDialog();
                // nếu thành công thì refresh
                LoadGamesFromConfig();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Add window: {ex.Message}\n\n{ex.StackTrace}", "Crash Debug", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
            LoadGamesFromConfig(); // Optional refresh
        }

        private void EditGame_Click(object sender, RoutedEventArgs e)
        {
            // Ép kiểu tường minh để tránh xung đột namespace với WinForms
            if (sender is System.Windows.Controls.Button btn &&
                btn.DataContext is MasterPlay.UI.ViewModels.GameViewModel gameVm)
            {
                try
                {
                    // Kiểm tra dữ liệu game trước khi mở window
                    if (gameVm.Game == null) return;

                    // Khởi tạo và truyền tham số trực tiếp vào Constructor
                    var editWindow = new MasterPlay.UI.EditGameWindow(gameVm.Game);
                    editWindow.Owner = this;

                    // Hiển thị cửa sổ và cập nhật lại danh sách sau khi đóng
                    if (editWindow.ShowDialog() == true)
                    {
                        LoadGamesFromConfig();
                    }
                }
                catch (Exception ex)
                {
                    // Chỉ hiện lỗi khi hệ thống gặp sự cố nghiêm trọng (như lỗi InitializeComponent)
                    MessageBox.Show($"Lỗi hệ thống khi mở chỉnh sửa: {ex.Message}", "Notification");
                }
            }
        }

        private void PlayGame_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is GameViewModel vm)
            {
                // TODO: Launch game
                MessageBox.Show($"Launch game: {vm.Game.Name} ({vm.Game.ExeName}.exe)");
            }
        }
    }
}