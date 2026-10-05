using System.Collections.Concurrent;
using System.Threading.Channels;
using MasterPlay.Core.Entities;
using MasterPlay.Infrastructure.Integrations.Images;

namespace MasterPlay.Host.Integrations;

/// <summary>
/// Hang doi game vua luu anh moi, cho <see cref="ImageAutoSyncService"/> dua ve
/// noi luu cuoi cung: Cloudinary neu chu thu vien la admin, con lai luu tren may
/// (<see cref="GameImageSync"/>, DOMAIN_RULES §12).
///
/// Chay NEN, sau khi response da tra: luu game khong phai cho tai anh, va nguon
/// anh loi thi luu game van thanh cong voi link goc. Anh hien link goc cho toi lan
/// tai danh sach ke tiep. Hang doi nam trong RAM — Host tat giua chung thi nut
/// "Dong bo anh" (POST /api/tray/sync-images) vet lai.
/// </summary>
public sealed class ImageAutoSyncQueue
{
    private readonly Channel<string> channel = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<string, byte> pending = new(StringComparer.Ordinal);

    internal ChannelReader<string> Reader => channel.Reader;

    /// <summary>
    /// Xep hang game co anh. Noi luu phu thuoc chu thu vien nen worker moi quyet
    /// dinh; anh da o dung cho thi worker bo qua ma khong goi mang.
    /// </summary>
    public void EnqueueIfNeeded(IEnumerable<Game> games)
    {
        foreach (var game in games)
        {
            if (string.IsNullOrWhiteSpace(game.BannerPath) &&
                string.IsNullOrWhiteSpace(game.BackdropPath) &&
                string.IsNullOrWhiteSpace(game.PosterPath))
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

    /// <summary>Goi TRUOC khi xu ly: luu tiep trong luc dang tai se xep hang lai.</summary>
    internal void MarkTaken(string gameId) => pending.TryRemove(gameId, out _);
}

public sealed class ImageAutoSyncService(
    ImageAutoSyncQueue queue,
    ILogger<ImageAutoSyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var gameId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            queue.MarkTaken(gameId);

            try
            {
                var result = await GameImageSync.SyncGameAsync(gameId, stoppingToken);
                foreach (var failure in result.Failures)
                {
                    logger.LogWarning(
                        "Image auto-sync failed for game {GameId} ({Role}): {Reason}",
                        failure.GameId, failure.Role, failure.Reason);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Image auto-sync failed for game {GameId}", gameId);
            }
        }
    }
}
