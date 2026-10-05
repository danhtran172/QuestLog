using System.Globalization;
using System.Text;

namespace MasterPlay.Infrastructure.Integrations;

/// <summary>
/// Do do giong nhau giua hai ten game, dung chung cho kho metadata va RAWG.
///
/// Truoc day moi nguon lay KET QUA DAU TIEN cua o tim kiem, nen banner (Steam),
/// backdrop (RAWG) va poster (IGDB) co the den tu ba game khac nhau. Moi nguon gio
/// chon ung vien giong ten nhat va bo qua neu khong ung vien nao du giong.
///
/// Diem so la he so Dice tren cap ky tu cua ten da chuan hoa (0..1). Chuan hoa bo
/// dau, ky hieu ™/®, dau cau, va doi so La Ma dung rieng thanh so ("Hades II" =
/// "Hades 2").
/// </summary>
public static class GameNameMatcher
{
    /// <summary>Duoi nguong nay coi la KHAC game — tra null thay vi lay bua.</summary>
    public const double DefaultMinScore = 0.5;

    private static readonly Dictionary<string, string> RomanNumerals = new(StringComparer.Ordinal)
    {
        ["ii"] = "2", ["iii"] = "3", ["iv"] = "4", ["v"] = "5", ["vi"] = "6",
        ["vii"] = "7", ["viii"] = "8", ["ix"] = "9", ["x"] = "10",
    };

    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var decomposed = name.Trim().ToLowerInvariant().Replace("&", " and ").Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        var tokens = builder.ToString()
            .Replace('đ', 'd')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(token => RomanNumerals.TryGetValue(token, out var digit) ? digit : token);
        return string.Join(' ', tokens);
    }

    /// <summary>
    /// Cac tu NGUYEN BAN (chi ha chu, tach theo ky tu khong phai chu/so) de dua vao o
    /// tim kiem. KHONG doi so La Ma hay bo dau: chi muc tim kiem luu dung chu goc,
    /// "Hades 2" se khong khop lexeme "ii" cua "Hades II".
    /// </summary>
    public static IReadOnlyList<string> SearchTokens(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return [];
        var words = new List<string>();
        var current = new StringBuilder();
        foreach (var character in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                current.Append(character);
                continue;
            }
            if (current.Length > 0) words.Add(current.ToString());
            current.Clear();
        }
        if (current.Length > 0) words.Add(current.ToString());
        return words;
    }

    /// <summary>
    /// Diem giong nhau 0..1 cua ten <paramref name="candidate"/> so voi ten nguoi dung
    /// go (<paramref name="query"/>): nua Dice tren cap ky tu, nua ti le TU cua query
    /// co mat trong candidate. Dice mot minh thich ten NGAN ("Witcher 3" ra "The
    /// Witcher" thay vi "The Witcher 3: Wild Hunt"), nen can them phan theo tu.
    ///
    /// So hieu phan (2, 3, II...) la ten cua mot game KHAC: lech so thi nhan 0.6.
    /// </summary>
    public static double Similarity(string? query, string? candidate)
    {
        var normalizedQuery = Normalize(query);
        var normalizedCandidate = Normalize(candidate);
        if (normalizedQuery.Length == 0 || normalizedCandidate.Length == 0) return 0;
        if (string.Equals(normalizedQuery, normalizedCandidate, StringComparison.Ordinal)) return 1;

        var queryTokens = normalizedQuery.Split(' ');
        var candidateTokens = normalizedCandidate.Split(' ').ToHashSet(StringComparer.Ordinal);
        var recall = queryTokens.Count(candidateTokens.Contains) / (double)queryTokens.Length;
        var score = 0.5 * Dice(normalizedQuery, normalizedCandidate) + 0.5 * recall;

        var queryNumbers = queryTokens.Where(IsNumber).ToHashSet(StringComparer.Ordinal);
        var candidateNumbers = candidateTokens.Where(IsNumber).ToHashSet(StringComparer.Ordinal);
        return queryNumbers.SetEquals(candidateNumbers) ? score : score * 0.6;
    }

    private static bool IsNumber(string token) => token.All(char.IsDigit);

    /// <summary>
    /// Moi tu nguoi dung go deu phai la tien to cua mot tu TRONG TEN ung vien
    /// ("shadow" khop "Shadows"). Thieu mot tu la game KHAC: "Assassin's Creed Shadow"
    /// khong bao gio duoc khop sang "Assassin's Creed IV Black Flag".
    /// </summary>
    public static bool CoversAllWords(string? query, string? candidate)
    {
        var candidateTokens = Normalize(candidate).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var queryTokens = Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return queryTokens.Length > 0 && queryTokens.All(word =>
            candidateTokens.Any(token => token.StartsWith(word, StringComparison.Ordinal)));
    }

    private static double Dice(string left, string right)
    {
        var a = left.Replace(" ", string.Empty, StringComparison.Ordinal);
        var b = right.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (a.Length == 0 || b.Length == 0) return 0;
        if (string.Equals(a, b, StringComparison.Ordinal)) return 1;
        if (a.Length < 2 || b.Length < 2) return 0;

        var bigrams = new Dictionary<int, int>();
        for (var index = 0; index < a.Length - 1; index++)
        {
            var key = (a[index] << 16) | a[index + 1];
            bigrams[key] = bigrams.GetValueOrDefault(key) + 1;
        }

        var common = 0;
        for (var index = 0; index < b.Length - 1; index++)
        {
            var key = (b[index] << 16) | b[index + 1];
            if (bigrams.TryGetValue(key, out var count) && count > 0)
            {
                bigrams[key] = count - 1;
                common++;
            }
        }

        return 2.0 * common / (a.Length - 1 + b.Length - 1);
    }

    /// <summary>
    /// Ung vien giong <paramref name="query"/> nhat, hoac null khi khong ung vien
    /// nao dat <paramref name="minScore"/>. Hoa diem: uu tien ten bat dau bang chuoi
    /// tim kiem, roi ten ngan hon (ban goc truoc ban mo rong).
    /// </summary>
    public static T? PickBest<T>(
        string query,
        IEnumerable<T> candidates,
        Func<T, string?> nameOf,
        double minScore = DefaultMinScore)
        where T : class
    {
        var normalizedQuery = Normalize(query);
        return candidates
            .Select(candidate => (Candidate: candidate, Name: Normalize(nameOf(candidate)), Score: Similarity(query, nameOf(candidate))))
            .Where(item => item.Name.Length > 0 && item.Score >= minScore)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Name.StartsWith(normalizedQuery, StringComparison.Ordinal))
            .ThenBy(item => item.Name.Length)
            .Select(item => item.Candidate)
            .FirstOrDefault();
    }
}
