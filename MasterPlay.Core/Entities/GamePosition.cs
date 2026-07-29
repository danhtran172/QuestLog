using System;
using System.Collections.Generic;

namespace MasterPlay.Models.Models;

public partial class GamePosition
{
    public string GameId { get; set; } = null!;

    public int? PlayOrder { get; set; }

    public virtual Game Game { get; set; } = null!;
}
