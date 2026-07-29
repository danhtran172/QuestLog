using MasterPlay.Core.Localization;
using MasterPlay.Infrastructure.Configuration;
using System.Windows;

namespace MasterPlay.UI
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            LocalizationService.LanguageChanged += OnLanguageChanged;
            LocalizationService.SetLanguage(JsonService.LoadConfig().Language);
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LocalizationService.LanguageChanged -= OnLanguageChanged;
            base.OnExit(e);
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            foreach (var entry in LocalizationService.CurrentStrings)
                Resources[entry.Key] = entry.Value;
        }
    }
}
