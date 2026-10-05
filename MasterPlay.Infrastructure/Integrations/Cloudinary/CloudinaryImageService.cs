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

        private const string RootFolder = "QuestLog";
        /// <summary>Ten thu muc cu tren Cloudinary — anh da tai len van nam o day.</summary>
        private const string LegacyRootFolder = "MasterPlay";
        private const string CoverFolder = RootFolder + "/Covers";
        private const string CardCoverFolder = RootFolder + "/CardCovers";
        private const string PortraitCoverFolder = RootFolder + "/PortraitCovers";

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
        /// Upload mot anh cho <see cref="Images.GameImageSync"/>. Goi khi CHUA cau hinh
        /// Cloudinary thi nem <see cref="InvalidOperationException"/> — kiem tra
        /// <see cref="IsConfigured"/> truoc.
        /// </summary>
        internal static async Task<(string? Url, string? Error)> UploadAsync(
            string gameId,
            string imageSource,
            string type,
            CancellationToken cancellationToken)
        {
            var outcome = await TryUploadAsync(GetClient(), gameId, imageSource, type, cancellationToken);
            return (outcome.Url, outcome.Error);
        }
    }
}
