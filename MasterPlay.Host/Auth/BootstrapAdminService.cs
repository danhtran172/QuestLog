namespace MasterPlay.Host.Auth;

public sealed class BootstrapAdminService(
    IConfiguration configuration,
    SupabaseAuthOptions options,
    SupabaseAuthGateway gateway,
    ILogger<BootstrapAdminService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var username = configuration["QUESTLOG_BOOTSTRAP_ADMIN_USERNAME"]?.Trim();
        var email = configuration["QUESTLOG_BOOTSTRAP_ADMIN_EMAIL"]?.Trim();
        var password = configuration["QUESTLOG_BOOTSTRAP_ADMIN_PASSWORD"];
        var supplied = new[] { username, email, password }.Count(value => !string.IsNullOrWhiteSpace(value));

        if (supplied == 0) return;
        if (supplied != 3)
        {
            logger.LogWarning(
                "QuestLog admin bootstrap skipped because one or more required environment variables are missing.");
            return;
        }

        if (!options.IsConfigured)
        {
            logger.LogWarning("QuestLog admin bootstrap skipped because Supabase Auth is not configured.");
            return;
        }

        try
        {
            var created = await gateway.BootstrapAdminAsync(
                username!,
                email!,
                password!,
                cancellationToken);
            logger.LogInformation(
                created
                    ? "QuestLog bootstrap administrator was created."
                    : "QuestLog bootstrap administrator already exists; no account fields were changed.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "QuestLog bootstrap administrator could not be created.");
        }
        finally
        {
            // The bootstrap password is needed only during startup. Remove the child
            // process copy even when the account already existed or creation failed.
            Environment.SetEnvironmentVariable(
                "QUESTLOG_BOOTSTRAP_ADMIN_PASSWORD",
                null,
                EnvironmentVariableTarget.Process);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
