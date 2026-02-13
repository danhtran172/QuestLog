using MasterPlay.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MasterPlay.Service.Services
{
    public class PlayTimeService
    {
        private readonly ILogger<PlayTimeService> _logger;
        private System.Timers.Timer? _timer;

        // --- Win32 API để check Active Window và Input ---
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        public PlayTimeService(ILogger<PlayTimeService> logger)
        {
            _logger = logger;
        }

        public void Start()
        {
            _logger.LogInformation("PlayTime Service started (1 min interval).");
            _timer = new System.Timers.Timer(60000); // 1 phút
            _timer.Elapsed += (s, e) => TrackPlayTime();
            _timer.AutoReset = true;
            _timer.Start();
        }

        private void TrackPlayTime()
        {
            try
            {
                var games = ConfigManager.LoadGames();
                bool dataChanged = false;

                foreach (var game in games)
                {
                    if (game.IsDisabled || string.IsNullOrEmpty(game.ExeName)) continue;

                    // 1. Check Process đang chạy
                    string processName = game.ExeName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
                    var processes = Process.GetProcessesByName(processName);
                    if (processes.Length == 0) continue;

                    // 2. Check Game Window có đang được Focus (Foreground) không
                    bool isForeground = false;
                    IntPtr fgWindow = GetForegroundWindow();
                    GetWindowThreadProcessId(fgWindow, out uint fgPid);
                    if (processes.Any(p => p.Id == fgPid)) isForeground = true;

                    // 3. Check Input (Tay cầm/Chuột/Phím) trong 1 phút qua
                    bool hadInput = GetIdleTime() < 60000; // Idle dưới 60s nghĩa là có hoạt động

                    if (isForeground && hadInput)
                    {
                        game.TotalPlayTimeSeconds += 60;
                        dataChanged = true;
                        _logger.LogInformation("Playtime +1m for {name}. Total: {time}s", game.Name, game.TotalPlayTimeSeconds);
                    }
                }

                if (dataChanged)
                {
                    ConfigManager.SaveGamesData(games);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking playtime.");
            }
        }

        // Hàm tính thời gian máy tính không nhận được bất kỳ Input nào (ms)
        private uint GetIdleTime()
        {
            LASTINPUTINFO lastInput = new LASTINPUTINFO();
            lastInput.cbSize = (uint)Marshal.SizeOf(lastInput);
            GetLastInputInfo(ref lastInput);
            return (uint)Environment.TickCount - lastInput.dwTime;
        }

        public void Stop()
        {
            _timer?.Stop();
        }
    }
}