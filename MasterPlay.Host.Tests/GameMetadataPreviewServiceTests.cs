using MasterPlay.Core.Entities;
using MasterPlay.Host.Metadata;
using MasterPlay.Infrastructure.Integrations;
using MasterPlay.Infrastructure.Integrations.Igdb.Models;
using MasterPlay.Infrastructure.Integrations.Rawg;
using MasterPlay.Infrastructure.Integrations.Rawg.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace MasterPlay.Host.Tests;

public sealed class GameNameMatcherTests
{
    [Theory]
    [InlineData("Hades II", "Hades 2")]
    [InlineData("Pokémon™ Legends: Arceus", "pokemon legends arceus")]
    [InlineData("Ratchet & Clank", "Ratchet and Clank")]
    public void Equivalent_names_score_one(string left, string right) =>
        Assert.Equal(1, GameNameMatcher.Similarity(left, right));

    [Fact]
    public void Picks_closest_name_instead_of_first_result()
    {
        string[] results = ["The Witcher", "The Witcher 2: Assassins of Kings", "The Witcher 3: Wild Hunt"];

        Assert.Equal("The Witcher 3: Wild Hunt", GameNameMatcher.PickBest("Witcher 3", results, name => name));
        Assert.Equal("The Witcher", GameNameMatcher.PickBest("the witcher", results, name => name));
    }

    [Fact]
    public void Rejects_unrelated_names() =>
        Assert.Null(GameNameMatcher.PickBest("Hollow Knight", ["Knights of the Old Republic"], name => name));

    [Fact]
    public void Catalog_match_requires_every_typed_word_in_the_name()
    {
        Assert.True(GameNameMatcher.CoversAllWords("assassin's creed shadow", "Assassin's Creed Shadows"));
        Assert.False(GameNameMatcher.CoversAllWords("assassin's creed shadow", "Assassin's Creed IV Black Flag"));
        Assert.False(GameNameMatcher.CoversAllWords("assassin's creed shadow", "Assassin's Creed"));
        Assert.True(GameNameMatcher.CoversAllWords("witcher 3", "The Witcher 3: Wild Hunt"));
        Assert.True(GameNameMatcher.CoversAllWords("hades 2", "Hades II"));
    }

    [Fact]
    public void Search_tokens_keep_original_words() =>
        Assert.Equal(["hades", "ii"], GameNameMatcher.SearchTokens("Hades II™"));
}

public sealed class GameMetadataPreviewServiceTests
{
    private static MetadataCatalogGame CatalogGame(
        string name = "Hades",
        string? developer = "Supergiant Games",
        string? publisher = "Supergiant Games",
        string? poster = "https://img/poster.jpg",
        string? backdrop = "https://img/backdrop.jpg",
        string? banner = "https://img/banner.jpg",
        int? metaScore = 93,
        int? avgPlaytime = 22,
        long? igdbId = 113112) =>
        new(1, igdbId, name, developer, publisher, null, new DateOnly(2020, 9, 17), false, null,
            poster, backdrop, banner, metaScore, avgPlaytime, null, 1);

    private static GameMetadataPreviewService Service(FakeProviders providers) =>
        new(providers, NullLogger<GameMetadataPreviewService>.Instance);

    private static Task<IReadOnlyList<Series>> NoSeries(CancellationToken _) =>
        Task.FromResult<IReadOnlyList<Series>>([]);

    [Fact]
    public async Task Catalog_fields_win_and_only_missing_fields_call_providers()
    {
        var providers = new FakeProviders
        {
            Catalog = new MetadataCatalogMatch(CatalogGame(), null, "Hades.exe"),
            Rawg = new GameMetadata { Name = "Hades", SlugName = "hades-2", MetaScore = 50, AvgPlaytime = 99, Background_Image = "https://rawg/bg.jpg" },
        };

        var result = await Service(providers).PreviewAsync("hades", "metadata", NoSeries, default);

        var preview = Assert.IsType<GameMetadataPreviewDto>(result.Preview);
        Assert.Equal("Hades", preview.Name);
        Assert.Equal(93, preview.MetaScore);
        Assert.Equal(22, preview.AvgPlaytimeHours);
        Assert.Equal("https://img/backdrop.jpg", preview.BackdropPath);
        Assert.Equal("hades-2", preview.SlugName);
        Assert.Equal("Hades.exe", preview.ExeName);
        Assert.Equal(1, preview.MetadataGameId);
        Assert.Equal(0, providers.IgdbCalls);
        Assert.Equal(0, providers.SteamCalls);
        Assert.Equal(["Hades"], providers.RawgNames);
    }

    [Fact]
    public async Task Missing_catalog_fields_are_filled_from_providers_using_catalog_name()
    {
        var providers = new FakeProviders
        {
            Catalog = new MetadataCatalogMatch(CatalogGame(name: "Hades", poster: null, backdrop: null, banner: null, metaScore: null, avgPlaytime: null), null, null),
            IgdbById = new IgdbMetadata { IgdbId = 113112, Name = "Hades", CoverUrl = "https://igdb/cover.jpg" },
            Rawg = new GameMetadata { Name = "Hades", MetaScore = 93, AvgPlaytime = 21, Background_Image = "https://rawg/bg.jpg" },
            SteamPair = ("https://steam/header.jpg", "https://steam/hero.jpg"),
        };

        var result = await Service(providers).PreviewAsync("hades goty", "metadata", NoSeries, default);

        var preview = result.Preview!;
        Assert.Equal("https://igdb/cover.jpg", preview.PosterPath);
        Assert.Equal("https://rawg/bg.jpg", preview.BackdropPath);
        Assert.Equal("https://steam/header.jpg", preview.BannerPath);
        Assert.Equal(93, preview.MetaScore);
        Assert.Equal(21, preview.AvgPlaytimeHours);
        Assert.Equal(113112, providers.IgdbByIdRequested);
        Assert.Equal(["Hades"], providers.RawgNames);
    }

    [Fact]
    public async Task Provider_failure_becomes_warning_instead_of_failing_sync()
    {
        var providers = new FakeProviders
        {
            IgdbById = null,
            IgdbByName = new IgdbMetadata { IgdbId = 7, Name = "Celeste", Developers = "Maddy Makes Games", CoverUrl = "https://igdb/c.jpg" },
            RawgError = new RawgException("RAWG_NOT_CONFIGURED", "RAWG API key is not configured."),
        };

        var result = await Service(providers).PreviewAsync("celeste", "metadata", NoSeries, default);

        var preview = Assert.IsType<GameMetadataPreviewDto>(result.Preview);
        Assert.Equal("Celeste", preview.Name);
        Assert.Equal("Maddy Makes Games", preview.Developer);
        Assert.Null(preview.ExeName);
        var warning = Assert.Single(preview.Warnings!, w => w.Provider == "rawg");
        Assert.Equal("RAWG_NOT_CONFIGURED", warning.Code);
    }

    [Fact]
    public async Task Nothing_found_reports_every_provider_reason()
    {
        var providers = new FakeProviders
        {
            IgdbConfigured = false,
            RawgError = new RawgException("RAWG_KEY_INVALID", "RAWG rejected the API key."),
        };

        var result = await Service(providers).PreviewAsync("unknown game", "metadata", NoSeries, default);

        Assert.Null(result.Preview);
        Assert.Equal("METADATA_NOT_FOUND", result.ErrorCode);
        Assert.Contains("RAWG rejected the API key.", result.ErrorMessage);
        Assert.Contains("IGDB credentials are not configured", result.ErrorMessage);
    }

    [Fact]
    public async Task Image_sync_uses_catalog_before_providers()
    {
        var providers = new FakeProviders { Catalog = new MetadataCatalogMatch(CatalogGame(), null, null) };
        var service = Service(providers);

        Assert.Equal("https://img/backdrop.jpg", (await service.PreviewAsync("Hades", "cover", NoSeries, default)).Preview!.BackdropPath);
        Assert.Equal("https://img/banner.jpg", (await service.PreviewAsync("Hades", "card-cover", NoSeries, default)).Preview!.BannerPath);
        Assert.Equal("https://img/poster.jpg", (await service.PreviewAsync("Hades", "portrait-cover", NoSeries, default)).Preview!.PosterPath);
        Assert.Empty(providers.RawgNames);
        Assert.Equal(0, providers.SteamCalls);
        Assert.Equal(0, providers.IgdbCalls);
    }

    [Fact]
    public async Task Catalog_series_name_matches_existing_library_series()
    {
        var providers = new FakeProviders
        {
            Catalog = new MetadataCatalogMatch(CatalogGame(), "Hades", null),
        };
        IReadOnlyList<Series> existing = [new Series { Id = 4, Name = "Hades" }];

        var result = await Service(providers).PreviewAsync("Hades", "metadata", _ => Task.FromResult(existing), default);

        Assert.Equal(4, result.Preview!.Series!.Id);
    }

    private sealed class FakeProviders : IGameMetadataProviders
    {
        public bool IgdbConfigured { get; set; } = true;
        public MetadataCatalogMatch? Catalog { get; init; }
        public IgdbMetadata? IgdbById { get; init; }
        public IgdbMetadata? IgdbByName { get; init; }
        public GameMetadata? Rawg { get; init; }
        public Exception? RawgError { get; init; }
        public (string? Banner, string? Hero) SteamPair { get; init; }
        public int IgdbCalls { get; private set; }
        public int SteamCalls { get; private set; }
        public int? IgdbByIdRequested { get; private set; }
        public List<string> RawgNames { get; } = [];

        public Task<MetadataCatalogMatch?> FindCatalogAsync(string name, CancellationToken cancellationToken) => Task.FromResult(Catalog);

        public Task<IgdbMetadata?> IgdbByIdAsync(int igdbId, CancellationToken cancellationToken)
        {
            IgdbCalls++;
            IgdbByIdRequested = igdbId;
            return Task.FromResult(IgdbById);
        }

        public Task<IgdbMetadata?> IgdbByNameAsync(string name, CancellationToken cancellationToken)
        {
            IgdbCalls++;
            return Task.FromResult(IgdbByName);
        }

        public Task<GameMetadata?> RawgAsync(string name, CancellationToken cancellationToken)
        {
            lock (RawgNames) RawgNames.Add(name);
            return RawgError is null ? Task.FromResult(Rawg) : Task.FromException<GameMetadata?>(RawgError);
        }

        public Task<(string? Banner, string? Hero)> SteamBannerAndHeroAsync(string name, CancellationToken cancellationToken)
        {
            SteamCalls++;
            return Task.FromResult(SteamPair);
        }

        public Task<string?> SteamBannerAsync(string name, CancellationToken cancellationToken) { SteamCalls++; return Task.FromResult(SteamPair.Banner); }
        public Task<string?> SteamHeroAsync(string name, CancellationToken cancellationToken) { SteamCalls++; return Task.FromResult(SteamPair.Hero); }
        public Task<string?> SteamPosterAsync(string name, CancellationToken cancellationToken) { SteamCalls++; return Task.FromResult<string?>(null); }
        public Task<bool> SteamReachableAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
