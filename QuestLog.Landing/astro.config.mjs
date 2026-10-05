// @ts-check
import { defineConfig } from "astro/config";
import tailwindcss from "@tailwindcss/vite";

// Xuat HTML tinh -> deploy thang len Firebase Hosting, khong can adapter.
export default defineConfig({
  site: process.env.PUBLIC_SITE_URL ?? "https://getquestlog.web.app",
  output: "static",
  // Astro khong tu doc PORT: cho preview chay song song nhieu ban tren cong khac 4321.
  server: { port: Number(process.env.PORT) || 4321 },
  i18n: {
    defaultLocale: "vi",
    locales: ["vi", "en"],
    routing: { prefixDefaultLocale: false },
  },
  vite: {
    plugins: [tailwindcss()],
  },
});
