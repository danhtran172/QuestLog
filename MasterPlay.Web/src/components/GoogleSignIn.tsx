import { useEffect, useRef, useState } from "react";
import { ExternalLink, Loader2, X } from "lucide-react";
import { useAuth } from "@/lib/auth";
import { authErrorMessage, isAbortError } from "@/lib/authErrors";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";

/** Logo "G" nhieu mau cua Google, bundle san — khong tai tu URL ngoai. */
export function GoogleMark({ size = 18 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 48 48" aria-hidden="true" focusable="false">
      <path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z" />
      <path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z" />
      <path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z" />
      <path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z" />
    </svg>
  );
}

/**
 * Nut "Tiep tuc voi Google" + trang thai cho. Dang ky va dang nhap bang Google la
 * cung mot luong: tai khoan moi se duoc AuthGate dua sang man chon ten dang nhap.
 */
export function GoogleSignInButton({
  disabled,
  onBusyChange,
}: {
  disabled?: boolean;
  onBusyChange?: (busy: boolean) => void;
}) {
  const { signInWithGoogle, reopenGoogle } = useAuth();
  const { t } = useI18n();
  const [waiting, setWaiting] = useState(false);
  const [authorizeUrl, setAuthorizeUrl] = useState<string | null>(null);
  const [blocked, setBlocked] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const controllerRef = useRef<AbortController | null>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);

  useEffect(() => () => controllerRef.current?.abort(), []);

  const start = async () => {
    controllerRef.current?.abort();
    const controller = new AbortController();
    controllerRef.current = controller;
    setError(null);
    setBlocked(false);
    setAuthorizeUrl(null);
    setWaiting(true);
    onBusyChange?.(true);
    try {
      await signInWithGoogle(controller.signal, (event) => {
        setAuthorizeUrl(event.authorizeUrl);
        setBlocked(event.type === "blocked");
      });
    } catch (flowError) {
      if (!isAbortError(flowError)) setError(authErrorMessage(flowError, t));
      if (controllerRef.current === controller) {
        setWaiting(false);
        onBusyChange?.(false);
        window.setTimeout(() => buttonRef.current?.focus(), 0);
      }
    }
  };

  const cancel = () => {
    controllerRef.current?.abort();
    controllerRef.current = null;
    setWaiting(false);
    onBusyChange?.(false);
    window.setTimeout(() => buttonRef.current?.focus(), 0);
  };

  if (waiting) {
    return (
      <div className="rounded-xl border border-primary/30 bg-primary/5 p-4" role="status" aria-live="polite">
        <div className="flex items-start gap-3">
          <Loader2 className="mt-0.5 shrink-0 animate-spin text-primary" size={18} aria-hidden="true" />
          <div className="min-w-0">
            <p className="text-sm font-extrabold text-fg">{t("auth.googleWaiting")}</p>
            <p className="mt-1 text-xs leading-5 text-muted-fg">
              {blocked ? t("auth.googlePopupBlocked") : t("auth.googleWaitingHint")}
            </p>
          </div>
        </div>
        <div className="mt-3 flex flex-wrap justify-end gap-2">
          <button
            type="button"
            onClick={cancel}
            className="inline-flex min-h-10 items-center gap-1.5 rounded-lg border border-border px-3 text-sm font-bold text-muted-fg outline-none hover:bg-surface-2 hover:text-fg focus-visible:ring-2 focus-visible:ring-primary/45"
          >
            <X size={15} aria-hidden="true" />
            {t("action.cancel")}
          </button>
          {authorizeUrl ? (
            <button
              type="button"
              onClick={() => {
                reopenGoogle(authorizeUrl);
                setBlocked(false);
              }}
              className="inline-flex min-h-10 items-center gap-1.5 rounded-lg bg-primary px-3 text-sm font-bold text-primary-fg outline-none hover:bg-primary/90 focus-visible:ring-2 focus-visible:ring-primary/50"
            >
              <ExternalLink size={15} aria-hidden="true" />
              {t("auth.googleOpenAgain")}
            </button>
          ) : null}
        </div>
      </div>
    );
  }

  return (
    <div>
      <button
        ref={buttonRef}
        type="button"
        onClick={() => void start()}
        disabled={disabled}
        className={cn(
          "flex min-h-11 w-full items-center justify-center gap-2.5 rounded-lg border border-border bg-surface/70 px-4 font-bold text-fg outline-none transition-colors",
          "hover:border-primary/45 hover:bg-surface-2 disabled:cursor-not-allowed disabled:opacity-60 focus-visible:ring-2 focus-visible:ring-primary/45",
        )}
      >
        <GoogleMark />
        {t("auth.continueWithGoogle")}
      </button>
      {error ? (
        <p className="mt-2 text-sm text-danger" role="alert">{error}</p>
      ) : null}
    </div>
  );
}
