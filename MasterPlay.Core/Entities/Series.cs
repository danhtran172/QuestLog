using System;
using System.Collections.Generic;

namespace MasterPlay.Core.Entities;

public partial class Series
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Game> Games { get; set; } = new List<Game>();
}
