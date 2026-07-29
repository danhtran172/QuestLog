namespace MasterPlay.Core.Entities;

public class GameOwnershipInfo
{
    public string GameId { get; set; } = string.Empty;

    public string? SteamAccount { get; set; }

    public string? SteamPassword { get; set; }

    public string? UbisoftAccount { get; set; }

    public string? UbisoftPassword { get; set; }

    public string? SupportDocumentUrl { get; set; }

    public string? DownloadUrl { get; set; }

    public string? Note { get; set; }

    public virtual Game Game { get; set; } = null!;
}
