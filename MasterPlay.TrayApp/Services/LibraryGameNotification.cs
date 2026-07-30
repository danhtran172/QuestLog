namespace MasterPlay.TrayApp.Services;

public enum LibraryGameNotificationKind
{
    Detected,
    Added
}

public sealed record LibraryGameNotification(
    LibraryGameNotificationKind Kind,
    string GameName);
