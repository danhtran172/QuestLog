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
            _logger.LogInformation("PLAYTIME SERVICE STARTED");
            _timer = new System.Timers.Timer(30000);
            _timer.Elapsed += (s, e) => TrackPlayTime();
            _timer.AutoReset = true;
            _timer.Start();
        }

        private void TrackPlayTime()
        {
            try
            {
                DataService.SyncReadyGames();

                var games = DataService.LoadGames().ToList();
                //var games = DataHelper.LoadGames().Where(x => x.Ready).ToList();
                bool dataChanged = false;

                foreach (var game in games)
                {
                    if (game.IsDisabled || string.IsNullOrEmpty(game.ExeName) || !game.Ready) continue;

                    string processName = game.ExeName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
                    var processes = Process.GetProcessesByName(processName);
                    if (processes.Length == 0) continue;

                    if (processes.Length > 0) _logger.LogInformation(game.Name + " is running");


                    bool isForeground = false;
                    IntPtr fgWindow = GetForegroundWindow();
                    GetWindowThreadProcessId(fgWindow, out uint fgPid);
                    if (processes.Any(p => p.Id == fgPid)) isForeground = true;

                    bool hadInput = GetIdleTime() < 30000;

                    if (isForeground && hadInput)
                    {
                        game.TotalPlayTimeSeconds += 30;
                        dataChanged = true;
                        _logger.LogInformation("Updated playtime for {name}, time: {time}s", game.Name, game.TotalPlayTimeSeconds);
                    }
                     if (!isForeground)
                    {
                        _logger.LogInformation("No foreground");

                    }
                     if (!hadInput)
                    {
                        _logger.LogInformation("No input");

                    }
                }

                if (dataChanged)
                {
                    DataService.SaveGamesData(games);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while tracking playtime.");
            }
        }

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
            _logger.LogInformation("PlayTime tracking stopped.");
        }
    }
}