using System.IO;

namespace MasterPlay.Shell;

/// <summary>
/// Shell khong co console: loi khoi dong Host/tray va loi kiem tra cap nhat ghi ra
/// %LOCALAPPDATA%\QuestLog\logs\shell.log de con dieu tra duoc.
/// </summary>
internal static class ShellLog
{
    private const long MaxBytes = 1024 * 1024;
    private static readonly object Gate = new();

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QuestLog", "logs", "shell.log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxBytes)
                {
                    File.Move(FilePath, FilePath + ".old", overwrite: true);
                }

                File.AppendAllText(FilePath, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} {message}{Environment.NewLine}");
            }
        }
        catch (IOException)
        {
            // Khong ghi duoc log thi thoi; khong de viec ghi log lam sap app.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
