import { useRef, useState, type FormEvent } from "react";
import {
  AlertTriangle,
  ArrowLeft,
  ArrowRight,
  CheckCircle2,
  Eye,
  EyeOff,
  Gamepad2,
  Loader2,
  RefreshCw,
  ServerCog,
  ShieldCheck,
} from "lucide-react";
import { useAuth } from "@/lib/auth";
import { authErrorMessage } from "@/lib/authErrors";
import { EMAIL_CODE_LENGTH } from "@/lib/emailCode";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { EmailCodeField } from "./EmailCodeField";
import { GoogleSignInButton } from "./GoogleSignIn";

type Mode = "login" | "signup" | "forgot" | "newPassword" | "reset";

const inputClass = cn(
  "h-11 w-full rounded-lg border border-border bg-bg/75 px-3 text-base text-fg shadow-inner outline-none",
  "placeholder:text-muted-fg/65 focus:border-primary focus:ring-2 focus:ring-primary/25",
  "aria-[invalid=true]:border-danger aria-[invalid=true]:ring-2 aria-[invalid=true]:ring-danger/20",
);

export function AuthScreen() {
  const {
    status,
    config,
    startupError,
    guestAvailable,
    login,
    signup,
    forgotPassword,
    verifyResetCode,
    resetPassword,
    continueAsGuest,
    retry,
  } = useAuth();
  const { lang, setLang, t } = useI18n();
  const [mode, setMode] = useState<Mode>("login");
  const [identifier, setIdentifier] = useState("");
  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [pending, setPending] = useState(false);
  const [guestPending, setGuestPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [verificationEmail, setVerificationEmail] = useState<string | null>(null);
  const [code, setCode] = useState("");
  const [resetTicket, setResetTicket] = useState<string | null>(null);
  const [resendAvailableAt, setResendAvailableAt] = useState<string | null>(null);
  const [googleBusy, setGoogleBusy] = useState(false);
  const firstFieldRef = useRef<HTMLInputElement>(null);
  const stepFieldRef = useRef<HTMLInputElement>(null);

  const enterAsGuest = async () => {
    setError(null);
    setGuestPending(true);
    try {
      await continueAsGuest();
    } catch (guestError) {
      setError(guestError instanceof Error ? guestError.message : String(guestError));
    } finally {
      setGuestPending(false);
    }
  };

  if (status === "loading") {
    return (
      <main className="grid min-h-full place-items-center bg-bg px-5" aria-busy="true">
        <div className="flex items-center gap-3 text-fg">
          <img src="/favicon.png" alt="" className="h-10 w-10 rounded-xl object-contain shadow-soft" />
          <span className="text-lg font-bold tracking-tight">QuestLog</span>
          <Loader2 className="animate-spin text-primary" size={18} aria-hidden="true" />
          <span className="sr-only">{t("auth.loading")}</span>
        </div>
      </main>
    );
  }

  if (status === "error") {
    return (
      <main className="grid min-h-full place-items-center bg-bg px-5 py-8">
        <section className="surface-card w-full max-w-xl p-6 sm:p-8" aria-labelledby="auth-setup-title">
          <div className="mb-5 flex items-center gap-3">
            <span className="grid h-11 w-11 place-items-center rounded-xl bg-warning/15 text-warning">
              <AlertTriangle aria-hidden="true" />
            </span>
            <div>
              <p className="text-sm font-semibold text-primary">QuestLog account</p>
              <h1 id="auth-setup-title" className="text-xl font-extrabold tracking-tight text-fg">
                {t("auth.serverUnavailable")}
              </h1>
            </div>
          </div>
          <p className="mb-5 text-sm leading-6 text-muted-fg">
            {startupError}
          </p>
          <button
            type="button"
            onClick={() => void retry()}
            className="glass-surface flex min-h-11 items-center justify-center gap-2 rounded-lg bg-primary px-4 font-bold text-primary-fg outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
          >
            <RefreshCw size={17} aria-hidden="true" />
            {t("auth.retry")}
          </button>
          {guestAvailable ? (
            <button
              type="button"
              onClick={() => void enterAsGuest()}
              disabled={guestPending}
              className="mt-3 flex min-h-10 items-center gap-2 rounded-lg px-1 text-sm font-bold text-primary outline-none hover:text-primary/80 disabled:cursor-wait disabled:opacity-60 focus-visible:ring-2 focus-visible:ring-primary/45"
            >
              {guestPending ? (
                <Loader2 className="animate-spin" size={16} aria-hidden="true" />
              ) : (
                <ArrowRight size={16} aria-hidden="true" />
              )}
              {t("auth.continueWithoutSignIn")}
            </button>
          ) : null}
          {error ? <p className="mt-3 text-sm text-danger" role="alert">{error}</p> : null}
        </section>
      </main>
    );
  }

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);

    if (status === "unconfigured") {
      setError(t("auth.setupDescription"));
      return;
    }

    if (mode === "forgot" && !identifier.trim()) {
      setError(t("auth.fieldsRequired"));
      firstFieldRef.current?.focus();
      return;
    }

    if (mode === "newPassword") {
      if (password.length < 12) {
        setError(t("auth.passwordPolicy"));
        return;
      }
      if (password !== confirmPassword) {
        setError(t("auth.passwordMismatch"));
        return;
      }
    }

    if (mode === "reset" && code.length !== EMAIL_CODE_LENGTH) {
      setError(t("auth.fieldsRequired"));
      return;
    }

    if (mode === "login" && (!identifier.trim() || !password)) {
      setError(t("auth.fieldsRequired"));
      firstFieldRef.current?.focus();
      return;
    }

    if (mode === "signup") {
      if (!username.trim() || !email.trim() || !password) {
        setError(t("auth.fieldsRequired"));
        firstFieldRef.current?.focus();
        return;
      }
      if (password !== confirmPassword) {
        setError(t("auth.passwordMismatch"));
        return;
      }
    }

    setPending(true);
    try {
      if (mode === "login") {
        await login({ identifier: identifier.trim(), password });
      } else if (mode === "signup") {
        const response = await signup({ username: username.trim(), email: email.trim(), password });
        setVerificationEmail(response.email);
      } else if (mode === "forgot") {
        // Server luon tra 202 du tai khoan co ton tai hay khong, nen buoc nay
        // khong tiet lo email nao da dang ky.
        await requestRecoveryCode();
        setCode("");
        goTo("reset");
      } else if (mode === "reset") {
        // Kiem tra ma NGAY; dung thi moi sang man dat mat khau moi.
        setResetTicket(await verifyResetCode(identifier.trim(), code));
        setPassword("");
        setConfirmPassword("");
        goTo("newPassword");
      } else {
        await resetPassword({ resetTicket: resetTicket ?? "", newPassword: password });
      }
    } catch (submitError) {
      if (mode === "reset") setCode("");
      if (mode === "newPassword" && (submitError as { code?: unknown }).code === "RESET_TICKET_INVALID") {
        // Ticket het han (10 phut): quay lai buoc nhap ma de xin ma moi.
        setResetTicket(null);
        setCode("");
        goTo("reset");
      }
      setError(authErrorMessage(submitError, t));
    } finally {
      setPending(false);
    }
  };

  // Server luon tra 202 (khong tiet lo tai khoan co ton tai) va tu ap cooldown.
  const requestRecoveryCode = async () => {
    await forgotPassword(identifier.trim());
    setResendAvailableAt(new Date(Date.now() + 60_000).toISOString());
  };

  const switchMode = (nextMode: Mode) => {
    setMode(nextMode);
    setError(null);
    setVerificationEmail(null);
    setCode("");
    setResetTicket(null);
    window.setTimeout(() => firstFieldRef.current?.focus(), 0);
  };

  // Buoc tiep theo trong luong quen mat khau: giu nguyen du lieu da nhap.
  const goTo = (nextMode: Mode) => {
    setMode(nextMode);
    window.setTimeout(() => stepFieldRef.current?.focus(), 0);
  };

  const recovering = mode === "forgot" || mode === "newPassword" || mode === "reset";
  const showGoogle = Boolean(config?.googleEnabled) && status !== "unconfigured" && !recovering;
  const titleKey = {
    login: "auth.signInTitle",
    signup: "auth.signUpTitle",
    forgot: "auth.forgotTitle",
    newPassword: "auth.forgotTitle",
    reset: "auth.forgotTitle",
  }[mode];
  const descriptionKey = {
    login: "auth.signInDescription",
    signup: "auth.signUpDescription",
    forgot: "auth.forgotDescription",
    reset: "auth.codeSentIfExists",
    newPassword: "auth.newPasswordDescription",
  }[mode];
  const submitKey = {
    login: "auth.signIn",
    signup: "auth.createAccount",
    forgot: "auth.sendCode",
    reset: "auth.verifyCode",
    newPassword: "auth.resetPassword",
  }[mode];

  return (
    <main className="grid min-h-full bg-bg lg:grid-cols-[minmax(280px,0.9fr)_minmax(440px,1.1fr)]">
      <aside className="relative isolate hidden overflow-hidden border-r border-border bg-surface/55 p-10 lg:flex lg:flex-col">
        <div className="pointer-events-none absolute inset-0 overflow-hidden" aria-hidden="true">
          <img
            src="/assets/auth/quest-map.png"
            alt=""
            className="absolute left-[48%] top-[53%] w-[118%] max-w-none -translate-x-1/2 -translate-y-1/2 opacity-[0.42] saturate-[0.72] contrast-[0.92]"
            style={{
              maskImage:
                "radial-gradient(ellipse 76% 61% at 46% 52%, black 35%, rgba(0, 0, 0, 0.9) 57%, transparent 90%)",
              WebkitMaskImage:
                "radial-gradient(ellipse 76% 61% at 46% 52%, black 35%, rgba(0, 0, 0, 0.9) 57%, transparent 90%)",
            }}
          />
          <div className="absolute inset-0 bg-gradient-to-b from-surface/85 via-surface/25 to-surface/90" />
          <div className="absolute inset-0 bg-gradient-to-r from-surface/20 via-transparent to-surface/90" />
        </div>

        <div className="relative z-10 flex items-center gap-3">
          <img src="/favicon.png" alt="" className="h-11 w-11 rounded-xl object-contain shadow-soft" />
          <span className="text-xl font-extrabold tracking-tight text-fg">QuestLog</span>
        </div>
        <div className="relative z-10 my-auto max-w-md">
          <Gamepad2 className="mb-6 text-primary" size={38} strokeWidth={1.7} aria-hidden="true" />
          <h1 className="text-balance text-4xl font-black leading-tight tracking-[-0.035em] text-fg">
            {t("auth.welcomeLine")}
          </h1>
          <p className="mt-4 max-w-sm text-base leading-7 text-muted-fg">{t("auth.welcomeDescription")}</p>
        </div>
        <p className="relative z-10 flex items-center gap-2 text-xs text-muted-fg">
          <ShieldCheck size={15} className="text-primary" aria-hidden="true" />
          {t("auth.trustLine")}
        </p>
      </aside>

      <section className="relative flex min-h-full items-center justify-center px-5 py-9 sm:px-8">
        <div className="absolute right-5 top-5 flex rounded-lg border border-border bg-surface/70 p-1" aria-label={t("auth.language")}>
          {(["en-US", "vi-VN"] as const).map((value) => (
            <button
              key={value}
              type="button"
              onClick={() => setLang(value)}
              aria-pressed={lang === value}
              className={cn(
                "min-h-9 rounded-md px-2.5 text-xs font-bold outline-none focus-visible:ring-2 focus-visible:ring-primary/45",
                lang === value ? "bg-primary text-primary-fg" : "text-muted-fg hover:text-fg",
              )}
            >
              {value === "en-US" ? "EN" : "VI"}
            </button>
          ))}
        </div>

        <div className="w-full max-w-[390px]">
          <div className="mb-7 lg:hidden">
            <div className="mb-5 flex items-center gap-2.5">
              <img src="/favicon.png" alt="" className="h-9 w-9 rounded-lg object-contain" />
              <span className="font-extrabold tracking-tight">QuestLog</span>
            </div>
          </div>

          {verificationEmail ? (
            <div role="status" className="surface-card p-6 text-center">
              <CheckCircle2 className="mx-auto text-primary" size={36} aria-hidden="true" />
              <h1 className="mt-4 text-2xl font-black tracking-tight">{t("auth.checkEmail")}</h1>
              <p className="mt-2 text-sm leading-6 text-muted-fg">
                {t("auth.verificationSent")} <span className="font-semibold text-fg">{verificationEmail}</span>
              </p>
              <button
                type="button"
                onClick={() => switchMode("login")}
                className="mt-6 min-h-11 w-full rounded-lg bg-primary px-4 font-bold text-primary-fg outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
              >
                {t("auth.backToSignIn")}
              </button>
            </div>
          ) : (
            <>
              {status === "unconfigured" ? (
                <div className="mb-5 rounded-xl border border-warning/45 bg-warning/10 p-3.5" role="status">
                  <div className="flex items-start gap-2.5">
                    <ServerCog className="mt-0.5 shrink-0 text-warning" size={17} aria-hidden="true" />
                    <div>
                      <p className="text-sm font-extrabold text-fg">{t("auth.setupRequired")}</p>
                      <p className="mt-1 text-xs leading-5 text-muted-fg">{t("auth.setupDescription")}</p>
                    </div>
                  </div>
                </div>
              ) : null}
              {recovering ? (
                <button
                  type="button"
                  onClick={() => switchMode("login")}
                  className="mb-4 inline-flex min-h-9 items-center gap-1.5 rounded-md px-1 text-sm font-bold text-muted-fg outline-none hover:text-fg focus-visible:ring-2 focus-visible:ring-primary/45"
                >
                  <ArrowLeft size={16} aria-hidden="true" />
                  {t("auth.backToSignIn")}
                </button>
              ) : null}
              <h1 className="text-3xl font-black tracking-[-0.025em] text-fg">{t(titleKey)}</h1>
              <p className="mt-2 text-sm text-muted-fg">{t(descriptionKey)}</p>

              {recovering ? null : (
              <div className="mt-6 grid grid-cols-2 gap-1 rounded-xl border border-border bg-surface/65 p-1">
                {(["login", "signup"] as const).map((value) => (
                  <button
                    key={value}
                    type="button"
                    onClick={() => switchMode(value)}
                    aria-pressed={mode === value}
                    className={cn(
                      "min-h-10 rounded-lg px-3 text-sm font-bold outline-none focus-visible:ring-2 focus-visible:ring-primary/45",
                      mode === value ? "bg-bg text-fg shadow-soft" : "text-muted-fg hover:text-fg",
                    )}
                  >
                    {t(value === "login" ? "auth.signIn" : "auth.createAccount")}
                  </button>
                ))}
              </div>
              )}

              {showGoogle ? (
                <div className="mt-6">
                  <GoogleSignInButton disabled={pending || guestPending} onBusyChange={setGoogleBusy} />
                  <div className="mt-5 flex items-center gap-3" aria-hidden="true">
                    <span className="h-px flex-1 bg-border" />
                    <span className="text-xs font-bold uppercase tracking-wider text-muted-fg">{t("auth.or")}</span>
                    <span className="h-px flex-1 bg-border" />
                  </div>
                </div>
              ) : null}

              <form
                className={cn("space-y-4", showGoogle ? "mt-5" : "mt-6")}
                onSubmit={submit}
                aria-busy={pending}
                aria-disabled={googleBusy}
                noValidate
              >
                {mode !== "signup" ? (
                  <label className="block">
                    <span className="mb-1.5 block text-sm font-bold text-fg">{t("auth.identifier")}</span>
                    <input
                      ref={firstFieldRef}
                      name="identifier"
                      autoComplete="username"
                      value={identifier}
                      readOnly={mode === "newPassword" || mode === "reset"}
                      onChange={(event) => setIdentifier(event.target.value)}
                      aria-invalid={Boolean(error && !identifier.trim())}
                      className={inputClass}
                    />
                  </label>
                ) : (
                  <>
                    <label className="block">
                      <span className="mb-1.5 block text-sm font-bold text-fg">{t("auth.username")}</span>
                      <input
                        ref={firstFieldRef}
                        name="username"
                        autoComplete="username"
                        value={username}
                        onChange={(event) => setUsername(event.target.value)}
                        minLength={3}
                        maxLength={32}
                        className={inputClass}
                      />
                    </label>
                    <label className="block">
                      <span className="mb-1.5 block text-sm font-bold text-fg">{t("auth.email")}</span>
                      <input
                        name="email"
                        type="email"
                        inputMode="email"
                        autoComplete="email"
                        value={email}
                        onChange={(event) => setEmail(event.target.value)}
                        className={inputClass}
                      />
                    </label>
                  </>
                )}

                {mode === "reset" ? (
                  <EmailCodeField
                    id="recovery-code"
                    disabled={pending}
                    label={t("auth.code")}
                    inputRef={stepFieldRef}
                    value={code}
                    onChange={(value) => {
                      setCode(value);
                      setError(null);
                    }}
                    resendAvailableAt={resendAvailableAt}
                    onResend={async () => {
                      setError(null);
                      try {
                        await requestRecoveryCode();
                      } catch (resendError) {
                        setError(authErrorMessage(resendError, t));
                      }
                    }}
                  />
                ) : null}

                {mode === "forgot" || mode === "reset" ? null : (
                <label className="block">
                  <span className="mb-1.5 block text-sm font-bold text-fg">
                    {t(mode === "newPassword" ? "auth.newPassword" : "auth.password")}
                  </span>
                  <span className="relative block">
                    <input
                      ref={mode === "newPassword" ? stepFieldRef : undefined}
                      name="password"
                      type={showPassword ? "text" : "password"}
                      autoComplete={mode === "login" ? "current-password" : "new-password"}
                      value={password}
                      onChange={(event) => setPassword(event.target.value)}
                      className={cn(inputClass, "pr-11")}
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
                  {mode === "login" ? (
                    <span className="mt-1.5 flex justify-end">
                      <button
                        type="button"
                        onClick={() => switchMode("forgot")}
                        className="min-h-8 rounded-md px-1 text-xs font-bold text-primary outline-none hover:text-primary/80 focus-visible:ring-2 focus-visible:ring-primary/45"
                      >
                        {t("auth.forgotPassword")}
                      </button>
                    </span>
                  ) : null}
                  {mode === "newPassword" ? (
                    <span className="mt-1.5 block text-xs text-muted-fg">{t("auth.passwordPolicy")}</span>
                  ) : null}
                </label>
                )}

                {mode === "signup" || mode === "newPassword" ? (
                  <label className="block">
                    <span className="mb-1.5 block text-sm font-bold text-fg">{t("auth.confirmPassword")}</span>
                    <input
                      name="confirmPassword"
                      type={showPassword ? "text" : "password"}
                      autoComplete="new-password"
                      value={confirmPassword}
                      onChange={(event) => setConfirmPassword(event.target.value)}
                      className={inputClass}
                    />
                    {mode === "signup" ? (
                      <span className="mt-1.5 block text-xs text-muted-fg">{t("auth.verificationHint")}</span>
                    ) : null}
                  </label>
                ) : null}

                {error ? (
                  <p className="flex items-start gap-2 rounded-lg border border-danger/50 bg-danger/10 p-3 text-sm text-fg" role="alert">
                    <AlertTriangle className="mt-0.5 shrink-0 text-danger" size={16} aria-hidden="true" />
                    {error}
                  </p>
                ) : null}

                <button
                  type="submit"
                  disabled={pending || googleBusy}
                  className="glass-surface flex min-h-11 w-full items-center justify-center gap-2 rounded-lg bg-primary px-4 font-extrabold text-primary-fg outline-none disabled:cursor-wait disabled:opacity-65 focus-visible:ring-2 focus-visible:ring-primary/50"
                >
                  {pending ? <Loader2 className="animate-spin" size={17} aria-hidden="true" /> : <ArrowRight size={17} aria-hidden="true" />}
                  {t(submitKey)}
                </button>
              </form>
              {guestAvailable && !recovering ? (
                <div className="mt-5 flex items-center gap-3">
                  <span className="h-px flex-1 bg-border" aria-hidden="true" />
                  <button
                    type="button"
                    onClick={() => void enterAsGuest()}
                    disabled={pending || guestPending || googleBusy}
                    className="inline-flex min-h-9 items-center gap-1.5 rounded-md px-2 text-sm font-bold text-muted-fg outline-none transition-colors hover:text-primary disabled:cursor-wait disabled:opacity-60 focus-visible:ring-2 focus-visible:ring-primary/45"
                  >
                    {guestPending ? (
                      <Loader2 className="animate-spin" size={15} aria-hidden="true" />
                    ) : null}
                    {t("auth.continueWithoutSignIn")}
                  </button>
                  <span className="h-px flex-1 bg-border" aria-hidden="true" />
                </div>
              ) : null}
            </>
          )}
        </div>
      </section>
    </main>
  );
}
