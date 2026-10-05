using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MasterPlay.Installer;

/// <summary>
/// Hop chon thu muc kieu Explorer. WPF tren .NET Framework khong co OpenFolderDialog, con
/// FolderBrowserDialog cua WinForms la cay thu muc kieu cu.
/// </summary>
internal static class FolderPicker
{
    private const uint PickFolders = 0x20;
    private const uint ForceFileSystem = 0x40;
    private const uint PathMustExist = 0x800;
    private const uint FileSystemPath = 0x80058000;

    public static string? Show(Window owner, string title, string? initialDirectory)
    {
        var dialog = (IFileOpenDialog)new FileOpenDialog();
        try
        {
            dialog.GetOptions(out var options);
            dialog.SetOptions(options | PickFolders | ForceFileSystem | PathMustExist);
            dialog.SetTitle(title);

            if (initialDirectory != null &&
                SHCreateItemFromParsingName(initialDirectory, IntPtr.Zero, typeof(IShellItem).GUID, out var folder) == 0)
            {
                dialog.SetFolder(folder);
                Marshal.ReleaseComObject(folder);
            }

            // Huy tra HRESULT_FROM_WIN32(ERROR_CANCELLED), khong phai loi.
            if (dialog.Show(new WindowInteropHelper(owner).Handle) != 0) return null;

            dialog.GetResult(out var result);
            try
            {
                result.GetDisplayName(FileSystemPath, out var path);
                return path;
            }
            finally
            {
                Marshal.ReleaseComObject(result);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        string path,
        IntPtr bindContext,
        [MarshalAs(UnmanagedType.LPStruct)] Guid interfaceId,
        out IShellItem item);

    [ComImport]
    [Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    private class FileOpenDialog;

    // Thu tu ham phai dung bang vtable: IModalWindow -> IFileDialog -> IFileOpenDialog.
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("d57c7288-d4ad-4768-be02-9d969532d960")]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr owner);
        void SetFileTypes(uint count, IntPtr filterSpec);
        void SetFileTypeIndex(uint index);
        void GetFileTypeIndex(out uint index);
        void Advise(IntPtr events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(uint options);
        void GetOptions(out uint options);
        void SetDefaultFolder(IShellItem item);
        void SetFolder(IShellItem item);
        void GetFolder(out IShellItem item);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void GetResult(out IShellItem item);
        void AddPlace(IShellItem item, int placement);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
        void Close(int result);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr filter);
        void GetResults(out IntPtr items);
        void GetSelectedItems(out IntPtr items);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    private interface IShellItem
    {
        void BindToHandler(IntPtr bindContext, ref Guid handler, ref Guid interfaceId, out IntPtr result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint form, [MarshalAs(UnmanagedType.LPWStr)] out string name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }
}
