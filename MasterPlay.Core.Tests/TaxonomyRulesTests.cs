using MasterPlay.Core.Entities;
using MasterPlay.Core.Taxonomy;

namespace MasterPlay.Core.Tests;

public class TaxonomyRulesTests
{
    private const string Shared = TaxonomyRules.SharedOwnerKey;
    private const string Alice = "user:alice";
    private const string Bob = "user:bob";

    [Theory]
    [InlineData("  Agent   /  Spy ", "agent / spy")]
    [InlineData("Đông Á", "đông á")]
    [InlineData("ĂN\tCHƠI", "ăn chơi")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeName_trims_casefolds_and_collapses_whitespace(string? input, string expected) =>
        Assert.Equal(expected, TaxonomyRules.NormalizeName(input));

    [Theory]
    [InlineData(Shared, null, true)]
    [InlineData(Shared, Shared, true)]
    [InlineData(Shared, Alice, false)]
    [InlineData(Alice, null, true)]
    [InlineData(Alice, Shared, true)]
    [InlineData(Alice, Alice, true)]
    [InlineData(Alice, Bob, false)]
    public void CanPlaceTheme_follows_I1_and_I2(string themeOwner, string? groupOwner, bool expected) =>
        Assert.Equal(expected, TaxonomyRules.CanPlaceTheme(themeOwner, groupOwner));

    [Fact]
    public void Personal_group_fills_missing_name_from_the_other_language()
    {
        Assert.True(TaxonomyRules.TryResolveGroupNames(null, " Soulslike ", false, out var en, out var vi));
        Assert.Equal("Soulslike", en);
        Assert.Equal("Soulslike", vi);
    }

    [Fact]
    public void Group_names_are_required()
    {
        Assert.False(TaxonomyRules.TryResolveGroupNames(" ", null, false, out _, out _));
        Assert.False(TaxonomyRules.TryResolveGroupNames("Era", "", true, out _, out _));
        Assert.True(TaxonomyRules.TryResolveGroupNames("Era", "Thời đại", true, out var en, out var vi));
        Assert.Equal(("Era", "Thời đại"), (en, vi));
    }

    [Theory]
    [InlineData("vi-VN", "Thời đại")]
    [InlineData("vi", "Thời đại")]
    [InlineData("en-US", "Era")]
    [InlineData(null, "Era")]
    public void GroupDisplayName_follows_language(string? language, string expected) =>
        Assert.Equal(expected, TaxonomyRules.GroupDisplayName(
            new ThemeGroup { NameEn = "Era", NameVi = "Thời đại" }, language));

    [Fact]
    public void Dedupe_prefers_personal_over_shared_with_same_key()
    {
        var items = new[]
        {
            (Key: "desert", Owner: Shared, Id: 1),
            (Key: "cozy", Owner: Alice, Id: 2),
            (Key: "desert", Owner: Alice, Id: 3),
            (Key: "ocean", Owner: Shared, Id: 4),
        };

        var result = TaxonomyRules.Dedupe(items, item => item.Key, item => item.Owner);

        Assert.Equal(new[] { 2, 3, 4 }, result.Select(item => item.Id));
    }

    [Fact]
    public void OrderGroups_puts_shared_first_then_sort_order()
    {
        var groups = new[]
        {
            new ThemeGroup { Id = 1, LibraryOwnerKey = Alice, SortOrder = 0 },
            new ThemeGroup { Id = 2, LibraryOwnerKey = Shared, SortOrder = 1 },
            new ThemeGroup { Id = 3, LibraryOwnerKey = Shared, SortOrder = 0 },
        };

        Assert.Equal(new[] { 3, 2, 1 }, TaxonomyRules.OrderGroups(groups).Select(group => group.Id));
    }

    [Fact]
    public void OrderThemes_puts_shared_before_personal()
    {
        var themes = new[]
        {
            new Theme { Id = 1, Name = "Cozy", LibraryOwnerKey = Alice, SortOrder = 0 },
            new Theme { Id = 2, Name = "Ocean", LibraryOwnerKey = Shared, SortOrder = 5 },
            new Theme { Id = 3, Name = "Jungle", LibraryOwnerKey = Shared, SortOrder = 5 },
        };

        Assert.Equal(new[] { 3, 2, 1 }, TaxonomyRules.OrderThemes(themes).Select(theme => theme.Id));
    }
}
