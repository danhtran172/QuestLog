using System;
using System.Collections.Generic;

namespace MasterPlay.Core.Entities;

public partial class Theme
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? IconData { get; set; }

    public int SortOrder { get; set; }

    /// <summary>'system:shared' | 'user:&lt;id&gt;' | 'guest:local'. Server gan, client khong gui.</summary>
    public string LibraryOwnerKey { get; set; } = Taxonomy.TaxonomyRules.SharedOwnerKey;

    /// <summary>Ten da chuan hoa (TaxonomyRules.NormalizeName). MasterPlayContext tu tinh khi luu.</summary>
    public string NameKey { get; set; } = string.Empty;

    public int? GroupId { get; set; }

    public virtual ThemeGroup? Group { get; set; }

    public virtual ICollection<Game> Games { get; set; } = new List<Game>();
}
