using MasterPlay.Core.Entities;

namespace MasterPlay.TrayApp.Services;

public sealed record GameActivity(Game Game, DateTimeOffset DetectedAt);
