namespace MasterPlay.Host.Auth;

public sealed class SupabaseAuthOptions
{
    public string Url { get; init; } = string.Empty;
    public string AnonKey { get; init; } = string.Empty;
    public string ServiceRoleKey { get; init; } = string.Empty;
    public bool GoogleEnabled { get; init; }
    public string GoogleRedirectUrl { get; init; } = string.Empty;
    public bool RegistrationEnabled { get; init; } = true;

    /// <summary>
    /// Google sign-in is offered only when it is switched on AND has the one
    /// allowlisted callback URL. The URL is never derived from request headers.
    /// </summary>
    public bool GoogleReady =>
        IsConfigured &&
        GoogleEnabled &&
        Uri.TryCreate(GoogleRedirectUrl, UriKind.Absolute, out var redirect) &&
        (redirect.Scheme == Uri.UriSchemeHttps || redirect.IsLoopback) &&
        string.IsNullOrEmpty(redirect.Query) &&
        string.IsNullOrEmpty(redirect.Fragment);

    public bool IsConfigured =>
        Uri.TryCreate(Url, UriKind.Absolute, out _) &&
        !string.IsNullOrWhiteSpace(AnonKey) &&
        !string.IsNullOrWhiteSpace(ServiceRoleKey);

    public static SupabaseAuthOptions FromConfiguration(IConfiguration configuration)
    {
        static string First(IConfiguration config, params string[] keys) =>
            keys.Select(key => config[key])
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?
                .Trim() ?? string.Empty;

        var googleValue = First(configuration, "SUPABASE_GOOGLE_ENABLED", "Supabase:GoogleEnabled");
        var registrationValue = First(
            configuration,
            "QUESTLOG_REGISTRATION_ENABLED",
            "QuestLog:RegistrationEnabled");

        return new SupabaseAuthOptions
        {
            Url = First(
                configuration,
                "SUPABASE_URL",
                "SUPABASE_PROJECT_URL",
                "Supabase:Url").TrimEnd('/'),
            // Supabase now labels this the publishable key. Keep the legacy
            // anon-key names for existing QuestLog installations.
            AnonKey = First(
                configuration,
                "SUPABASE_PUBLISHABLE_KEY",
                "SUPABASE_ANON_KEY",
                "Supabase:PublishableKey",
                "Supabase:AnonKey"),
            // The dashboard now labels service_role as a secret key. This
            // value is server-only and is never returned by /api/auth/config.
            ServiceRoleKey = First(
                configuration,
                "SUPABASE_SECRET_KEY",
                "SUPABASE_SERVICE_ROLE_KEY",
                "Supabase:SecretKey",
                "Supabase:ServiceRoleKey"),
            GoogleEnabled = bool.TryParse(googleValue, out var googleEnabled) && googleEnabled,
            GoogleRedirectUrl = First(
                configuration,
                "SUPABASE_GOOGLE_REDIRECT_URL",
                "Supabase:GoogleRedirectUrl"),
            RegistrationEnabled = !bool.TryParse(registrationValue, out var registrationEnabled) ||
                                  registrationEnabled,
        };
    }
}
