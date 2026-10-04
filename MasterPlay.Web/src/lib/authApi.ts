import type { components } from "./api/types.gen";
import { getDesktopGuestToken } from "./desktopBridge";
import { httpRequest } from "./http";

type Schemas = components["schemas"];

export type AuthConfig = Schemas["AuthConfig"];
export type AuthAccount = Schemas["AuthAccount"];
export type AuthSession = Schemas["AuthSession"];
export type GuestSession = Schemas["GuestSession"];
export type LoginRequest = Schemas["LoginRequest"];
export type SignupRequest = Schemas["SignupRequest"];
export type SignupResponse = Schemas["SignupResponse"];
export type ChangePasswordRequest = Schemas["ChangePasswordRequest"];
export type ChangeUsernameRequest = Schemas["ChangeUsernameRequest"];
export type ChangeEmailRequest = Schemas["ChangeEmailRequest"];
export type EmailChangeResponse = Schemas["EmailChangeResponse"];
export type UsernameClaimRequest = Schemas["UsernameClaimRequest"];
export type GoogleStartRequest = Schemas["GoogleStartRequest"];
export type GoogleStartResponse = Schemas["GoogleStartResponse"];
export type GoogleCompleteResponse = Schemas["GoogleCompleteResponse"];
export type ForgotPasswordRequest = Schemas["ForgotPasswordRequest"];
export type ForgotPasswordResponse = Schemas["ForgotPasswordResponse"];
export type ResetPasswordRequest = Schemas["ResetPasswordRequest"];
export type VerifyResetCodeRequest = Schemas["VerifyResetCodeRequest"];
export type ResetTicketResponse = Schemas["ResetTicketResponse"];
export type ReauthCodeResponse = Schemas["ReauthCodeResponse"];
export type ReauthTicketResponse = Schemas["ReauthTicketResponse"];

/** Desktop Google flows are bound to the WebView2 handshake token. */
function desktopHeaders(client: GoogleStartRequest["client"]): HeadersInit | undefined {
  if (client !== "desktop") return undefined;
  const token = getDesktopGuestToken();
  return token ? { "X-QuestLog-Desktop-Token": token } : undefined;
}

export const authApi = {
  config: () => httpRequest<AuthConfig>("/api/auth/config"),
  login: (body: LoginRequest) =>
    httpRequest<AuthSession>("/api/auth/login", {
      method: "POST",
      body: JSON.stringify(body),
    }),
  signup: (body: SignupRequest) =>
    httpRequest<SignupResponse>("/api/auth/signup", {
      method: "POST",
      body: JSON.stringify(body),
    }),
  guest: (desktopToken: string) =>
    httpRequest<GuestSession>("/api/auth/guest", {
      method: "POST",
      headers: { "X-QuestLog-Desktop-Token": desktopToken },
    }),
  restoreSession: () =>
    httpRequest<AuthSession>("/api/auth/session", { method: "POST" }),
  me: () => httpRequest<AuthAccount>("/api/auth/me"),
  changePassword: (body: ChangePasswordRequest) =>
    httpRequest<AuthSession>("/api/auth/change-password", {
      method: "POST",
      body: JSON.stringify(body),
    }),
  changeUsername: (body: ChangeUsernameRequest) =>
    httpRequest<AuthAccount>("/api/auth/account/username", {
      method: "PATCH",
      body: JSON.stringify(body),
    }),
  requestEmailChange: (body: ChangeEmailRequest) =>
    httpRequest<EmailChangeResponse>("/api/auth/account/email-change", {
      method: "POST",
      body: JSON.stringify(body),
    }),
  claimUsername: (body: UsernameClaimRequest) =>
    httpRequest<AuthAccount>("/api/auth/account/username-claim", {
      method: "POST",
      body: JSON.stringify(body),
    }),
  googleStart: (client: GoogleStartRequest["client"]) =>
    httpRequest<GoogleStartResponse>("/api/auth/google/start", {
      method: "POST",
      headers: desktopHeaders(client),
      body: JSON.stringify({ client } satisfies GoogleStartRequest),
    }),
  googleComplete: (client: GoogleStartRequest["client"], flowId: string) =>
    httpRequest<GoogleCompleteResponse>("/api/auth/google/complete", {
      method: "POST",
      headers: desktopHeaders(client),
      body: JSON.stringify({ flowId }),
    }),
  forgotPassword: (body: ForgotPasswordRequest) =>
    httpRequest<ForgotPasswordResponse>("/api/auth/password/forgot", {
      method: "POST",
      body: JSON.stringify(body),
    }),
  verifyResetCode: (body: VerifyResetCodeRequest) =>
    httpRequest<ResetTicketResponse>("/api/auth/password/verify-code", {
      method: "POST",
      body: JSON.stringify(body),
    }),
  resetPassword: (body: ResetPasswordRequest) =>
    httpRequest<AuthSession>("/api/auth/password/reset", {
      method: "POST",
      body: JSON.stringify(body),
    }),
  sendReauthCode: () =>
    httpRequest<ReauthCodeResponse>("/api/auth/reauth/email", { method: "POST" }),
  verifyReauthCode: (code: string) =>
    httpRequest<ReauthTicketResponse>("/api/auth/reauth/verify", {
      method: "POST",
      body: JSON.stringify({ code }),
    }),
  logout: () => httpRequest<void>("/api/auth/logout", { method: "POST" }),
};
