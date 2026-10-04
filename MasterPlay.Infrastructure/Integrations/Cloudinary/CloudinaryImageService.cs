using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using CloudinaryClient = CloudinaryDotNet.Cloudinary;
using MasterPlay.Infrastructure.Configuration;
using MasterPlay.Infrastructure.Data;
using MasterPlay.Infrastructure.Data.Services;
using MasterPlay.Infrastructure.Integrations;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MasterPlay.Infrastructure.Integrations.Cloudinary
{
    /// <summary>
    /// Mot anh khong len duoc Cloudinary. <see cref="Role"/> dung ten vai tro
    /// cua DOMAIN_RULES §12 (banner / backdrop / poster), khong dung ten thu muc.
    /// </summary>
    public sealed record ImageSyncFailure(string GameId, string GameName, string Role, string Reason);

    public class ImageSyncResult
    {
        public int TotalGames { get; set; }

        public int CoverUploaded { get; set; }
        public int CoverAlreadySynced { get; set; }
        public int CoverMissing { get; set; }

        public int CardCoverUploaded { get; set; }
        public int CardCoverAlreadySynced { get; set; }
        public int CardCoverMissing { get; set; }

        public int PortraitCoverUploaded { get; set; }
        public int PortraitCoverAlreadySynced { get; set; }
        public int PortraitCoverMissing { get; set; }

        /// <summary>Tung anh loi kem LY DO — chi dem so thi nguoi dung khong biet sua gi.</summary>
        public List<ImageSyncFailure> Failures { get; } = new();

        public int Failed => Failures.Count;
    }

    public static class CloudinaryImageService
    {
        private static CloudinaryClient? _cloudinary;
        private static long _credentialRevision = -1;

        /// <summary>So game xu ly cung luc. Moi game toi da ba lan upload tuan tu.</summary>
        private const int MaxParallelGames = 4;

        private const string RootFolder = "QuestLog";
        /// <summary>Ten thu muc cu tren Cloudinary — anh da tai len van nam o day.</summary>
        private const string LegacyRootFolder = "MasterPlay";
        private const string CoverFolder = RootFolder + "/Covers";
        private const string CardCoverFolder = RootFolder + "/CardCovers";
        private const string PortraitCoverFolder = RootFolder + "/PortraitCovers";

        private enum Slot { Banner, Backdrop, Poster }

        private sealed record GameImages(string Id, string Name, string? BannerPath, string? BackdropPath, string? PosterPath);

        private sealed record UploadOutcome(string? Url, string? Error);

        /// <summary>Co du ba gia tri credential (Vault cua Host truoc, roi toi config.json).</summary>
        public static bool IsConfigured
        {
            get
            {
                var (cloudName, apiKey, apiSecret) = ResolveCredentials();
                return !string.IsNullOrWhiteSpace(cloudName) &&
                       !string.IsNullOrWhiteSpace(apiKey) &&
                       !string.IsNullOrWhiteSpace(apiSecret);
            }
        }

        private static (string? CloudName, string? ApiKey, string? ApiSecret) ResolveCredentials()
        {
            var config = JsonService.LoadConfig();
            var cloudName = RuntimeIntegrationCredentials.TryGet("cloudinary", "cloudName", out var configuredCloudName)
                ? configuredCloudName
                : config.CloudinaryCloudName;
            var apiKey = RuntimeIntegrationCredentials.TryGet("cloudinary", "apiKey", out var configuredApiKey)
                ? configuredApiKey
                : config.CloudinaryApiKey;
            var apiSecret = RuntimeIntegrationCredentials.TryGet("cloudinary", "apiSecret", out var configuredApiSecret)
                ? configuredApiSecret
                : config.CloudinaryApiSecret;
            return (cloudName, apiKey, apiSecret);
        }

        private static CloudinaryClient GetClient()
        {
            var revision = RuntimeIntegrationCredentials.Revision("cloudinary");
            if (_cloudinary != null && _credentialRevision == revision) return _cloudinary;

            var (cloudName, apiKey, apiSecret) = ResolveCredentials();
            if (string.IsNullOrWhiteSpace(cloudName) ||
                string.IsNullOrWhiteSpace(apiKey) ||
                string.IsNullOrWhiteSpace(apiSecret))
            {
                throw new InvalidOperationException("Chua cau hinh Cloudinary trong config.json");
            }

            var account = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new CloudinaryClient(account);
            _credentialRevision = revision;
            return _cloudinary;
        }

        public static bool IsCloudinaryUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            bool isCloudinaryDomain = url.Contains("res.cloudinary.com", StringComparison.OrdinalIgnoreCase);
            // Nhan CA thu muc cu: anh tai len truoc khi doi ten van nam trong
            // /MasterPlay/, khong nhan thi app coi chung la anh nguoi ngoai.
            bool isInAppFolder =
                url.Contains($"/{RootFolder}/", StringComparison.OrdinalIgnoreCase) ||
                url.Contains($"/{LegacyRootFolder}/", StringComparison.OrdinalIgnoreCase);

            return isCloudinaryDomain && isInAppFolder;
        }

        private static string GetFolder(string type) => type switch
        {
            "cover" => CoverFolder,
            "card-cover" => CardCoverFolder,
            "portrait-cover" => PortraitCoverFolder,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown game image type.")
        };

        private static string GetFullPublicId(string gameId, string type) =>
            $"{GetFolder(type)}/{gameId}";

        public static async Task<string?> UploadGameImageAsync(string gameId, string imageSource, string type)
        {
            if (string.IsNullOrWhiteSpace(imageSource))
                return null;

            if (IsCloudinaryUrl(imageSource))
                return imageSource;

            var outcome = await TryUploadAsync(GetClient(), gameId, imageSource, type, CancellationToken.None);
            if (outcome.Error != null)
                Console.WriteLine($"[Cloudinary] Upload that bai ({type}, game {gameId}): {outcome.Error}");
            return outcome.Url;
        }

        /// <summary>
        /// Upload mot anh va tra ve LY DO khi that bai thay vi nuot loi.
        /// Ly do lay tu thong bao loi cua Cloudinary hoac cua tang mang — khong
        /// cai nao chua credential (chu ky nam trong body request, khong nam o day).
        /// </summary>
        private static async Task<UploadOutcome> TryUploadAsync(
            CloudinaryClient cloudinary,
            string gameId,
            string imageSource,
            string type,
            CancellationToken cancellationToken)
        {
            try
            {
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(imageSource),
                    Folder = GetFolder(type),
                    PublicId = gameId,
                    Overwrite = true,
                    Invalidate = true,
                    UseFilename = false,
                    UniqueFilename = false
                };

                var result = await cloudinary.UploadAsync(uploadParams, cancellationToken);

                if (result.StatusCode == System.Net.HttpStatusCode.OK && result.SecureUrl != null)
                    return new UploadOutcome(result.SecureUrl.ToString(), null);

                return new UploadOutcome(null, string.IsNullOrWhiteSpace(result.Error?.Message)
                    ? $"Cloudinary returned HTTP {(int)result.StatusCode}."
                    : result.Error.Message);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException)
            {
                return new UploadOutcome(null, "Cloudinary request timed out.");
            }
            catch (HttpRequestException ex)
            {
                return new UploadOutcome(null, $"Network error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return new UploadOutcome(null, $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        public static async Task DeleteGameImagesAsync(string gameId)
        {
            try
            {
                var cloudinary = GetClient();
                foreach (var type in new[] { "card-cover", "cover", "portrait-cover" })
                {
                    await cloudinary.DestroyAsync(new DeletionParams(GetFullPublicId(gameId, type)));
                }
            }
            catch (Exception)
            {
                Console.WriteLine("[Cloudinary] Delete request failed.");
            }
        }

        /// <summary>
        /// Dua ba anh cua moi game len Cloudinary va thay duong dan trong CSDL.
        ///
        /// <paramref name="ownerKey"/> = null: moi game tren may (Tray). Co gia tri:
        /// chi thu vien cua chu do (Host, theo tai khoan dang dang nhap).
        ///
        /// Chua cau hinh Cloudinary thi nem <see cref="InvalidOperationException"/>
        /// NGAY, khong chay het thu vien roi bao moi anh deu loi.
        /// </summary>
        public static async Task<ImageSyncResult> MigrateAllGameImagesToCloudinaryAsync(
            IProgress<string>? progress = null,
            string? ownerKey = null,
            CancellationToken cancellationToken = default)
        {
            var cloudinary = GetClient();
            DataService.EnsureInitialized();

            List<GameImages> games;
            using (var context = new MasterPlayContext())
            {
                var query = context.Games.AsNoTracking();
                if (ownerKey != null)
                    query = query.Where(game => game.LibraryOwnerKey == ownerKey);

                games = await query
                    .OrderBy(game => game.Name)
                    .Select(game => new GameImages(game.Id, game.Name, game.BannerPath, game.BackdropPath, game.PosterPath))
                    .ToListAsync(cancellationToken);
            }

            var result = new ImageSyncResult { TotalGames = games.Count };
            var resultLock = new object();
            var done = 0;

            await Parallel.ForEachAsync(
                games,
                new ParallelOptions { MaxDegreeOfParallelism = MaxParallelGames, CancellationToken = cancellationToken },
                async (game, token) =>
                {
                    var status = await SyncGameAsync(cloudinary, game, result, resultLock, token);
                    var index = Interlocked.Increment(ref done);
                    progress?.Report($"({index}/{games.Count}) {game.Name} -> {status}");
                });

            progress?.Report("Hoan tat dong bo anh len Cloudinary.");
            return result;
        }

        /// <summary>
        /// Co anh can dua len Cloudinary khong: co link va chua nam trong thu muc
        /// cua app. Dung de khoi xep hang nhung lan luu khong doi anh.
        /// </summary>
        public static bool NeedsUpload(string? imagePath) =>
            !string.IsNullOrWhiteSpace(imagePath) && !IsCloudinaryUrl(imagePath);

        /// <summary>
        /// Dua anh cua MOT game len Cloudinary — dung cho tu upload sau khi luu.
        /// Game khong con ton tai thi tra ve ket qua rong.
        /// </summary>
        public static async Task<ImageSyncResult> SyncGameImagesAsync(string gameId, CancellationToken cancellationToken = default)
        {
            var cloudinary = GetClient();

            GameImages? game;
            using (var context = new MasterPlayContext())
            {
                game = await context.Games.AsNoTracking()
                    .Where(item => item.Id == gameId)
                    .Select(item => new GameImages(item.Id, item.Name, item.BannerPath, item.BackdropPath, item.PosterPath))
                    .SingleOrDefaultAsync(cancellationToken);
            }

            var result = new ImageSyncResult { TotalGames = game is null ? 0 : 1 };
            if (game is not null)
                await SyncGameAsync(cloudinary, game, result, new object(), cancellationToken);
            return result;
        }

        private static async Task<string> SyncGameAsync(
            CloudinaryClient cloudinary,
            GameImages game,
            ImageSyncResult result,
            object resultLock,
            CancellationToken cancellationToken)
        {
            var cardCoverStatus = await SyncSlotAsync(cloudinary, game, Slot.Banner, game.BannerPath, result, resultLock, cancellationToken);
            var coverStatus = await SyncSlotAsync(cloudinary, game, Slot.Backdrop, game.BackdropPath, result, resultLock, cancellationToken);
            var portraitCoverStatus = await SyncSlotAsync(cloudinary, game, Slot.Poster, game.PosterPath, result, resultLock, cancellationToken);
            return $"card cover: {cardCoverStatus}, cover: {coverStatus}, portrait cover: {portraitCoverStatus}";
        }

        private static async Task<string> SyncSlotAsync(
            CloudinaryClient cloudinary,
            GameImages game,
            Slot slot,
            string? source,
            ImageSyncResult result,
            object resultLock,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                lock (resultLock) Count(result, slot, missing: true);
                return "missing";
            }

            if (IsCloudinaryUrl(source))
            {
                lock (resultLock) Count(result, slot, alreadySynced: true);
                return "already synced";
            }

            var outcome = await TryUploadAsync(cloudinary, game.Id, source, FolderType(slot), cancellationToken);
            string? reason = outcome.Error;

            // Chi thay dung COT anh nay, va chi khi no van la gia tri luc bat dau.
            // Ghi lai ca dong game tu ban chup dau phien se de len moi thay doi
            // xay ra trong luc dong bo (gio choi do Tray cong don, sua tay tren web).
            if (outcome.Url != null && !await ReplaceImagePathAsync(game.Id, slot, source, outcome.Url, cancellationToken))
                reason = "Image changed during sync; kept the newer value.";

            lock (resultLock)
            {
                if (reason == null)
                    Count(result, slot, uploaded: true);
                else
                    result.Failures.Add(new ImageSyncFailure(game.Id, game.Name, RoleName(slot), reason));
            }

            return reason == null ? "uploaded" : $"FAILED ({reason})";
        }

        private static async Task<bool> ReplaceImagePathAsync(
            string gameId,
            Slot slot,
            string expected,
            string replacement,
            CancellationToken cancellationToken)
        {
            // Cung mot cau lenh: thay o anh VA ghi lai "ban copy nay tu link goc nao"
            // vao ImageSources (xem GameImageSources). Ten cot/khoa JSON la hang so
            // theo tung nhanh, chi gia tri di qua tham so.
            await using var context = new MasterPlayContext();
            var affected = slot switch
            {
                Slot.Banner => await context.Database.ExecuteSqlInterpolatedAsync(
                    $$"""
                    UPDATE Games SET BannerPath = {{replacement}},
                        ImageSources = json_set(CASE WHEN json_valid(ImageSources) THEN ImageSources ELSE '{}' END,
                            '$.banner', json_object('source', {{expected}}, 'url', {{replacement}}))
                    WHERE Id = {{gameId}} AND BannerPath = {{expected}}
                    """, cancellationToken),
                Slot.Backdrop => await context.Database.ExecuteSqlInterpolatedAsync(
                    $$"""
                    UPDATE Games SET BackdropPath = {{replacement}},
                        ImageSources = json_set(CASE WHEN json_valid(ImageSources) THEN ImageSources ELSE '{}' END,
                            '$.backdrop', json_object('source', {{expected}}, 'url', {{replacement}}))
                    WHERE Id = {{gameId}} AND BackdropPath = {{expected}}
                    """, cancellationToken),
                _ => await context.Database.ExecuteSqlInterpolatedAsync(
                    $$"""
                    UPDATE Games SET PosterPath = {{replacement}},
                        ImageSources = json_set(CASE WHEN json_valid(ImageSources) THEN ImageSources ELSE '{}' END,
                            '$.poster', json_object('source', {{expected}}, 'url', {{replacement}}))
                    WHERE Id = {{gameId}} AND PosterPath = {{expected}}
                    """, cancellationToken),
            };
            return affected == 1;
        }

        private static void Count(ImageSyncResult result, Slot slot, bool uploaded = false, bool alreadySynced = false, bool missing = false)
        {
            switch (slot)
            {
                case Slot.Banner:
                    if (uploaded) result.CardCoverUploaded++;
                    if (alreadySynced) result.CardCoverAlreadySynced++;
                    if (missing) result.CardCoverMissing++;
                    break;
                case Slot.Backdrop:
                    if (uploaded) result.CoverUploaded++;
                    if (alreadySynced) result.CoverAlreadySynced++;
                    if (missing) result.CoverMissing++;
                    break;
                default:
                    if (uploaded) result.PortraitCoverUploaded++;
                    if (alreadySynced) result.PortraitCoverAlreadySynced++;
                    if (missing) result.PortraitCoverMissing++;
                    break;
            }
        }

        private static string FolderType(Slot slot) => slot switch
        {
            Slot.Banner => "card-cover",
            Slot.Backdrop => "cover",
            _ => "portrait-cover",
        };

        private static string RoleName(Slot slot) => slot switch
        {
            Slot.Banner => "banner",
            Slot.Backdrop => "backdrop",
            _ => "poster",
        };
    }
}
