// Anh chup app that (chup bang script, chi doc), moi ngon ngu mot bo.
// Them anh: dat file vao src/assets/app/<vi|en>/<ten>.webp va them ten vao ShotName.
import type { ImageMetadata } from "astro";
import type { Locale } from "./i18n/ui";

export type ShotName =
  | "library"
  | "list"
  | "queue"
  | "filter"
  | "detail"
  | "console"
  | "card-done"
  | "card-playing";

const modules = import.meta.glob<{ default: ImageMetadata }>("./assets/app/*/*.webp", { eager: true });

export function shot(lang: Locale, name: ShotName): ImageMetadata {
  const mod = modules[`./assets/app/${lang}/${name}.webp`];
  if (!mod) throw new Error(`Thieu anh chup app: ${lang}/${name}.webp`);
  return mod.default;
}
