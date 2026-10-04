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

    /// <summary>
    /// Whether the month component of <see cref="FinishedAt"/> is real data.
    ///
    /// A player who only remembers the year stores 1 January of that year with
    /// this set to false, so the UI can show "2019" instead of a January that
    /// never happened. Sorting and the Finished-year filter keep reading
    /// <see cref="FinishedAt"/> and are unaffected (DOMAIN_RULES 9.3).
    /// </summary>
    public bool HasMonth { get; set; } = true;

    public double? Score { get; set; }

    public virtual Game Game { get; set; } = null!;
}
