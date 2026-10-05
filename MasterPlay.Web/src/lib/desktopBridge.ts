type QuestLogDesktopBridge = Readonly<{
  guestToken: string;
}>;

type WebViewMessageEvent = Event & Readonly<{ data: unknown }>;

type QuestLogWebView = Readonly<{
  postMessage: (message: unknown) => void;
  addEventListener: (type: "message", listener: (event: WebViewMessageEvent) => void) => void;
  removeEventListener: (type: "message", listener: (event: WebViewMessageEvent) => void) => void;
}>;

declare global {
  var __QUESTLOG_DESKTOP__: QuestLogDesktopBridge | undefined;

  interface Window {
    chrome?: { webview?: QuestLogWebView };
  }
}

export function getDesktopGuestToken(): string | null {
  const token = globalThis.__QUESTLOG_DESKTOP__?.guestToken?.trim();
  return token && token.length >= 32 ? token : null;
}

/** Dang chay trong WebView2 cua Shell (khong phai trinh duyet thuong). */
export function isDesktopShell(): boolean {
  return window.chrome?.webview != null;
}

export async function setConsoleFullscreen(enabled: boolean): Promise<void> {
  const webview = window.chrome?.webview;
  if (webview) {
    webview.postMessage({ type: "questlog.console-fullscreen", enabled });
    return;
  }

  if (enabled && !document.fullscreenElement) {
    await document.documentElement.requestFullscreen();
  } else if (!enabled && document.fullscreenElement) {
    await document.exitFullscreen();
  }
}

export async function toggleFullscreen(): Promise<void> {
  const webview = window.chrome?.webview;
  if (webview) {
    webview.postMessage({ type: "questlog.fullscreen-toggle" });
    return;
  }

  await setConsoleFullscreen(document.fullscreenElement == null);
}

export function subscribeFullscreenState(listener: (fullscreen: boolean) => void): () => void {
  const webview = window.chrome?.webview;
  if (!webview) {
    const onChange = () => listener(document.fullscreenElement != null);
    document.addEventListener("fullscreenchange", onChange);
    return () => document.removeEventListener("fullscreenchange", onChange);
  }

  const onMessage = (event: WebViewMessageEvent) => {
    const message = event.data;
    if (!message || typeof message !== "object") return;
    if (!("type" in message) || message.type !== "questlog.fullscreen-state") return;
    if (!("fullscreen" in message) || typeof message.fullscreen !== "boolean") return;
    listener(message.fullscreen);
  };
  webview.addEventListener("message", onMessage);
  return () => webview.removeEventListener("message", onMessage);
}

export function subscribeConsoleToggle(listener: () => void): () => void {
  const webview = window.chrome?.webview;
  if (!webview) return () => {};

  const onMessage = (event: WebViewMessageEvent) => {
    const message = event.data;
    if (!message || typeof message !== "object") return;
    if (!("type" in message) || message.type !== "questlog.console-toggle") return;
    listener();
  };
  webview.addEventListener("message", onMessage);
  return () => webview.removeEventListener("message", onMessage);
}

/**
 * Shell da tai xong ban moi (Velopack) va bao `questlog.update-ready`. Ngay khi
 * dang ky, hoi lai trang thai vi Shell co the da bao truoc khi trang nay tai xong.
 * Ngoai vo desktop (trinh duyet) khong co cap nhat: khong lam gi.
 */
export function subscribeUpdateReady(listener: (version: string) => void): () => void {
  const webview = window.chrome?.webview;
  if (!webview) return () => {};

  const onMessage = (event: WebViewMessageEvent) => {
    const message = event.data;
    if (!message || typeof message !== "object") return;
    if (!("type" in message) || message.type !== "questlog.update-ready") return;
    if (!("version" in message) || typeof message.version !== "string") return;
    listener(message.version);
  };
  webview.addEventListener("message", onMessage);
  webview.postMessage({ type: "questlog.update-status" });
  return () => webview.removeEventListener("message", onMessage);
}

/** Shell dung tray, ap dung ban moi va mo lai app. */
export function applyUpdateAndRestart(): void {
  window.chrome?.webview?.postMessage({ type: "questlog.update-apply" });
}
