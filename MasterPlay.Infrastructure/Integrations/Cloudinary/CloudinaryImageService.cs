using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using CloudinaryClient = CloudinaryDotNet.Cloudinary;
using MasterPlay.Infrastructure.Configuration;
using MasterPlay.Infrastructure.Data.Services;
using System;
using System.Threading.Tasks;

namespace MasterPlay.Infrastructure.Integrations.Cloudinary
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

        public int SteamLibraryUploaded { get; set; }
        public int SteamLibraryAlreadySynced { get; set; }
        public int SteamLibraryMissing { get; set; }

        public int Failed { get; set; }
    }

    public static class CloudinaryImageService
    {
        private static CloudinaryClient? _cloudinary;


        private const string RootFolder = "MasterPlay";
        private const string CoverFolder = RootFolder + "/Covers";
        private const string GalleryFolder = RootFolder + "/Galleries";
        private const string SteamLibraryFolder = RootFolder + "/SteamLibraries";

        private static CloudinaryClient GetClient()
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
            _cloudinary = new CloudinaryClient(account);
            return _cloudinary;
        }







        public static bool IsCloudinaryUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            bool isCloudinaryDomain = url.Contains("res.cloudinary.com", StringComparison.OrdinalIgnoreCase);
            bool isInMasterPlayFolder = url.Contains($"/{RootFolder}/", StringComparison.OrdinalIgnoreCase);

            return isCloudinaryDomain && isInMasterPlayFolder;
        }

        private static string GetFolder(string type) => type switch
        {
            "cover" => CoverFolder,
            "gallery" => GalleryFolder,
            "steam-library" => SteamLibraryFolder,
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
                foreach (var type in new[] { "cover", "gallery", "steam-library" })
                {
                    await cloudinary.DestroyAsync(new DeletionParams(GetFullPublicId(gameId, type)));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cloudinary Delete Error] {ex.Message}");
            }
        }









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
                string steamLibraryStatus;


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

                if (string.IsNullOrWhiteSpace(game.SteamLibraryImagePath))
                {
                    result.SteamLibraryMissing++;
                    steamLibraryStatus = "missing";
                }
                else if (IsCloudinaryUrl(game.SteamLibraryImagePath))
                {
                    result.SteamLibraryAlreadySynced++;
                    steamLibraryStatus = "already synced";
                }
                else
                {
                    var url = await UploadGameImageAsync(game.Id, game.SteamLibraryImagePath, "steam-library");
                    if (url != null)
                    {
                        game.SteamLibraryImagePath = url;
                        changed = true;
                        result.SteamLibraryUploaded++;
                        steamLibraryStatus = "uploaded";
                    }
                    else
                    {
                        result.Failed++;
                        steamLibraryStatus = "FAILED";
                    }
                }

                if (changed)
                {
                    DataService.UpdateGame(game);
                }

                progress?.Report(
                    $"({index}/{games.Count}) {game.Name} -> cover: {coverStatus}, gallery: {galleryStatus}, steam library: {steamLibraryStatus}");
            }

            progress?.Report("Hoan tat dong bo anh len Cloudinary.");
            return result;
        }
    }
}
