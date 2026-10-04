namespace MasterPlay.Core.Entities;

/// <summary>
/// Nhom theme (Thoi dai / Boi canh / ...). Nhom 'system:shared' do admin quan ly,
/// nhom ca nhan chi chu so huu thay. Xem spec 2026-10-04-theme-groups-design §4.
/// </summary>
public class ThemeGroup
{
    public int Id { get; set; }

    public string LibraryOwnerKey { get; set; } = Taxonomy.TaxonomyRules.SharedOwnerKey;

    public string NameEn { get; set; } = string.Empty;

    public string NameVi { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public virtual ICollection<Theme> Themes { get; set; } = new List<Theme>();
}
