using Velopack;

namespace MasterPlay.Shell;

/// <summary>
/// Diem vao tu viet thay cho Main ma WPF sinh tu App.xaml: Velopack phai chay
/// TRUOC moi thu khac. Khi Setup/Update.exe goi exe voi --veloapp-*, Run() xu ly
/// hook roi thoat ngay, khong duoc mo cua so hay khoi dong Host.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main()
    {
        var firstRunAfterInstall = false;

        VelopackApp.Build()
            // Ban da tai ve duoc ap dung o AppUpdater.ApplyPendingOnStartup, SAU khi
            // tray da thoat em. De Velopack tu ap dung luc khoi dong thi no giet
            // cung tray, mat toi 30 giay choi chua kip ghi.
            .SetAutoApplyOnStartup(false)
            // Tray la mot phan cua ban cai: dang ky khoi dong cung Windows ngay luc cai,
            // ke ca khi cai --silent va chua ai mo app.
            .OnAfterInstallFastCallback(_ =>
            {
                TrayProcess.EnableAutoStart();
                UninstallShortcut.Create();
            })
            .OnBeforeUpdateFastCallback(_ => TrayProcess.RequestShutdown(TimeSpan.FromSeconds(10)))
            // Ca luc cap nhat: may da cai ban chua co shortcut go cai dat cung nhan duoc.
            .OnAfterUpdateFastCallback(_ => UninstallShortcut.Create())
            .OnBeforeUninstallFastCallback(_ =>
            {
                TrayProcess.RequestShutdown(TimeSpan.FromSeconds(10));
                UninstallShortcut.Remove();
                // Thu muc cai bi xoa: dong tu khoi dong cua tray tro vao exe khong con.
                TrayProcess.RemoveAutoStart();
                // Velopack khong tu tat app dang mo khi go: file bi khoa, thu muc sot lai.
                DesktopRuntime.StopOtherInstances();
            })
            // Setup.exe mo app ngay sau khi cai: hoi ngon ngu va thu muc du lieu.
            .OnFirstRun(_ => firstRunAfterInstall = true)
            .Run();

        if (DesktopRuntime.IsPackaged)
        {
            if (!DesktopRuntime.TryAcquireSingleInstance())
            {
                DesktopRuntime.ActivateRunningInstance();
                return;
            }

            // Ban da tai o lan chay truoc ma nguoi dung chua khoi dong lai: ap dung
            // ngay, truoc khi mo Host/cua so. Ham nay khong tra ve neu co ban cho.
            DesktopRuntime.Updater.ApplyPendingOnStartup();
        }

        // Host/tray khoi dong trong App.OnStartup, SAU man cai dat lan dau: ca hai doc
        // thu muc du lieu ngay khi chay nen phai doi nguoi dung chon xong.
        var app = new App { FirstRunAfterInstall = firstRunAfterInstall };
        app.InitializeComponent();
        app.Run();
    }
}
