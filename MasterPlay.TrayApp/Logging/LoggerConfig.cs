using Serilog;

namespace MasterPlay.TrayApp.Logging
{

    public static class LoggerConfig
    {

        public static void Configure()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    path: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "log-.txt"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 3,
                    // shared: tien trinh thu hai khong mo duoc file dang bi khoa, nen
                    // dong canh bao "da co mot tray khac dang chay" bien mat - dung
                    // luc nguoi dung dang thac mac vi sao tray khong len.
                    shared: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();
        }
    }
}