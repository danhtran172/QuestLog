using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MasterPlay.Core.Entities;
using MasterPlay.Infrastructure.Data;
using MasterPlay.Infrastructure.Integrations.Igdb.Models;
using Microsoft.EntityFrameworkCore;

namespace MasterPlay.Infrastructure.Integrations.Igdb;

/// <summary>Bao cao mot lan sync — de tang tren log / hien thi, khong bat buoc dung.</summary>
public sealed class IgdbSyncResult
{
    public required int IgdbId { get; init; }
    public required string MatchedName { get; init; }
    public bool NameUpdated { get; init; }
    public bool ReleaseDateUpdated { get; init; }
    public bool DeveloperUpdated { get; init; }
    public bool PublisherUpdated { get; init; }
    public bool CoverUpdated { get; init; }
    public string? SeriesName { get; init; }
    public bool SeriesCreated { get; init; }
    public bool MarkedAsDlc { get; init; }
    public string? BaseGameName { get; init; }
    /// <summary>IGDB bao la DLC nhung khong tim thay game goc nao trong thu vien.</summary>
    public bool BaseGameUnresolved { get; init; }
}

/// <summary>
/// Ap ket qua IGDB len entity <see cref="Game"/>.
///
/// Nghiep vu nam O DAY (mot lan, o backend) chu khong o Host hay WPF — AGENTS.md §0.
/// Ham nay KHONG goi SaveChanges; tang goi tu quyet dinh ranh gioi giao dich.
///
/// Nguyen tac ghi de:
///   - Name / PublishDate / Developer / Publisher: GHI DE (IGDB la nguon chuan hon RAWG).
///   - Portrait cover ghi vao <see cref="Game.PosterPath"/>; khong ghi de
///     detail cover cua RAWG qua <see cref="Game.BackdropPath"/>.
///   - SeriesId: chi gan khi game CHUA co series.
///   - IsDlc: chi nang len true, KHONG bao gio ha ve false (tranh xoa lua chon tay).
///   - BaseGameId: chi gan khi dang trong.
///   - KHONG dung den MetaScore / AvgPlayTime / SlugName / anh gallery — RAWG va Steam van giu.
/// </summary>
public static class IgdbMetadataSync
{
    /// <summary>Do dai toi thieu de cho phep khop "chua nhau" — ngan khop nham voi ten ngan.</summary>
    private const int MinContainmentLength = 6;

    private static readonly Regex PossessiveSuffix =
        new(@"['’]s\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static async Task<IgdbSyncResult> ApplyAsync(
        MasterPlayContext db,
        Game game,
        IgdbMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(metadata);

        var nameUpdated = false;
        if (!string.IsNullOrWhiteSpace(metadata.Name) &&
            !string.Equals(game.Name, metadata.Name, StringComparison.Ordinal))
        {
            game.Name = metadata.Name;
            nameUpdated = true;
        }

        var releaseUpdated = false;
        if (metadata.ReleaseDate is { } releaseDate && game.PublishDate != releaseDate)
        {
            game.PublishDate = releaseDate;
            releaseUpdated = true;
        }

        var developerUpdated = false;
        if (!string.IsNullOrWhiteSpace(metadata.Developers) &&
            !string.Equals(game.Developer, metadata.Developers, StringComparison.Ordinal))
        {
            game.Developer = metadata.Developers;
            developerUpdated = true;
        }

        var publisherUpdated = false;
        if (!string.IsNullOrWhiteSpace(metadata.Publishers) &&
            !string.Equals(game.Publisher, metadata.Publishers, StringComparison.Ordinal))
        {
            game.Publisher = metadata.Publishers;
            publisherUpdated = true;
        }

        var coverUpdated = false;
        if (!string.IsNullOrWhiteSpace(metadata.CoverUrl))
        {
            game.PosterPath = metadata.CoverUrl.Trim();
            coverUpdated = true;
        }

        game.IgdbId = metadata.IgdbId;

        var (series, seriesCreated) = await ResolveSeriesAsync(db, game, metadata, cancellationToken)
            .ConfigureAwait(false);

        var markedAsDlc = false;
        if (metadata.IsDlc && !game.IsDlc)
        {
            // Chi nang len true. Ha ve false se xoa quyet dinh tay cua nguoi dung,
            // va DOMAIN_RULES §13 co cascade rieng phai chay o luong luu, khong phai o day.
            game.IsDlc = true;
            markedAsDlc = true;
        }

        var baseGame = await ResolveBaseGameAsync(db, game, metadata, cancellationToken).ConfigureAwait(false);
        if (baseGame is not null && string.IsNullOrWhiteSpace(game.BaseGameId))
            game.BaseGameId = baseGame.Id;

        return new IgdbSyncResult
        {
            IgdbId = metadata.IgdbId,
            MatchedName = metadata.Name,
            NameUpdated = nameUpdated,
            ReleaseDateUpdated = releaseUpdated,
            DeveloperUpdated = developerUpdated,
            PublisherUpdated = publisherUpdated,
            CoverUpdated = coverUpdated,
            SeriesName = series?.Name,
            SeriesCreated = seriesCreated,
            MarkedAsDlc = markedAsDlc,
            BaseGameName = baseGame?.Name,
            BaseGameUnresolved = metadata.ParentGameIgdbId.HasValue && baseGame is null
        };
    }

    /// <summary>
    /// Map ung vien series cua IGDB vao bang Series hien co, tao moi neu chua co.
    ///
    /// Thu tu uu tien co chu y: quet HET ung vien de tim khop CHINH XAC truoc, roi moi
    /// den khop "chua nhau". Nho vay taxonomy san co cua nguoi dung luon thang ten IGDB.
    /// </summary>
    private static async Task<(Series? Series, bool Created)> ResolveSeriesAsync(
        MasterPlayContext db,
        Game game,
        IgdbMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (metadata.SeriesCandidates.Count == 0)
            return (null, false);

        // Chi tu dien vao o trong. Series la lua chon bien tap cua nguoi dung.
        if (game.SeriesId.HasValue)
        {
            var current = await db.Series
                .FirstOrDefaultAsync(item => item.Id == game.SeriesId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (current is not null)
                return (current, false);
        }

        var existing = await db.Series.ToListAsync(cancellationToken).ConfigureAwait(false);

        var matched = MatchSeries(existing, metadata.SeriesCandidates);
        if (matched is not null)
        {
            game.SeriesId = matched.Id;
            return (matched, false);
        }

        var newName = NewSeriesName(metadata.SeriesCandidates);
        if (newName is null)
            return (null, false);

        var created = new Series { Name = newName };
        db.Series.Add(created);
        // Can Id thuc de gan SeriesId; Series la bang nho nen chi phi khong dang ke.
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        game.SeriesId = created.Id;
        return (created, true);
    }

    /// <summary>
    /// Khop ung vien series cua IGDB voi cac Series DA CO. Tra <c>null</c> khi khong khop —
    /// nguoi goi tu quyet dinh co tao moi hay khong.
    ///
    /// Ham thuan (khong cham DB) de ca Host lan WPF dung chung MOT quy tac (AGENTS.md §0).
    /// Quet het ung vien tim khop CHINH XAC truoc, roi moi den khop "chua nhau" —
    /// nho vay taxonomy san co cua nguoi dung luon thang ten IGDB.
    /// </summary>
    public static Series? MatchSeries(IEnumerable<Series> existing, IReadOnlyList<string> candidates)
    {
        if (existing is null || candidates is null || candidates.Count == 0)
            return null;

        var indexed = existing
            .Select(item => (Item: item, Key: NormalizeKey(item.Name)))
            .Where(entry => entry.Key.Length > 0)
            .ToList();

        var candidateKeys = candidates
            .Select(NormalizeKey)
            .Where(key => key.Length > 0)
            .ToList();

        foreach (var key in candidateKeys)
        {
            var exact = indexed.FirstOrDefault(entry => entry.Key == key);
            if (exact.Item is not null)
                return exact.Item;
        }

        foreach (var key in candidateKeys)
        {
            var partial = indexed.FirstOrDefault(entry =>
                Contains(entry.Key, key) || Contains(key, entry.Key));
            if (partial.Item is not null)
                return partial.Item;
        }

        return null;
    }

    /// <summary>Ten dung de tao Series moi khi <see cref="MatchSeries"/> tra null.</summary>
    public static string? NewSeriesName(IReadOnlyList<string> candidates) =>
        candidates?.FirstOrDefault(name => NormalizeKey(name).Length > 0);

    /// <summary>
    /// Tim game goc trong mot tap game da nam san trong bo nho (dung cho WPF editor).
    /// Uu tien <see cref="Game.IgdbId"/>, roi den khop ten da chuan hoa.
    /// </summary>
    public static Game? MatchBaseGame(IEnumerable<Game> candidates, IgdbMetadata metadata, string? currentGameId)
    {
        if (candidates is null || metadata?.ParentGameIgdbId is not { } parentIgdbId)
            return null;

        var pool = candidates
            .Where(item => !item.IsDisabled && item.Id != currentGameId)
            .ToList();

        var byId = pool.FirstOrDefault(item => item.IgdbId == parentIgdbId);
        if (byId is not null)
            return byId;

        var parentKey = NormalizeKey(metadata.ParentGameName);
        return parentKey.Length == 0
            ? null
            : pool.FirstOrDefault(item => NormalizeKey(item.Name) == parentKey);
    }

    /// <summary>
    /// Tim game goc trong thu vien: uu tien IgdbId (chac chan), roi den khop ten da chuan hoa.
    /// </summary>
    private static async Task<Game?> ResolveBaseGameAsync(
        MasterPlayContext db,
        Game game,
        IgdbMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (metadata.ParentGameIgdbId is not { } parentIgdbId)
            return null;

        var byId = await db.Games
            .FirstOrDefaultAsync(
                item => item.IgdbId == parentIgdbId && !item.IsDisabled && item.Id != game.Id,
                cancellationToken)
            .ConfigureAwait(false);
        if (byId is not null)
            return byId;

        if (string.IsNullOrWhiteSpace(metadata.ParentGameName))
            return null;

        var parentKey = NormalizeKey(metadata.ParentGameName);
        if (parentKey.Length == 0)
            return null;

        // Khong the so sanh khoa chuan hoa trong SQL — keo ve tap nho roi loc trong bo nho.
        var candidates = await db.Games
            .Where(item => !item.IsDisabled && !item.IsDlc && item.Id != game.Id)
            .Select(item => new { item.Id, item.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var matchId = candidates.FirstOrDefault(item => NormalizeKey(item.Name) == parentKey)?.Id;
        if (matchId is null)
            return null;

        return await db.Games
            .FirstOrDefaultAsync(item => item.Id == matchId, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool Contains(string haystack, string needle) =>
        needle.Length >= MinContainmentLength && haystack.Contains(needle, StringComparison.Ordinal);

    /// <summary>
    /// Chuan hoa ten de so khop: bo so huu cach ('s), bo moi ky tu khong phai chu/so, ha chu thuong.
    /// "Marvel's Spider-Man" va "Marvel Spider-Man" cung ra "marvelspiderman".
    /// </summary>
    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var withoutPossessive = PossessiveSuffix.Replace(value, string.Empty);
        var builder = new StringBuilder(withoutPossessive.Length);
        foreach (var character in withoutPossessive)
        {
            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
