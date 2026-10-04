import { useRef, useState, type FormEvent } from "react";
import { AlertTriangle, Eye, EyeOff, KeyRound, Loader2, LogOut, ShieldCheck } from "lucide-react";
import { useAuth } from "@/lib/auth";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";

const fieldClass = cn(
  "h-11 w-full rounded-lg border border-border bg-bg/75 px-3 text-base text-fg shadow-inner outline-none",
  "focus:border-primary focus:ring-2 focus:ring-primary/25 aria-[invalid=true]:border-danger",
);

export function PasswordChangeScreen() {
  const { account, changePassword, logout } = useAuth();
  const { t } = useI18n();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmation, setConfirmation] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const currentRef = useRef<HTMLInputElement>(null);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);
    if (!currentPassword || newPassword.length < 12) {
      setError(t("auth.passwordPolicy"));
      currentRef.current?.focus();
      return;
    }
    if (newPassword !== confirmation) {
      setError(t("auth.passwordMismatch"));
      return;
    }

    setPending(true);
    try {
      await changePassword({ currentPassword, newPassword });
    } catch (changeError) {
      setError(changeError instanceof Error ? changeError.message : String(changeError));
    } finally {
      setPending(false);
    }
  };

  return (
    <main className="grid min-h-full place-items-center bg-bg px-5 py-8">
      <section className="surface-card w-full max-w-md p-6 sm:p-8" aria-labelledby="password-change-title">
        <div className="mb-6 flex items-center gap-3">
          <span className="grid h-11 w-11 place-items-center rounded-xl bg-primary/15 text-primary">
            <KeyRound aria-hidden="true" />
          </span>
          <div>
            <p className="text-sm font-semibold text-primary">{account?.username}</p>
            <h1 id="password-change-title" className="text-xl font-black tracking-tight text-fg">
              {t("auth.changeBootstrapPassword")}
            </h1>
          </div>
        </div>

        <p className="mb-6 text-sm leading-6 text-muted-fg">{t("auth.changeBootstrapDescription")}</p>

        <form className="space-y-4" onSubmit={submit} aria-busy={pending}>
          <label className="block">
            <span className="mb-1.5 block text-sm font-bold">{t("auth.currentPassword")}</span>
            <input
              ref={currentRef}
              name="currentPassword"
              type={showPassword ? "text" : "password"}
              autoComplete="current-password"
              value={currentPassword}
              onChange={(event) => setCurrentPassword(event.target.value)}
              className={fieldClass}
            />
          </label>

          <label className="block">
            <span className="mb-1.5 block text-sm font-bold">{t("auth.newPassword")}</span>
            <span className="relative block">
              <input
                name="newPassword"
                type={showPassword ? "text" : "password"}
                autoComplete="new-password"
                value={newPassword}
                onChange={(event) => setNewPassword(event.target.value)}
                aria-describedby="password-policy"
                className={cn(fieldClass, "pr-11")}
              />
              <button
                type="button"
                onClick={() => setShowPassword((value) => !value)}
                aria-label={t(showPassword ? "auth.hidePassword" : "auth.showPassword")}
                className="absolute right-1 top-1 grid h-9 w-9 place-items-center rounded-md text-muted-fg outline-none hover:text-fg focus-visible:ring-2 focus-visible:ring-primary/45"
              >
                {showPassword ? <EyeOff size={17} aria-hidden="true" /> : <Eye size={17} aria-hidden="true" />}
              </button>
            </span>
            <span id="password-policy" className="mt-1.5 flex items-center gap-1.5 text-xs text-muted-fg">
              <ShieldCheck size={14} className="text-primary" aria-hidden="true" />
              {t("auth.passwordPolicy")}
            </span>
          </label>

          <label className="block">
            <span className="mb-1.5 block text-sm font-bold">{t("auth.confirmPassword")}</span>
            <input
              name="confirmation"
              type={showPassword ? "text" : "password"}
              autoComplete="new-password"
              value={confirmation}
              onChange={(event) => setConfirmation(event.target.value)}
              className={fieldClass}
            />
          </label>

          {error ? (
            <p role="alert" className="flex items-start gap-2 rounded-lg border border-danger/50 bg-danger/10 p-3 text-sm">
              <AlertTriangle className="mt-0.5 shrink-0 text-danger" size={16} aria-hidden="true" />
              {error}
            </p>
          ) : null}

          <button
            type="submit"
            disabled={pending}
            className="glass-surface flex min-h-11 w-full items-center justify-center gap-2 rounded-lg bg-primary px-4 font-extrabold text-primary-fg outline-none disabled:cursor-wait disabled:opacity-65 focus-visible:ring-2 focus-visible:ring-primary/50"
          >
            {pending ? <Loader2 className="animate-spin" size={17} aria-hidden="true" /> : <KeyRound size={17} aria-hidden="true" />}
            {t("auth.changePassword")}
          </button>

          <button
            type="button"
            onClick={() => void logout()}
            className="flex min-h-11 w-full items-center justify-center gap-2 rounded-lg px-4 text-sm font-bold text-muted-fg outline-none hover:bg-surface-2/65 hover:text-fg focus-visible:ring-2 focus-visible:ring-primary/45"
          >
            <LogOut size={16} aria-hidden="true" />
            {t("menu.logout")}
          </button>
        </form>
      </section>
    </main>
  );
}
