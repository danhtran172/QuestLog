using System;
using System.Collections.Generic;

namespace MasterPlay.Models.Models;

public partial class OwnershipStatus
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;
    public string ColorCode { get; set; } = null!;

    public int SortOrder { get; set; }

    public virtual ICollection<Game> Games { get; set; } = new List<Game>();
}
