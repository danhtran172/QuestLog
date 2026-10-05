// Anh chup app (chup bang script, chi doc; du lieu hien thi da duoc dung lai
// cho da dang va day du — xem README). Moi ngon ngu mot bo; chip diem dung chung.
import type { ImageMetadata } from "astro";
import type { Locale } from "./i18n/ui";

export type ShotName =
  | "library"
  | "card-done"
  | "card-upcoming"
  | "dashboard"
  | "detail"
  | "queue"
  | "releases"
  | "filter"
  | "themes"
  | "label-card-1"
  | "label-card-2"
  | "label-card-3"
  | "sort-profile"
  | "quickstats"
  | "console";

const shots = import.meta.glob<{ default: ImageMetadata }>("./assets/app/{vi,en}/*.webp", { eager: true });
const chips = import.meta.glob<{ default: ImageMetadata }>("./assets/app/chips/*.webp", { eager: true });

export function shot(lang: Locale, name: ShotName): ImageMetadata {
  const mod = shots[`./assets/app/${lang}/${name}.webp`];
  if (!mod) throw new Error(`Thieu anh chup app: ${lang}/${name}.webp`);
  return mod.default;
}

export function chip(name: string): ImageMetadata {
  const mod = chips[`./assets/app/chips/${name}.webp`];
  if (!mod) throw new Error(`Thieu anh chip: chips/${name}.webp`);
  return mod.default;
}
