using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace MasterPlay.Shell;

/// <summary>
/// Hai che do chay cua vo desktop:
///
///   Dev      run-web.cmd mo Host :5099 va Vite :5173 roi moi mo Shell. Shell chi tro
///            WebView2 vao QUESTORY_URL (mac dinh Vite), khong tu khoi dong gi.
///   Ban cai  Thu muc cai co host\MasterPlay.Host.exe (Host phuc vu luon giao dien
///            da build) va tray\MasterPlay.TrayApp.exe. Shell tu khoi dong ca hai,
///            Host song chet theo Shell, tray chay tiep sau khi Shell dong.
/// </summary>
internal static class DesktopRuntime
{
    // Cong co dinh: WebView2 luu phien dang nhap theo origin, doi cong moi lan chay
    // la mat dang nhap. Tranh 5099/5173 de ban cai va ban dev chay song song duoc.
    private const int PackagedPort = 47615;
    private const string SingleInstanceMutexName = "QuestLogDesktopSingleInstance";
    private const string SessionTokenVariable = "QUESTLOG_DESKTOP_SESSION_TOKEN";

    private static readonly string HostExecutable =
        Path.Combine(AppContext.BaseDirectory, "host", "MasterPlay.Host.exe");

    private static Mutex? _singleInstance;
    private static IntPtr _hostJob;
    private static AppUpdater? _updater;

    /// <summary>QUESTORY_URL dat tay thi luon la dev, ke ca khi chay tu thu muc cai.</summary>
    public static bool IsPackaged { get; } =
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("QUESTORY_URL")) &&
        File.Exists(HostExecutable);

    public static string AppUrl { get; } = IsPackaged
        ? $"http://127.0.0.1:{PackagedPort}/"
        : Environment.GetEnvironmentVariable("QUESTORY_URL") ?? "http://localhost:5173/";

    public static Process? HostProcess { get; private set; }

    public static AppUpdater Updater => _updater ??= new AppUpdater();

    public static bool TryAcquireSingleInstance()
    {
        _singleInstance = new Mutex(true, SingleInstanceMutexName, out var isOnlyInstance);
        return isOnlyInstance;
    }

    /// <summary>Mo lan thu hai (bam shortcut khi app dang chay): dua cua so cu len truoc.</summary>
    public static void ActivateRunningInstance()
    {
        foreach (var process in Process.GetProcessesByName("QuestLog"))
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId || process.MainWindowHandle == IntPtr.Zero) continue;
                ShowWindow(process.MainWindowHandle, SwRestore);
                SetForegroundWindow(process.MainWindowHandle);
                return;
            }
        }
    }

    /// <summary>Tat cac cua so QuestLog chay tu cung thu muc nay; Host chet theo (job object).</summary>
    public static void StopOtherInstances()
    {
        var self = Environment.ProcessPath;
        foreach (var process in Process.GetProcessesByName("QuestLog"))
        {
            using (process)
            {
                try
                {
                    if (process.Id == Environment.ProcessId ||
                        !string.Equals(process.MainModule?.FileName, self, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    process.Kill();
                    process.WaitForExit(5000);
                }
                catch (Exception ex)
                {
                    ShellLog.Write($"Khong tat duoc QuestLog {process.Id} khi go cai dat: {ex.Message}");
                }
            }
        }
    }

    public static void StartLocalServices()
    {
        // Token moi moi lan chay, chi Shell (tiem vao WebView2) va Host biet. Dat vao
        // bien moi truong cua chinh Shell: MainWindow doc no y nhu khi chay qua run-web.cmd.
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(SessionTokenVariable, token);

        StartHost();
        TrayProcess.EnsureRunning();
    }

    private static void StartHost()
    {
        var startInfo = new ProcessStartInfo(HostExecutable)
        {
            WorkingDirectory = Path.GetDirectoryName(HostExecutable)!,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add($"http://127.0.0.1:{PackagedPort}");

        try
        {
            HostProcess = Process.Start(startInfo);
            if (HostProcess != null) BindToShellLifetime(HostProcess);
        }
        catch (Exception ex)
        {
            ShellLog.Write($"Khong khoi dong duoc Host: {ex}");
        }
    }

    /// <summary>
    /// Job object KILL_ON_JOB_CLOSE: Shell thoat (ke ca bi crash/kill) thi Windows
    /// dong handle job va giet Host, khong de Host mo coi giu cong. SILENT_BREAKAWAY
    /// de game do Host khoi chay KHONG nam trong job — dong QuestLog khong duoc tat game.
    /// </summary>
    private static void BindToShellLifetime(Process process)
    {
        _hostJob = CreateJobObject(IntPtr.Zero, null);
        if (_hostJob == IntPtr.Zero)
        {
            ShellLog.Write($"CreateJobObject that bai: {Marshal.GetLastWin32Error()}");
            return;
        }

        var info = new JobObjectExtendedLimitInformation
        {
            BasicLimitInformation = new JobObjectBasicLimitInformation
            {
                LimitFlags = JobObjectLimitKillOnJobClose | JobObjectLimitSilentBreakawayOk,
            },
        };
        var length = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
        var buffer = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(info, buffer, fDeleteOld: false);
            if (!SetInformationJobObject(_hostJob, JobObjectExtendedLimitInformationClass, buffer, (uint)length) ||
                !AssignProcessToJobObject(_hostJob, process.Handle))
            {
                ShellLog.Write($"Khong gan duoc Host vao job: {Marshal.GetLastWin32Error()}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private const int SwRestore = 9;
    private const int JobObjectExtendedLimitInformationClass = 9;
    private const uint JobObjectLimitSilentBreakawayOk = 0x00001000;
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }
}
