using MasterPlay.Service.Services;
using MasterPlay.Service.Workers;
using Serilog;

namespace MasterPlay.Service
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Cấu hình Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("logs/masterplay_log.txt", rollingInterval: RollingInterval.Day)
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
                .UseSerilog() // Thay thế logger mặc định bằng Serilog
                .UseWindowsService(options => { options.ServiceName = "MasterPlayService"; })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton<LibraryService>();
                    services.AddSingleton<BackupService>();
                    services.AddHostedService<MasterPlayWorker>();
                });
    }
}