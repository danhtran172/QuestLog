/**
 * Loi auth -> chuoi giao dien. Ma loi co ban dich (auth.error.<CODE>) thi dung
 * ban dich; con lai hien message cua backend. Khong nuot loi nao.
 */
export function authErrorMessage(error: unknown, t: (key: string) => string): string {
  const code = (error as { code?: unknown } | null)?.code;
  if (typeof code === "string") {
    const key = `auth.error.${code}`;
    const translated = t(key);
    if (translated !== key) return translated;
  }
  return error instanceof Error ? error.message : String(error);
}

/** AbortError tu nut Huy — khong phai loi can hien. */
export function isAbortError(error: unknown): boolean {
  return (error as { name?: unknown } | null)?.name === "AbortError";
}

/** Thoi diem duoc gui lai ma, lay tu loi 429 CODE_RESEND_COOLDOWN neu co. */
export function resendAvailableAtFromError(error: unknown): string | null {
  const details = (error as { details?: unknown } | null)?.details as { resendAvailableAt?: unknown } | undefined;
  return typeof details?.resendAvailableAt === "string" ? details.resendAvailableAt : null;
}
