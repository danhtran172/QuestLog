namespace MasterPlay.Host.Auth;

public sealed record AuthConfigDto(
    bool Configured,
    bool RegistrationEnabled,
    bool GoogleEnabled);

public sealed record LoginRequestDto(string? Identifier, string? Password);

public sealed record SignupRequestDto(string? Username, string? Email, string? Password);

public sealed record SignupResponseDto(bool RequiresEmailVerification, string Email);

public sealed record SignupCodeRequestDto(string? Identifier);

public sealed record VerifySignupCodeRequestDto(string? Identifier, string? Code);

public sealed record SignupCodeResponseDto(bool Sent, DateTimeOffset ResendAvailableAt);

public sealed record ChangePasswordRequestDto(string? NewPassword, string? ReauthTicket, string? CurrentPassword);

public sealed record ChangeUsernameRequestDto(string? Username, string? ReauthTicket);

public sealed record ChangeEmailRequestDto(string? Email, string? ReauthTicket);

public sealed record EmailChangeResponseDto(bool RequiresConfirmation, string Email);

public sealed record UsernameClaimRequestDto(string? Username);

public sealed record GoogleStartRequestDto(string? Client);

public sealed record GoogleStartResponseDto(string FlowId, string AuthorizeUrl, DateTimeOffset ExpiresAt);

public sealed record GoogleCompleteRequestDto(string? FlowId);

public sealed record GoogleCompleteResponseDto(string Status, AuthSessionDto? Session);

public sealed record ForgotPasswordRequestDto(string? Identifier);

public sealed record ForgotPasswordResponseDto(bool Sent);

public sealed record VerifyResetCodeRequestDto(string? Identifier, string? Code);

public sealed record ResetTicketResponseDto(string ResetTicket, DateTimeOffset ExpiresAt);

public sealed record ResetPasswordRequestDto(string? ResetTicket, string? NewPassword);

public sealed record ReauthCodeResponseDto(bool Sent, DateTimeOffset ResendAvailableAt);

public sealed record ReauthVerifyRequestDto(string? Code);

public sealed record ReauthTicketResponseDto(string ReauthTicket, DateTimeOffset ExpiresAt);

/// <summary>Values of <see cref="AuthAccountDto.Onboarding"/>.</summary>
public static class AccountOnboarding
{
    public const string Complete = "complete";
    public const string UsernameRequired = "usernameRequired";
}

public sealed record AuthAccountDto(
    string Id,
    string? Username,
    string Email,
    string Role,
    string Status,
    bool EmailVerified,
    bool MustChangePassword,
    bool MfaRequired,
    string Onboarding,
    bool HasPassword);

public sealed record AuthSessionDto(
    AuthAccountDto Account,
    string AccessToken,
    DateTimeOffset ExpiresAt);

public sealed record GuestSessionDto(
    string AccessToken,
    DateTimeOffset ExpiresAt);
