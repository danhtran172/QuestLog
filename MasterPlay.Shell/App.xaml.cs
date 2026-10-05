using System.Windows;

namespace MasterPlay.Shell;

/// <summary>
/// Ban cai: man cai dat lan dau (neu can) -> Host + tray -> cua so chinh. Dev
/// (run-web.cmd) mo thang cua so chinh vi Host/Vite da chay san.
///
/// ShutdownMode bat dau la OnExplicitShutdown: voi mac dinh OnLastWindowClose, dong
/// man cai dat (cua so duy nhat luc do) la WPF tat luon app truoc khi kip mo
/// cua so chinh.
/// </summary>
public partial class App : Application
{
    /// <summary>Velopack bao day la lan mo dau tien ngay sau khi cai.</summary>
    internal bool FirstRunAfterInstall { get; init; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (DesktopRuntime.IsPackaged)
        {
            var setup = FirstRunSetup.Inspect(FirstRunAfterInstall);
            if (setup.Required && new SetupWindow(setup).ShowDialog() != true)
            {
                // Dong man cai dat = huy: khong mo app voi lua chon nua voi.
                Shutdown();
                return;
            }

            DesktopRuntime.StartLocalServices();
        }

        var main = new MainWindow();
        MainWindow = main;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        main.Show();
    }
}
