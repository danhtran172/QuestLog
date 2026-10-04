using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace MasterPlay.Host;

internal static class NativePicker
{
    public static Task<string?> PickFolderAsync()
    {
        var ownerHandle = GetForegroundWindow();
        return RunStaDialog(() =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Choose folder",
                ShowNewFolderButton = true,
                UseDescriptionForTitle = true,
            };
            return ShowDialog(dialog, ownerHandle) == DialogResult.OK
                ? dialog.SelectedPath
                : null;
        });
    }

    public static Task<string?> PickFileAsync(string filter)
    {
        var ownerHandle = GetForegroundWindow();
        return RunStaDialog(() =>
        {
            using var dialog = new OpenFileDialog
            {
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false,
                Filter = filter switch
                {
                    "exe-or-shortcut" => "Executable or shortcut (*.exe;*.lnk)|*.exe;*.lnk|All files (*.*)|*.*",
                    "database" => "Database (*.db;*.sqlite;*.sqlite3)|*.db;*.sqlite;*.sqlite3|All files (*.*)|*.*",
                    "json" => "JSON (*.json)|*.json|All files (*.*)|*.*",
                    _ => "All files (*.*)|*.*",
                },
            };
            return ShowDialog(dialog, ownerHandle) == DialogResult.OK
                ? dialog.FileName
                : null;
        });
    }

    private static DialogResult ShowDialog(CommonDialog dialog, IntPtr ownerHandle) =>
        ownerHandle == IntPtr.Zero
            ? dialog.ShowDialog()
            : dialog.ShowDialog(new WindowOwner(ownerHandle));

    private static Task<T> RunStaDialog<T>(Func<T> showDialog)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(showDialog());
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "QuestLog native picker",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private sealed class WindowOwner(IntPtr handle) : IWin32Window
    {
        public IntPtr Handle { get; } = handle;
    }
}
