import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { useQueryClient } from "@tanstack/react-query";
import { ApiRequestError, setAuthToken, setSessionRecovery } from "./api/client";
import { routeWithoutSession, startupRoute } from "./authStartup";
import { getDesktopGuestToken } from "./desktopBridge";
import { runGoogleFlow, sleep, type GoogleFlowEvent } from "./googleAuthFlow";
import { withStartupRetry } from "./startupRetry";
import {
  authApi,
  type AuthAccount,
  type AuthConfig,
  type AuthSession,
  type ChangeEmailRequest,
  type ChangePasswordRequest,
  type ChangeUsernameRequest,
  type EmailChangeResponse,
  type GoogleStartRequest,
  type LoginRequest,
  type ResetPasswordRequest,
  type SignupRequest,
  type SignupResponse,
} from "./authApi";

const GUEST_SESSION_KEY = "questlog:desktop-guest";
const GOOGLE_WINDOW_NAME = "questlog-google";

function googleClient(): GoogleStartRequest["client"] {
  return getDesktopGuestToken() ? "desktop" : "web";
}

/**
 * Desktop: window.open di qua NewWindowRequested cua Shell -> trinh duyet he thong
 * (Google chan OAuth trong WebView nhung). Web: dung popup da mo san neu co.
 */
function openGoogleWindow(
  client: GoogleStartRequest["client"],
  authorizeUrl: string,
  popup: Window | null,
): boolean {
  if (client === "desktop") {
    window.open(authorizeUrl, "_blank");
    return true;
  }
  if (popup && !popup.closed) {
    popup.location.href = authorizeUrl;
    return true;
  }
  return window.open(authorizeUrl, GOOGLE_WINDOW_NAME) !== null;
}

export type AuthStatus =
  | "loading"
  | "signedOut"
  | "signedIn"
  | "guest"
  | "unconfigured"
  | "error";

type AuthContextValue = {
  status: AuthStatus;
  account: AuthAccount | null;
  config: AuthConfig | null;
  startupError: string | null;
  guestAvailable: boolean;
  login: (request: LoginRequest) => Promise<void>;
  signup: (request: SignupRequest) => Promise<SignupResponse>;
  /** Re-sends the sign-up confirmation code; returns when another resend is allowed. */
  resendSignupCode: (identifier: string) => Promise<string>;
  /** Confirms the email with the emailed code and signs in. */
  verifySignupCode: (identifier: string, code: string) => Promise<void>;
  /** Runs one Google sign-in; resolves once the session is accepted. Abort to cancel. */
  signInWithGoogle: (signal: AbortSignal, onEvent?: (event: GoogleFlowEvent) => void) => Promise<void>;
  /** Re-opens the Google page from a user gesture (popup blocked or tab closed). */
  reopenGoogle: (authorizeUrl: string) => void;
  claimUsername: (username: string) => Promise<void>;
  forgotPassword: (identifier: string) => Promise<void>;
  /** Checks the emailed recovery code; returns the reset ticket for the new-password step. */
  verifyResetCode: (identifier: string, code: string) => Promise<string>;
  resetPassword: (request: ResetPasswordRequest) => Promise<void>;
  sendReauthCode: () => Promise<string>;
  verifyReauthCode: (code: string) => Promise<string>;
  continueAsGuest: () => Promise<void>;
  changePassword: (request: ChangePasswordRequest) => Promise<void>;
  changeUsername: (request: ChangeUsernameRequest) => Promise<void>;
  requestEmailChange: (request: ChangeEmailRequest) => Promise<EmailChangeResponse>;
  logout: () => Promise<void>;
  retry: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<AuthStatus>("loading");
  const [account, setAccount] = useState<AuthAccount | null>(null);
  const [config, setConfig] = useState<AuthConfig | null>(null);
  const [expiresAt, setExpiresAt] = useState<string | null>(null);
  const [startupError, setStartupError] = useState<string | null>(null);
  const guestAvailable = getDesktopGuestToken() !== null;

  const acceptSession = useCallback((session: AuthSession) => {
    window.sessionStorage.removeItem(GUEST_SESSION_KEY);
    setAuthToken(session.accessToken);
    setAccount(session.account);
    setExpiresAt(session.expiresAt);
    setStartupError(null);
    setStatus("signedIn");
  }, []);

  const acceptGuestSession = useCallback((session: { accessToken: string; expiresAt: string }) => {
    window.sessionStorage.setItem(GUEST_SESSION_KEY, "1");
    setAuthToken(session.accessToken);
    setAccount(null);
    setExpiresAt(session.expiresAt);
    setStartupError(null);
    setStatus("guest");
  }, []);

  const clearSession = useCallback(() => {
    window.sessionStorage.removeItem(GUEST_SESSION_KEY);
    setAuthToken(null);
    setAccount(null);
    setExpiresAt(null);
    queryClient.clear();
    setStatus("signedOut");
  }, [queryClient]);

  const restore = useCallback(async () => {
    setStatus("loading");
    setStartupError(null);
    try {
      const nextConfig = await withStartupRetry(() => authApi.config());
      setConfig(nextConfig);
      const desktopToken = getDesktopGuestToken();
      const enterAsGuest = async (token: string) => {
        acceptGuestSession(await withStartupRetry(() => authApi.guest(token)));
      };
      const route = startupRoute({
        desktop: desktopToken !== null,
        guestThisSession: window.sessionStorage.getItem(GUEST_SESSION_KEY) === "1",
        authConfigured: nextConfig.configured,
      });
      if (route === "guest" && desktopToken) {
        await enterAsGuest(desktopToken);
        return;
      }
      if (route === "unconfigured") {
        setAuthToken(null);
        setStatus("unconfigured");
        return;
      }

      try {
        acceptSession(await withStartupRetry(() => authApi.restoreSession()));
      } catch (error) {
        if (error instanceof ApiRequestError && error.status === 401) {
          // Desktop: chua dang nhap (hoac da dang xuat) thi vao thang app bang du lieu
          // tren may; man dang nhap chi mo khi nguoi dung chon "Dang nhap".
          if (routeWithoutSession(desktopToken !== null) === "guest" && desktopToken) {
            await enterAsGuest(desktopToken);
          } else {
            clearSession();
          }
          return;
        }
        throw error;
      }
    } catch (error) {
      setAuthToken(null);
      setStartupError(error instanceof Error ? error.message : String(error));
      setStatus("error");
    }
  }, [acceptGuestSession, acceptSession, clearSession]);

  useEffect(() => {
    void restore();
  }, [restore]);

  useEffect(() => {
    if (status !== "signedIn" || !expiresAt) return;
    const refreshAt = new Date(expiresAt).getTime() - 60_000;
    const delay = Math.max(5_000, refreshAt - Date.now());
    const timer = window.setTimeout(async () => {
      try {
        acceptSession(await authApi.restoreSession());
      } catch {
        clearSession();
      }
    }, delay);
    return () => window.clearTimeout(timer);
  }, [acceptSession, clearSession, expiresAt, status]);

  // Phien bi thu hoi giua chung (dang xuat noi khac, dat lai mat khau): thu lam moi bang
  // cookie refresh; khong duoc thi ve man dang nhap thay vi de tung man hinh bao loi.
  useEffect(() => {
    if (status !== "signedIn") return;
    setSessionRecovery(async () => {
      try {
        acceptSession(await authApi.restoreSession());
        return true;
      } catch {
        clearSession();
        return false;
      }
    });
    return () => setSessionRecovery(null);
  }, [acceptSession, clearSession, status]);

  const login = useCallback(async (request: LoginRequest) => {
    acceptSession(await authApi.login(request));
  }, [acceptSession]);

  const signup = useCallback((request: SignupRequest) => authApi.signup(request), []);

  const resendSignupCode = useCallback(
    async (identifier: string) => (await authApi.resendSignupCode(identifier)).resendAvailableAt,
    [],
  );

  const verifySignupCode = useCallback(async (identifier: string, code: string) => {
    acceptSession(await authApi.verifySignupCode({ identifier, code }));
  }, [acceptSession]);

  const signInWithGoogle = useCallback(
    async (signal: AbortSignal, onEvent?: (event: GoogleFlowEvent) => void) => {
      const client = googleClient();
      // Web: mo cua so NGAY trong click handler (truoc moi await) de trinh duyet
      // khong chan popup; sau khi co authorizeUrl moi dieu huong no.
      let popup: Window | null = null;
      if (client === "web") {
        popup = window.open("about:blank", GOOGLE_WINDOW_NAME);
        if (popup) popup.opener = null;
      }
      try {
        const session = await runGoogleFlow<AuthSession>(
          {
            start: () => authApi.googleStart(client),
            complete: (flowId) => authApi.googleComplete(client, flowId),
            open: (authorizeUrl) => openGoogleWindow(client, authorizeUrl, popup),
            sleep,
            now: Date.now,
          },
          signal,
          onEvent,
        );
        acceptSession(session);
      } catch (error) {
        popup?.close();
        throw error;
      }
    },
    [acceptSession],
  );

  const reopenGoogle = useCallback((authorizeUrl: string) => {
    openGoogleWindow(googleClient(), authorizeUrl, null);
  }, []);

  const claimUsername = useCallback(async (username: string) => {
    setAccount(await authApi.claimUsername({ username }));
  }, []);

  const forgotPassword = useCallback(async (identifier: string) => {
    await authApi.forgotPassword({ identifier });
  }, []);

  const verifyResetCode = useCallback(
    async (identifier: string, code: string) =>
      (await authApi.verifyResetCode({ identifier, code })).resetTicket,
    [],
  );

  const resetPassword = useCallback(async (request: ResetPasswordRequest) => {
    acceptSession(await authApi.resetPassword(request));
  }, [acceptSession]);

  const sendReauthCode = useCallback(
    async () => (await authApi.sendReauthCode()).resendAvailableAt,
    [],
  );

  const verifyReauthCode = useCallback(
    async (code: string) => (await authApi.verifyReauthCode(code)).reauthTicket,
    [],
  );

  const continueAsGuest = useCallback(async () => {
    const desktopToken = getDesktopGuestToken();
    if (!desktopToken) {
      throw new Error("Guest access is available only inside the QuestLog desktop app.");
    }
    acceptGuestSession(await authApi.guest(desktopToken));
  }, [acceptGuestSession]);

  const changePassword = useCallback(async (request: ChangePasswordRequest) => {
    acceptSession(await authApi.changePassword(request));
  }, [acceptSession]);

  const changeUsername = useCallback(async (request: ChangeUsernameRequest) => {
    setAccount(await authApi.changeUsername(request));
  }, []);

  const requestEmailChange = useCallback(
    (request: ChangeEmailRequest) => authApi.requestEmailChange(request),
    [],
  );

  const logout = useCallback(async () => {
    if (status === "guest") {
      clearSession();
      return;
    }
    try {
      await authApi.logout();
    } finally {
      clearSession();
    }
  }, [clearSession, status]);

  const value = useMemo<AuthContextValue>(
    () => ({
      status,
      account,
      config,
      startupError,
      guestAvailable,
      login,
      signup,
      resendSignupCode,
      verifySignupCode,
      signInWithGoogle,
      reopenGoogle,
      claimUsername,
      forgotPassword,
      verifyResetCode,
      resetPassword,
      sendReauthCode,
      verifyReauthCode,
      continueAsGuest,
      changePassword,
      changeUsername,
      requestEmailChange,
      logout,
      retry: restore,
    }),
    [
      account,
      changePassword,
      changeUsername,
      claimUsername,
      config,
      continueAsGuest,
      forgotPassword,
      guestAvailable,
      login,
      logout,
      reopenGoogle,
      requestEmailChange,
      resendSignupCode,
      resetPassword,
      restore,
      verifyResetCode,
      sendReauthCode,
      signInWithGoogle,
      signup,
      verifyReauthCode,
      verifySignupCode,
      startupError,
      status,
    ],
  );

  return <AuthContext value={value}>{children}</AuthContext>;
}

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext);
  if (!value) throw new Error("useAuth must be used inside <AuthProvider>");
  return value;
}
