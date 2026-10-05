using Microsoft.Win32;
namespace MasterPlay.TrayApp.Startup
{
    public static class StartupHelper
    {
        private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = MasterPlay.Infrastructure.TrayControl.TrayControlProtocol.AutoStartValueName;

        public static bool EnableStartup(string executablePath)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath, true);
                if (key == null)
                    return false;

                key.SetValue(AppName, $"\"{executablePath}\"", RegistryValueKind.String);
                return IsStartupEnabled();
            }
            catch
            {
                return false;
            }
        }

        public static bool DisableStartup()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                key?.DeleteValue(AppName, false);
                return !IsStartupEnabled();
            }
            catch
            {
                return false;
            }
        }

        public static bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
