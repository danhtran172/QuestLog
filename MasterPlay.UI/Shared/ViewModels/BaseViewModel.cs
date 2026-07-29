using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows.Threading;

namespace MasterPlay.UI.Shared.ViewModels
{
    public abstract partial class BaseViewModel : ObservableObject
    {

        [ObservableProperty]
        private string statusMessage = string.Empty;


        public event Action<bool>? RequestClose;


        protected void RaiseRequestClose(bool success)
        {
            RequestClose?.Invoke(success);
        }


        private readonly DispatcherTimer _statusTimer;


        protected BaseViewModel()
        {
            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _statusTimer.Tick += (s, e) =>
            {
                StatusMessage = string.Empty;
                _statusTimer.Stop();
            };
        }


        partial void OnStatusMessageChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _statusTimer.Stop();
                _statusTimer.Start();
            }
        }
    }
}