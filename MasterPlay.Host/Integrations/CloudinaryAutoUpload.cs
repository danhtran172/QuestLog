using System.Collections.Concurrent;
using System.Threading.Channels;
using MasterPlay.Core.Entities;
using MasterPlay.Infrastructure.Integrations.Cloudinary;

namespace MasterPlay.Host.Integrations;

/// <summary>
/// Hang doi game vua luu anh moi, cho <see cref="CloudinaryAutoUploadService"/> dua len Cloudinary.
///
/// Upload chay NEN, sau khi response da tra: luu game khong phai cho Cloudinary,
/// va Cloudinary sap thi luu game van thanh cong voi link goc. Anh hien link goc
/// cho toi lan tai danh sach ke tiep. Hang doi nam trong RAM — Host tat giua
/// chung thi nut "Dong bo anh" (POST /api/tray/sync-images) vet lai.
/// </summary>
public sealed class CloudinaryAutoUploadQueue
{
    private readonly Channel<string> channel = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<string, byte> pending = new(StringComparer.Ordinal);

    internal ChannelReader<string> Reader => channel.Reader;

    /// <summary>Xep hang nhung game co anh chua nam tren Cloudinary cua app.</summary>
    public void EnqueueIfNeeded(IEnumerable<Game> games)
    {
        foreach (var game in games)
        {
            if (!CloudinaryImageService.NeedsUpload(game.BannerPath) &&
                !CloudinaryImageService.NeedsUpload(game.BackdropPath) &&
                !CloudinaryImageService.NeedsUpload(game.PosterPath))
            {
                continue;
            }

            // Game da cho trong hang thi khong xep them: luc den luot, worker doc
            // anh MOI NHAT tu CSDL chu khong doc ban chup luc xep hang.
            if (pending.TryAdd(game.Id, 0))
                channel.Writer.TryWrite(game.Id);
        }
    }

    public void EnqueueIfNeeded(Game game) => EnqueueIfNeeded([game]);

    /// <summary>Goi TRUOC khi xu ly: luu tiep trong luc dang upload se xep hang lai.</summary>
    internal void MarkTaken(string gameId) => pending.TryRemove(gameId, out _);
}

public sealed class CloudinaryAutoUploadService(
    CloudinaryAutoUploadQueue queue,
    ILogger<CloudinaryAutoUploadService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var gameId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            queue.MarkTaken(gameId);

            // Chua cau hinh Cloudinary la lua chon hop le, khong phai loi.
            if (!CloudinaryImageService.IsConfigured) continue;

            try
            {
                var result = await CloudinaryImageService.SyncGameImagesAsync(gameId, stoppingToken);
                foreach (var failure in result.Failures)
                {
                    logger.LogWarning(
                        "Cloudinary auto-upload failed for game {GameId} ({Role}): {Reason}",
                        failure.GameId, failure.Role, failure.Reason);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Cloudinary auto-upload failed for game {GameId}", gameId);
            }
        }
    }
}
