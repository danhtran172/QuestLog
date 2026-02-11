using CommunityToolkit.Mvvm.ComponentModel;
using MasterPlay.Models;
using System;

namespace MasterPlay.UI.ViewModels
{
    public partial class GameViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayPlayTime))]
        private Game _game;

        public string DisplayPlayTime
        {
            get
            {
                var ts = TimeSpan.FromSeconds(Game.TotalPlayTimeSeconds);
                return $"{ts.Hours:D2}h{ts.Minutes:D2}m";
            }
        }

        public GameViewModel(Game game)
        {
            Game = game;
        }
    }
}