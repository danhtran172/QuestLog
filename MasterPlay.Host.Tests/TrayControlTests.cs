using System.IO.Pipes;
using MasterPlay.Infrastructure.TrayControl;

namespace MasterPlay.Host.Tests;

public sealed class TrayControlTests
{
    // Ten rieng cho tung test: khong dung nham tray that dang chay tren may.
    private static string TestPipeName() => "QuestLogTrayControlTest-" + Guid.NewGuid().ToString("N");

    [Fact]
    public async Task No_tray_listening_means_null_instead_of_an_error()
    {
        var reply = await TrayControlClient.SendAsync(
            TestPipeName(), new TrayControlRequest(TrayControlProtocol.Status), CancellationToken.None);

        Assert.Null(reply);
    }

    [Fact]
    public async Task Request_and_reply_round_trip_over_the_pipe()
    {
        var pipeName = TestPipeName();
        TrayControlRequest? received = null;
        var server = Task.Run(async () =>
        {
            await using var pipe = new NamedPipeServerStream(
                pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, TrayControlProtocol.Options);
            await pipe.WaitForConnectionAsync();
            received = await TrayControlProtocol.ReadAsync<TrayControlRequest>(pipe, CancellationToken.None);
            await TrayControlProtocol.WriteAsync(
                pipe,
                new TrayControlResponse(
                    true,
                    Status: new TrayControlStatus(
                        [new TrayServiceState(TrayControlProtocol.Playtime, true), new TrayServiceState(TrayControlProtocol.Scanner, false)],
                        StartWithWindows: true)),
                CancellationToken.None);
        });

        var reply = await TrayControlClient.SendAsync(
            pipeName,
            new TrayControlRequest(TrayControlProtocol.SetService, TrayControlProtocol.Scanner, Enabled: false),
            CancellationToken.None);
        await server;

        Assert.Equal(new TrayControlRequest("set-service", "scanner", false), received);
        Assert.NotNull(reply);
        Assert.True(reply.Ok);
        Assert.True(reply.Status!.StartWithWindows);
        Assert.Equal(
            ["playtime:True", "scanner:False"],
            reply.Status.Services.Select(service => $"{service.Name}:{service.Enabled}"));
    }

    [Fact]
    public async Task Error_reply_keeps_its_code_and_message()
    {
        var pipeName = TestPipeName();
        var server = Task.Run(async () =>
        {
            await using var pipe = new NamedPipeServerStream(
                pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, TrayControlProtocol.Options);
            await pipe.WaitForConnectionAsync();
            await TrayControlProtocol.ReadAsync<TrayControlRequest>(pipe, CancellationToken.None);
            await TrayControlProtocol.WriteAsync(
                pipe,
                new TrayControlResponse(false, TrayControlProtocol.ScannerStopped, "The library scanner is turned off."),
                CancellationToken.None);
        });

        var reply = await TrayControlClient.SendAsync(
            pipeName, new TrayControlRequest(TrayControlProtocol.ScanNow), CancellationToken.None);
        await server;

        Assert.False(reply!.Ok);
        Assert.Equal("SCANNER_STOPPED", reply.ErrorCode);
        Assert.Equal("The library scanner is turned off.", reply.Message);
    }
}
