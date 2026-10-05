using MasterPlay.Core.Entities;

namespace MasterPlay.Host;

/// <summary>
/// Cai dat DOMAIN_RULES phia server. Day la NGUON SU THAT nghiep vu.
/// Frontend chi gui query va render — khong duoc cai lai bat ky nguong nao.
/// </summary>
public static class LibraryQuery
{
    /// <summary>
    /// SystemKey suy ra tam thoi tu Id/Name.
    ///
    /// TODO(S1): sau khi migration them cot ProgressStatus.SystemKey / OwnershipStatus.SystemKey
    /// thi doc thang tu DB va XOA ham nay. Xem DOMAIN_RULES §3.1.
    /// Hien tai van phai map vi DB that chua co cot do.
    /// </summary>
    public static string ProgressSystemKey(ProgressStatus? p)
    {
        if (p is null) return "";
        return p.Id switch
        {
            1 => "Done",
            2 => "Playing",
            3 => "NextPlay",
            4 => "Intended",
            _ => NormalizeName(p.Name),
        };
    }

    public static string OwnershipSystemKey(OwnershipStatus? o)
    {
        if (o is null) return "";
        var normalizedName = NormalizeName(o.Name);
        if (normalizedName is "Owned" or "Shared" or "Subscription" or "Exclusive" or "Picklocked" or "Wishlist")
            return normalizedName;

        // Tuong thich catalog legacy cho den khi cot SystemKey duoc migrate.
        return o.Id switch
        {
            2 => "Pirated",
            6 => "SteamOff",
            _ => normalizedName,
        };
    }

    /// <summary>
    /// Bo cuc khung thong tin so huu (DOMAIN_RULES §14): dung chung tai khoan ->
    /// "sharedAccount" (tai khoan Steam/Ubisoft + tai lieu), ban tai ve -> "download"
    /// (link + ghi chu), con lai -> null (khong co gi de xem).
    /// </summary>
    public static string? OwnershipInfoLayout(OwnershipStatus? o) => OwnershipSystemKey(o) switch
    {
        "Shared" or "SteamOff" => "sharedAccount",
        "Picklocked" or "Pirated" => "download",
        _ => null,
    };

    private static string NormalizeName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "" : name.Replace(" ", "");

    /// <summary>Nguong dai do dai trung binh (gio) — DOMAIN_RULES §5.5.</summary>
    public static string? AvgBand(int? hours)
    {
        if (hours is null || hours <= 0) return null;
        return hours switch
        {
            >= 80 => "VeryLong",
            >= 60 => "Long",
            >= 35 => "Medium",
            >= 20 => "Short",
            _ => "VeryShort",
        };
    }

    /// <summary>
    /// Nguong MetaScore — DOMAIN_RULES §5.5.
    /// Ranh gioi Low/VeryLow la 50 (KHONG phai 60). Xem mismatch M16.
    /// </summary>
    public static string? MetaBand(int? meta)
    {
        if (meta is null || meta <= 0) return null;
        return meta switch
        {
            >= 90 => "VeryHigh",
            >= 80 => "High",
            >= 70 => "Medium",
            >= 50 => "Low",
            _ => "VeryLow",
        };
    }

    /// <summary>
    /// Chat lieu hien thi User Score — DOMAIN_RULES §2.1.
    /// Frontend chi map band sang style, khong duoc lap lai cac nguong nay.
    /// </summary>
    public static string? ScoreMaterialBand(double? score)
    {
        if (score is null || score <= 0) return null;
        if (score == 11) return "Opal";
        return score switch
        {
            >= 10 => "Diamond",
            >= 9 => "Platinum",
            >= 8 => "Gold",
            >= 6 => "Silver",
            >= 3 => "Copper",
            _ => "Rust",
        };
    }

    /// <summary>
    /// Initials — DOMAIN_RULES §6.2.
    /// Tach theo ' ' '-' '_' '.', ghep chu cai dau, so khop bang CONTAINS.
    /// </summary>
    public static bool MatchesInitials(string? text, string term)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(term)) return false;
        var words = text.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return false;
        var initials = new string(words.Select(w => char.ToLowerInvariant(w[0])).ToArray());
        return initials.Contains(term);
    }

    private static bool Contains(string? value, string term) =>
        (value ?? "").ToLowerInvariant().Contains(term);

    /// <summary>DOMAIN_RULES §4 — phan loai theo tab bang SystemKey.</summary>
    public static IEnumerable<Game> ApplyTab(IEnumerable<Game> games, string tab, LaunchTargets? launchTargets = null) => tab switch
    {
        "console" => games.Where((launchTargets
            ?? throw new ArgumentNullException(nameof(launchTargets), "Tab console can LaunchTargets.")).CanLaunch),
        "planning" => games.Where(g => ProgressSystemKey(g.Progress) != "Done"),
        "finished" => games.Where(g => ProgressSystemKey(g.Progress) == "Done"),
        "queue" => games.Where(g =>
            ProgressSystemKey(g.Progress) is "Playing" or "NextPlay" or "Intended"),
        _ => games,
    };

    /// <summary>DOMAIN_RULES §6.3, §6.4.</summary>
    public static IEnumerable<Game> ApplySearch(IEnumerable<Game> games, string? q, string scope)
    {
        var term = (q ?? "").Trim().ToLowerInvariant();
        if (term.Length == 0) return games;

        if (scope == "releaseYear")
        {
            // So BANG DUNG NAM. Khong parse duoc -> tra RONG (mismatch M26).
            if (!int.TryParse(term, out var year)) return Enumerable.Empty<Game>();
            return games.Where(g => g.PublishDate?.Year == year);
        }

        return games.Where(g => scope switch
        {
            "name" => Contains(g.Name, term) || MatchesInitials(g.Name, term),
            "series" => Contains(g.Series?.Name, term) || MatchesInitials(g.Series?.Name, term),
            "publisher" => Contains(g.Publisher, term) || MatchesInitials(g.Publisher, term),
            "developer" => Contains(g.Developer, term) || MatchesInitials(g.Developer, term),
            _ => Contains(g.Name, term)
                 || Contains(g.Series?.Name, term)
                 || g.GameGenres.Any(x => Contains(x.Genre?.Name, term))
                 || g.GameThemes.Any(x => Contains(x.Theme?.Name, term))
                 || MatchesInitials(g.Name, term)
                 || MatchesInitials(g.Series?.Name, term),
        });
    }

    /// <summary>Tham so loc — khop query string trong openapi.yaml.</summary>
    public sealed class FilterArgs
    {
        public string? FinishedYearOp { get; init; }
        public double? FinishedYearValue { get; init; }
        public string? ExcitedOp { get; init; }
        public double? ExcitedValue { get; init; }
        public string? ScoreOp { get; init; }
        public double? ScoreValue { get; init; }
        public string? PlaytimeOp { get; init; }
        public int? PlaytimeSeconds { get; init; }
        public string[] AvgBands { get; init; } = [];
        public bool IncludeAvgNone { get; init; }
        public string[] MetaBands { get; init; } = [];
        public bool IncludeMetaNone { get; init; }
        /// <summary>SystemKey (khong phai id) — §5.8. So voi ProgressSystemKey/OwnershipSystemKey.</summary>
        public string[] ProgressKeys { get; init; } = [];
        public string[] OwnershipKeys { get; init; } = [];
        /// <summary>Genre va Theme la MOT nhom, quan he OR — §5.7.</summary>
        public int[] GenreIds { get; init; } = [];
        public int[] ThemeIds { get; init; } = [];
        public bool ReverseFilter { get; init; }

        /// <summary>
        /// DOMAIN_RULES §5.9 — phai kiem tra cai nay TRUOC khi dao.
        /// Chua bat loc nao + reverse=true => tra TOAN BO (khong phai rong).
        /// </summary>
        public bool HasAnyFilter =>
            FinishedYearValue.HasValue
            || ExcitedValue.HasValue
            || ScoreValue.HasValue
            || PlaytimeSeconds.HasValue
            || AvgBands.Length > 0
            || IncludeAvgNone
            || MetaBands.Length > 0
            || IncludeMetaNone
            || ProgressKeys.Length > 0
            || OwnershipKeys.Length > 0
            || GenreIds.Length > 0
            || ThemeIds.Length > 0;

        public int ActiveCount =>
            (FinishedYearValue.HasValue ? 1 : 0)
            + (ExcitedValue.HasValue ? 1 : 0)
            + (ScoreValue.HasValue ? 1 : 0)
            + (PlaytimeSeconds.HasValue ? 1 : 0)
            + (AvgBands.Length > 0 || IncludeAvgNone ? 1 : 0)
            + (MetaBands.Length > 0 || IncludeMetaNone ? 1 : 0)
            + (ProgressKeys.Length > 0 ? 1 : 0)
            + (OwnershipKeys.Length > 0 ? 1 : 0)
            // Genre + Theme OR voi nhau nen tinh la MOT nhom.
            + (GenreIds.Length > 0 || ThemeIds.Length > 0 ? 1 : 0);
    }

    /// <summary>
    /// So sanh so — DOMAIN_RULES §5.2.
    /// null LUON tra false (game bi loai), epsilon 1e-6 cho so thuc.
    /// </summary>
    private static bool CompareDouble(double? value, double target, string? op)
    {
        if (!value.HasValue) return false;
        const double eps = 1e-6;
        return op switch
        {
            "gt" => value.Value > target + eps,
            "gte" => value.Value >= target - eps,
            "eq" => Math.Abs(value.Value - target) < eps,
            "lte" => value.Value <= target + eps,
            "lt" => value.Value < target - eps,
            _ => false,
        };
    }

    /// <summary>So sanh so nguyen (playtime) — khong dung epsilon.</summary>
    private static bool CompareInt(int? value, int target, string? op)
    {
        if (!value.HasValue) return false;
        return op switch
        {
            "gt" => value.Value > target,
            "gte" => value.Value >= target,
            "eq" => value.Value == target,
            "lte" => value.Value <= target,
            "lt" => value.Value < target,
            _ => false,
        };
    }

    /// <summary>
    /// Loc — DOMAIN_RULES §5.
    /// Luu y thu tu o cuoi ham: kiem tra HasAnyFilter TRUOC khi dao (mismatch M17).
    /// </summary>
    public static IEnumerable<Game> ApplyFilter(IEnumerable<Game> games, FilterArgs f)
    {
        if (!f.HasAnyFilter && !f.ReverseFilter) return games;

        return games.Where(g =>
        {
            if (!f.HasAnyFilter) return true;   // ← TRUOC khi dao

            var matches = true;

            // Nam hoan thanh: khop neu CO IT NHAT MOT lan thoa (ANY) — §5.6.
            if (f.FinishedYearValue.HasValue)
            {
                var years = g.Finishes is { Count: > 0 }
                    ? g.Finishes.Select(x => (double?)x.FinishedAt.Year)
                    : (!string.IsNullOrWhiteSpace(g.FinishedIn) && int.TryParse(g.FinishedIn, out var fy)
                        ? [fy]
                        : Enumerable.Empty<double?>());

                matches &= years.Any(y => CompareDouble(y, f.FinishedYearValue.Value, f.FinishedYearOp));
            }

            if (f.ExcitedValue.HasValue)
                matches &= CompareDouble(g.Excited, f.ExcitedValue.Value, f.ExcitedOp);

            if (f.ScoreValue.HasValue)
                matches &= CompareDouble(g.Score, f.ScoreValue.Value, f.ScoreOp);

            if (f.PlaytimeSeconds.HasValue)
                matches &= CompareInt(g.TotalPlayTimeSeconds, f.PlaytimeSeconds.Value, f.PlaytimeOp);

            // Avg: co "None" RIENG, OR voi danh sach dai — §5.4.
            if (f.IncludeAvgNone || f.AvgBands.Length > 0)
            {
                var pass = false;
                if (f.IncludeAvgNone && (g.AvgPlayTime is null || g.AvgPlayTime <= 0)) pass = true;
                if (!pass && f.AvgBands.Length > 0)
                {
                    var band = AvgBand(g.AvgPlayTime);
                    pass = band is not null && f.AvgBands.Contains(band);
                }
                matches &= pass;
            }

            if (f.IncludeMetaNone || f.MetaBands.Length > 0)
            {
                var pass = false;
                if (f.IncludeMetaNone && (g.MetaScore is null || g.MetaScore <= 0)) pass = true;
                if (!pass && f.MetaBands.Length > 0)
                {
                    var band = MetaBand(g.MetaScore);
                    pass = band is not null && f.MetaBands.Contains(band);
                }
                matches &= pass;
            }

            // Progress / Ownership: AND giua nhom, OR trong nhom — §5.8.
            if (f.ProgressKeys.Length > 0)
                matches &= f.ProgressKeys.Contains(ProgressSystemKey(g.Progress));

            if (f.OwnershipKeys.Length > 0)
                matches &= f.OwnershipKeys.Contains(OwnershipSystemKey(g.Ownership));

            // Genre OR Theme — §5.7. Khong phai AND.
            if (f.GenreIds.Length > 0 || f.ThemeIds.Length > 0)
                matches &= g.GameGenres.Any(x => f.GenreIds.Contains(x.GenreId))
                           || g.GameThemes.Any(x => f.ThemeIds.Contains(x.ThemeId));

            return f.ReverseFilter ? !matches : matches;
        });
    }

    private const int Last = int.MaxValue;

    /// <summary>
    /// DOMAIN_RULES §7.1, §7.2 — sort day du moi khoa phu.
    /// Thu hang lay tu SortOrder trong DB, KHONG hardcode (mismatch M22, M23).
    /// </summary>
    public static IEnumerable<Game> ApplySort(IEnumerable<Game> games, string sort, string tab)
    {
        if (sort == "default")
        {
            return tab switch
            {
                "console" => games
                    .OrderBy(game => game.LastPlayedAt.HasValue ? 0 : 1)
                    .ThenByDescending(game => game.LastPlayedAt)
                    .ThenBy(game => game.Name),

                "planning" => games
                    .OrderBy(g => g.Progress?.SortOrder ?? Last)
                    .ThenByDescending(g => g.Excited ?? -1)
                    .ThenBy(g => g.AvgPlayTime ?? Last)
                    .ThenBy(g => g.Name),

                "finished" => games
                    .OrderBy(g => g.Progress?.SortOrder ?? Last)
                    .ThenByDescending(g => g.Score ?? -1)
                    .ThenByDescending(g => LastFinishedAt(g) ?? DateTime.MinValue)
                    .ThenByDescending(g => g.PublishDate)
                    .ThenByDescending(g => g.MetaScore ?? -1),

                "queue" => games
                    // Game DANG CHOI luon dung dau hang cho, du PlayOrder cua no
                    // la bao nhieu. GET tra thu tu nay roi ghi lai PlayOrder, nen
                    // du lieu tu chuan hoa ve dung quy tac.
                    .OrderBy(g => ProgressSystemKey(g.Progress) == "Playing" ? 0 : 1)
                    .ThenBy(g => g.GamePosition?.PlayOrder.HasValue == true ? 0 : 1)
                    .ThenBy(g => g.GamePosition?.PlayOrder ?? Last)
                    .ThenByDescending(g => g.Excited ?? -1)
                    .ThenBy(g => g.AvgPlayTime ?? Last),

                _ => games
                    .OrderByDescending(g => g.Ready)
                    .ThenByDescending(g => g.PublishDate)
                    .ThenByDescending(g => g.Excited ?? -1)
                    .ThenBy(g => g.Name)
                    .ThenBy(g => g.Progress?.SortOrder ?? Last)
                    .ThenBy(g => g.Ownership?.SortOrder ?? Last),
            };
        }

        return sort switch
        {
            "status" => games.OrderByDescending(g => g.Ready)
                             .ThenByDescending(g => g.PublishDate)
                             .ThenBy(g => g.Name),
            "name" => games.OrderBy(g => g.Name),
            "playTime" => games.OrderByDescending(g => g.TotalPlayTimeSeconds ?? -1),
            "publish" => games.OrderByDescending(g => g.PublishDate),
            "finishTime" or "finishedYear" => games
                .OrderByDescending(g => LastFinishedAt(g) ?? DateTime.MinValue)
                .ThenBy(g => g.Name),
            "metaScore" => games.OrderByDescending(g => g.MetaScore ?? -1),
            "excited" => games.OrderByDescending(g => g.Excited ?? -1),
            "score" => games.OrderByDescending(g => g.Score ?? -1),
            "ownership" => games.OrderBy(g => g.Ownership?.SortOrder ?? Last),
            "progress" => games.OrderBy(g => g.Progress?.SortOrder ?? Last),
            "avgPlaytime" => games.OrderByDescending(g => g.AvgPlayTime ?? -1),
            _ => games.OrderByDescending(g => g.Ready),
        };
    }

    /// <summary>
    /// DOMAIN_RULES §7.4 — executes a persistent multi-key profile on the backend.
    /// Null values stay last for both directions and Name is the deterministic tie-breaker.
    /// </summary>
    public static IEnumerable<Game> ApplySortProfile(
        IEnumerable<Game> games,
        IReadOnlyList<SortRuleDto> profile)
    {
        var rules = profile.Count == 0
            ? new List<SortRuleDto> { new("name", "asc") }
            : profile.ToList();

        if (!rules.Any(rule => rule.Key == "name"))
            rules.Add(new SortRuleDto("name", "asc"));

        IOrderedEnumerable<Game>? ordered = null;
        foreach (var rule in rules)
        {
            var selector = ProfileSelector(rule.Key);
            var comparer = new ProfileValueComparer(rule.Direction == "desc");
            ordered = ordered is null
                ? games.OrderBy(selector, comparer)
                : ordered.ThenBy(selector, comparer);
        }

        return ordered ?? games.OrderBy(g => g.Name);
    }

    private static Func<Game, IComparable?> ProfileSelector(string key) => key switch
    {
        "status" => game => game.Ready,
        "name" => game => game.Name,
        "playTime" => game => game.TotalPlayTimeSeconds,
        "publish" => game => game.PublishDate,
        "finishTime" => game => LastFinishedAt(game),
        "metaScore" => game => game.MetaScore,
        "excited" => game => game.Excited,
        "score" => game => game.Score,
        "ownership" => game => game.Ownership?.SortOrder,
        "progress" => game => game.Progress?.SortOrder,
        "avgPlaytime" => game => game.AvgPlayTime,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unsupported profile sort key."),
    };

    private sealed class ProfileValueComparer(bool descending) : IComparer<IComparable?>
    {
        public int Compare(IComparable? left, IComparable? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return 1;
            if (right is null) return -1;
            var comparison = left.CompareTo(right);
            return descending ? -comparison : comparison;
        }
    }

    /// <summary>Thoi diem hoan thanh gan nhat — DOMAIN_RULES §9.3 (sort dung MAX).</summary>
    public static DateTime? LastFinishedAt(Game g)
    {
        if (g.Finishes is { Count: > 0 })
            return g.Finishes.Max(f => f.FinishedAt);

        // Fallback cot cu FinishedIn (deprecated, xem DOMAIN_RULES §9.1).
        if (!string.IsNullOrWhiteSpace(g.FinishedIn) &&
            int.TryParse(g.FinishedIn, out var y) &&
            y is >= 1 and <= 9999)
            return new DateTime(y, 1, 1);

        return null;
    }

    /// <summary>DOMAIN_RULES §17.</summary>
    public static CardHintsDto BuildCardHints(string sort, string tab, string scope) => new(
        ShowDeveloper: scope == "developer",
        ShowPublisher: scope == "publisher" && tab != "all",
        ShowFinishTime: tab != "finished",
        ShowPlaytime: sort == "playTime" && tab != "all",
        ShowExcited: sort == "excited" && tab != "planning",
        ShowScore: sort == "score" && tab != "finished",
        ShowOwnership: true,
        ShowMetaScore: true,
        ShowAvgPlaytime: true,
        ShowPublishDate: true,
        ShowSeries: tab == "all",
        ShowProgress: true);
}
