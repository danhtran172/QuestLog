import type { GameFinishEntry } from "./api/domain";
/* Duoi `.ts` la CO Y (tsconfig bat `allowImportingTsExtensions`): bo test chay
   bang `node --experimental-strip-types` giai duong dan theo dung luat ESM, va
   mot import gia tri khong duoi se khong tim thay file. Cac import kieu (`import
   type`) bi xoa luc strip nen khong dinh chuyen nay. */
import { finishTimeParts } from "./format.ts";

/**
 * Ban nhap cua o "thoi diem hoan thanh".
 *
 * Giu duoi dang CHUOI chu khong phai so: o nhap nam co luc rong, co luc dang
 * go do ("20"), so hoa qua som thi hai trang thai do bien thanh `NaN` va `20`
 * — mot cai mat, mot cai sai.
 *
 * `month` rong = nguoi choi chi nho nam. Do la du lieu that, khong phai "chua
 * dien xong" (DOMAIN_RULES §9.1).
 */
export type FinishTimeDraft = { year: string; month: string };

export const FINISH_YEAR_MIN = 1970;

/** Chan nam tuong lai xa — mot nam go nham se troi len dau moi bang sort. */
export function finishYearMax(now: Date = new Date()): number {
  return now.getFullYear() + 1;
}

export const FINISH_MONTHS = ["01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12"] as const;

export function finishTimeDraftFromEntry(finish: GameFinishEntry): FinishTimeDraft {
  const parts = finishTimeParts(finish.finishedAt);
  return {
    year: parts?.year ?? "",
    month: finish.hasMonth ? parts?.month ?? "" : "",
  };
}

export function finishTimeDraftFromNow(now: Date = new Date()): FinishTimeDraft {
  return {
    year: String(now.getFullYear()),
    month: String(now.getMonth() + 1).padStart(2, "0"),
  };
}

/** Cho phep o nam rong hoac dang go do — chi chan ky tu khong phai so. */
export function isFinishYearDraft(value: string): boolean {
  return value === "" || /^\d{1,4}$/.test(value);
}

export function isValidFinishTimeDraft(draft: FinishTimeDraft, now: Date = new Date()): boolean {
  if (!/^\d{4}$/.test(draft.year)) return false;
  const year = Number(draft.year);
  if (year < FINISH_YEAR_MIN || year > finishYearMax(now)) return false;
  if (draft.month === "") return true;
  const month = Number(draft.month);
  return Number.isInteger(month) && month >= 1 && month <= 12;
}

/** Than yeu cau cho ca POST lan PUT. Chi goi khi ban nhap da hop le. */
export function toFinishTimeRequest(draft: FinishTimeDraft): {
  finishedYear: number;
  finishedMonth: number | null;
} {
  return {
    finishedYear: Number(draft.year),
    finishedMonth: draft.month === "" ? null : Number(draft.month),
  };
}

/** Xem truoc dung chuoi se hien tren pill sau khi luu. */
export function formatFinishTimeDraft(draft: FinishTimeDraft): string | null {
  if (!isValidFinishTimeDraft(draft)) return null;
  return draft.month === "" ? draft.year : `${draft.month}/${draft.year}`;
}

export function finishTimeDraftEquals(a: FinishTimeDraft, b: FinishTimeDraft): boolean {
  return a.year === b.year && a.month === b.month;
}
