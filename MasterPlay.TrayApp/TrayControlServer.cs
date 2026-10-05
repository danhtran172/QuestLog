using System.IO.Pipes;
using MasterPlay.Infrastructure.TrayControl;
using Microsoft.Extensions.Logging;

namespace MasterPlay.TrayApp
{
    /// <summary>
    /// Nghe lenh tu Host (panel "Dich vu nen" trong QuestLog) qua named pipe. Moi ket
    /// noi la mot lenh. Viec that (bat/tat dich vu, quet ngay...) do handler lam tren
    /// UI thread, y nhu khi bam menu.
    /// </summary>
    internal sealed class TrayControlServer : IDisposable
    {
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

        private readonly Func<TrayControlRequest, Task<TrayControlResponse>> _handle;
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _stop = new();

        public TrayControlServer(Func<TrayControlRequest, Task<TrayControlResponse>> handle, ILogger logger)
        {
            _handle = handle;
            _logger = logger;
            _ = Task.Run(AcceptLoopAsync);
        }

        private async Task AcceptLoopAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                NamedPipeServerStream? pipe = null;
                try
                {
                    pipe = new NamedPipeServerStream(
                        TrayControlProtocol.PipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        TrayControlProtocol.Options);
                    await pipe.WaitForConnectionAsync(_stop.Token);

                    // Phuc vu song song: bat/tat dich vu mat vai giay, khong duoc chan
                    // lenh xem trang thai cua lan mo panel tiep theo.
                    _ = ServeAsync(pipe);
                    pipe = null;
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Vd mot tray khac (ban dev) dang giu ten pipe: tray van chay binh thuong,
                    // chi la QuestLog khong dieu khien duoc. Thu lai cham de log khong day dac.
                    _logger.LogWarning(ex, "Tray control pipe failed; retrying.");
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), _stop.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                finally
                {
                    pipe?.Dispose();
                }
            }
        }

        private async Task ServeAsync(NamedPipeServerStream pipe)
        {
            using (pipe)
            {
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                    timeout.CancelAfter(RequestTimeout);

                    var request = await TrayControlProtocol.ReadAsync<TrayControlRequest>(pipe, timeout.Token);
                    var response = request == null
                        ? new TrayControlResponse(false, TrayControlProtocol.BadRequest, "Empty request.")
                        : await _handle(request);
                    await TrayControlProtocol.WriteAsync(pipe, response, timeout.Token);
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException or System.Text.Json.JsonException)
                {
                    _logger.LogWarning(ex, "Could not serve a tray control request.");
                }
            }
        }

        // Khong Dispose _stop: vong nghe co the van dang doc token luc tray thoat.
        public void Dispose() => _stop.Cancel();
    }
}
