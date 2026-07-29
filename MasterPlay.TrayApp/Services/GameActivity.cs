using MasterPlay.Models.Models;

namespace MasterPlay.TrayApp.Services;

public sealed record GameActivity(Game Game, DateTimeOffset DetectedAt);
