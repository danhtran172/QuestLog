import type { components } from "@/lib/api/types.gen";
import { httpRequest } from "@/lib/http";

export type Settings = components["schemas"]["Settings"];

export const settingsApi = {
  get: () => httpRequest<Settings>("/api/settings"),
  update: (settings: Settings) =>
    httpRequest<Settings>("/api/settings", {
      method: "PUT",
      body: JSON.stringify(settings),
    }),
};
