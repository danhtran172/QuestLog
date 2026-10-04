using MasterPlay.Core.Entities;
using MasterPlay.Infrastructure.Configuration;

namespace MasterPlay.Host;

/// <summary>
/// Persistent default sort profiles for library views. Config owns persistence;
/// LibraryQuery owns execution so the frontend never sorts domain data.
/// </summary>
public static class LibrarySortProfiles
{
    private static readonly string[] Views = ["all", "planning", "finished"];

    private static readonly HashSet<string> AllowedKeys = new(StringComparer.Ordinal)
    {
        "status", "name", "playTime", "publish", "finishTime", "metaScore",
        "excited", "score", "ownership", "progress", "avgPlaytime",
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<SortRuleDto>> Defaults =
        new Dictionary<string, IReadOnlyList<SortRuleDto>>(StringComparer.Ordinal)
        {
            ["all"] =
            [
                new("status", "desc"),
                new("publish", "desc"),
                new("excited", "desc"),
                new("name", "asc"),
                new("progress", "asc"),
                new("ownership", "asc"),
            ],
            ["planning"] =
            [
                new("progress", "asc"),
                new("excited", "desc"),
                new("avgPlaytime", "asc"),
                new("name", "asc"),
            ],
            ["finished"] =
            [
                new("progress", "asc"),
                new("score", "desc"),
                new("finishTime", "desc"),
                new("publish", "desc"),
                new("metaScore", "desc"),
            ],
        };

    public static bool IsConfigurableView(string view) =>
        Views.Contains(view, StringComparer.Ordinal);

    public static ViewSortProfilesDto GetAll() =>
        new(Views.Select(Get).ToList());

    public static ViewSortProfileDto Get(string view)
    {
        if (!IsConfigurableView(view))
            throw new ArgumentException($"Unsupported sort profile view: {view}", nameof(view));

        var config = JsonService.LoadConfig();
        var profiles = config.LibrarySortProfiles ?? new();
        if (!profiles.TryGetValue(view, out var storedRules))
            return new(view, Defaults[view].ToList(), Customized: false);

        var rules = storedRules
            .Select(rule => new SortRuleDto(rule.Key, rule.Direction))
            .ToList();
        return new(view, rules, Customized: true);
    }

    public static IReadOnlyList<SortRuleDto> GetEffectiveRules(string view) => Get(view).Rules;

    public static bool TryValidate(
        string view,
        IReadOnlyList<SortRuleDto>? rules,
        out string code,
        out string message)
    {
        if (!IsConfigurableView(view))
        {
            code = "INVALID_SORT_PROFILE_VIEW";
            message = "Sort profiles are supported only for all, planning, and finished.";
            return false;
        }

        if (rules is null)
        {
            code = "SORT_PROFILE_RULES_REQUIRED";
            message = "Sort profile rules are required.";
            return false;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            if (!AllowedKeys.Contains(rule.Key))
            {
                code = "INVALID_SORT_PROFILE_KEY";
                message = $"Unsupported sort key: {rule.Key}";
                return false;
            }

            if (rule.Direction is not ("asc" or "desc"))
            {
                code = "INVALID_SORT_DIRECTION";
                message = $"Unsupported sort direction: {rule.Direction}";
                return false;
            }

            if (!seen.Add(rule.Key))
            {
                code = "DUPLICATE_SORT_PROFILE_KEY";
                message = $"Sort key appears more than once: {rule.Key}";
                return false;
            }
        }

        code = string.Empty;
        message = string.Empty;
        return true;
    }

    public static ViewSortProfileDto Save(string view, IReadOnlyList<SortRuleDto> rules)
    {
        var config = JsonService.LoadConfig();
        config.LibrarySortProfiles ??= new();
        config.LibrarySortProfiles[view] = rules
            .Select(rule => new LibrarySortRuleConfig
            {
                Key = rule.Key,
                Direction = rule.Direction,
            })
            .ToList();
        JsonService.SaveConfig(config);
        return Get(view);
    }

    public static ViewSortProfileDto Reset(string view)
    {
        var config = JsonService.LoadConfig();
        config.LibrarySortProfiles ??= new();
        if (config.LibrarySortProfiles.Remove(view))
            JsonService.SaveConfig(config);
        return Get(view);
    }
}
