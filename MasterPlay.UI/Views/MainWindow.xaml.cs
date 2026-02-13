using MasterPlay.Helpers;
using MasterPlay.Models;
using MasterPlay.UI.ViewModels;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MasterPlay.UI
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<GameViewModel> Games { get; } = new ObservableCollection<GameViewModel>();
        public ICommand RefreshCommand { get; set; }
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            RefreshCommand = new RelayCommand(LoadGamesData);

            LoadGamesData();
        }

        private void LoadGamesData()
        {
            Games.Clear();
            Data.SortGames();
            Data.SyncReadyGames();
            var activeGames = Data.LoadGames().Where(g => !g.IsDisabled).ToList();

            foreach (var game in activeGames)
            {
                var vm = new GameViewModel(game);
                Games.Add(vm);
            }

            if (activeGames.Count == 0)
            {
                MessageBox.Show("There are no Actived Game.",
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
                LoadGamesData();
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
            LoadGamesData(); // Optional refresh
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
                        LoadGamesData();
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
                //MessageBox.Show($"Launch game: {vm.Game.Name} ({vm.Game.ExeName}.exe)");
                FileHelper.RunProcess(vm.getProcessPath());
            }
        }
    }
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _execute();
        public event EventHandler CanExecuteChanged;
    }
}