using MasterPlay.TrayApp;
using MasterPlay.TrayApp.Logging;
using MasterPlay.TrayApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace MasterPlay.TrayApp
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Cấu hình Serilog
            LoggerConfig.Configure();

            ApplicationConfiguration.Initialize();

            var services = new ServiceCollection();
            ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();

            // KHÔNG gọi loggerFactory.AddSerilog(Log.Logger) ở đây nữa.
            // Trước đây dòng này add thêm 1 provider Serilog thứ 2 vào ILoggerFactory,
            // trong khi ConfigureServices() bên dưới đã add 1 provider Serilog rồi
            // (qua AddLogging -> AddSerilog). Có 2 provider cùng trỏ về Log.Logger
            // khiến mọi dòng log bị ghi lặp 2 lần xuống file.

            Application.Run(new TrayApplicationContext(serviceProvider));
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                // Truyền thẳng Log.Logger cho tường minh, tránh phụ thuộc
                // vào ambient static ngầm định gây khó nhận ra khi có ai
                // đó đăng ký thêm provider Serilog ở chỗ khác như bug vừa gặp.
                builder.AddSerilog(Log.Logger, dispose: true);
            });

            services.AddSingleton<PlayTimeService>();
            services.AddSingleton<LibraryService>();
            services.AddSingleton<BackupService>();
        }
    }
}