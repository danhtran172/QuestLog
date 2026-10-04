// @ts-check
import { defineConfig } from "astro/config";
import tailwindcss from "@tailwindcss/vite";

// Xuat HTML tinh -> deploy thang len Cloudflare Pages, khong can adapter.
export default defineConfig({
  site: process.env.PUBLIC_SITE_URL ?? "https://questlog.pages.dev",
  output: "static",
  i18n: {
    defaultLocale: "vi",
    locales: ["vi", "en"],
    routing: { prefixDefaultLocale: false },
  },
  vite: {
    plugins: [tailwindcss()],
  },
});
