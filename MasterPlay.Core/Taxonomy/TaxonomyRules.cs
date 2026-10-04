using System.Text;
using MasterPlay.Core.Entities;

namespace MasterPlay.Core.Taxonomy;

/// <summary>
/// Quy tac thuan cua taxonomy theo chu so huu + nhom theme.
/// Xem docs/superpowers/specs/2026-10-04-theme-groups-design.md §4.
///
/// Dat o Core (khong phu thuoc EF/Host) de migration, endpoint va test dung chung
/// MOT dinh nghia — dac biet la NormalizeName: hai ban normalize lech nhau la
/// unique index va khu trung lap tra loi khac nhau ve cung mot ten.
/// </summary>
public static class TaxonomyRules
{
    public const string SharedOwnerKey = "system:shared";

    public static bool IsShared(string? ownerKey) =>
        string.Equals(ownerKey, SharedOwnerKey, StringComparison.Ordinal);

    /// <summary>
    /// trim + casefold (invariant, Unicode day du — khong dung lower() cua SQLite vi
    /// no chi ha ASCII) + gop chuoi khoang trang thanh mot dau cach.
    /// </summary>
    public static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var builder = new StringBuilder(name.Length);
        var pendingSpace = false;
        foreach (var character in name.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = true;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    /// <summary>
    /// I1/I2: theme chung chi vao nhom chung; theme ca nhan vao nhom chung hoac nhom
    /// cua chinh chu no. <paramref name="groupOwnerKey"/> null = chua phan nhom.
    /// </summary>
    public static bool CanPlaceTheme(string themeOwnerKey, string? groupOwnerKey)
    {
        if (groupOwnerKey is null) return true;
        if (IsShared(groupOwnerKey)) return true;
        return !IsShared(themeOwnerKey) &&
               string.Equals(themeOwnerKey, groupOwnerKey, StringComparison.Ordinal);
    }

    /// <summary>
    /// I6: nhom chung bat buoc du hai ten; nhom ca nhan can it nhat mot, ten thieu
    /// lay theo ten con lai.
    /// </summary>
    public static bool TryResolveGroupNames(
        string? nameEn,
        string? nameVi,
        bool requireBoth,
        out string resolvedEn,
        out string resolvedVi)
    {
        var en = nameEn?.Trim() ?? string.Empty;
        var vi = nameVi?.Trim() ?? string.Empty;

        if (requireBoth ? en.Length == 0 || vi.Length == 0 : en.Length == 0 && vi.Length == 0)
        {
            resolvedEn = resolvedVi = string.Empty;
            return false;
        }

        resolvedEn = en.Length > 0 ? en : vi;
        resolvedVi = vi.Length > 0 ? vi : en;
        return true;
    }

    /// <summary>Ten hien thi cua nhom theo ngon ngu: 'vi*' → NameVi, con lai → NameEn.</summary>
    public static string GroupDisplayName(ThemeGroup group, string? language) =>
        language is not null && language.TrimStart().StartsWith("vi", StringComparison.OrdinalIgnoreCase)
            ? group.NameVi
            : group.NameEn;

    /// <summary>
    /// §4.4: cung NameKey thi ban ca nhan thang ban chung. Giu nguyen thu tu dau vao
    /// cua nhung ban con lai.
    /// </summary>
    public static IReadOnlyList<T> Dedupe<T>(
        IEnumerable<T> items,
        Func<T, string> nameKey,
        Func<T, string> ownerKey)
    {
        var list = items.ToList();
        var personalKeys = list
            .Where(item => !IsShared(ownerKey(item)))
            .Select(nameKey)
            .ToHashSet(StringComparer.Ordinal);

        return list
            .Where(item => !IsShared(ownerKey(item)) || !personalKeys.Contains(nameKey(item)))
            .ToList();
    }

    /// <summary>§4.3: nhom chung truoc, roi nhom ca nhan; moi phan theo SortOrder, roi Id.</summary>
    public static IReadOnlyList<ThemeGroup> OrderGroups(IEnumerable<ThemeGroup> groups) =>
        groups
            .OrderBy(group => IsShared(group.LibraryOwnerKey) ? 0 : 1)
            .ThenBy(group => group.SortOrder)
            .ThenBy(group => group.Id)
            .ToList();

    /// <summary>§4.3: trong mot nhom, theme chung truoc, theme ca nhan sau; moi phan theo SortOrder, roi Name.</summary>
    public static IReadOnlyList<Theme> OrderThemes(IEnumerable<Theme> themes) =>
        themes
            .OrderBy(theme => IsShared(theme.LibraryOwnerKey) ? 0 : 1)
            .ThenBy(theme => theme.SortOrder)
            .ThenBy(theme => theme.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
