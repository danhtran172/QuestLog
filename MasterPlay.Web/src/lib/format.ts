/**
 * Format hien thi — cai dat DOMAIN_RULES o phia UI.
 *
 * RANH GIOI QUAN TRONG (AGENTS.md muc 2):
 *   - File nay CHI duoc format de HIEN THI.
 *   - KHONG duoc chua logic loc / sort / xep hang / nguong.
 *   - KHONG duoc doi don vi o tang du lieu; chi doi luc ve ra man hinh.
 */
import type { MetaBand } from "./api/domain";

/** Gia tri cao nhat co the nhap. 11 la Peak Score va hien thi thanh "10+". */
export const RATING_MAX = 11;
export const PEAK_SCORE = 11;

export function isPeakScore(value: number | null | undefined): boolean {
  return value === PEAK_SCORE;
}

/**
 * Kiem tra draft trong luc go: toi da hai chu so, phan le neu co chi la `.5`.
 * Cho phep dau cham cuoi (`9.`) de nguoi dung co the go tiep thanh `9.5`.
 */
export function isRatingInputDraft(value: string): boolean {
  if (value === "") return true;
  if (!/^\d{1,2}(?:\.5?)?$/.test(value)) return false;

  const numericPart = value.endsWith(".") ? value.slice(0, -1) : value;
  const parsed = Number(numericPart);
  return Number.isFinite(parsed) && parsed >= 0 && parsed <= RATING_MAX;
}

export function isValidRating(value: number): boolean {
  return Number.isFinite(value) &&
    value >= 0 &&
    value <= RATING_MAX &&
    Number.isInteger(value * 2);
}

/**
 * Score — thang **0-11, buoc 0.5**. 11 la Peak Score.
 *
 * 0 duoc coi la "chua cham diem" -> hien "—".
 */
export function formatScore(score: number | null | undefined): string | null {
  if (score == null || score === 0) return null;
  if (isPeakScore(score)) return "10+";
  return String(score);
}

/**
 * Excited — thang **0-11, buoc 0.5**. 11 la Peak Score.
 *
 * XAC MINH TU DB THAT: co ca gia tri le nhu 1.5, 4.5, 7.5, 9.5.
 * => KHONG duoc lam tron, va KHONG phai so nguyen nhu tai lieu ban dau ghi.
 * Ban Lovable ep ve 0-5 nguyen nen sai gap doi: vua cat thang, vua mat phan .5.
 */
export const EXCITED_MAX = RATING_MAX;

export function formatExcited(excited: number | null | undefined): string | null {
  if (excited == null || excited === 0) return null;
  if (isPeakScore(excited)) return "10+";
  return String(excited);
}

/**
 * Dai NHIET DO cua Excited — quy uoc nguoi dung: cang hao huc cang nong.
 *
 *   1 – 3   extreme-cold
 *   3 – 6   cold          (Cold Iced; bang goc chi ghi 3–5, khoang ho 5–6 gop vao day)
 *   6 – 8   neutral
 *   8 – 9   fire
 *   9 – 10  extreme-fire  (ke ca 10)
 *   11      lava          (Peak Score)
 *
 * Client CHI map gia tri -> dai de chon hieu ung; nguong nam o day, khong rai
 * rac trong tung component.
 */
export type ExcitedBand =
  | "extreme-cold"
  | "cold"
  | "neutral"
  | "fire"
  | "extreme-fire"
  | "lava";

export function excitedBandOf(value: number | null | undefined): ExcitedBand | null {
  if (value == null || value === 0) return null;
  if (isPeakScore(value)) return "lava";
  if (value >= 9) return "extreme-fire";
  if (value >= 8) return "fire";
  if (value >= 6) return "neutral";
  if (value >= 3) return "cold";
  return "extreme-cold";
}

/**
 * Tong thoi gian choi: API LUON tra GIAY — DOMAIN_RULES §1.
 * Hien thi "{h}h {m}m". Day la cho DUY NHAT duoc doi don vi.
 */
export function formatPlaytimeSeconds(seconds: number | null | undefined): string {
  if (seconds == null || seconds <= 0) return "0h 0m";
  const totalMinutes = Math.floor(seconds / 60);
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return `${hours}h ${minutes}m`;
}

/** Thoi luong choi trung binh: API/DB da tra GIO, chi them hau to khi hien thi. */
export function formatAvgPlaytimeHours(hours: number | null | undefined): string {
  return hours == null ? "—" : `${hours}h`;
}

/** Nguoi dung nhap GIO cho filter playtime -> phai nhan 3600 TRUOC khi gui (DOMAIN_RULES §5.3). */
export function playtimeHoursToSeconds(hours: number): number {
  return Math.round(hours * 3600);
}

/** Tach nam tu chuoi ISO date. */
export function yearOf(isoDate: string | null | undefined): string | null {
  if (!isoDate) return null;
  const year = isoDate.slice(0, 4);
  return /^\d{4}$/.test(year) ? year : null;
}

/** Ngay phat hanh day du theo dinh dang giao dien da chot: dd/MM/yyyy. */
export function formatPublishDate(isoDate: string | null | undefined): string | null {
  if (!isoDate) return null;
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(isoDate);
  return match ? `${match[3]}/${match[2]}/${match[1]}` : null;
}

/**
 * So ngay con lai toi ngay phat hanh, TINH THEO GIO QUOC TE (UTC).
 * Ca hai moc deu quy ve nua dem UTC nen ket qua khong lech theo mui gio may.
 * Tra null neu khong parse duoc hoac ngay da toi/da qua.
 */
export function daysUntilPublishUtc(isoDate: string | null | undefined): number | null {
  if (!isoDate) return null;
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(isoDate);
  if (!match) return null;
  const target = Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
  const now = new Date();
  const today = Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate());
  const days = Math.round((target - today) / 86_400_000);
  return days > 0 ? days : null;
}

/**
 * Thoi gian hoan thanh: `MM/yyyy`, hoac chi `yyyy` khi nguoi choi khong nho
 * thang (`hasMonth = false`).
 *
 * Backend van luu mot ngay day du cho moc "chi nho nam" (1 thang 1), nen KHONG
 * duoc doc thang tu chuoi va hien no — se bia ra mot thang 1 khong co that.
 * DOMAIN_RULES §9.1.
 */
export function formatFinishTime(
  isoDateTime: string | null | undefined,
  hasMonth = true,
): string | null {
  const parts = finishTimeParts(isoDateTime);
  if (!parts) return null;
  return hasMonth ? `${parts.month}/${parts.year}` : parts.year;
}

/** Nam va thang duoi dang chuoi, doc thang tu ISO — khong qua `Date`. */
export function finishTimeParts(
  isoDateTime: string | null | undefined,
): { year: string; month: string } | null {
  if (!isoDateTime) return null;
  const match = /^(\d{4})-(\d{2})/.exec(isoDateTime);
  return match ? { year: match[1], month: match[2] } : null;
}

/** Gia tri dung cho input type=month, khong suy dien ngay. */
export function finishMonthInputValue(isoDateTime: string | null | undefined): string {
  const parts = finishTimeParts(isoDateTime);
  return parts ? `${parts.year}-${parts.month}` : "";
}

/**
 * Mau chip MetaScore theo BAND do backend tra ve.
 * Client KHONG tu so sanh nguong — ngan tai dien mismatch M16/M39.
 */
export const META_BAND_TONE: Record<MetaBand, string> = {
  VeryHigh: "[--meta-tone:var(--color-meta-veryhigh)]",
  High: "[--meta-tone:var(--color-meta-high)]",
  Medium: "[--meta-tone:var(--color-meta-medium)]",
  Low: "[--meta-tone:var(--color-meta-low)]",
  VeryLow: "[--meta-tone:var(--color-meta-verylow)]",
};

export const META_BAND_NONE_TONE =
  "[--meta-tone:var(--color-muted-fg)]";
