using System;
using System.Collections.Generic;

namespace MasterPlay.Core.Entities;

public partial class Theme
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? IconData { get; set; }

    public int SortOrder { get; set; }

    public virtual ICollection<Game> Games { get; set; } = new List<Game>();
}
