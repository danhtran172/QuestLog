using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace MasterPlay.Service.Workers
{
    public class MasterPlayWorker : BackgroundService
    {
        private readonly IHostedService _backupService;
        private readonly IHostedService _libraryService;

        public MasterPlayWorker(
            IHostedService backupService,
            IHostedService libraryService)  // Inject thêm service con
        {
            _backupService = backupService;
            _libraryService = libraryService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
            Console.WriteLine("MasterPlay Main Worker started");

            // Khởi động BackupService
            if (_backupService is BackgroundService bgService)
            {
                await bgService.StartAsync(stoppingToken);
            }

            // Khởi động GameLibraryScannerService
            if (_libraryService is BackgroundService gameLibService)
            {
                await gameLibService.StartAsync(stoppingToken);
            }

            // Giữ worker chính chạy (có thể thêm track playtime sau)
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            // Stop BackupService
            if (_backupService is BackgroundService bgService)
            {
                await bgService.StopAsync(cancellationToken);
            }

            // Stop GameLibraryScannerService
            if (_libraryService is BackgroundService gameLibService)
            {
                await gameLibService.StopAsync(cancellationToken);
            }

            await base.StopAsync(cancellationToken);
        }
    }
}