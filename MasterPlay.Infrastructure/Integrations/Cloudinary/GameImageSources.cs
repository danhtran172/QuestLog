using System;
using System.Collections.Generic;
using System.Text.Json;

namespace MasterPlay.Infrastructure.Integrations.Cloudinary
{
    /// <summary>
    /// Doc cot <c>Games.ImageSources</c>: o anh nao dang la ban Cloudinary do app
    /// upload, va ban do copy tu link goc nao.
    ///
    /// Can vi ban copy co URL KHAC link goc. Khong biet duoc no la copy thi:
    ///   - merge metadata tuong nguoi dung da sua tay anh, nen anh moi tren kho
    ///     khong bao gio ve toi thu vien va moi lan dong bo deu bao "giu lai";
    ///   - form sua gui lai link goc cu (tai truoc khi upload xong) se de len ban copy.
    /// </summary>
    public static class GameImageSources
    {
        public const string Banner = "banner";
        public const string Backdrop = "backdrop";
        public const string Poster = "poster";

        private sealed record Entry(string? Source, string? Url);

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        /// <summary>
        /// Link goc ma <paramref name="current"/> dai dien. O anh khong phai ban
        /// copy (hoac ban copy da bi thay) thi tra lai chinh <paramref name="current"/>.
        /// </summary>
        public static string? Origin(string? imageSourcesJson, string role, string? current)
        {
            if (string.IsNullOrWhiteSpace(current)) return current;

            var entry = Read(imageSourcesJson, role);
            return entry?.Url is { } url &&
                   entry.Source is { } source &&
                   string.Equals(url, current.Trim(), StringComparison.Ordinal)
                ? source
                : current;
        }

        /// <summary>
        /// Gia tri nen luu khi client gui <paramref name="incoming"/> cho mot o anh
        /// dang la <paramref name="current"/>: client gui lai dung link goc cua ban
        /// copy dang co thi giu ban copy.
        /// </summary>
        public static string? KeepUploadedCopy(string? imageSourcesJson, string role, string? current, string? incoming)
        {
            if (incoming is null || current is null || string.Equals(incoming, current, StringComparison.Ordinal))
                return incoming;

            return string.Equals(Origin(imageSourcesJson, role, current), incoming, StringComparison.Ordinal)
                ? current
                : incoming;
        }

        private static Entry? Read(string? json, string role)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var map = JsonSerializer.Deserialize<Dictionary<string, Entry>>(json, JsonOptions);
                return map is not null && map.TryGetValue(role, out var entry) ? entry : null;
            }
            catch (JsonException)
            {
                // Cot hong thi coi nhu khong co ban copy nao: so sanh bang URL hien
                // tai, dung nhu truoc khi co cot nay.
                return null;
            }
        }
    }
}
