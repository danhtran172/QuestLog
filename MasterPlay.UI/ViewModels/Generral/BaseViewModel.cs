using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows.Threading;

namespace MasterPlay.UI.ViewModels
{
    public abstract partial class BaseViewModel : ObservableObject
    {
        // --- Properties ---
        [ObservableProperty]
        private string statusMessage = string.Empty;

        // --- Events ---
        public event Action<bool>? RequestClose;

        // --- Protected method để các class con gọi ---
        protected void RaiseRequestClose(bool success)
        {
            RequestClose?.Invoke(success);
        }

        // --- Private Fields ---
        private readonly DispatcherTimer _statusTimer;

        // --- Constructors and Initialization ---
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

        // --- Partial Methods: Property Changed Handlers ---
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