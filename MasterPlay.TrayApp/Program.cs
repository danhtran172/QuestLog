using MasterPlay.TrayApp;
using MasterPlay.TrayApp.Logging;
using MasterPlay.Core.Localization;
using MasterPlay.Models;
using MasterPlay.TrayApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace MasterPlay.TrayApp
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {

            LoggerConfig.Configure();

            ApplicationConfiguration.Initialize();
            LocalizationService.SetLanguage(JsonService.LoadConfig().Language);

            var services = new ServiceCollection();
            ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();







            Application.Run(new TrayApplicationContext(serviceProvider));
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                builder.ClearProviders();



                builder.AddSerilog(Log.Logger, dispose: true);
            });

            services.AddSingleton<PlayTimeService>();
            services.AddSingleton<LibraryService>();
            services.AddSingleton<BackupService>();
        }
    }
}
