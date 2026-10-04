/**
 * Vong doi mot lan dang nhap Google phia client — thuan, khong React, de test.
 *
 * Host giu PKCE verifier va doi code (design 2026-10-04 §6). Client chi:
 * 1. goi start, mo authorizeUrl (popup tren Web, trinh duyet he thong tren Desktop),
 * 2. hoi complete moi 1,5 giay cho toi khi co session hoac flow het han.
 */

export const GOOGLE_POLL_INTERVAL_MS = 1_500;
export const GOOGLE_MAX_TRANSIENT_FAILURES = 3;

export type GoogleFlowStart = { flowId: string; authorizeUrl: string; expiresAt: string };
export type GoogleFlowPoll<S> = { status: "pending" | "completed"; session?: S | null };

export type GoogleFlowDeps<S> = {
  start: () => Promise<GoogleFlowStart>;
  complete: (flowId: string) => Promise<GoogleFlowPoll<S>>;
  /** Opens the Google page. Returns false when the browser blocked the window. */
  open: (authorizeUrl: string) => boolean;
  sleep: (ms: number, signal: AbortSignal) => Promise<void>;
  now: () => number;
};

export type GoogleFlowEvent =
  | { type: "opened"; authorizeUrl: string }
  | { type: "blocked"; authorizeUrl: string };

/** Raised when the flow outlives its server-side lifetime. Mirrors OAUTH_FLOW_EXPIRED. */
export class GoogleFlowExpiredError extends Error {
  readonly code = "OAUTH_FLOW_EXPIRED";
  constructor() {
    super("The Google sign-in attempt expired.");
    this.name = "GoogleFlowExpiredError";
  }
}

/** Network failures and 5xx responses are worth retrying; everything else is final. */
export function isTransientError(error: unknown): boolean {
  if (error instanceof TypeError) return true;
  const status = (error as { status?: unknown } | null)?.status;
  return typeof status === "number" && status >= 500;
}

function abortError(): Error {
  const error = new Error("Google sign-in was cancelled.");
  error.name = "AbortError";
  return error;
}

export async function runGoogleFlow<S>(
  deps: GoogleFlowDeps<S>,
  signal: AbortSignal,
  onEvent: (event: GoogleFlowEvent) => void = () => {},
): Promise<S> {
  const flow = await deps.start();
  if (signal.aborted) throw abortError();

  onEvent(deps.open(flow.authorizeUrl)
    ? { type: "opened", authorizeUrl: flow.authorizeUrl }
    : { type: "blocked", authorizeUrl: flow.authorizeUrl });

  const deadline = Date.parse(flow.expiresAt);
  let transientFailures = 0;

  while (true) {
    if (signal.aborted) throw abortError();
    if (Number.isFinite(deadline) && deps.now() >= deadline) throw new GoogleFlowExpiredError();

    await deps.sleep(GOOGLE_POLL_INTERVAL_MS, signal);
    if (signal.aborted) throw abortError();

    try {
      const result = await deps.complete(flow.flowId);
      transientFailures = 0;
      if (result.status === "completed" && result.session) return result.session;
    } catch (error) {
      if (!isTransientError(error) || ++transientFailures >= GOOGLE_MAX_TRANSIENT_FAILURES) throw error;
    }
  }
}

/** setTimeout that resolves early (and quietly) when the signal aborts. */
export function sleep(ms: number, signal: AbortSignal): Promise<void> {
  return new Promise((resolve) => {
    if (signal.aborted) {
      resolve();
      return;
    }
    const timer = setTimeout(done, ms);
    signal.addEventListener("abort", done, { once: true });
    function done() {
      clearTimeout(timer);
      signal.removeEventListener("abort", done);
      resolve();
    }
  });
}
