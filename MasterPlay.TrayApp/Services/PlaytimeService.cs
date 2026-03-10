using MasterPlay.Models;
using MasterPlay.Models.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MasterPlay.TrayApp.Services
{
    // PlayTimeService
    public class PlayTimeService
    {
        // Fields
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

        // Constructor
        public PlayTimeService(ILogger<PlayTimeService> logger)
        {
            _logger = logger;
        }

        // Public Methods
        public void Start()
        {
            _logger.LogInformation("PLAYTIME SERVICE STARTED");
            _timer = new System.Timers.Timer(30000);
            _timer.Elapsed += (s, e) => TrackPlayTime();
            _timer.AutoReset = true;
            _timer.Start();
        }

        public void Stop()
        {
            _timer?.Stop();
            _logger.LogInformation("PlayTime tracking stopped.");
        }

        // Private Methods
        private void TrackPlayTime()
        {
            try
            {
                var gameList = DataService.LoadActiveGames();
                foreach (var game in gameList)
                {
                    if (string.IsNullOrEmpty(game.ExeName)) continue;

                    string processName = game.ExeName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
                    var processes = Process.GetProcessesByName(processName);
                    if (processes.Length == 0) continue;

                    _logger.LogInformation(game.Name + " is running");

                    bool isForeground = false;
                    IntPtr fgWindow = GetForegroundWindow();
                    GetWindowThreadProcessId(fgWindow, out uint fgPid);
                    if (processes.Any(p => p.Id == fgPid)) isForeground = true;

                    bool hadInput = GetIdleTime() < 30000;

                    if (isForeground && hadInput)
                    {
                        int newSeconds = (game.TotalPlayTimeSeconds ?? 0) + 30;

                        using (var context = new MasterPlayContext())
                        using (var transaction = context.Database.BeginTransaction())
                        {
                            try
                            {
                                // Update game's total play time
                                var gameToUpdate = new Game { Id = game.Id };
                                context.Games.Attach(gameToUpdate);
                                gameToUpdate.TotalPlayTimeSeconds = newSeconds;
                                context.Entry(gameToUpdate).Property(x => x.TotalPlayTimeSeconds).IsModified = true;

                                // Add diary entry
                                var diary = new PlayDiary
                                {
                                    GameId = game.Id,
                                    Date = DateTime.Now,
                                    TimeSpent = 30,
                                    Notes = null
                                };
                                context.PlayDiaries.Add(diary);

                                context.SaveChanges();
                                transaction.Commit();

                                _logger.LogInformation("Updated playtime for {name}, time: {time}s, diary added.", game.Name, newSeconds);
                            }
                            catch (Exception ex)
                            {
                                transaction.Rollback();
                                _logger.LogError(ex, "Failed to update playtime and diary for {name}", game.Name);
                            }
                        }
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
    }
}