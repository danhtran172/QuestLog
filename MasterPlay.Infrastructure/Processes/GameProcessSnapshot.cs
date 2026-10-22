using System.Diagnostics;
using System.Linq.Expressions;
using MasterPlay.Core.Entities;

namespace MasterPlay.Infrastructure.Processes;

/// <summary>
/// Game nao duoc theo doi tien trinh. Dung chung cho PlayTimeService (Tray) va
/// GameProcessMonitor (Host) — doi o day la doi ca hai.
/// </summary>
public static class GameProcessWatch
{
    /// <summary>
    /// Co ExeName, hoac co launch target la .exe. KHONG doi `Ready`: game chay tu
    /// launch target hay ngoai thu muc da quet van la game dang chay va van phai duoc dem gio.
    /// </summary>
    public static readonly Expression<Func<Game, bool>> Watchable = g =>
        !g.IsDisabled && !g.IsDlc &&
        ((g.ExeName != null && g.ExeName != "") ||
         (g.LaunchTargetKind == "exe" && g.LaunchTargetPath != null && g.LaunchTargetPath != ""));
}

/// <summary>
/// Anh chup cac tien trinh dang chay, va quy tac DUY NHAT khop chung voi mot game.
/// Dung chung cho PlayTimeService (Tray, dem gio choi) va GameProcessMonitor (Host,
/// field `isRunning`), nen nut "Playing" tren UI va gio choi luon nhan cung mot game.
/// </summary>
public sealed class GameProcessSnapshot
{
    private static readonly IReadOnlySet<int> NoProcesses = new HashSet<int>();

    private readonly Dictionary<string, HashSet<int>> _processIds = new(StringComparer.OrdinalIgnoreCase);

    public GameProcessSnapshot(IEnumerable<(string Name, int Id)> processes)
    {
        foreach (var (name, id) in processes)
        {
            var key = NormalizeProcessName(name);
            if (key.Length == 0) continue;
            if (!_processIds.TryGetValue(key, out var ids))
                _processIds[key] = ids = [];
            ids.Add(id);
        }
    }

    /// <summary>Mot lan Process.GetProcesses cho ca tick, thay vi goi GetProcessesByName tung game.</summary>
    public static GameProcessSnapshot Capture()
    {
        var processes = Process.GetProcesses();
        try
        {
            var entries = new List<(string Name, int Id)>(processes.Length);
            foreach (var process in processes)
            {
                // Tien trinh vua thoat giua chung thi bo qua, khong lam hong ca lan chup.
                try { entries.Add((process.ProcessName, process.Id)); }
                catch (InvalidOperationException) { }
            }
            return new GameProcessSnapshot(entries);
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }

    /// <summary>PID cua moi tien trinh khop ExeName; rong neu game khong chay.</summary>
    public IReadOnlySet<int> ProcessIdsFor(string? exeName)
    {
        var key = NormalizeProcessName(exeName);
        return key.Length > 0 && _processIds.TryGetValue(key, out var ids) ? ids : NoProcesses;
    }

    public bool IsRunning(string? exeName) => ProcessIdsFor(exeName).Count > 0;

    /// <summary>
    /// PID cua mot game: tien trinh trung ExeName, CONG tien trinh trung file .exe cua launch
    /// target. Launcher kieu "Play X.exe" co the chay game ngay trong tien trinh cua no, ten
    /// khac ExeName; khong tinh ca file nay thi bam Play xong UI van tuong game chua chay.
    /// Shortcut (.lnk) khong tu ra ten tien trinh duoc nen chi dua vao ExeName.
    /// </summary>
    public IReadOnlySet<int> ProcessIdsFor(string? exeName, string? launchTargetKind, string? launchTargetPath)
    {
        var byExeName = ProcessIdsFor(exeName);
        var byLaunchTarget = string.Equals(launchTargetKind, "exe", StringComparison.OrdinalIgnoreCase)
            ? ProcessIdsFor(launchTargetPath)
            : NoProcesses;

        if (byLaunchTarget.Count == 0) return byExeName;
        if (byExeName.Count == 0) return byLaunchTarget;
        return byExeName.Union(byLaunchTarget).ToHashSet();
    }

    public IReadOnlySet<int> ProcessIdsFor(Game game) =>
        ProcessIdsFor(game.ExeName, game.LaunchTargetKind, game.LaunchTargetPath);

    /// <summary>
    /// Ten tien trinh tu ExeName: bo thu muc va duoi ".exe", khong phan biet hoa thuong.
    /// Chi cat ".exe" — "Game.v2.exe" la tien trinh "Game.v2", khong phai "Game".
    /// </summary>
    public static string NormalizeProcessName(string? executableName)
    {
        var name = Path.GetFileName(executableName?.Trim() ?? string.Empty);
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }
}
