using System.Diagnostics;

namespace MasterPlay.UI.Services.Runtime;

public sealed class GameProcessMonitor : IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);
    private CancellationTokenSource? _cancellation;
    private Task? _worker;

    public event Action<IReadOnlySet<string>>? RunningProcessesChanged;

    public void Start()
    {
        if (_worker is { IsCompleted: false })
            return;

        _cancellation = new CancellationTokenSource();
        _worker = RunAsync(_cancellation.Token);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        await PublishSnapshotAsync(cancellationToken);
        using var timer = new PeriodicTimer(RefreshInterval);

        while (await timer.WaitForNextTickAsync(cancellationToken))
            await PublishSnapshotAsync(cancellationToken);
    }

    private async Task PublishSnapshotAsync(CancellationToken cancellationToken)
    {
        var processNames = await Task.Run(CaptureRunningProcessNames, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        RunningProcessesChanged?.Invoke(processNames);
    }

    private static IReadOnlySet<string> CaptureRunningProcessNames()
    {
        var processNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Process[] processes;

        try
        {
            processes = Process.GetProcesses();
        }
        catch
        {
            return processNames;
        }

        foreach (var process in processes)
        {
            try
            {
                processNames.Add(process.ProcessName);
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        return processNames;
    }

    public void Dispose()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
        _worker = null;
    }
}
