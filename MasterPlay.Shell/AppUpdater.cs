using Velopack;
using Velopack.Sources;

namespace MasterPlay.Shell;

/// <summary>
/// Tu cap nhat qua Velopack. Ban phat hanh nam o repo PUBLIC rieng (chi chua file
/// cai dat, khong co ma nguon) nen app doc duoc ma khong can token.
///
/// Luong: kiem tra sau khi mo app va moi vai gio -> co ban moi thi tai ngam ->
/// bao giao dien (questlog.update-ready). Nguoi dung bam "Khoi dong lai" thi ap
/// dung ngay; bo qua thi lan mo app sau se ap dung truoc khi hien cua so.
/// </summary>
internal sealed class AppUpdater
{
    public const string ReleasesRepository = "https://github.com/danhtran172/questlog-releases";

    // Thu nghiem cap nhat bang thu muc releases tren may thay vi GitHub.
    private const string FeedOverrideVariable = "QUESTLOG_UPDATE_FEED";
    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan TrayShutdownTimeout = TimeSpan.FromSeconds(15);

    private readonly UpdateManager _manager;
    private VelopackAsset? _ready;

    public AppUpdater()
    {
        var feedOverride = Environment.GetEnvironmentVariable(FeedOverrideVariable);
        _manager = string.IsNullOrWhiteSpace(feedOverride)
            ? new UpdateManager(new GithubSource(ReleasesRepository, accessToken: null, prerelease: false))
            : new UpdateManager(feedOverride);
    }

    /// <summary>Phien ban da tai xong, cho khoi dong lai. Null neu chua co.</summary>
    public string? ReadyVersion => _ready?.Version.ToString();

    /// <summary>Goi tren luong nen; bao ve UI thread qua callback.</summary>
    public event Action<string>? UpdateReady;

    public void ApplyPendingOnStartup()
    {
        if (!_manager.IsInstalled) return;

        var pending = _manager.UpdatePendingRestart;
        if (pending == null) return;

        ShellLog.Write($"Ap dung ban {pending.Version} da tai o lan chay truoc.");
        Apply(pending);
    }

    public async Task RunAsync(CancellationToken cancellation)
    {
        if (!_manager.IsInstalled) return;

        await Task.Delay(FirstCheckDelay, cancellation);
        while (!cancellation.IsCancellationRequested)
        {
            await CheckAndDownloadAsync(cancellation);
            await Task.Delay(CheckInterval, cancellation);
        }
    }

    public void ApplyAndRestart()
    {
        if (_ready != null) Apply(_ready);
    }

    private async Task CheckAndDownloadAsync(CancellationToken cancellation)
    {
        try
        {
            var update = await _manager.CheckForUpdatesAsync();
            if (update == null) return;

            var target = update.TargetFullRelease;
            if (_ready != null && _ready.Version >= target.Version) return;

            await _manager.DownloadUpdatesAsync(update, null, cancellation);
            _ready = target;
            ShellLog.Write($"Da tai xong ban {target.Version}.");
            UpdateReady?.Invoke(target.Version.ToString());
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Mat mang / GitHub gioi han luot goi la binh thuong: khong lam phien
            // nguoi dung, chi ghi log va thu lai o lan kiem tra sau.
            ShellLog.Write($"Kiem tra cap nhat that bai: {ex.Message}");
        }
    }

    private void Apply(VelopackAsset release)
    {
        // Host tu chet theo Shell (job object). Tray thi phai bao thoat em truoc.
        TrayProcess.RequestShutdown(TrayShutdownTimeout);
        _manager.ApplyUpdatesAndRestart(release);
    }
}
