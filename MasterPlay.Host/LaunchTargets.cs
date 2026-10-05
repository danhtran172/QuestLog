using MasterPlay.Core.Entities;
using MasterPlay.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MasterPlay.Host;

/// <summary>
/// Nguon DUY NHAT tra loi "game nay bam Play duoc khong, va mo file nao".
/// canLaunch, tab console va POST /launch cung dung chung, nen nut Play chi hien
/// khi file that su con tren dia — game da xoa khoi o cung se mat nut Play.
/// </summary>
public sealed class LaunchTargets
{
    private readonly ILookup<string, string> _detectedPaths;
    private readonly Func<string, bool> _fileExists;

    public LaunchTargets(IEnumerable<(string ExeName, string FullPath)> detected, Func<string, bool>? fileExists = null)
    {
        _detectedPaths = detected.ToLookup(p => p.ExeName, p => p.FullPath, StringComparer.OrdinalIgnoreCase);
        _fileExists = fileExists ?? File.Exists;
    }

    public static async Task<LaunchTargets> LoadAsync(MasterPlayContext db)
    {
        var detected = await db.DetectedProcesses.AsNoTracking()
            .Select(p => new { p.ExeName, p.FullPath })
            .ToListAsync();
        return new LaunchTargets(detected.Select(p => (p.ExeName, p.FullPath)));
    }

    /// <summary>
    /// File Play se mo, chi khi file do con ton tai: LaunchTargetPath neu co,
    /// nguoc lai la exe da scan thay khop ExeName.
    /// </summary>
    public string? ResolveExistingPath(Game game)
    {
        if (!string.IsNullOrWhiteSpace(game.LaunchTargetPath))
            return _fileExists(game.LaunchTargetPath) ? game.LaunchTargetPath : null;

        if (string.IsNullOrWhiteSpace(game.ExeName)) return null;
        var processName = game.ExeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? game.ExeName
            : game.ExeName + ".exe";
        return _detectedPaths[processName].FirstOrDefault(_fileExists);
    }

    public bool CanLaunch(Game game) => !game.IsDlc && ResolveExistingPath(game) is not null;
}
