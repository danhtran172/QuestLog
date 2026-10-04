import { useEffect, useRef, useState, type Ref } from "react";
import { Loader2, RefreshCw } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { EMAIL_CODE_LENGTH, normalizeEmailCode } from "@/lib/emailCode";
import { cn } from "@/lib/utils";

function secondsUntil(iso: string | null, now: number): number {
  if (!iso) return 0;
  const at = Date.parse(iso);
  return Number.isFinite(at) ? Math.max(0, Math.ceil((at - now) / 1000)) : 0;
}

/**
 * O nhap ma 6 so gui qua email, kem nut gui lai co dem nguoc. Du 6 so (go hay dan)
 * thi tu gui form chua no. Chi giu ma trong state cua form; gui di dung mot lan.
 */
export function EmailCodeField({
  id,
  label,
  value,
  onChange,
  resendAvailableAt,
  onResend,
  inputRef,
  disabled,
  className,
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  resendAvailableAt: string | null;
  onResend: () => Promise<void>;
  inputRef?: Ref<HTMLInputElement>;
  /** Dang gui form: khong tu gui them lan nua. */
  disabled?: boolean;
  className?: string;
}) {
  const { t } = useI18n();
  const [now, setNow] = useState(() => Date.now());
  const [resending, setResending] = useState(false);
  const ownRef = useRef<HTMLInputElement | null>(null);
  const remaining = secondsUntil(resendAvailableAt, now);

  useEffect(() => {
    if (remaining <= 0) return;
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [remaining]);

  const setRefs = (element: HTMLInputElement | null) => {
    ownRef.current = element;
    if (typeof inputRef === "function") inputRef(element);
    else if (inputRef) inputRef.current = element;
  };

  const change = (raw: string) => {
    const next = normalizeEmailCode(raw);
    onChange(next);
    if (next.length === EMAIL_CODE_LENGTH && value.length !== EMAIL_CODE_LENGTH && !disabled) {
      // Doi React commit gia tri moi roi moi gui, de handler doc dung ma vua nhap.
      window.setTimeout(() => ownRef.current?.form?.requestSubmit(), 0);
    }
  };

  const resend = async () => {
    setResending(true);
    try {
      await onResend();
    } finally {
      setResending(false);
      setNow(Date.now());
    }
  };

  return (
    <div>
      <label className="block" htmlFor={id}>
        <span className="mb-1.5 block text-xs font-bold uppercase tracking-wide text-muted-fg">{label}</span>
        <input
          ref={setRefs}
          id={id}
          name={id}
          inputMode="numeric"
          autoComplete="one-time-code"
          placeholder={"•".repeat(EMAIL_CODE_LENGTH)}
          value={value}
          readOnly={disabled}
          onChange={(event) => change(event.target.value)}
          aria-describedby={`${id}-hint`}
          className={cn(
            "h-11 w-full rounded-lg border border-border bg-bg/70 px-3 text-center font-mono text-lg tracking-[0.4em] text-fg shadow-inner outline-none",
            "placeholder:text-muted-fg/40 focus:border-primary focus:ring-2 focus:ring-primary/25",
            className,
          )}
        />
      </label>
      <div className="mt-1.5 flex justify-end">
        <button
          type="button"
          onClick={() => void resend()}
          disabled={remaining > 0 || resending}
          className="inline-flex min-h-8 items-center gap-1.5 rounded-md px-1.5 text-xs font-bold text-primary outline-none hover:text-primary/80 disabled:cursor-default disabled:text-muted-fg focus-visible:ring-2 focus-visible:ring-primary/45"
        >
          {resending ? <Loader2 className="animate-spin" size={13} aria-hidden="true" /> : <RefreshCw size={13} aria-hidden="true" />}
          <span id={`${id}-hint`}>
            {remaining > 0 ? t("auth.resendIn").replace("{seconds}", String(remaining)) : t("auth.resendCode")}
          </span>
        </button>
      </div>
    </div>
  );
}
