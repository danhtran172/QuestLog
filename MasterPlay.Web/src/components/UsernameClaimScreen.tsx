import { useEffect, useRef, useState, type FormEvent } from "react";
import { AlertTriangle, ArrowRight, Loader2, LogOut, UserRound } from "lucide-react";
import { useAuth } from "@/lib/auth";
import { authErrorMessage } from "@/lib/authErrors";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { GoogleMark } from "./GoogleSignIn";

/**
 * Buoc cuoi khi dang ky bang Google: tai khoan moi chua co ten dang nhap.
 * Host chan moi domain endpoint (403 USERNAME_REQUIRED) cho toi khi claim xong,
 * nen man nay khong co duong tat nao vao thu vien.
 */
export function UsernameClaimScreen() {
  const { account, claimUsername, logout } = useAuth();
  const { t } = useI18n();
  const [username, setUsername] = useState("");
  const [pending, setPending] = useState(false);
  const [logoutPending, setLogoutPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    inputRef.current?.focus();
  }, []);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);
    const trimmed = username.trim();
    if (!/^[A-Za-z0-9_.-]{3,32}$/.test(trimmed)) {
      setError(t("account.usernameRules"));
      inputRef.current?.focus();
      return;
    }

    setPending(true);
    try {
      await claimUsername(trimmed);
    } catch (claimError) {
      setError(authErrorMessage(claimError, t));
      inputRef.current?.focus();
    } finally {
      setPending(false);
    }
  };

  const signOut = async () => {
    setLogoutPending(true);
    try {
      await logout();
    } catch (logoutError) {
      setError(authErrorMessage(logoutError, t));
      setLogoutPending(false);
    }
  };

  return (
    <main className="grid min-h-full place-items-center bg-bg px-5 py-8">
      <section className="surface-card w-full max-w-md p-6 sm:p-8" aria-labelledby="username-claim-title">
        <div className="mb-6 flex items-center gap-3">
          <span className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-primary/15 text-primary">
            <UserRound aria-hidden="true" />
          </span>
          <div className="min-w-0">
            <h1 id="username-claim-title" className="text-xl font-black tracking-tight text-fg">
              {t("auth.chooseUsernameTitle")}
            </h1>
            <p className="mt-0.5 text-sm text-muted-fg">{t("auth.chooseUsernameDescription")}</p>
          </div>
        </div>

        <p className="mb-5 flex items-center gap-2 rounded-lg border border-border bg-bg/40 px-3 py-2.5 text-sm text-muted-fg">
          <GoogleMark size={16} />
          <span className="min-w-0 truncate">
            {t("auth.signedInWithGoogleAs")} <span className="font-semibold text-fg">{account?.email}</span>
          </span>
        </p>

        <form className="space-y-4" onSubmit={submit} aria-busy={pending} noValidate>
          <label className="block" htmlFor="claim-username">
            <span className="mb-1.5 block text-sm font-bold text-fg">{t("auth.username")}</span>
            <input
              ref={inputRef}
              id="claim-username"
              name="username"
              autoComplete="username"
              value={username}
              onChange={(event) => {
                setUsername(event.target.value);
                setError(null);
              }}
              maxLength={32}
              aria-invalid={Boolean(error)}
              aria-describedby="claim-username-rules"
              className={cn(
                "h-11 w-full rounded-lg border border-border bg-bg/75 px-3 text-base text-fg shadow-inner outline-none",
                "focus:border-primary focus:ring-2 focus:ring-primary/25 aria-[invalid=true]:border-danger",
              )}
            />
            <span id="claim-username-rules" className="mt-1.5 block text-xs text-muted-fg">
              {t("account.usernameRules")}
            </span>
          </label>

          {error ? (
            <p className="flex items-start gap-2 rounded-lg border border-danger/50 bg-danger/10 p-3 text-sm text-fg" role="alert">
              <AlertTriangle className="mt-0.5 shrink-0 text-danger" size={16} aria-hidden="true" />
              {error}
            </p>
          ) : null}

          <button
            type="submit"
            disabled={pending || logoutPending}
            className="glass-surface flex min-h-11 w-full items-center justify-center gap-2 rounded-lg bg-primary px-4 font-extrabold text-primary-fg outline-none disabled:cursor-wait disabled:opacity-65 focus-visible:ring-2 focus-visible:ring-primary/50"
          >
            {pending ? <Loader2 className="animate-spin" size={17} aria-hidden="true" /> : <ArrowRight size={17} aria-hidden="true" />}
            {t("auth.continue")}
          </button>
        </form>

        <button
          type="button"
          onClick={() => void signOut()}
          disabled={pending || logoutPending}
          className="mt-4 inline-flex min-h-10 items-center gap-2 rounded-lg px-1 text-sm font-bold text-muted-fg outline-none hover:text-fg disabled:cursor-wait disabled:opacity-60 focus-visible:ring-2 focus-visible:ring-primary/45"
        >
          {logoutPending ? <Loader2 className="animate-spin" size={16} aria-hidden="true" /> : <LogOut size={16} aria-hidden="true" />}
          {t("menu.logout")}
        </button>
      </section>
    </main>
  );
}
