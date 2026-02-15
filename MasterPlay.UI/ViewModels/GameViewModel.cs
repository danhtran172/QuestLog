using CommunityToolkit.Mvvm.ComponentModel;
using MasterPlay.Helpers;
using MasterPlay.Models;
using Newtonsoft.Json;
using System;
using System.Windows;

namespace MasterPlay.UI.ViewModels
{
    public partial class GameViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayPlayTime))]
        private Game _game;

        [ObservableProperty]
        private bool hasPlayableExe;
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

        public GameViewModel(Game game)
        {
            Game = game;
            hasPlayableExe = game.Ready;
        }
    }
}