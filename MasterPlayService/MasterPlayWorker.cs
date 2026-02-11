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

        public MasterPlayWorker(IHostedService backupService)
        {
            _backupService = backupService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
            Console.WriteLine("MasterPlay Main Worker started");

            // Khởi động BackupService (nó sẽ tự chạy timer)
            if (_backupService is BackgroundService bgService)
            {
                await bgService.StartAsync(stoppingToken);
            }

            // Giữ worker chính chạy (có thể thêm track playtime sau)
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_backupService is BackgroundService bgService)
            {
                await bgService.StopAsync(cancellationToken);
            }
            await base.StopAsync(cancellationToken);
        }
    }
}