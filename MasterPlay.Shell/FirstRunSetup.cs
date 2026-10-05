using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using MasterPlay.Infrastructure.Configuration;

namespace MasterPlay.Shell;

internal enum SetupNotice
{
    None,
    /// <summary>Thu muc chon luc cai khong con (o dia ngoai da rut, bi xoa).</summary>
    MissingFolder,
    /// <summary>File luu vi tri du lieu bi hong.</summary>
    UnreadableLocation,
}

internal enum SetupPathError
{
    None,
    Required,
    NotFullPath,
    InsideInstall,
    NotWritable,
}

internal sealed record SetupState(bool Required, string Language, string DataDirectory, SetupNotice Notice);

/// <summary>
/// Phan "cai dat" ma Setup.exe cua Velopack khong hoi duoc (no khong co trang tuy
/// chon): ngon ngu va thu muc du lieu. Chay mot lan ngay sau khi cai, TRUOC khi Host
/// va tray khoi dong vi ca hai doc thu muc du lieu ngay luc chay.
/// </summary>
internal static class FirstRunSetup
{
    public const string English = "en-US";
    public const string Vietnamese = "vi-VN";

    private const string LanguageKey = "Language";

    private static readonly JsonSerializerOptions ConfigJson = new()
    {
        WriteIndented = true,
        // Giu nguyen chu co dau trong duong dan (C:\Users\Thuận) cho de doc.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Can hien man cai dat khi: vua cai xong (ke ca cai lai), lua chon cu khong dung
    /// duoc nua, hoac may chua tung co du lieu QuestLog. Ban cap nhat cua nguoi dung
    /// cu khong roi vao truong hop nao nen khong bi hoi lai.
    /// </summary>
    public static SetupState Inspect(bool firstRunAfterInstall)
    {
        string? chosen = null;
        var notice = SetupNotice.None;
        try
        {
            chosen = AppPaths.ReadChosenDataDirectory(AppPaths.LocationFilePath);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            ShellLog.Write($"Khong doc duoc vi tri du lieu da chon: {ex.Message}");
            notice = SetupNotice.UnreadableLocation;
        }

        if (chosen != null &&
            !AppPaths.IsSamePath(chosen, AppPaths.DefaultDataDirectory) &&
            !Directory.Exists(chosen))
        {
            notice = SetupNotice.MissingFolder;
        }

        var directory = chosen ?? AppPaths.DefaultDataDirectory;
        var required = firstRunAfterInstall ||
                       notice != SetupNotice.None ||
                       (chosen == null && !AppPaths.HasDefaultData());
        return new SetupState(required, ReadLanguage(directory) ?? SystemLanguage(), directory, notice);
    }

    public static SetupPathError ValidateDataDirectory(string input, out string fullPath, out string? detail)
    {
        fullPath = string.Empty;
        detail = null;

        var trimmed = input.Trim().Trim('"');
        if (trimmed.Length == 0) return SetupPathError.Required;
        if (!Path.IsPathFullyQualified(trimmed)) return SetupPathError.NotFullPath;

        try
        {
            fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmed));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return SetupPathError.NotFullPath;
        }

        // Velopack thay thu muc cai moi lan cap nhat va xoa no khi go: du lieu de trong
        // do la mat.
        if (IsInside(fullPath, InstallRoot)) return SetupPathError.InsideInstall;

        // Thu muc mac dinh luon ghi duoc, va KHONG duoc tao truoc o day: AppPaths chi doi
        // thu muc MasterPlay cu sang khi thu muc QuestLog chua ton tai.
        if (AppPaths.IsSamePath(fullPath, AppPaths.DefaultDataDirectory)) return SetupPathError.None;

        try
        {
            Directory.CreateDirectory(fullPath);
            var probe = Path.Combine(fullPath, $".questlog-write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            detail = ex.Message;
            return SetupPathError.NotWritable;
        }

        return SetupPathError.None;
    }

    /// <summary>Ghi lua chon. Nem loi neu khong ghi duoc — man cai dat hien loi do.</summary>
    public static void Apply(string dataDirectory, string language, bool dataDirectoryChanged)
    {
        AppPaths.SaveChosenDataDirectory(AppPaths.LocationFilePath, dataDirectory);

        // Hoi lai AppPaths thay vi ghi thang vao dataDirectory: chon thu muc mac dinh tren
        // may con du lieu MasterPlay cu thi AppPaths phai doi ten thu muc cu truoc, neu
        // khong config.json moi se tao thu muc QuestLog rong va chan buoc di doi do.
        var directory = AppPaths.GetDataDirectory();
        WriteLanguage(Path.Combine(directory, AppPaths.ConfigFileName), language);

        // Tray dang chay tu truoc (ban cai cu, ban dev) da doc thu muc du lieu luc no khoi
        // dong. Tat no de StartLocalServices mo lai voi thu muc moi. Chi doi ngon ngu thi
        // khong can: tray tu nap lai khi config.json doi.
        if (dataDirectoryChanged) TrayProcess.RequestShutdown(TimeSpan.FromSeconds(10));
    }

    /// <summary>Lan cai lai: config dang co thi giu ngon ngu cua no lam lua chon san.</summary>
    private static string? ReadLanguage(string directory)
    {
        try
        {
            var configPath = Path.Combine(directory, AppPaths.ConfigFileName);
            if (!File.Exists(configPath)) return null;
            if (JsonNode.Parse(File.ReadAllText(configPath)) is not JsonObject config) return null;

            var value = config
                .FirstOrDefault(property => property.Key.Equals(LanguageKey, StringComparison.OrdinalIgnoreCase))
                .Value?.GetValue<string>();
            return value is English or Vietnamese ? value : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            // Chi dung de goi y lua chon san; doc khong duoc thi lay theo Windows.
            return null;
        }
    }

    private static string SystemLanguage() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi" ? Vietnamese : English;

    /// <summary>
    /// Chi sua Language, giu nguyen moi khoa khac. config.json chua co thi tao file chi
    /// co Language; JsonService dien phan con lai bang gia tri mac dinh o lan doc dau.
    /// </summary>
    private static void WriteLanguage(string configPath, string language)
    {
        var config = File.Exists(configPath)
            ? JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject
              ?? throw new InvalidDataException($"{configPath} is not a JSON object.")
            : new JsonObject();

        // JsonService (Newtonsoft) doc khoa khong phan biet hoa thuong: sua dung khoa dang co.
        var key = config
            .Select(property => property.Key)
            .FirstOrDefault(name => name.Equals(LanguageKey, StringComparison.OrdinalIgnoreCase)) ?? LanguageKey;
        config[key] = language;
        File.WriteAllText(configPath, config.ToJsonString(ConfigJson));
    }

    /// <summary>Thu muc goc Velopack (cha cua current\), noi chua Update.exe va moi ban cai.</summary>
    private static string InstallRoot =>
        Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)) ?? AppContext.BaseDirectory;

    private static bool IsInside(string path, string root)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        return path.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
