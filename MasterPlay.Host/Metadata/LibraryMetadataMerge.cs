using System.Text.Json;
using MasterPlay.Core.Entities;
using MasterPlay.Infrastructure.Integrations.Cloudinary;

namespace MasterPlay.Host.Metadata;

/// <summary>
/// Anh chup GIA TRI KHO tai lan dong bo gan nhat cua mot dong thu vien.
///
/// Day la moc so sanh, khong phai du lieu hien thi: khong man hinh nao doc no.
/// Chi co nghia trong <see cref="LibraryMetadataMerge"/>.
/// </summary>
public sealed record MetadataBaseline(
    string? Name,
    string? Developer,
    string? Publisher,
    string? PublishDate,
    bool? IsDlc,
    string? BannerUrl,
    string? BackdropUrl,
    string? PosterUrl,
    int? MetaScore,
    int? AvgPlaytimeHours)
{
    public static MetadataBaseline From(MetadataCatalogGame source) => new(
        source.Name,
        source.Developer,
        source.Publisher,
        source.PublishDate?.ToString("yyyy-MM-dd"),
        source.IsDlc,
        source.BannerUrl,
        source.BackdropUrl,
        source.PosterUrl,
        source.MetaScore,
        source.AvgPlaytimeHours);
}

public sealed record LibraryMetadataMergeResult(
    string GameId,
    string Name,
    long MetadataGameId,
    IReadOnlyList<string> Updated,
    IReadOnlyList<string> Kept)
{
    public bool Changed => Updated.Count > 0;
}

/*
 * Kho sua metadata thi dong thu vien cua nguoi dung doi theo — TRU field ma
 * chinh ho da sua tay. Sua tay thang.
 *
 * TAI SAO PHAI CO BASELINE: `PUT /api/games/{id}` nhan mot ban ghi DAY DU —
 * client gui lai MOI field moi lan luu, ke ca field nguoi dung khong cham vao.
 * Vi vay KHONG the nhin request ma biet ho da sua gi. So sanh gia tri hien tai
 * voi gia tri KHO DA TRA VE LAN TRUOC thi biet duoc, bat ke no bi sua qua duong
 * nao — ke ca duong chua ton tai.
 *
 * Ba gia tri cho moi field:
 *   L = dang o thu vien   B = kho noi gi lan truoc   C = kho noi gi lan nay
 *   L == B  -> nguoi dung chua dong vao  -> lay C
 *   L != B  -> nguoi dung da sua         -> giu L
 *
 * B LUON tien len C sau khi hop nhat, ke ca o field vua giu lai. He qua co chu
 * y: nguoi dung go bo sua doi cua minh (dat lai dung gia tri kho dang hien) thi
 * field do tu dong bam theo kho tro lai. "Muon nhan cap nhat lai thi xoa sua
 * doi di" la quy tac giai thich duoc, khong can nut nao.
 *
 * KHI CHUA CO BASELINE (dong noi tu truoc khi co tinh nang nay, hoac JSON hong):
 * chi dien vao o DANG TRONG roi ghi baseline. Khong doan. O trong thi dien vao
 * khong mat gi cua ai; con ghi de mot gia tri khong ro tu dau ra la co the xoa
 * mat cong nguoi dung go tu nhung phien ban truoc.
 */
public static class LibraryMetadataMerge
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize(MetadataBaseline baseline) =>
        JsonSerializer.Serialize(baseline, JsonOptions);

    public static MetadataBaseline? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<MetadataBaseline>(json, JsonOptions);
        }
        catch (JsonException)
        {
            // Baseline hong chi lam mat moc so sanh, khong duoc lam hong ca lan
            // dong bo. Coi nhu chua co: di duong "chi dien o trong".
            return null;
        }
    }

    /// <summary>Ten field trong <see cref="LibraryMetadataMergeResult.Updated"/> la mot o anh.</summary>
    public static bool IsImageField(string field) =>
        field is "bannerPath" or "backdropPath" or "posterPath";

    /// <summary>
    /// Hop nhat mot dong thu vien voi ban ghi kho tuong ung.
    /// <paramref name="apply"/> = false tinh ra ket qua ma khong cham vao game.
    /// </summary>
    public static LibraryMetadataMergeResult Merge(
        Game game,
        MetadataCatalogGame catalog,
        DateTime syncedAt,
        bool apply)
    {
        var baseline = Deserialize(game.MetadataBaseline);
        var hasBaseline = baseline is not null;
        var updated = new List<string>();
        var kept = new List<string>();
        var writes = new List<Action>();

        void Consider<T>(string field, T current, T previous, T incoming, Action commit)
        {
            var take = hasBaseline
                ? Equals(current, previous) && !Equals(current, incoming)
                // Bootstrap: chi dien o trong. Xet tren gia tri DA chuan hoa.
                : IsEmpty(current) && !IsEmpty(incoming);

            if (take)
            {
                updated.Add(field);
                writes.Add(commit);
            }
            else if (hasBaseline && !Equals(current, previous) && !Equals(current, incoming))
            {
                // Chi bao "giu lai" khi that su co xung dot: nguoi dung da sua VA
                // kho dang noi khac. Field nguoi dung sua thanh dung y kho thi
                // khong phai xung dot, bao ra chi lam nhieu bao cao.
                kept.Add(field);
            }
        }

        // Ten khong duoc thanh rong: cot NOT NULL, va `SaveGame` chan ten trong.
        // Kho khai bao `name` NOT NULL nhung day la du lieu mang ve tu ngoai.
        if (!string.IsNullOrWhiteSpace(catalog.Name))
        {
            Consider("name", Norm(game.Name), Norm(baseline?.Name), Norm(catalog.Name),
                () => game.Name = catalog.Name.Trim());
        }

        Consider("developer", Norm(game.Developer), Norm(baseline?.Developer), Norm(catalog.Developer),
            () => game.Developer = Norm(catalog.Developer));
        Consider("publisher", Norm(game.Publisher), Norm(baseline?.Publisher), Norm(catalog.Publisher),
            () => game.Publisher = Norm(catalog.Publisher));
        Consider("publishDate", Date(game.PublishDate), baseline?.PublishDate, Date(catalog.PublishDate),
            () => game.PublishDate = catalog.PublishDate);
        Consider("isDlc", (bool?)game.IsDlc, baseline?.IsDlc, (bool?)catalog.IsDlc,
            () => game.IsDlc = catalog.IsDlc);

        // Ba vai tro anh — DOMAIN_RULES §12. Doi ung MOT-MOT, khong doi cho nhau.
        // O anh dang la ban Cloudinary app tu upload thi so bang LINK GOC cua no:
        // ban copy khac URL nhung khong phai nguoi dung sua tay.
        string? Image(string role, string? current) =>
            Norm(GameImageSources.Origin(game.ImageSources, role, current));

        Consider("bannerPath", Image(GameImageSources.Banner, game.BannerPath), Norm(baseline?.BannerUrl), Norm(catalog.BannerUrl),
            () => game.BannerPath = Norm(catalog.BannerUrl));
        Consider("backdropPath", Image(GameImageSources.Backdrop, game.BackdropPath), Norm(baseline?.BackdropUrl), Norm(catalog.BackdropUrl),
            () => game.BackdropPath = Norm(catalog.BackdropUrl));
        Consider("posterPath", Image(GameImageSources.Poster, game.PosterPath), Norm(baseline?.PosterUrl), Norm(catalog.PosterUrl),
            () => game.PosterPath = Norm(catalog.PosterUrl));

        // `MetaScore` va `AvgPlayTime` mac dinh 0 trong DB chu khong NULL, nen 0
        // o day la "chua co" — xem `Zero`. Khong game nao meta score 0 that.
        Consider("metaScore", Zero(game.MetaScore), Zero(baseline?.MetaScore), Zero(catalog.MetaScore),
            () => game.MetaScore = catalog.MetaScore);
        Consider("avgPlaytimeHours", Zero(game.AvgPlayTime), Zero(baseline?.AvgPlaytimeHours), Zero(catalog.AvgPlaytimeHours),
            () => game.AvgPlayTime = catalog.AvgPlaytimeHours);

        if (apply)
        {
            foreach (var write in writes) write();

            // Baseline va revision tien len KE CA khi khong field nao doi: lan sau
            // moi biet "kho noi gi lan truoc" la gi.
            game.MetadataBaseline = Serialize(MetadataBaseline.From(catalog));
            game.MetadataRevision = catalog.Revision;
            game.MetadataSyncedAt = syncedAt;
        }

        return new LibraryMetadataMergeResult(game.Id, game.Name, catalog.Id, updated, kept);
    }

    /// <summary>
    /// Ghi baseline luc noi dong thu vien vao kho lan dau (import).
    /// </summary>
    public static void Record(Game game, MetadataCatalogGame catalog, DateTime syncedAt)
    {
        game.MetadataGameId = catalog.Id;
        game.MetadataRevision = catalog.Revision;
        game.MetadataSyncedAt = syncedAt;
        game.MetadataBaseline = Serialize(MetadataBaseline.From(catalog));
    }

    // NULL va chuoi rong la CUNG MOT trang thai o day. Thu vien luu "chua co"
    // bang NULL, con kho co the tra ve "" — khong chuan hoa thi moi lan dong bo
    // lai thay mot "thay doi" khong co that.
    private static string? Norm(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Date(DateOnly? value) => value?.ToString("yyyy-MM-dd");
    private static int? Zero(int? value) => value is null or 0 ? null : value;

    private static bool IsEmpty<T>(T value) => value switch
    {
        null => true,
        string text => string.IsNullOrWhiteSpace(text),
        // `false` khong phai "trong" — la mot gia tri. Bootstrap khong duoc lat
        // `isDlc` cua ai, vi moi dong deu dang false hoac true mot cach co y.
        _ => false,
    };
}
