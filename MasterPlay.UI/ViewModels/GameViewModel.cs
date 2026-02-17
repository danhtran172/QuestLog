using CommunityToolkit.Mvvm.ComponentModel;
using MasterPlay.Helpers;
using MasterPlay.Models;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Windows;
using System.Linq;


namespace MasterPlay.UI.ViewModels
{
    public partial class GameViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayPlayTime))]
        private Game _game;

        [ObservableProperty]
        private bool hasPlayableExe;
        public bool ShowFolderWarning => !checkFolderWarning();
        public bool IsGameRunning =>  CheckRunningGame();
        public string DisplayPlayTime
        {
            get
            {
                var ts = TimeSpan.FromSeconds(Game.TotalPlayTimeSeconds);
                return $"{(int)ts.TotalHours:D2}h{ts.Minutes:D2}m";
            }
        }

        public string getProcessPath()
        {
            var available = Data.LoadAvailableGames();
            try
            {               
                var processName = Game.ExeName + ".exe";

                string fullPath = available.FirstOrDefault(a =>
                    string.Equals(a.ExeName, processName, StringComparison.OrdinalIgnoreCase))?.FullPath;
                return fullPath;
            }
            catch
            {
                return string.Empty;
            }
        }

        public bool checkFolderWarning()
        {
            return FileHelper.FileCheck(Game.SaveFolderPath) && FileHelper.FileCheck(Game.BackupFolderPath);
        }
        public GameViewModel(Game game)
        {
            Game = game;
            hasPlayableExe = game.Ready;
        }


        public bool CheckRunningGame()
            
            {
                if (Game.Ready)
                {
                    var processName = Game?.ExeName;
                    // Loại bỏ đuôi .exe nếu người dùng lỡ nhập vào
                    if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        processName = processName.Substring(0, processName.Length - 4);
                    }

                    Process[] processes = Process.GetProcessesByName(processName);
                    return processes.Length > 0;
                }
                return false;
            }

}
}