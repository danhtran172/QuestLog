using System.Security.Cryptography;
using System.Text;
using MasterPlay.Host.Auth;
using Microsoft.Extensions.Time.Testing;

namespace MasterPlay.Host.Tests;

public sealed class GoogleOAuthFlowStoreTests
{
    [Fact]
    public void Code_challenge_is_S256_of_the_verifier()
    {
        var flow = new GoogleOAuthFlowStore().Create(GoogleOAuthClient.Web)!;
        var expected = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(flow.CodeVerifier)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        Assert.Equal(expected, flow.CodeChallenge);
        Assert.NotEqual(flow.State, flow.FlowId);
        Assert.True(flow.CodeVerifier.Length >= 43);
    }

    [Fact]
    public void State_is_single_use()
    {
        var store = new GoogleOAuthFlowStore();
        var flow = store.Create(GoogleOAuthClient.Desktop)!;

        Assert.Same(flow, store.TakeByState(flow.State));
        Assert.Null(store.TakeByState(flow.State));
        Assert.Same(flow, store.Get(flow.FlowId));
    }

    [Fact]
    public void Flow_expires_after_its_lifetime()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = new GoogleOAuthFlowStore(clock);
        var flow = store.Create(GoogleOAuthClient.Web)!;

        clock.Advance(GoogleOAuthFlowStore.Lifetime);

        Assert.Null(store.Get(flow.FlowId));
    }

    [Fact]
    public void Store_refuses_new_flows_when_full()
    {
        var store = new GoogleOAuthFlowStore();
        for (var index = 0; index < GoogleOAuthFlowStore.Capacity; index++)
        {
            Assert.NotNull(store.Create(GoogleOAuthClient.Web));
        }

        Assert.Null(store.Create(GoogleOAuthClient.Web));
    }

    [Fact]
    public void Exchange_is_handed_to_one_caller_only()
    {
        var flow = new GoogleOAuthFlowStore().Create(GoogleOAuthClient.Web)!;
        Assert.Equal(GoogleFlowProgress.Pending, flow.TryBeginExchange().Progress);

        flow.Authorize("code-1");
        flow.Authorize("code-2");

        Assert.Equal((GoogleFlowProgress.Authorized, "code-1"), flow.TryBeginExchange());
        Assert.Equal(GoogleFlowProgress.Pending, flow.TryBeginExchange().Progress);
    }

    [Fact]
    public void Cancelled_flow_never_exchanges()
    {
        var flow = new GoogleOAuthFlowStore().Create(GoogleOAuthClient.Web)!;
        flow.Authorize("code");
        flow.Cancel();

        Assert.Equal((GoogleFlowProgress.Cancelled, (string?)null), flow.TryBeginExchange());
    }
}

public sealed class ReauthTicketStoreTests
{
    [Fact]
    public void Ticket_is_bound_to_account_and_session()
    {
        var store = new ReauthTicketStore();
        var (ticket, _) = store.Issue("user-a", "session-1");

        Assert.Null(store.Reserve(ticket, "user-b", "session-1"));
        Assert.Null(store.Reserve(ticket, "user-a", "session-2"));
        Assert.NotNull(store.Reserve(ticket, "user-a", "session-1"));
    }

    [Fact]
    public void Ticket_is_single_use_unless_released()
    {
        var store = new ReauthTicketStore();
        var (ticket, _) = store.Issue("user", "session");

        var reservation = store.Reserve(ticket, "user", "session")!;
        Assert.Null(store.Reserve(ticket, "user", "session"));

        store.Release(reservation);
        Assert.NotNull(store.Reserve(ticket, "user", "session"));
        Assert.Null(store.Reserve(ticket, "user", "session"));
    }

    [Fact]
    public void Ticket_expires()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = new ReauthTicketStore(clock);
        var (ticket, _) = store.Issue("user", "session");

        clock.Advance(ReauthTicketStore.Lifetime);

        Assert.Null(store.Reserve(ticket, "user", "session"));
    }

    [Fact]
    public void Unknown_ticket_is_rejected() =>
        Assert.Null(new ReauthTicketStore().Reserve("not-a-ticket", "user", "session"));
}

public sealed class EmailCodeThrottleTests
{
    [Fact]
    public void Resend_waits_for_the_cooldown()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var throttle = new EmailCodeThrottle(clock);

        Assert.True(throttle.TryRecordSend("key", out var next));
        Assert.False(throttle.TryRecordSend("key", out var stillNext));
        Assert.Equal(next, stillNext);

        clock.Advance(EmailCodeThrottle.ResendCooldown);
        Assert.True(throttle.TryRecordSend("key", out _));
    }

    [Fact]
    public void Five_wrong_codes_block_until_a_new_code_is_sent()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var throttle = new EmailCodeThrottle(clock);
        throttle.TryRecordSend("key", out _);

        for (var attempt = 0; attempt < EmailCodeThrottle.MaxFailedAttempts; attempt++)
        {
            Assert.False(throttle.AttemptsExceeded("key"));
            throttle.RecordFailure("key");
        }

        Assert.True(throttle.AttemptsExceeded("key"));
        clock.Advance(EmailCodeThrottle.ResendCooldown);
        throttle.TryRecordSend("key", out _);
        Assert.False(throttle.AttemptsExceeded("key"));
    }
}

public sealed class PasswordResetTicketStoreTests
{
    [Fact]
    public void Ticket_carries_the_verified_account_once()
    {
        var store = new PasswordResetTicketStore();
        var (ticket, _) = store.Issue("user", "player@example.test");

        var reservation = store.Reserve(ticket)!;
        Assert.Equal(("user", "player@example.test"), (reservation.Entry.UserId, reservation.Entry.Email));
        Assert.Null(store.Reserve(ticket));
    }

    [Fact]
    public void Released_ticket_can_be_used_again_until_it_expires()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = new PasswordResetTicketStore(clock);
        var (ticket, _) = store.Issue("user", "player@example.test");

        store.Release(store.Reserve(ticket)!);
        Assert.NotNull(store.Reserve(ticket));

        var (expiring, _) = store.Issue("user", "player@example.test");
        clock.Advance(PasswordResetTicketStore.Lifetime);
        Assert.Null(store.Reserve(expiring));
    }

    [Fact]
    public void Unknown_ticket_is_rejected() => Assert.Null(new PasswordResetTicketStore().Reserve("nope"));
}
