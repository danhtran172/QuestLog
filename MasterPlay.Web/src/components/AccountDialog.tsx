import { useEffect, useRef, useState, type FormEvent, type Ref } from "react";
import {
  AlertTriangle,
  AtSign,
  CheckCircle2,
  Eye,
  EyeOff,
  KeyRound,
  Loader2,
  LogOut,
  Pencil,
  Save,
  ShieldCheck,
  UserRound,
} from "lucide-react";
import { useAuth } from "@/lib/auth";
import { authErrorMessage, resendAvailableAtFromError } from "@/lib/authErrors";
import { EMAIL_CODE_LENGTH } from "@/lib/emailCode";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { EmailCodeField } from "./EmailCodeField";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, IconButton } from "./ui";

const fieldClass = cn(
  "h-11 w-full rounded-lg border border-border bg-bg/70 px-3 text-sm text-fg shadow-inner outline-none",
  "placeholder:text-muted-fg/70 focus:border-primary focus:ring-2 focus:ring-primary/25",
  "aria-[invalid=true]:border-danger aria-[invalid=true]:ring-1 aria-[invalid=true]:ring-danger/25",
);

type Feedback = { kind: "error" | "success"; message: string } | null;

function PasswordField({
  id,
  label,
  value,
  onChange,
  autoComplete = "current-password",
  describedBy,
  inputRef,
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  autoComplete?: "current-password" | "new-password";
  describedBy?: string;
  inputRef?: Ref<HTMLInputElement>;
}) {
  const { t } = useI18n();
  const [visible, setVisible] = useState(false);

  return (
    <label className="block" htmlFor={id}>
      <span className="mb-1.5 block text-xs font-bold uppercase tracking-wide text-muted-fg">
        {label}
      </span>
      <span className="relative block">
        <input
          ref={inputRef}
          id={id}
          name={id}
          type={visible ? "text" : "password"}
          autoComplete={autoComplete}
          value={value}
          onChange={(event) => onChange(event.target.value)}
          aria-describedby={describedBy}
          className={cn(fieldClass, "pr-11")}
        />
        <button
          type="button"
          onClick={() => setVisible((current) => !current)}
          aria-label={t(visible ? "auth.hidePassword" : "auth.showPassword")}
          className="absolute right-1 top-1 grid h-9 w-9 place-items-center rounded-md text-muted-fg outline-none transition-colors hover:bg-surface-2 hover:text-fg focus-visible:ring-2 focus-visible:ring-primary/45"
        >
          {visible ? <EyeOff size={17} aria-hidden="true" /> : <Eye size={17} aria-hidden="true" />}
        </button>
      </span>
    </label>
  );
}

function FeedbackMessage({ feedback }: { feedback: Feedback }) {
  if (!feedback) return null;
  const success = feedback.kind === "success";

  return (
    <p
      role={success ? "status" : "alert"}
      className={cn(
        "flex items-start gap-2 rounded-lg border p-3 text-sm",
        success
          ? "border-success/35 bg-success/10 text-fg"
          : "border-danger/45 bg-danger/10 text-fg",
      )}
    >
      {success ? (
        <CheckCircle2 className="mt-0.5 shrink-0 text-success" size={16} aria-hidden="true" />
      ) : (
        <AlertTriangle className="mt-0.5 shrink-0 text-danger" size={16} aria-hidden="true" />
      )}
      {feedback.message}
    </p>
  );
}

function SubmitButton({ pending, label }: { pending: boolean; label: string }) {
  return (
    <button
      type="submit"
      disabled={pending}
      className="inline-flex min-h-11 items-center justify-center gap-2 rounded-lg bg-primary px-4 text-sm font-extrabold text-primary-fg outline-none transition-colors hover:bg-primary/90 disabled:cursor-wait disabled:opacity-60 focus-visible:ring-2 focus-visible:ring-primary/50"
    >
      {pending ? <Loader2 className="animate-spin" size={16} aria-hidden="true" /> : <Save size={16} aria-hidden="true" />}
      {label}
    </button>
  );
}

export function AccountDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const { t } = useI18n();
  const {
    account,
    changePassword,
    changeUsername,
    requestEmailChange,
    sendReauthCode,
    verifyReauthCode,
    logout,
  } = useAuth();
  const [username, setUsername] = useState("");
  const [usernameEditing, setUsernameEditing] = useState(false);
  const [usernameConfirming, setUsernameConfirming] = useState(false);
  const [usernamePending, setUsernamePending] = useState(false);
  const [usernameFeedback, setUsernameFeedback] = useState<Feedback>(null);
  const usernameRef = useRef<HTMLInputElement>(null);
  const usernameCodeRef = useRef<HTMLInputElement>(null);

  const [email, setEmail] = useState("");
  const [emailEditing, setEmailEditing] = useState(false);
  const [emailConfirming, setEmailConfirming] = useState(false);
  const [emailPending, setEmailPending] = useState(false);
  const [emailFeedback, setEmailFeedback] = useState<Feedback>(null);
  const emailRef = useRef<HTMLInputElement>(null);
  const emailCodeRef = useRef<HTMLInputElement>(null);

  const [newPassword, setNewPassword] = useState("");
  const [passwordConfirmation, setPasswordConfirmation] = useState("");
  const [passwordEditing, setPasswordEditing] = useState(false);
  const [passwordConfirming, setPasswordConfirming] = useState(false);
  const [passwordPending, setPasswordPending] = useState(false);
  const [passwordFeedback, setPasswordFeedback] = useState<Feedback>(null);
  const passwordCodeRef = useRef<HTMLInputElement>(null);
  const newPasswordRef = useRef<HTMLInputElement>(null);
  const passwordConfirmationRef = useRef<HTMLInputElement>(null);

  const [code, setCode] = useState("");
  const [resendAvailableAt, setResendAvailableAt] = useState<string | null>(null);
  const [codeSending, setCodeSending] = useState(false);

  const [logoutPending, setLogoutPending] = useState(false);
  const [logoutError, setLogoutError] = useState<string | null>(null);
  const wasOpenRef = useRef(false);

  useEffect(() => {
    if (open && !wasOpenRef.current) {
      setUsername(account?.username ?? "");
      setEmail("");
      setCode("");
      setUsernameEditing(false);
      setUsernameConfirming(false);
      setCode("");
      setEmailEditing(false);
      setEmailConfirming(false);
      setCode("");
      setNewPassword("");
      setPasswordConfirmation("");
      setPasswordEditing(false);
      setPasswordConfirming(false);
      setUsernameFeedback(null);
      setEmailFeedback(null);
      setPasswordFeedback(null);
      setLogoutError(null);
    }
    wasOpenRef.current = open;
  }, [account?.username, open]);

  // Gui ma xac thuc lai. 429 CODE_RESEND_COOLDOWN nghia la ma vua gui van dung duoc.
  const requestCode = async (setFeedback: (feedback: Feedback) => void) => {
    setCodeSending(true);
    try {
      setResendAvailableAt(await sendReauthCode());
    } catch (error) {
      const cooldownUntil = resendAvailableAtFromError(error);
      if (cooldownUntil) setResendAvailableAt(cooldownUntil);
      else setFeedback({ kind: "error", message: authErrorMessage(error, t) });
    } finally {
      setCodeSending(false);
    }
  };

  const codeHint = codeSending
    ? t("account.codeSending")
    : t("account.codeSentTo").replace("{email}", account?.email ?? "");

  const submitUsername = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setUsernameFeedback(null);
    const trimmedUsername = username.trim();
    if (!/^[A-Za-z0-9_.-]{3,32}$/.test(trimmedUsername)) {
      setUsernameFeedback({ kind: "error", message: t("account.usernameRules") });
      usernameRef.current?.focus();
      return;
    }

    if (!usernameConfirming) {
      if (trimmedUsername === account?.username) {
        setUsernameFeedback({ kind: "error", message: t("account.noChanges") });
        usernameRef.current?.focus();
        return;
      }
      setUsernameConfirming(true);
      void requestCode(setUsernameFeedback);
      window.setTimeout(() => usernameCodeRef.current?.focus(), 0);
      return;
    }

    if (code.length !== EMAIL_CODE_LENGTH) {
      setUsernameFeedback({ kind: "error", message: t("auth.fieldsRequired") });
      usernameCodeRef.current?.focus();
      return;
    }

    setUsernamePending(true);
    try {
      await changeUsername({ username: trimmedUsername, reauthTicket: await verifyReauthCode(code) });
      setCode("");
      setUsernameEditing(false);
      setUsernameConfirming(false);
      setUsernameFeedback({ kind: "success", message: t("account.usernameSaved") });
    } catch (error) {
      setCode("");
      setUsernameFeedback({
        kind: "error",
        message: authErrorMessage(error, t),
      });
      window.setTimeout(() => usernameCodeRef.current?.focus(), 0);
    } finally {
      setUsernamePending(false);
    }
  };

  const editUsername = () => {
    setUsername(account?.username ?? "");
    setCode("");
    setUsernameConfirming(false);
    setUsernameFeedback(null);
    setUsernameEditing(true);
    window.setTimeout(() => usernameRef.current?.focus(), 0);
  };

  const cancelUsernameEdit = () => {
    setUsername(account?.username ?? "");
    setCode("");
    setUsernameEditing(false);
    setUsernameConfirming(false);
    setUsernameFeedback(null);
  };

  const submitEmail = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setEmailFeedback(null);
    if (!emailRef.current?.validity.valid) {
      setEmailFeedback({ kind: "error", message: t("auth.fieldsRequired") });
      emailRef.current?.focus();
      return;
    }

    const trimmedEmail = email.trim();
    if (!emailConfirming) {
      if (trimmedEmail.toLowerCase() === account?.email.toLowerCase()) {
        setEmailFeedback({ kind: "error", message: t("account.noChanges") });
        emailRef.current?.focus();
        return;
      }
      setEmailConfirming(true);
      void requestCode(setEmailFeedback);
      window.setTimeout(() => emailCodeRef.current?.focus(), 0);
      return;
    }

    if (code.length !== EMAIL_CODE_LENGTH) {
      setEmailFeedback({ kind: "error", message: t("auth.fieldsRequired") });
      emailCodeRef.current?.focus();
      return;
    }

    setEmailPending(true);
    try {
      const response = await requestEmailChange({ email: trimmedEmail, reauthTicket: await verifyReauthCode(code) });
      setCode("");
      setEmailEditing(false);
      setEmailConfirming(false);
      setEmailFeedback({
        kind: "success",
        message: `${t("account.emailConfirmationSent")} ${response.email}.`,
      });
    } catch (error) {
      setCode("");
      setEmailFeedback({
        kind: "error",
        message: authErrorMessage(error, t),
      });
      window.setTimeout(() => emailCodeRef.current?.focus(), 0);
    } finally {
      setEmailPending(false);
    }
  };

  const editEmail = () => {
    setEmail(account?.email ?? "");
    setCode("");
    setEmailConfirming(false);
    setEmailFeedback(null);
    setEmailEditing(true);
    window.setTimeout(() => emailRef.current?.focus(), 0);
  };

  const cancelEmailEdit = () => {
    setEmail(account?.email ?? "");
    setCode("");
    setEmailEditing(false);
    setEmailConfirming(false);
    setEmailFeedback(null);
  };

  const submitPassword = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setPasswordFeedback(null);
    if (newPassword.length < 12) {
      setPasswordFeedback({ kind: "error", message: t("auth.passwordPolicy") });
      newPasswordRef.current?.focus();
      return;
    }
    if (newPassword !== passwordConfirmation) {
      setPasswordFeedback({ kind: "error", message: t("auth.passwordMismatch") });
      passwordConfirmationRef.current?.focus();
      return;
    }

    if (!passwordConfirming) {
      setPasswordConfirming(true);
      void requestCode(setPasswordFeedback);
      window.setTimeout(() => passwordCodeRef.current?.focus(), 0);
      return;
    }

    if (code.length !== EMAIL_CODE_LENGTH) {
      setPasswordFeedback({ kind: "error", message: t("auth.fieldsRequired") });
      passwordCodeRef.current?.focus();
      return;
    }

    setPasswordPending(true);
    try {
      await changePassword({ newPassword, reauthTicket: await verifyReauthCode(code) });
      setCode("");
      setNewPassword("");
      setPasswordConfirmation("");
      setPasswordEditing(false);
      setPasswordConfirming(false);
      setPasswordFeedback({ kind: "success", message: t(hasPassword ? "account.passwordChanged" : "account.passwordSet") });
    } catch (error) {
      setCode("");
      setPasswordFeedback({
        kind: "error",
        message: authErrorMessage(error, t),
      });
      window.setTimeout(() => passwordCodeRef.current?.focus(), 0);
    } finally {
      setPasswordPending(false);
    }
  };

  const editPassword = () => {
    setCode("");
    setNewPassword("");
    setPasswordConfirmation("");
    setPasswordConfirming(false);
    setPasswordFeedback(null);
    setPasswordEditing(true);
    window.setTimeout(() => newPasswordRef.current?.focus(), 0);
  };

  const cancelPasswordEdit = () => {
    setCode("");
    setNewPassword("");
    setPasswordConfirmation("");
    setPasswordEditing(false);
    setPasswordConfirming(false);
    setPasswordFeedback(null);
  };

  const signOut = async () => {
    setLogoutError(null);
    setLogoutPending(true);
    try {
      await logout();
    } catch (error) {
      setLogoutError(error instanceof Error ? error.message : String(error));
      setLogoutPending(false);
    }
  };

  const anotherSectionIsEditing = usernameEditing || emailEditing || passwordEditing;
  const hasPassword = account?.hasPassword ?? true;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[88vh] max-w-xl gap-0 overflow-hidden p-0">
        <DialogHeader className="border-b border-border px-5 py-4 pr-16">
          <DialogTitle className="text-lg">{t("account.title")}</DialogTitle>
          <DialogDescription>{t("account.description")}</DialogDescription>
        </DialogHeader>

        <div className="scrollbar-thin max-h-[calc(88vh-82px)] overflow-y-auto">
          <section className="border-b border-border bg-surface-2/20 px-5 py-4" aria-label={t("menu.account")}>
            <div className="flex items-center gap-3">
              <span className="grid h-11 w-11 shrink-0 place-items-center rounded-xl border border-primary/25 bg-primary/12 text-primary">
                <UserRound size={21} aria-hidden="true" />
              </span>
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-extrabold text-fg">{account?.username}</p>
                <p className="truncate text-xs text-muted-fg">{account?.email}</p>
              </div>
              <span className="rounded-md border border-border bg-bg/40 px-2 py-1 text-fs-10 font-bold uppercase tracking-wider text-muted-fg">
                {account?.role}
              </span>
            </div>
          </section>

          <div className="space-y-4 p-5">
            <form className="rounded-xl border border-border bg-surface-2/20 p-4" onSubmit={submitUsername} aria-busy={usernamePending}>
              <div className="flex items-start justify-between gap-3">
                <div className="flex min-w-0 items-start gap-3">
                  <UserRound className="mt-0.5 shrink-0 text-primary" size={18} aria-hidden="true" />
                  <div>
                    <h3 className="text-sm font-extrabold text-fg">{t("account.usernameTitle")}</h3>
                    <p className="mt-0.5 text-xs leading-5 text-muted-fg">{t("account.usernameDescription")}</p>
                  </div>
                </div>
                {!anotherSectionIsEditing ? (
                  <IconButton
                    onClick={editUsername}
                    label={`${t("action.edit")} ${t("account.usernameTitle")}`}
                    icon={<Pencil aria-hidden="true" />}
                    tone="edit"
                    className="h-11 w-11"
                  />
                ) : null}
              </div>
              {usernameEditing ? (
                <div className="mt-4 space-y-3">
                  <label className="block" htmlFor="account-username">
                    <span className="mb-1.5 block text-xs font-bold uppercase tracking-wide text-muted-fg">{t("auth.username")}</span>
                    <input
                      ref={usernameRef}
                      id="account-username"
                      name="username"
                      autoComplete="username"
                      value={username}
                      onChange={(event) => {
                        setUsername(event.target.value);
                        setUsernameFeedback(null);
                        if (usernameConfirming) {
                          setUsernameConfirming(false);
                          setCode("");
                        }
                      }}
                      aria-describedby="account-username-rules"
                      className={fieldClass}
                    />
                    <span id="account-username-rules" className="mt-1.5 block text-xs text-muted-fg">{t("account.usernameRules")}</span>
                  </label>
                  {usernameConfirming ? (
                    <div className="rounded-lg border border-primary/20 bg-primary/5 p-3">
                      <p className="mb-3 text-xs leading-5 text-muted-fg">{codeHint}</p>
                      <EmailCodeField
                        id="username-email-code"
                        disabled={usernamePending}
                        label={t("auth.code")}
                        value={code}
                        onChange={(value) => {
                          setCode(value);
                          setUsernameFeedback(null);
                        }}
                        resendAvailableAt={resendAvailableAt}
                        onResend={() => requestCode(setUsernameFeedback)}
                        inputRef={usernameCodeRef}
                      />
                    </div>
                  ) : null}
                  <FeedbackMessage feedback={usernameFeedback} />
                  <div className="flex justify-end gap-2">
                    <button
                      type="button"
                      onClick={cancelUsernameEdit}
                      disabled={usernamePending}
                      className="min-h-11 rounded-lg border border-border px-4 text-sm font-bold text-muted-fg outline-none hover:bg-surface-2 hover:text-fg disabled:cursor-wait disabled:opacity-60 focus-visible:ring-2 focus-visible:ring-primary/45"
                    >
                      {t("action.cancel")}
                    </button>
                    <SubmitButton
                      pending={usernamePending}
                      label={t(usernameConfirming ? "account.confirmSave" : "action.save")}
                    />
                  </div>
                </div>
              ) : (
                <div className="mt-3 border-t border-border/70 pt-3">
                  <p className="truncate text-sm font-bold text-fg">{account?.username}</p>
                  {usernameFeedback ? (
                    <div className="mt-3"><FeedbackMessage feedback={usernameFeedback} /></div>
                  ) : null}
                </div>
              )}
            </form>

            <form className="rounded-xl border border-border bg-surface-2/20 p-4" onSubmit={submitEmail} aria-busy={emailPending}>
              <div className="flex items-start justify-between gap-3">
                <div className="flex min-w-0 items-start gap-3">
                  <AtSign className="mt-0.5 shrink-0 text-primary" size={18} aria-hidden="true" />
                  <div>
                    <h3 className="text-sm font-extrabold text-fg">{t("account.emailTitle")}</h3>
                    <p className="mt-0.5 text-xs leading-5 text-muted-fg">{t("account.emailDescription")}</p>
                  </div>
                </div>
                {!anotherSectionIsEditing ? (
                  <IconButton
                    onClick={editEmail}
                    label={`${t("action.edit")} ${t("account.emailTitle")}`}
                    icon={<Pencil aria-hidden="true" />}
                    tone="edit"
                    className="h-11 w-11"
                  />
                ) : null}
              </div>
              {emailEditing ? (
                <div className="mt-4 space-y-3">
                  <label className="block" htmlFor="account-new-email">
                    <span className="mb-1.5 block text-xs font-bold uppercase tracking-wide text-muted-fg">{t("account.newEmail")}</span>
                    <input
                      ref={emailRef}
                      id="account-new-email"
                      name="email"
                      type="email"
                      required
                      autoComplete="email"
                      value={email}
                      onChange={(event) => {
                        setEmail(event.target.value);
                        setEmailFeedback(null);
                        if (emailConfirming) {
                          setEmailConfirming(false);
                          setCode("");
                        }
                      }}
                      className={fieldClass}
                    />
                  </label>
                  {emailConfirming ? (
                    <div className="rounded-lg border border-primary/20 bg-primary/5 p-3">
                      <p className="mb-3 text-xs leading-5 text-muted-fg">{codeHint}</p>
                      <EmailCodeField
                        id="email-email-code"
                        disabled={emailPending}
                        label={t("auth.code")}
                        value={code}
                        onChange={(value) => {
                          setCode(value);
                          setEmailFeedback(null);
                        }}
                        resendAvailableAt={resendAvailableAt}
                        onResend={() => requestCode(setEmailFeedback)}
                        inputRef={emailCodeRef}
                      />
                    </div>
                  ) : null}
                  <FeedbackMessage feedback={emailFeedback} />
                  <div className="flex justify-end gap-2">
                    <button
                      type="button"
                      onClick={cancelEmailEdit}
                      disabled={emailPending}
                      className="min-h-11 rounded-lg border border-border px-4 text-sm font-bold text-muted-fg outline-none hover:bg-surface-2 hover:text-fg disabled:cursor-wait disabled:opacity-60 focus-visible:ring-2 focus-visible:ring-primary/45"
                    >
                      {t("action.cancel")}
                    </button>
                    <SubmitButton
                      pending={emailPending}
                      label={t(emailConfirming ? "account.confirmSave" : "action.save")}
                    />
                  </div>
                </div>
              ) : (
                <div className="mt-3 border-t border-border/70 pt-3">
                  <p className="truncate text-sm font-bold text-fg">{account?.email}</p>
                  {emailFeedback ? (
                    <div className="mt-3"><FeedbackMessage feedback={emailFeedback} /></div>
                  ) : null}
                </div>
              )}
            </form>

            <form className="rounded-xl border border-border bg-surface-2/20 p-4" onSubmit={submitPassword} aria-busy={passwordPending}>
              <div className="flex items-start justify-between gap-3">
                <div className="flex min-w-0 items-start gap-3">
                  <KeyRound className="mt-0.5 shrink-0 text-primary" size={18} aria-hidden="true" />
                  <div>
                    <h3 className="text-sm font-extrabold text-fg">{t(hasPassword ? "account.passwordTitle" : "account.setPasswordTitle")}</h3>
                    <p className="mt-0.5 text-xs leading-5 text-muted-fg">{t(hasPassword ? "account.passwordDescription" : "account.setPasswordDescription")}</p>
                  </div>
                </div>
                {!anotherSectionIsEditing ? (
                  <IconButton
                    onClick={editPassword}
                    label={`${t("action.edit")} ${t("account.passwordTitle")}`}
                    icon={<Pencil aria-hidden="true" />}
                    tone="edit"
                    className="h-11 w-11"
                  />
                ) : null}
              </div>
              {passwordEditing ? (
                <div className="mt-4 space-y-3">
                  <PasswordField
                    id="password-new-password"
                    label={t("auth.newPassword")}
                    value={newPassword}
                    onChange={(value) => {
                      setNewPassword(value);
                      setPasswordFeedback(null);
                      if (passwordConfirming) {
                        setPasswordConfirming(false);
                        setCode("");
                      }
                    }}
                    autoComplete="new-password"
                    describedBy="account-password-policy"
                    inputRef={newPasswordRef}
                  />
                  <p id="account-password-policy" className="flex items-center gap-1.5 text-xs text-muted-fg"><ShieldCheck size={14} className="text-primary" aria-hidden="true" />{t("auth.passwordPolicy")}</p>
                  <PasswordField
                    id="password-confirmation"
                    label={t("auth.confirmPassword")}
                    value={passwordConfirmation}
                    onChange={(value) => {
                      setPasswordConfirmation(value);
                      setPasswordFeedback(null);
                      if (passwordConfirming) {
                        setPasswordConfirming(false);
                        setCode("");
                      }
                    }}
                    autoComplete="new-password"
                    inputRef={passwordConfirmationRef}
                  />
                  {passwordConfirming ? (
                    <div className="rounded-lg border border-primary/20 bg-primary/5 p-3">
                      <p className="mb-3 text-xs leading-5 text-muted-fg">{codeHint}</p>
                      <EmailCodeField
                        id="password-email-code"
                        disabled={passwordPending}
                        label={t("auth.code")}
                        value={code}
                        onChange={(value) => {
                          setCode(value);
                          setPasswordFeedback(null);
                        }}
                        resendAvailableAt={resendAvailableAt}
                        onResend={() => requestCode(setPasswordFeedback)}
                        inputRef={passwordCodeRef}
                      />
                    </div>
                  ) : null}
                  <FeedbackMessage feedback={passwordFeedback} />
                  <div className="flex justify-end gap-2">
                    <button
                      type="button"
                      onClick={cancelPasswordEdit}
                      disabled={passwordPending}
                      className="min-h-11 rounded-lg border border-border px-4 text-sm font-bold text-muted-fg outline-none hover:bg-surface-2 hover:text-fg disabled:cursor-wait disabled:opacity-60 focus-visible:ring-2 focus-visible:ring-primary/45"
                    >
                      {t("action.cancel")}
                    </button>
                    <SubmitButton
                      pending={passwordPending}
                      label={t(passwordConfirming ? "account.confirmSave" : "action.save")}
                    />
                  </div>
                </div>
              ) : (
                <div className="mt-3 border-t border-border/70 pt-3">
                  {hasPassword ? (
                    <p className="text-sm font-bold tracking-[0.18em] text-fg">
                      <span aria-hidden="true">••••••••••••</span>
                      <span className="sr-only">{t("account.passwordMasked")}</span>
                    </p>
                  ) : (
                    <p className="text-sm font-bold text-muted-fg">{t("account.passwordNotSet")}</p>
                  )}
                  {passwordFeedback ? (
                    <div className="mt-3"><FeedbackMessage feedback={passwordFeedback} /></div>
                  ) : null}
                </div>
              )}
            </form>

            <section className="rounded-xl border border-danger/25 bg-danger/5 p-4">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <div>
                  <h3 className="text-sm font-extrabold text-fg">{t("account.signOutTitle")}</h3>
                  <p className="mt-0.5 text-xs leading-5 text-muted-fg">{t("account.signOutDescription")}</p>
                </div>
                <button
                  type="button"
                  onClick={() => void signOut()}
                  disabled={logoutPending}
                  className="inline-flex min-h-11 shrink-0 items-center justify-center gap-2 rounded-lg border border-danger/35 px-4 text-sm font-extrabold text-danger outline-none transition-colors hover:bg-danger/10 disabled:cursor-wait disabled:opacity-60 focus-visible:ring-2 focus-visible:ring-danger/40"
                >
                  {logoutPending ? <Loader2 className="animate-spin" size={16} aria-hidden="true" /> : <LogOut size={16} aria-hidden="true" />}
                  {t("menu.logout")}
                </button>
              </div>
              {logoutError ? <div className="mt-3"><FeedbackMessage feedback={{ kind: "error", message: logoutError }} /></div> : null}
            </section>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
