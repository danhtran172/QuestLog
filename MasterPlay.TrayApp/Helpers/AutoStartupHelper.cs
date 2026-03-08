using Microsoft.Win32;
using System;

namespace MasterPlay.TrayApp.Helpers
{
    public static class StartupHelper
    {
        private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "MasterPlayTray";

        public static void EnableStartup()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true))
                {
                    if (key != null)
                    {
                        string exePath = $"\"{System.Reflection.Assembly.GetExecutingAssembly().Location}\"";
                        key.SetValue(AppName, exePath);
                    }
                }
            }
            catch (Exception) { /* Bỏ qua lỗi */ }
        }

        public static void DisableStartup()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true))
                {
                    if (key != null && key.GetValue(AppName) != null)
                        key.DeleteValue(AppName);
                }
            }
            catch (Exception) { }
        }

        public static bool IsStartupEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false))
            {
                return key?.GetValue(AppName) != null;
            }
        }
    }
}