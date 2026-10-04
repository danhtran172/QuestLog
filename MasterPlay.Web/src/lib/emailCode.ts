/** Do dai ma Supabase gui qua email (Email OTP length = 6 trong AUTH_SETUP). */
export const EMAIL_CODE_LENGTH = 6;

/** Giu lai chu so, cat dung do dai ma — dan "Ma: 123 456" van ra "123456". */
export function normalizeEmailCode(raw: string): string {
  return raw.replace(/\D/g, "").slice(0, EMAIL_CODE_LENGTH);
}
