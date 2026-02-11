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
                return $"{ts.Hours:D2}h{ts.Minutes:D2}m";
            }
        }

        public void CheckPlayableExe()
        {
            if (Game == null || string.IsNullOrWhiteSpace(Game.ExeName))
            {
                
                hasPlayableExe = false;
                return;
            }

            try
            {
                var available = ConfigManager.LoadAvailableGames();
                

                var able = available.Any(a =>
                    string.Equals(a.ExeName, Game.ExeName, StringComparison.OrdinalIgnoreCase));
                string fullPath = available.FirstOrDefault(a =>
                    string.Equals(a.ExeName, Game.ExeName, StringComparison.OrdinalIgnoreCase))?.FullPath;

                
                hasPlayableExe = able && FileHelper.FileCheck(fullPath);
                }
            catch
            {
                hasPlayableExe = false;
            }
        }

        public GameViewModel(Game game)
        {
            Game = game;
        }

    }
}