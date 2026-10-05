using MasterPlay.TrayApp;
using MasterPlay.TrayApp.Logging;
using MasterPlay.Core.Localization;
using MasterPlay.Core.Metadata;
using MasterPlay.Infrastructure.Configuration;
using MasterPlay.TrayApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace MasterPlay.TrayApp
{
    internal static class Program
    {
        // Ten khong co tien to namespace -> mutex nam trong phien dang nhap hien tai.
        private const string SingleInstanceMutexName = "QuestLogTraySingleInstance";

        [STAThread]
        static void Main()
        {
            LoggerConfig.Configure();
            RegisterGlobalExceptionHandlers();

            // Hai tray cung chay la dem gio hai lan va sao luu chong nhau tren cung
            // mot CSDL SQLite. Log cu cho thay tray tung duoc mo lai nhieu lan trong
            // ngay, nen chan o day.
            using var singleInstance = new Mutex(true, SingleInstanceMutexName, out var isOnlyInstance);
            if (!isOnlyInstance)
            {
                Log.Warning("Another QuestLog tray instance is already running; exiting.");
                Log.CloseAndFlush();
                return;
            }

            try
            {
                ApplicationConfiguration.Initialize();

                try
                {
                    LocalizationService.SetLanguage(JsonService.LoadConfig().Language);
                }
                catch (Exception ex)
                {
                    // config.json hong khong duoc chan tray khoi dong: chay tiep bang
                    // ngon ngu mac dinh.
                    Log.Warning(ex, "Could not read the language from config; using the default.");
                }

                var services = new ServiceCollection();
                ConfigureServices(services);
                using var serviceProvider = services.BuildServiceProvider();

                Application.Run(new TrayApplicationContext(serviceProvider));
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "The tray application terminated unexpectedly.");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        // Tray khong co cua so: mot ngoai le khong bat lam tien trinh bien mat khong
        // dong log nao, nguoi dung chi thay icon tray tu nhien mat.
        private static void RegisterGlobalExceptionHandlers()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.ThreadException += (_, args) =>
                Log.Error(args.Exception, "Unhandled exception on the UI thread.");

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                Log.Fatal(args.ExceptionObject as Exception, "Unhandled exception.");
                Log.CloseAndFlush();
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                Log.Error(args.Exception, "Unobserved task exception.");
                args.SetObserved();
            };
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(Log.Logger, dispose: true);
            });

            services.AddSingleton<PlayTimeService>();
            services.AddSingleton<IGameMetadataResolver, NullGameMetadataResolver>();
            services.AddSingleton<LibraryService>();
        }
    }
}
