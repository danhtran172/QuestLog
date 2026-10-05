using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace MasterPlay.Shell;

/// <summary>
/// Velopack khong co file go rieng: go cai la chay Update.exe --uninstall, nguoi dung
/// khong biet. Shortcut nay dat canh QuestLog.exe trong thu muc cai va trong Start
/// Menu. Thu muc cai bi Velopack xoa khi go; ban Start Menu thi phai tu xoa.
/// </summary>
internal static class UninstallShortcut
{
    // Ten theo ngon ngu Windows luc cai (man chon ngon ngu chua chay). Go/tao lai xoa ca hai.
    private static readonly string[] AllNames = ["Gỡ cài đặt QuestLog.lnk", "Uninstall QuestLog.lnk"];

    private static string Name =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi" ? AllNames[0] : AllNames[1];

    /// <summary>Thu muc goc Velopack (cha cua current\), noi chua Update.exe.</summary>
    private static string InstallRoot =>
        Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)) ?? AppContext.BaseDirectory;

    private static string StartMenuPrograms =>
        Environment.GetFolderPath(Environment.SpecialFolder.Programs);

    public static void Create()
    {
        var updateExe = Path.Combine(InstallRoot, "Update.exe");
        if (!File.Exists(updateExe))
        {
            ShellLog.Write($"Khong thay Update.exe de tao shortcut go cai dat: {updateExe}");
            return;
        }

        // QuestLog.exe o thu muc goc la stub cua Velopack, khong doi duong dan qua cac ban cap nhat.
        var icon = Path.Combine(InstallRoot, "QuestLog.exe");
        if (!File.Exists(icon)) icon = updateExe;

        Remove();
        foreach (var folder in new[] { InstallRoot, StartMenuPrograms })
        {
            try
            {
                Write(Path.Combine(folder, Name), updateExe, "--uninstall", InstallRoot, icon);
            }
            catch (Exception ex)
            {
                ShellLog.Write($"Khong tao duoc shortcut go cai dat trong {folder}: {ex.Message}");
            }
        }
    }

    public static void Remove()
    {
        foreach (var folder in new[] { InstallRoot, StartMenuPrograms })
        foreach (var name in AllNames)
        {
            try
            {
                File.Delete(Path.Combine(folder, name));
            }
            catch (Exception ex)
            {
                ShellLog.Write($"Khong xoa duoc shortcut go cai dat {name} trong {folder}: {ex.Message}");
            }
        }
    }

    private static void Write(string path, string target, string arguments, string workingDirectory, string icon)
    {
        var link = (IShellLinkW)new ShellLink();
        try
        {
            link.SetPath(target);
            link.SetArguments(arguments);
            link.SetWorkingDirectory(workingDirectory);
            link.SetIconLocation(icon, 0);
            link.SetDescription("QuestLog");
            ((IPersistFile)link).Save(path, true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink;

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] char[] file, int maxPath, IntPtr findData, uint flags);
        void GetIDList(out IntPtr idList);
        void SetIDList(IntPtr idList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] char[] name, int maxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] char[] dir, int maxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] char[] args, int maxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCmd);
        void SetShowCmd(int showCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] char[] iconPath, int maxPath, out int iconIndex);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relativePath, uint reserved);
        void Resolve(IntPtr hwnd, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }
}
