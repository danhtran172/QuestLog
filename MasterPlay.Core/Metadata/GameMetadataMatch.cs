using MasterPlay.Core.Entities;

namespace MasterPlay.Core.Metadata;

public sealed record GameMetadataMatch(
    Game Game,
    IReadOnlyCollection<string> ExecutableNames);
