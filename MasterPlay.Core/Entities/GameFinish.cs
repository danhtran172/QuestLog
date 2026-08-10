using System;

namespace MasterPlay.Core.Entities;

/// <summary>
/// A single completion of a game. A game can be completed more than once.
/// </summary>
public class GameFinish
{
    public int Id { get; set; }

    public string GameId { get; set; } = string.Empty;

    public DateTime FinishedAt { get; set; }

    public double? Score { get; set; }

    public virtual Game Game { get; set; } = null!;
}
