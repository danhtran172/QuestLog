// Du lieu bang "Thu vien cua ban" o hero — cung bo du lieu voi anh chup app
// (Split Fiction vua xong 10+, Black Flag Resynced dang choi 40h 0m,
// Resident Evil Requiem choi tiep, GTA VI sap ra mat). Chi dung nhung gi app
// hien that: chip diem, chip hao huc, tong gio choi, ngay phat hanh.
import type { ImageMetadata } from "astro";
import type { Locale } from "./i18n/ui";
import { chip } from "./shots";
import splitFiction from "./assets/glance/split-fiction.webp";
import blackFlag from "./assets/glance/black-flag-resynced.webp";
import requiem from "./assets/glance/re-requiem.webp";
import gtaVi from "./assets/glance/gta-vi.webp";

export type GlanceKind = "finished" | "playing" | "next" | "upcoming";

export interface GlanceRow {
  kind: GlanceKind;
  /** Mau trang thai trong app (bien --color-* cua landing). */
  status: "done" | "playing" | "nextplay" | "planned";
  name: string;
  thumb: ImageMetadata;
  value: string;
  sub?: string;
  chip?: ImageMetadata;
}

export function glanceRows(lang: Locale): GlanceRow[] {
  return [
    {
      kind: "finished",
      status: "done",
      name: "Split Fiction",
      thumb: splitFiction,
      value: "10+",
      sub: lang === "vi" ? "09/2026" : "Sep 2026",
      chip: chip("score-opal"),
    },
    {
      kind: "playing",
      status: "playing",
      name: "Assassin's Creed Black Flag Resynced",
      thumb: blackFlag,
      // Dinh dang tong gio choi cua app (formatPlaytimeSeconds): "{h}h {m}m".
      value: "40h 0m",
    },
    {
      kind: "next",
      status: "nextplay",
      name: "Resident Evil 9: Requiem",
      thumb: requiem,
      value: "9.5",
      chip: chip("excited-extreme-fire"),
    },
    {
      kind: "upcoming",
      status: "planned",
      name: "Grand Theft Auto VI",
      thumb: gtaVi,
      value: lang === "vi" ? "19/11/2026" : "Nov 19, 2026",
    },
  ];
}
