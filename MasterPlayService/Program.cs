using MasterPlay.Service.Services;
using MasterPlay.Service.Workers;
using Serilog;

namespace MasterPlay.Service
{
    public class Program
    {
        public static async Task Main(string[] args)
        {

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string logPath = Path.Combine(baseDir, "logs", "masterplay_log.txt");
            Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Console()
                    .WriteTo.File(logPath,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7)
                    .CreateLogger();
            try
            {
                Log.Information("===== Starting MasterPlay Service Host =====");
                await CreateHostBuilder(args).Build().RunAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .UseWindowsService(options => { options.ServiceName = "MasterPlayService"; })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton<LibraryService>();
                    services.AddSingleton<BackupService>();
                    services.AddSingleton<PlayTimeService>();

                    services.AddHostedService<MasterPlayWorker>();
                });
    }
}