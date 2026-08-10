using MasterPlay.Core.Localization;
using MasterPlay.Infrastructure.Configuration;
using MasterPlay.UI.Services;
using MasterPlay.UI.Services.Input;
using System.Windows;

namespace MasterPlay.UI
{
    public partial class App : System.Windows.Application
    {
        private ControllerNavigationService? _controllerNavigationService;

        protected override void OnStartup(StartupEventArgs e)
        {
            var config = JsonService.LoadConfig();
            LocalizationService.LanguageChanged += OnLanguageChanged;
            LocalizationService.SetLanguage(config.Language);
            ThemeService.Apply(config.isDarkMode);
            base.OnStartup(e);

            _controllerNavigationService = new ControllerNavigationService();
            _controllerNavigationService.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LocalizationService.LanguageChanged -= OnLanguageChanged;
            _controllerNavigationService?.Dispose();
            base.OnExit(e);
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            foreach (var entry in LocalizationService.CurrentStrings)
                Resources[entry.Key] = entry.Value;
        }
    }
}
