using MasterPlay.Core.Entities;

namespace MasterPlay.Host.Tests;

/// <summary>DOMAIN_RULES §5.7, §5.8, §5.9, §5.10 — nhom chip Progress/Ownership/Genre/Theme.</summary>
public sealed class LibraryFilterTests
{
    private static readonly ProgressStatus Done = new() { Id = 1, Name = "Done", ColorCode = "#000" };
    private static readonly ProgressStatus Playing = new() { Id = 2, Name = "Playing", ColorCode = "#000" };
    private static readonly OwnershipStatus Owned = new() { Name = "Owned" };
    private static readonly OwnershipStatus Wishlist = new() { Name = "Wishlist" };

    private static Game NewGame(
        string id,
        ProgressStatus? progress = null,
        OwnershipStatus? ownership = null,
        int[]? genres = null,
        int[]? themes = null)
    {
        var game = new Game { Id = id, Name = id, Progress = progress, Ownership = ownership };
        foreach (var genreId in genres ?? [])
            game.GameGenres.Add(new GameGenre { GameId = id, GenreId = genreId });
        foreach (var themeId in themes ?? [])
            game.GameThemes.Add(new GameTheme { GameId = id, ThemeId = themeId });
        return game;
    }

    private static string[] Ids(IEnumerable<Game> games) => games.Select(g => g.Id).ToArray();

    [Fact]
    public void Progress_keys_match_any_selected_key()
    {
        var games = new[] { NewGame("a", Done), NewGame("b", Playing), NewGame("c") };

        var result = LibraryQuery.ApplyFilter(games, new() { ProgressKeys = ["Done", "Playing"] });

        Assert.Equal(["a", "b"], Ids(result));
    }

    [Fact]
    public void Progress_and_ownership_are_AND_between_groups()
    {
        var games = new[]
        {
            NewGame("done-owned", Done, Owned),
            NewGame("done-wish", Done, Wishlist),
            NewGame("playing-owned", Playing, Owned),
        };

        var result = LibraryQuery.ApplyFilter(games, new() { ProgressKeys = ["Done"], OwnershipKeys = ["Owned"] });

        Assert.Equal(["done-owned"], Ids(result));
    }

    [Fact]
    public void Genre_and_theme_are_OR_not_AND()
    {
        var games = new[]
        {
            NewGame("genre-only", genres: [1]),
            NewGame("theme-only", themes: [7]),
            NewGame("neither", genres: [2], themes: [8]),
        };

        var result = LibraryQuery.ApplyFilter(games, new() { GenreIds = [1], ThemeIds = [7] });

        Assert.Equal(["genre-only", "theme-only"], Ids(result));
    }

    [Fact]
    public void Reverse_inverts_chip_groups()
    {
        var games = new[] { NewGame("a", Done), NewGame("b", Playing) };

        var result = LibraryQuery.ApplyFilter(games, new() { ProgressKeys = ["Done"], ReverseFilter = true });

        Assert.Equal(["b"], Ids(result));
    }

    [Fact]
    public void Reverse_without_any_filter_returns_everything()
    {
        var games = new[] { NewGame("a", Done), NewGame("b", Playing) };

        var result = LibraryQuery.ApplyFilter(games, new() { ReverseFilter = true });

        Assert.Equal(["a", "b"], Ids(result));
    }

    [Fact]
    public void Genre_and_theme_count_as_one_active_group()
    {
        var filter = new LibraryQuery.FilterArgs
        {
            ProgressKeys = ["Done"],
            OwnershipKeys = ["Owned"],
            GenreIds = [1],
            ThemeIds = [7],
        };

        Assert.True(filter.HasAnyFilter);
        Assert.Equal(3, filter.ActiveCount);
    }
}
