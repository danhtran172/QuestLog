import type { Plugin } from "vite";

/**
 * Mock API chay NGAY TRONG VITE DEV SERVER.
 *
 * Chon middleware thay vi Service Worker (MSW) vi:
 *   - Khong phu thuoc dang ky Service Worker (bi chan trong mot so moi truong nhung).
 *   - La HTTP THAT -> gan voi backend that hon, DevTools Network nhin thay duoc.
 *   - Khong can them dependency runtime.
 *
 * Chi hoat dong o `vite dev`. Ban build KHONG kem mock — se goi API that.
 * PHAI XOA plugin nay khi MasterPlay.Host san sang.
 */
export function mockApiPlugin(): Plugin {
  let settings = {
    language: "en-US" as const,
    themeMode: "jungle" as "light" | "dark" | "system" | "jungle",
    jungleFx: "auto" as "auto" | "off" | "medium" | "full",
    jungleWeather: "clear" as "clear" | "rain",
    appMode: "desktop" as "desktop" | "console",
    backupRootPath: "",
    databasePath: "",
    libraries: { paths: [] as string[], runtime: [] },
    cloudinary: { cloudName: "", configured: false },
    rawg: { configured: false },
  };

  return {
    name: "questory-mock-api",
    apply: "serve",
    configureServer(server) {
      server.middlewares.use(async (req, res, next) => {
        const url = req.url ?? "";
        if (!url.startsWith("/api/")) return next();

        // Nap qua Vite de file TS duoc transform va hot-reload theo.
        const lib = (await server.ssrLoadModule("/src/mocks/library.ts")) as typeof import("./src/mocks/library");

        const parsed = new URL(url, "http://localhost");
        const send = (status: number, body: unknown) => {
          res.statusCode = status;
          res.setHeader("Content-Type", "application/json");
          res.end(JSON.stringify(body));
        };

        try {
          if (parsed.pathname === "/api/capabilities") {
            return send(200, lib.getCapabilities());
          }
          if (parsed.pathname === "/api/settings" && req.method === "GET") {
            return send(200, settings);
          }
          if (parsed.pathname === "/api/settings" && req.method === "PUT") {
            const chunks: Buffer[] = [];
            for await (const chunk of req) chunks.push(Buffer.from(chunk));
            const update = JSON.parse(Buffer.concat(chunks).toString("utf8")) as Partial<typeof settings>;
            settings = { ...settings, ...update };
            return send(200, settings);
          }
          if (parsed.pathname === "/api/games") {
            return send(200, lib.queryGames(parsed.searchParams));
          }

          // Endpoint chua mock -> tra dung shape loi cua hop dong (API_CONTRACT §1.4),
          // khong tra HTML cua Vite.
          return send(404, {
            error: {
              code: "NOT_IMPLEMENTED_IN_MOCK",
              message: `Mock chua cai dat ${parsed.pathname}`,
            },
          });
        } catch (err) {
          return send(500, {
            error: {
              code: "MOCK_ERROR",
              message: err instanceof Error ? err.message : String(err),
            },
          });
        }
      });
    },
  };
}
