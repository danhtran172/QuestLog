using MasterPlay.Service.Services;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;

namespace MasterPlay.Service.Workers
{
    public class MasterPlayWorker : BackgroundService
    {
        private readonly BackupService _backupService;
         private readonly LibraryService _library;
        private readonly PlayTimeService _playTime;
        private readonly ILogger _logger;
        public MasterPlayWorker(BackupService backupService, LibraryService library, PlayTimeService playTime, ILogger<MasterPlayWorker> logger)
        {
            _backupService = backupService;
            _library = library;
            _playTime = playTime;

            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MasterPlayWorker is starting...");

            _backupService.Start(stoppingToken);
            _library.Start(stoppingToken);
            _playTime.Start();

            _logger.LogInformation("MasterPlayWorker is now running in background.");

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("MasterPlayWorker is receiving Stop signal...");
            _backupService.Stop();
            _library.Stop();
            await base.StopAsync(cancellationToken);
            _logger.LogInformation("MasterPlayWorker has stopped.");
        }
    }
}