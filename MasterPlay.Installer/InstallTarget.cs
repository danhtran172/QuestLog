using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Microsoft.Win32;

namespace MasterPlay.Installer;

internal enum InstallPathError
{
    None,
    Required,
    NotFullPath,
    DriveRoot,
    /// <summary>Program Files, Windows...: can quyen quan tri, ban cap nhat khong ghi duoc.</summary>
    ProtectedFolder,
    /// <summary>Trung hoac long voi thu muc du lieu/log cua QuestLog.</summary>
    DataFolder,
    NotEmpty,
}

/// <summary>Cac thu muc lam moc khi kiem tra, tach rieng de test khong phu thuoc may.</summary>
internal sealed class InstallEnvironment
{
    public InstallEnvironment(IEnumerable<string> protectedFolders, IEnumerable<string> dataFolders)
    {
        ProtectedFolders = protectedFolders.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
        DataFolders = dataFolders.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
    }

    public IReadOnlyList<string> ProtectedFolders { get; }
    public IReadOnlyList<string> DataFolders { get; }

    public static InstallEnvironment Current()
    {
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new InstallEnvironment(
            new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                // Bo cai 32/64-bit: ProgramFiles tra thu muc khac nhau, lay ca ban 64-bit.
                Environment.GetEnvironmentVariable("ProgramW6432") ?? string.Empty,
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            },
            new[]
            {
                // Khop AppPaths (MasterPlay.Infrastructure/Configuration/AppPaths.cs).
                Path.Combine(roaming, "QuestLog"),
                Path.Combine(roaming, "MasterPlay"),
                // Log cua Shell va file data-location.json.
                Path.Combine(local, "QuestLog"),
                InstallTarget.ReadChosenDataDirectory(Path.Combine(local, "QuestLog", "data-location.json")) ?? string.Empty,
            });
    }
}

/// <summary>
/// Thu muc cai do nguoi dung chon. Velopack thay noi dung thu muc nay moi lan cap nhat va
/// XOA CA THU MUC khi go (cai vao thu muc co san file thi no doi ten roi xoa phan cu), nen
/// chi nhan mot thu muc trong, rieng cho QuestLog.
/// </summary>
internal static class InstallTarget
{
    public const string FolderName = "QuestLog";
    public const string PackId = "QuestLogDesktop";

    /// <summary>Cho Velopack tu chon khi khong truyen --installto.</summary>
    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), PackId);

    /// <summary>
    /// Chon "D:\Apps" trong hop thoai thi cai vao D:\Apps\QuestLog, khong rai file ra giua
    /// thu muc nguoi dung. Tru khi thu muc chon da ten QuestLog va dang trong.
    /// </summary>
    public static string FromPickedFolder(string picked)
    {
        var folder = TrimEnd(picked);
        return Path.GetFileName(folder).Equals(FolderName, StringComparison.OrdinalIgnoreCase) && IsEmptyOrMissing(folder)
            ? folder
            : Path.Combine(folder, FolderName);
    }

    public static InstallPathError Validate(string input, InstallEnvironment environment, out string fullPath)
    {
        fullPath = string.Empty;

        var trimmed = input.Trim().Trim('"');
        if (trimmed.Length == 0) return InstallPathError.Required;
        if (!IsFullyQualified(trimmed)) return InstallPathError.NotFullPath;

        string path;
        try
        {
            path = TrimEnd(Path.GetFullPath(trimmed));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return InstallPathError.NotFullPath;
        }

        fullPath = path;
        var root = Path.GetPathRoot(path);
        if (root != null && IsSamePath(path, root)) return InstallPathError.DriveRoot;

        if (environment.ProtectedFolders.Any(folder => IsInside(path, folder)))
        {
            return InstallPathError.ProtectedFolder;
        }

        // Ca hai chieu: cai vao trong thu muc du lieu thi go la mat du lieu; du lieu nam
        // trong thu muc cai cung vay.
        if (environment.DataFolders.Any(folder => IsInside(path, folder) || IsInside(folder, path)))
        {
            return InstallPathError.DataFolder;
        }

        return IsEmptyOrMissing(path) ? InstallPathError.None : InstallPathError.NotEmpty;
    }

    /// <summary>Thu muc cua ban dang cai (Velopack ghi vao muc go cai dat), neu con.</summary>
    public static string? FindExistingInstall()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{PackId}");
            if (key?.GetValue("InstallLocation") is not string location || location.Length == 0) return null;
            return File.Exists(Path.Combine(location, "Update.exe")) ? TrimEnd(location) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Tao thu muc va thu ghi mot file. Thu muc do minh tao ma cai khong xong thi
    /// <see cref="RemoveIfEmpty"/> don lai.
    /// </summary>
    public static bool TryPrepare(string directory, out bool created, out string? detail)
    {
        created = !Directory.Exists(directory);
        detail = null;
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".questlog-write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            detail = ex.Message;
            if (created) RemoveIfEmpty(directory);
            return false;
        }
    }

    public static void RemoveIfEmpty(string directory)
    {
        try
        {
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    internal static string? ReadChosenDataDirectory(string locationFile)
    {
        try
        {
            if (!File.Exists(locationFile)) return null;
            using var stream = File.OpenRead(locationFile);
            var location = (DataLocation?)new DataContractJsonSerializer(typeof(DataLocation)).ReadObject(stream);
            var directory = location?.DataDirectory;
            return string.IsNullOrWhiteSpace(directory) || !IsFullyQualified(directory!) ? null : directory;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SerializationException)
        {
            // Shell bao file hong o man cai dat lan dau; o day chi mat mot moc kiem tra.
            return null;
        }
    }

    private static bool IsEmptyOrMissing(string directory)
    {
        try
        {
            if (File.Exists(directory)) return false;
            return !Directory.Exists(directory) || !Directory.EnumerateFileSystemEntries(directory).Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Path.IsPathFullyQualified khong co tren .NET Framework.</summary>
    private static bool IsFullyQualified(string path)
    {
        if (path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && IsSeparator(path[2])) return true;
        return path.Length >= 3 && IsSeparator(path[0]) && IsSeparator(path[1]) && !IsSeparator(path[2]);
    }

    private static bool IsSeparator(char c) => c == '\\' || c == '/';

    /// <summary>Bo dau \ cuoi, tru o goc ("C:\").</summary>
    private static string TrimEnd(string path)
    {
        var trimmed = path.TrimEnd('\\', '/');
        return trimmed.Length == 2 && trimmed[1] == ':' ? trimmed + "\\" : trimmed;
    }

    private static bool IsSamePath(string left, string right) =>
        string.Equals(TrimEnd(Normalize(left)), TrimEnd(Normalize(right)), StringComparison.OrdinalIgnoreCase);

    private static bool IsInside(string path, string folder)
    {
        var normalizedPath = TrimEnd(Normalize(path));
        var normalizedFolder = TrimEnd(Normalize(folder));
        if (normalizedPath.Equals(normalizedFolder, StringComparison.OrdinalIgnoreCase)) return true;
        var prefix = normalizedFolder.EndsWith("\\", StringComparison.Ordinal) ? normalizedFolder : normalizedFolder + "\\";
        return normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    [DataContract]
    private sealed class DataLocation
    {
        [DataMember(Name = "dataDirectory")]
        public string? DataDirectory { get; set; }
    }
}
