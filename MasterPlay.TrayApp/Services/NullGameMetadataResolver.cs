using MasterPlay.Core.Metadata;

namespace MasterPlay.TrayApp.Services;

public sealed class NullGameMetadataResolver : IGameMetadataResolver
{
    public Task<GameMetadataMatch?> ResolveByExecutableAsync(
        string executablePath,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<GameMetadataMatch?>(null);
    }
}
