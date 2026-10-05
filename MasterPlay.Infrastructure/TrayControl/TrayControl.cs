using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MasterPlay.Infrastructure.TrayControl
{
    /// <summary>
    /// Kenh Host -> Tray. Tray la tien trinh WinForms rieng (phai song khi UI dong) nen
    /// giao dien web chi dieu khien duoc no qua Host: Host noi vao named pipe cua tray,
    /// gui MOT dong JSON va doc MOT dong JSON tra loi.
    ///
    /// Pipe chi mo cho user hien tai (PipeOptions.CurrentUserOnly) va ten gan voi user,
    /// nen hai tai khoan Windows tren cung may khong dung nham tray cua nhau.
    /// </summary>
    public static class TrayControlProtocol
    {
        public const string Status = "status";
        public const string SetService = "set-service";
        public const string ScanNow = "scan-now";
        public const string OpenLogs = "open-logs";

        public const string Playtime = "playtime";
        public const string Scanner = "scanner";

        public const string TrayBusy = "TRAY_BUSY";
        public const string CommandFailed = "TRAY_COMMAND_FAILED";
        public const string ScannerStopped = "SCANNER_STOPPED";
        public const string BadRequest = "BAD_REQUEST";

        /// <summary>Muc HKCU\...\Run cua tray. Phai khop StartupHelper (tray) va TrayProcess (Shell).</summary>
        public const string AutoStartValueName = "MasterPlayTray";

        public static string PipeName => $"QuestLogTrayControl-{Environment.UserDomainName}-{Environment.UserName}";

        public const PipeOptions Options = PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly;

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

        public static async Task WriteAsync<T>(Stream stream, T message, CancellationToken cancellationToken)
        {
            var line = JsonSerializer.Serialize(message, Json) + "\n";
            await stream.WriteAsync(Utf8.GetBytes(line), cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        public static async Task<T?> ReadAsync<T>(Stream stream, CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(stream, Utf8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
            var line = await reader.ReadLineAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(line) ? default : JsonSerializer.Deserialize<T>(line, Json);
        }
    }

    public sealed record TrayControlRequest(string Command, string? Service = null, bool? Enabled = null);

    public sealed record TrayServiceState(string Name, bool Enabled);

    public sealed record TrayControlStatus(IReadOnlyList<TrayServiceState> Services, bool StartWithWindows);

    public sealed record TrayControlResponse(
        bool Ok,
        string? ErrorCode = null,
        string? Message = null,
        TrayControlStatus? Status = null);

    public static class TrayControlClient
    {
        // Tray dang chay tra loi trong vai ms; het thoi gian nay coi nhu tray khong chay.
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(800);
        // Bat/tat dich vu cho worker dung han — vai giay.
        private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(20);

        /// <summary>Null khi tray khong chay (khong co ai nghe pipe).</summary>
        public static Task<TrayControlResponse?> SendAsync(TrayControlRequest request, CancellationToken cancellationToken) =>
            SendAsync(TrayControlProtocol.PipeName, request, cancellationToken);

        public static async Task<TrayControlResponse?> SendAsync(string pipeName, TrayControlRequest request, CancellationToken cancellationToken)
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, TrayControlProtocol.Options);
            try
            {
                await pipe.ConnectAsync(ConnectTimeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                return null;
            }

            using var reply = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            reply.CancelAfter(ReplyTimeout);
            await TrayControlProtocol.WriteAsync(pipe, request, reply.Token);
            return await TrayControlProtocol.ReadAsync<TrayControlResponse>(pipe, reply.Token)
                ?? throw new IOException("The tray closed the connection without replying.");
        }
    }
}
