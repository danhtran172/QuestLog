using MasterPlay.Infrastructure.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MasterPlay.Infrastructure.Integrations.Images
{
    /// <summary>
    /// Anh cua thu vien KHONG thuoc admin nam tren may, trong
    /// <c>%APPDATA%\QuestLog\images</c>. CSDL luu URL tuong doi
    /// <c>/local-images/{ten file}</c>, Host phuc vu file do (xem DOMAIN_RULES §12).
    ///
    /// Ten file = <c>{gameId}-{role}-{hash noi dung}.{duoi}</c>: doi anh thi doi ten,
    /// nen trinh duyet cache vinh vien duoc ma khong bao gio hien anh cu.
    ///
    /// Chi nhan anh raster nhan dien duoc bang magic bytes. SVG/HTML bi tu choi:
    /// file nay duoc phuc vu tu chinh origin cua app, script trong do se chay duoc.
    /// </summary>
    public static class LocalImageStore
    {
        public const string UrlPrefix = "/local-images/";

        /// <summary>Gioi han mot anh. Anh game lon nhat dang dung (backdrop 1920x1080) chi vai MB.</summary>
        public const int MaxBytes = 25 * 1024 * 1024;

        private static readonly Regex FileNamePattern = new(
            "^[A-Za-z0-9-]{1,120}\\.(jpg|png|gif|webp|avif)$",
            RegexOptions.CultureInvariant);

        private static readonly HttpClient Http = CreateHttpClient();

        public static string DirectoryPath => Path.Combine(AppPaths.GetDataDirectory(), "images");

        public static bool IsLocalUrl(string? value) =>
            value is not null && value.StartsWith(UrlPrefix, StringComparison.Ordinal);

        /// <summary>Ten file hop le (khong co '/', '..', duoi la). Dung ca o Host truoc khi mo file.</summary>
        public static bool IsValidFileName(string? fileName) =>
            fileName is not null && FileNamePattern.IsMatch(fileName);

        /// <summary>
        /// Duong dan file that cua mot URL <c>/local-images/...</c>. Sai dinh dang
        /// hoac file khong con thi tra <c>false</c>.
        /// </summary>
        public static bool TryResolveFile(string? url, out string path)
        {
            path = string.Empty;
            if (!IsLocalUrl(url)) return false;

            var fileName = url!.Substring(UrlPrefix.Length);
            if (!IsValidFileName(fileName)) return false;

            path = Path.Combine(DirectoryPath, fileName);
            return File.Exists(path);
        }

        public static string ContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".avif" => "image/avif",
            _ => "application/octet-stream",
        };

        /// <summary>
        /// Luu anh tu <paramref name="source"/> (http/https, data URL, hoac file anh
        /// tren may) va tra URL <c>/local-images/...</c>. That bai thi tra LY DO,
        /// khong nem — giong cach upload Cloudinary bao loi.
        /// </summary>
        public static async Task<(string? Url, string? Error)> SaveAsync(
            string gameId,
            string role,
            string source,
            CancellationToken cancellationToken)
        {
            byte[] bytes;
            try
            {
                var loaded = await LoadAsync(source.Trim(), cancellationToken);
                if (loaded.Error != null) return (null, loaded.Error);
                bytes = loaded.Bytes!;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException)
            {
                return (null, "Image download timed out.");
            }
            catch (HttpRequestException ex)
            {
                return (null, $"Network error: {ex.Message}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return (null, $"{ex.GetType().Name}: {ex.Message}");
            }

            var extension = SniffExtension(bytes);
            if (extension is null)
                return (null, "The source is not a supported image (JPEG, PNG, GIF, WebP or AVIF).");

            var hash = Convert.ToHexString(SHA256.HashData(bytes), 0, 8).ToLowerInvariant();
            var fileName = $"{SafeId(gameId)}-{role}-{hash}.{extension}";
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                var target = Path.Combine(DirectoryPath, fileName);
                if (!File.Exists(target))
                {
                    // Ghi ra file tam roi doi ten: Host khong bao gio phuc vu nua file.
                    var temp = target + ".tmp";
                    await File.WriteAllBytesAsync(temp, bytes, cancellationToken);
                    File.Move(temp, target, overwrite: true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return (null, $"Cannot write image file: {ex.Message}");
            }

            return (UrlPrefix + fileName, null);
        }

        /// <summary>
        /// Xoa cac ban anh cu cua cung o anh, tru <paramref name="keepUrl"/>. Goi
        /// SAU khi CSDL da tro sang ban moi. Loi xoa file chi de lai rac, khong hong du lieu.
        /// </summary>
        public static void DeleteOtherVersions(string gameId, string role, string keepUrl)
        {
            if (!Directory.Exists(DirectoryPath)) return;

            var keep = IsLocalUrl(keepUrl) ? keepUrl.Substring(UrlPrefix.Length) : null;
            foreach (var file in Directory.EnumerateFiles(DirectoryPath, $"{SafeId(gameId)}-{role}-*"))
            {
                var name = Path.GetFileName(file);
                if (string.Equals(name, keep, StringComparison.Ordinal) || !IsValidFileName(name)) continue;
                try { File.Delete(file); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }

        /// <summary>Duoi file theo magic bytes; <c>null</c> neu khong phai anh raster duoc ho tro.</summary>
        public static string? SniffExtension(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return "jpg";
            if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return "png";
            if (bytes.Length >= 6 && bytes[0] == 'G' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == '8') return "gif";
            if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8)) return "webp";
            if (bytes.Length >= 12 && bytes[4..8].SequenceEqual("ftyp"u8) &&
                (bytes[8..12].SequenceEqual("avif"u8) || bytes[8..12].SequenceEqual("avis"u8))) return "avif";
            return null;
        }

        /// <summary>Byte cua mot data URL base64 (<c>data:image/...;base64,...</c>); <c>null</c> neu sai dinh dang.</summary>
        public static byte[]? TryDecodeDataUrl(string value)
        {
            if (!value.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return null;

            var comma = value.IndexOf(',');
            if (comma < 0) return null;
            var header = value.AsSpan(5, comma - 5);
            if (!header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase)) return null;

            try { return Convert.FromBase64String(value.Substring(comma + 1)); }
            catch (FormatException) { return null; }
        }

        private static async Task<(byte[]? Bytes, string? Error)> LoadAsync(string source, CancellationToken cancellationToken)
        {
            if (source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var decoded = TryDecodeDataUrl(source);
                if (decoded is null) return (null, "The pasted image data is not a valid base64 data URL.");
                return decoded.Length > MaxBytes ? (null, "The image is larger than 25 MB.") : (decoded, null);
            }

            if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return (null, $"The image server returned HTTP {(int)response.StatusCode}.");
                if (response.Content.Headers.ContentLength is > MaxBytes)
                    return (null, "The image is larger than 25 MB.");

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var buffer = new MemoryStream();
                var chunk = new byte[81920];
                int read;
                while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
                {
                    if (buffer.Length + read > MaxBytes) return (null, "The image is larger than 25 MB.");
                    buffer.Write(chunk, 0, read);
                }
                return (buffer.ToArray(), null);
            }

            // Duong dan file tuyet doi tren may (du lieu cu tu Tray).
            if (Path.IsPathFullyQualified(source) && File.Exists(source))
            {
                var info = new FileInfo(source);
                if (info.Length > MaxBytes) return (null, "The image is larger than 25 MB.");
                return (await File.ReadAllBytesAsync(source, cancellationToken), null);
            }

            return (null, "Unsupported image source. Use an http(s) link, a pasted image, or an image file on this PC.");
        }

        private static string SafeId(string gameId) =>
            new string(gameId.Where(c => char.IsAsciiLetterOrDigit(c) || c == '-').Take(64).ToArray());

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("QuestLog/1.0");
            return client;
        }
    }
}
