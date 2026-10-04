namespace MasterPlay.Host.Integrations;

/// <summary>
/// Loads active Vault revisions into the in-process provider cache at startup.
/// A provider with no Vault revision is seeded once from its legacy source
/// (environment variables / config.json) so the Admin Console reflects the
/// credentials the app already uses. Failure is non-fatal: the Admin Console
/// exposes the safe configuration error, and no provider falls back to a
/// hardcoded credential.
/// </summary>
public sealed class IntegrationCredentialWarmupService(
    SupabaseIntegrationStore store,
    ILogger<IntegrationCredentialWarmupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var integrations = await store.ListAsync(cancellationToken);
            foreach (var integration in integrations)
            {
                if (integration.Configured)
                {
                    await store.ReadCredentialsAsync(integration.Provider, cancellationToken);
                    continue;
                }

                var legacy = LegacyIntegrationCredentials.Read(integration.Provider);
                if (legacy is null) continue;

                try
                {
                    await store.ReplaceAsync(integration.Provider, legacy, actorUserId: null, cancellationToken);
                    logger.LogInformation("Imported legacy {Provider} credentials into Admin Integrations", integration.Provider);
                }
                catch (IntegrationStoreException exception)
                {
                    logger.LogWarning("Legacy {Provider} credential import failed: {Code}", integration.Provider, exception.Code);
                }
            }
        }
        catch (IntegrationStoreException exception)
        {
            logger.LogWarning("Integration credential warmup skipped: {Code}", exception.Code);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
