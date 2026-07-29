using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using System;
using System.Threading.Tasks;

namespace MasterPlay.Models
{
    public class ImageSyncResult
    {
        public int TotalGames { get; set; }

        public int CoverUploaded { get; set; }
        public int CoverAlreadySynced { get; set; }
        public int CoverMissing { get; set; }

        public int GalleryUploaded { get; set; }
        public int GalleryAlreadySynced { get; set; }
        public int GalleryMissing { get; set; }

        public int Failed { get; set; }
    }

    public static class CloudinaryImageService
    {
        private static Cloudinary? _cloudinary;

        // Root folder chua toan bo anh cua app tren Cloudinary
        private const string RootFolder = "MasterPlay";
        private const string CoverFolder = RootFolder + "/Covers";
        private const string GalleryFolder = RootFolder + "/Galleries";

        private static Cloudinary GetClient()
        {
            if (_cloudinary != null) return _cloudinary;

            var config = JsonService.LoadConfig();
            if (string.IsNullOrWhiteSpace(config.CloudinaryCloudName) ||
                string.IsNullOrWhiteSpace(config.CloudinaryApiKey) ||
                string.IsNullOrWhiteSpace(config.CloudinaryApiSecret))
            {
                throw new InvalidOperationException("Chua cau hinh Cloudinary trong config.json");
            }

            var account = new Account(config.CloudinaryCloudName, config.CloudinaryApiKey, config.CloudinaryApiSecret);
            _cloudinary = new Cloudinary(account);
            return _cloudinary;
        }

        /// <summary>
        /// Kiem tra mot link anh da nam dung trong folder MasterPlay tren Cloudinary chua.
        /// Bat buoc 2 dieu kien: dung domain Cloudinary VA nam trong folder "/MasterPlay/".
        /// Neu anh o mien Cloudinary nhung khong thuoc folder nay (vi du upload tay ngoai luong,
        /// hoac tu nguon khac) thi van bi coi la CHUA dong bo va se duoc tai lai dung cho.
        /// </summary>
        public static bool IsCloudinaryUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            bool isCloudinaryDomain = url.Contains("res.cloudinary.com", StringComparison.OrdinalIgnoreCase);
            bool isInMasterPlayFolder = url.Contains($"/{RootFolder}/", StringComparison.OrdinalIgnoreCase);

            return isCloudinaryDomain && isInMasterPlayFolder;
        }

        private static string GetFolder(string type) =>
            type == "cover" ? CoverFolder : GalleryFolder;

        private static string GetFullPublicId(string gameId, string type) =>
            $"{GetFolder(type)}/{gameId}";

        /// <summary>
        /// Upload anh (tu URL hoac file local) len Cloudinary vao folder MasterPlay/Covers
        /// hoac MasterPlay/Galleries, ghi de anh cu cung loai.
        /// type: "cover" hoac "gallery"
        /// </summary>
        public static async Task<string?> UploadGameImageAsync(string gameId, string imageSource, string type)
        {
            if (string.IsNullOrWhiteSpace(imageSource))
                return null;

            if (IsCloudinaryUrl(imageSource))
                return imageSource;

            try
            {
                var cloudinary = GetClient();

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

                var result = await cloudinary.UploadAsync(uploadParams);

                if (result.StatusCode == System.Net.HttpStatusCode.OK && result.SecureUrl != null)
                    return result.SecureUrl.ToString();

                Console.WriteLine($"[Cloudinary] Upload that bai ({type}, game {gameId}): {result.Error?.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cloudinary Error] {ex.Message}");
                return null;
            }
        }

        public static async Task DeleteGameImagesAsync(string gameId)
        {
            try
            {
                var cloudinary = GetClient();
                foreach (var type in new[] { "cover", "gallery" })
                {
                    await cloudinary.DestroyAsync(new DeletionParams(GetFullPublicId(gameId, type)));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cloudinary Delete Error] {ex.Message}");
            }
        }

        /// <summary>
        /// Quet toan bo game trong DB. Voi tung anh (cover/gallery):
        /// - Neu da la link Cloudinary VA nam dung folder MasterPlay -> "AlreadySynced", KHONG tai lai.
        /// - Neu rong (chua co anh) -> "Missing", khong lam gi.
        /// - Neu co link nhung chua dung chuan (chua upload, hoac cloudinary nhung sai folder)
        ///   -> tai len lai dung folder va tinh vao "Uploaded".
        /// Moi game deu duoc log chi tiet qua progress de biet ly do cu the.
        /// </summary>
        public static async Task<ImageSyncResult> MigrateAllGameImagesToCloudinaryAsync(IProgress<string>? progress = null)
        {
            var result = new ImageSyncResult();
            var games = DataService.LoadGames();
            result.TotalGames = games.Count;

            int index = 0;
            foreach (var game in games)
            {
                index++;
                bool changed = false;
                string coverStatus;
                string galleryStatus;

                // ==== Cover ====
                if (string.IsNullOrWhiteSpace(game.ImagePath))
                {
                    result.CoverMissing++;
                    coverStatus = "missing";
                }
                else if (IsCloudinaryUrl(game.ImagePath))
                {
                    result.CoverAlreadySynced++;
                    coverStatus = "already synced";
                }
                else
                {
                    var url = await UploadGameImageAsync(game.Id, game.ImagePath, "cover");
                    if (url != null)
                    {
                        game.ImagePath = url;
                        changed = true;
                        result.CoverUploaded++;
                        coverStatus = "uploaded";
                    }
                    else
                    {
                        result.Failed++;
                        coverStatus = "FAILED";
                    }
                }

                // ==== Gallery ====
                if (string.IsNullOrWhiteSpace(game.GImagePath))
                {
                    result.GalleryMissing++;
                    galleryStatus = "missing";
                }
                else if (IsCloudinaryUrl(game.GImagePath))
                {
                    result.GalleryAlreadySynced++;
                    galleryStatus = "already synced";
                }
                else
                {
                    var url = await UploadGameImageAsync(game.Id, game.GImagePath, "gallery");
                    if (url != null)
                    {
                        game.GImagePath = url;
                        changed = true;
                        result.GalleryUploaded++;
                        galleryStatus = "uploaded";
                    }
                    else
                    {
                        result.Failed++;
                        galleryStatus = "FAILED";
                    }
                }

                if (changed)
                {
                    DataService.UpdateGame(game);
                }

                progress?.Report(
                    $"({index}/{games.Count}) {game.Name} -> cover: {coverStatus}, gallery: {galleryStatus}");
            }

            progress?.Report("Hoan tat dong bo anh len Cloudinary.");
            return result;
        }
    }
}