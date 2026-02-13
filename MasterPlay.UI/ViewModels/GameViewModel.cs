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

        //public void CheckPlayableGames()
        //{
        //    if (Game == null || string.IsNullOrWhiteSpace(Game.ExeName))
        //    {
        //        hasPlayableExe = false;
        //        return;
        //    }

        //    try
        //    {
        //        var available = ConfigManager.LoadAvailableGames();

        //        var processName = Game.ExeName + ".exe";
        //        var able = available.Any(a =>
        //            string.Equals(a.ExeName, processName, StringComparison.OrdinalIgnoreCase));
        //        string fullPath = available.FirstOrDefault(a =>
        //            string.Equals(a.ExeName, processName, StringComparison.OrdinalIgnoreCase))?.FullPath;


        //        hasPlayableExe = able && FileHelper.FileCheck(fullPath);
        //    }
        //    catch
        //    {
        //        hasPlayableExe = false;
        //    }
        //}

        public string getProcessPath()
        {
            try
            {
                var available = ConfigManager.LoadAvailableGames();
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