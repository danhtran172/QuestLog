using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterPlay.Installer;

/// <summary>
/// Setup.exe cua Velopack nam trong bo cai nay (tools\release.ps1 nhung vao luc build).
/// No cai .NET 8 neu thieu, giai nen app, tao shortcut, dang ky go cai dat va mo app.
/// Bo cai nay chi them buoc chon thu muc roi goi no voi --installto.
/// </summary>
internal static class VelopackSetup
{
    private const string ResourceName = "VelopackSetup.exe";

    public static bool IsBundled =>
        typeof(VelopackSetup).Assembly.GetManifestResourceInfo(ResourceName) != null;

    public static string LogPath { get; } = Path.Combine(Path.GetTempPath(), "QuestLog-Setup.log");

    public static IEnumerable<string> InstallArguments(string directory) =>
        new[] { "--installto", directory, "--log", LogPath };

    /// <summary>Giai nen Setup.exe ra thu muc tam, chay voi cac doi so cho san, tra ve ma thoat.</summary>
    public static async Task<int> RunAsync(IEnumerable<string> arguments)
    {
        var workDirectory = Path.Combine(Path.GetTempPath(), $"QuestLog-Setup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDirectory);
        try
        {
            var setupPath = Path.Combine(workDirectory, $"{InstallTarget.PackId}-win-Setup.exe");
            using (var resource = typeof(VelopackSetup).Assembly.GetManifestResourceStream(ResourceName)
                                  ?? throw new InvalidOperationException("The QuestLog package is missing from this installer."))
            using (var file = File.Create(setupPath))
            {
                await resource.CopyToAsync(file).ConfigureAwait(false);
            }

            var startInfo = new ProcessStartInfo(setupPath, BuildCommandLine(arguments))
            {
                UseShellExecute = false,
                WorkingDirectory = workDirectory,
            };
            using var process = Process.Start(startInfo)
                                ?? throw new InvalidOperationException("Setup.exe did not start.");
            // Process.WaitForExitAsync khong co tren .NET Framework.
            return await Task.Run(() =>
            {
                process.WaitForExit();
                return process.ExitCode;
            }).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                Directory.Delete(workDirectory, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Thu muc tam, Windows tu don.
            }
        }
    }

    /// <summary>Ghep doi so theo quy tac CommandLineToArgvW (ArgumentList khong co tren .NET Framework).</summary>
    internal static string BuildCommandLine(IEnumerable<string> arguments) =>
        string.Join(" ", arguments.Select(Quote));

    private static string Quote(string argument)
    {
        if (argument.Length > 0 && argument.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return argument;

        var builder = new StringBuilder("\"");
        var backslashes = 0;
        foreach (var c in argument)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            // Dau \ dung truoc dau " phai nhan doi, con " thi thoat bang \.
            builder.Append('\\', c == '"' ? backslashes * 2 + 1 : backslashes);
            backslashes = 0;
            builder.Append(c);
        }

        builder.Append('\\', backslashes * 2);
        return builder.Append('"').ToString();
    }
}
