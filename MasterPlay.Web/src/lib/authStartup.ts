/**
 * Mo app thi vao dau. Desktop mo thang vao app (guest, du lieu tren may) moi khi
 * khong co phien tai khoan de khoi phuc; man dang nhap chi hien khi nguoi dung chu
 * dong chon "Dang nhap". Web/hosted khong co guest nen van phai dang nhap.
 */
export type StartupRoute = "guest" | "restoreAccount" | "unconfigured";

export function startupRoute({
  desktop,
  guestThisSession,
  authConfigured,
}: {
  /** Chay trong Shell desktop (co guest token). */
  desktop: boolean;
  /** Lan tai lai trang trong cung phien guest. */
  guestThisSession: boolean;
  authConfigured: boolean;
}): StartupRoute {
  if (desktop && (guestThisSession || !authConfigured)) return "guest";
  return authConfigured ? "restoreAccount" : "unconfigured";
}

/** Khong co phien tai khoan de khoi phuc (Host tra 401). */
export function routeWithoutSession(desktop: boolean): "guest" | "signedOut" {
  return desktop ? "guest" : "signedOut";
}
