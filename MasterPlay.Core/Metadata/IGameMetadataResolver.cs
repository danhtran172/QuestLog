namespace MasterPlay.Core.Metadata;

public interface IGameMetadataResolver
{
    Task<GameMetadataMatch?> ResolveByExecutableAsync(
        string executablePath,
        CancellationToken cancellationToken);
}
