using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MasterPlay.Service.Workers;
using MasterPlay.Service.Services;

namespace MasterPlay.Service
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureServices(services =>
                {
                    services.AddHostedService<MasterPlayWorker>();
                    services.AddHostedService<BackupService>();
                    services.AddHostedService<LibraryService>();
                });
    }
}