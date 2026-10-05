using System;
using System.Windows;

namespace MasterPlay.Installer;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Chay kem doi so (--silent, --installto D:\Apps\QuestLog...): cai tu dong, chuyen
        // nguyen doi so cho Setup.exe cua Velopack, khong hien cua so.
        if (e.Args.Length > 0)
        {
            int exitCode;
            try
            {
                exitCode = VelopackSetup.IsBundled ? await VelopackSetup.RunAsync(e.Args) : 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                exitCode = 1;
            }

            Shutdown(exitCode);
            return;
        }

        var window = new InstallerWindow();
        window.Closed += (_, _) => Shutdown(window.ExitCode);
        window.Show();
    }
}
