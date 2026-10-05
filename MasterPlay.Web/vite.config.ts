import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import path from "node:path";
import { mockApiPlugin } from "./mock-api-plugin";

// SPA tinh. KHONG dung SSR / Nitro / Cloudflare target (xem AGENTS.md).
// Ban desktop: asset tinh do MasterPlay.Host phuc vu, shell la WebView2.
// Ban web: deploy tinh, goi Hosted API.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, import.meta.dirname, "");

  // Mac dinh goi API THAT (MasterPlay.Host doc DB that o AppData).
  // Dat VITE_USE_MOCKS=true de quay lai du lieu gia khi can dung UI offline.
  const useMocks = env.VITE_USE_MOCKS === "true";
  const apiTarget = env.VITE_API_TARGET || "http://127.0.0.1:5099";

  // Khong co PORT -> giu 5173: vo desktop (QuestLog.exe do run-web.cmd mo) tro vao cong nay.
  // Preview cua Claude Code truyen PORT (autoPort) khi 5173 dang bi run-web.cmd giu;
  // luc do bat strictPort de khong lang le nhay sang cong khac voi cong da cap.
  const port = Number(env.PORT) || 5173;

  return {
    plugins: [react(), tailwindcss(), ...(useMocks ? [mockApiPlugin()] : [])],
    resolve: {
      alias: { "@": path.resolve(import.meta.dirname, "./src") },
    },
    server: {
      port,
      strictPort: Boolean(env.PORT),
      // Proxy /api -> MasterPlay.Host. Nho vay frontend goi duong dan tuong doi,
      // khong can biet port cua backend, va khong dinh CORS.
      proxy: useMocks
        ? undefined
        : {
            "/api": { target: apiTarget, changeOrigin: true },
            // Anh thu vien luu tren may (khong phai admin) — Host phuc vu, DOMAIN_RULES §12.
            "/local-images": { target: apiTarget, changeOrigin: true },
          },
    },
    build: {
      outDir: "dist",
      // Ban build di kem bo cai cong khai: khong kem source map, khong lo ma nguon TS.
      sourcemap: false,
    },
  };
});
