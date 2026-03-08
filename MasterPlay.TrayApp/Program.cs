using MasterPlay.TrayApp;
using MasterPlay.TrayApp.Logging;
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
            // Cấu hình Serilog
            LoggerConfig.Configure();

            ApplicationConfiguration.Initialize();

            var services = new ServiceCollection();
            ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();

            // Gắn Serilog vào ILoggerFactory
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            loggerFactory.AddSerilog(Log.Logger);

            Application.Run(new TrayApplicationContext(serviceProvider));
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog();
            });

            services.AddSingleton<PlayTimeService>();
            services.AddSingleton<LibraryService>();
            services.AddSingleton<BackupService>();
        }
    }
}