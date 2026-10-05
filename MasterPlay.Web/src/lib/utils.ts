import { clsx, type ClassValue } from "clsx";
import { extendTailwindMerge } from "tailwind-merge";

// `text-fs-*` la co chu tu dinh nghia (index.css). Khong khai bao thi tailwind-merge
// coi no la MAU, giu ca `text-fs-11` lan `text-2xl` -> class mac dinh de len class truyen vao.
const twMerge = extendTailwindMerge({
  extend: { classGroups: { "font-size": [{ text: [(value: string) => /^fs-\d+$/.test(value)] }] } },
});

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

/**
 * Nguon anh cho `iconData` cua catalog/taxonomy.
 *
 * DB dang chua ca hai dang: data URL day du VA chuoi base64 TRAN. Nem chuoi
 * tran thang vao `src` thi trinh duyet hieu la duong dan tuong doi, goi nguoc
 * ve server (431) va anh vo. Chi chuan hoa luc HIEN THI — du lieu giu nguyen.
 */
export function iconSrc(iconData?: string | null): string | undefined {
  const value = iconData?.trim();
  if (!value) return undefined;
  if (/^(data:|https?:|blob:|\/)/i.test(value)) return value;

  const mime = value.startsWith("/9j/") ? "image/jpeg"
    : value.startsWith("R0lGOD") ? "image/gif"
      : value.startsWith("UklGR") ? "image/webp"
        : "image/png";
  return `data:${mime};base64,${value}`;
}

/**
 * Thu nho anh Cloudinary NGAY TREN URL cho nhung o hien thi be.
 *
 * The ket qua tim kiem ve trong o 80x56 nhung keo ve anh goc: mot trang 24 ket
 * qua nang 1.25 MB, ca tam 1920x1080 chi de ve thanh con tem. Them
 * `w_<rong>,c_fill,f_auto,q_auto` vao duong dan upload thi cung tam anh do con
 * 3 KB — nhe di 98%, va Cloudinary cache ban da bien doi nen lan sau khong phai
 * xu ly lai.
 *
 * `f_auto` de Cloudinary tu chon WebP/AVIF theo trinh duyet; `q_auto` chon muc
 * nen theo noi dung anh.
 *
 * CHI dung duoc voi URL Cloudinary. Anh Steam/RAWG/nguon khac tra ve NGUYEN SI:
 * chen doan bien doi vao URL cua ho la tao ra mot duong dan khong ton tai va
 * anh vo hoan toan — te hon han viec de no nang.
 */
export function thumbUrl(url: string | null | undefined, width: number): string | undefined {
  const value = url?.trim();
  if (!value) return undefined;

  const marker = "/image/upload/";
  if (!value.includes("res.cloudinary.com") || !value.includes(marker)) return value;
  // Da co san doan bien doi thi khong chong them lop nua.
  if (/\/image\/upload\/[^/]*(?:^|,)?(?:w_\d|c_|f_|q_)/.test(value)) return value;

  return value.replace(marker, `${marker}w_${width},c_fill,f_auto,q_auto/`);
}
