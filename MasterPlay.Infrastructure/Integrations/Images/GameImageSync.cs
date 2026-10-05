using MasterPlay.Infrastructure.Data;
using MasterPlay.Infrastructure.Data.Services;
using MasterPlay.Infrastructure.Integrations.Cloudinary;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MasterPlay.Infrastructure.Integrations.Images
{
    /// <summary>
    /// Dua ba anh cua game ve noi luu cuoi cung va thay duong dan trong CSDL.
    /// Noi luu theo CHU THU VIEN (<see cref="ImageStorageOwners"/>):
    /// admin -> Cloudinary, moi chu khac -> <see cref="LocalImageStore"/>. DOMAIN_RULES §12.
    ///
    /// Moi luong ghi anh luu link GOC truoc; hang doi nen cua Host, nut "Dong bo
    /// anh", menu Tray va lenh CLI deu di qua day.
    /// </summary>
    public static class GameImageSync
    {
        /// <summary>So game xu ly cung luc. Moi game toi da ba lan tai tuan tu.</summary>
        private const int MaxParallelGames = 4;

        private const string CloudinaryNotConfigured = "Cloudinary is not configured.";

        private enum Slot { Banner, Backdrop, Poster }

        private sealed record GameImages(
            string Id,
            string Name,
            string OwnerKey,
            string? BannerPath,
            string? BackdropPath,
            string? PosterPath,
            string? ImageSources);

        /// <summary>
        /// O anh nay con phai xu ly khong: co gia tri va chua nam o noi luu cua chu.
        /// Dung de khoi xep hang nhung lan luu khong doi anh.
        /// </summary>
        public static bool NeedsWork(bool usesCloud, string? imagePath) =>
            !string.IsNullOrWhiteSpace(imagePath) &&
            (usesCloud ? !CloudinaryImageService.IsCloudinaryUrl(imagePath) : !LocalImageStore.IsLocalUrl(imagePath));

        /// <summary>
        /// Dong bo moi game. <paramref name="ownerKey"/> = null: moi game tren may
        /// (Tray, CLI). Co gia tri: chi thu vien cua chu do (Host).
        /// Thu vien admin ma chua cau hinh Cloudinary thi tung anh bao loi kem ly do.
        /// Tai khoan chua tung dang nhap tren Host nay (khong biet role) bi bo qua,
        /// khong tinh vao ket qua.
        /// </summary>
        public static async Task<ImageSyncResult> SyncAllAsync(
            IProgress<string>? progress = null,
            string? ownerKey = null,
            CancellationToken cancellationToken = default)
        {
            DataService.EnsureInitialized();

            List<GameImages> games;
            using (var context = new MasterPlayContext())
            {
                var query = context.Games.AsNoTracking();
                if (ownerKey != null)
                    query = query.Where(game => game.LibraryOwnerKey == ownerKey);

                games = await query
                    .OrderBy(game => game.Name)
                    .Select(game => new GameImages(
                        game.Id, game.Name, game.LibraryOwnerKey,
                        game.BannerPath, game.BackdropPath, game.PosterPath, game.ImageSources))
                    .ToListAsync(cancellationToken);
            }

            var owners = await ImageStorageOwners.LoadAsync(cancellationToken);
            var unknownOwners = games
                .Where(game => ImageStorageOwners.Resolve(owners, game.OwnerKey) is null)
                .Select(game => game.OwnerKey)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var owner in unknownOwners)
                progress?.Report($"Skipped library {owner}: account has not signed in on this PC since image storage became per-role.");
            games = games.Where(game => !unknownOwners.Contains(game.OwnerKey)).ToList();

            var result = new ImageSyncResult { TotalGames = games.Count };
            var resultLock = new object();
            var done = 0;

            await Parallel.ForEachAsync(
                games,
                new ParallelOptions { MaxDegreeOfParallelism = MaxParallelGames, CancellationToken = cancellationToken },
                async (game, token) =>
                {
                    var usesCloud = ImageStorageOwners.Resolve(owners, game.OwnerKey) == true;
                    var status = await SyncGameAsync(game, usesCloud, skipUnconfiguredCloud: false, result, resultLock, token);
                    var index = Interlocked.Increment(ref done);
                    progress?.Report($"({index}/{games.Count}) {game.Name} [{(usesCloud ? "cloud" : "local")}] -> {status}");
                });

            progress?.Report("Image sync complete.");
            return result;
        }

        /// <summary>
        /// Dong bo anh cua MOT game — hang doi nen sau khi luu. Thu vien admin ma
        /// chua cau hinh Cloudinary, hoac chu chua biet role, thi giu link goc va
        /// KHONG bao loi. Game khong con thi tra ket qua rong.
        /// </summary>
        public static async Task<ImageSyncResult> SyncGameAsync(string gameId, CancellationToken cancellationToken = default)
        {
            GameImages? game;
            using (var context = new MasterPlayContext())
            {
                game = await context.Games.AsNoTracking()
                    .Where(item => item.Id == gameId)
                    .Select(item => new GameImages(
                        item.Id, item.Name, item.LibraryOwnerKey,
                        item.BannerPath, item.BackdropPath, item.PosterPath, item.ImageSources))
                    .SingleOrDefaultAsync(cancellationToken);
            }

            var usesCloud = game is null ? null : await ImageStorageOwners.UsesCloudAsync(game.OwnerKey, cancellationToken);
            var result = new ImageSyncResult { TotalGames = usesCloud is null ? 0 : 1 };
            if (game is not null && usesCloud is not null)
                await SyncGameAsync(game, usesCloud.Value, skipUnconfiguredCloud: true, result, new object(), cancellationToken);
            return result;
        }

        private static async Task<string> SyncGameAsync(
            GameImages game,
            bool usesCloud,
            bool skipUnconfiguredCloud,
            ImageSyncResult result,
            object resultLock,
            CancellationToken cancellationToken)
        {
            var banner = await SyncSlotAsync(game, Slot.Banner, game.BannerPath, usesCloud, skipUnconfiguredCloud, result, resultLock, cancellationToken);
            var backdrop = await SyncSlotAsync(game, Slot.Backdrop, game.BackdropPath, usesCloud, skipUnconfiguredCloud, result, resultLock, cancellationToken);
            var poster = await SyncSlotAsync(game, Slot.Poster, game.PosterPath, usesCloud, skipUnconfiguredCloud, result, resultLock, cancellationToken);
            return $"banner: {banner}, backdrop: {backdrop}, poster: {poster}";
        }

        private static async Task<string> SyncSlotAsync(
            GameImages game,
            Slot slot,
            string? source,
            bool usesCloud,
            bool skipUnconfiguredCloud,
            ImageSyncResult result,
            object resultLock,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                lock (resultLock) Count(result, slot, missing: true);
                return "missing";
            }

            var role = RoleName(slot);
            // Link goc that su cua o anh (ban copy Cloudinary/local -> link no copy tu do).
            // Ghi lai link GOC, khong ghi ban copy trung gian: metadata-refresh so
            // bang link goc, ghi ban copy vao thi anh bi coi nhu nguoi dung sua tay.
            var origin = GameImageSources.Origin(game.ImageSources, role, source) ?? source;

            (string? Url, string? Error) outcome;
            if (usesCloud)
            {
                if (CloudinaryImageService.IsCloudinaryUrl(source))
                {
                    lock (resultLock) Count(result, slot, alreadySynced: true);
                    return "already on Cloudinary";
                }

                if (!CloudinaryImageService.IsConfigured)
                {
                    if (skipUnconfiguredCloud) return "kept original (Cloudinary not configured)";
                    outcome = (null, CloudinaryNotConfigured);
                }
                else if (LocalImageStore.IsLocalUrl(source))
                {
                    outcome = LocalImageStore.TryResolveFile(source, out var file)
                        ? await CloudinaryImageService.UploadAsync(game.Id, file, FolderType(slot), cancellationToken)
                        : (null, "The local image file is missing.");
                }
                else
                {
                    outcome = await CloudinaryImageService.UploadAsync(game.Id, source, FolderType(slot), cancellationToken);
                }
            }
            else
            {
                var fetchFrom = source;
                if (LocalImageStore.IsLocalUrl(source))
                {
                    if (LocalImageStore.TryResolveFile(source, out _))
                    {
                        lock (resultLock) Count(result, slot, alreadySynced: true);
                        return "already on this PC";
                    }

                    // File da mat: tai lai tu link goc neu con biet.
                    fetchFrom = LocalImageStore.IsLocalUrl(origin) ? null : origin;
                }

                outcome = fetchFrom is null
                    ? (null, "The local image file is missing.")
                    : await LocalImageStore.SaveAsync(game.Id, role, fetchFrom, cancellationToken);
            }

            var reason = outcome.Error;
            // Chi thay dung COT anh nay, va chi khi no van la gia tri luc bat dau.
            if (outcome.Url != null)
            {
                if (await ReplaceImagePathAsync(game.Id, slot, source, outcome.Url, origin, cancellationToken))
                    LocalImageStore.DeleteOtherVersions(game.Id, role, outcome.Url);
                else
                    reason = "Image changed during sync; kept the newer value.";
            }

            lock (resultLock)
            {
                if (reason == null)
                    Count(result, slot, uploaded: true);
                else
                    result.Failures.Add(new ImageSyncFailure(game.Id, game.Name, role, reason));
            }

            return reason == null ? (usesCloud ? "uploaded" : "saved on this PC") : $"FAILED ({reason})";
        }

        private static async Task<bool> ReplaceImagePathAsync(
            string gameId,
            Slot slot,
            string expected,
            string replacement,
            string origin,
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
                            '$.banner', json_object('source', {{origin}}, 'url', {{replacement}}))
                    WHERE Id = {{gameId}} AND BannerPath = {{expected}}
                    """, cancellationToken),
                Slot.Backdrop => await context.Database.ExecuteSqlInterpolatedAsync(
                    $$"""
                    UPDATE Games SET BackdropPath = {{replacement}},
                        ImageSources = json_set(CASE WHEN json_valid(ImageSources) THEN ImageSources ELSE '{}' END,
                            '$.backdrop', json_object('source', {{origin}}, 'url', {{replacement}}))
                    WHERE Id = {{gameId}} AND BackdropPath = {{expected}}
                    """, cancellationToken),
                _ => await context.Database.ExecuteSqlInterpolatedAsync(
                    $$"""
                    UPDATE Games SET PosterPath = {{replacement}},
                        ImageSources = json_set(CASE WHEN json_valid(ImageSources) THEN ImageSources ELSE '{}' END,
                            '$.poster', json_object('source', {{origin}}, 'url', {{replacement}}))
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
            Slot.Banner => GameImageSources.Banner,
            Slot.Backdrop => GameImageSources.Backdrop,
            _ => GameImageSources.Poster,
        };
    }
}
