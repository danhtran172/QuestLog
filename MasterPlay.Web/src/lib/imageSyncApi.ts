import type { components } from "./api/types.gen";
import { httpRequest } from "./http";

/** Types originate from the backend-owned OpenAPI contract. */
export type ImageSyncReport = components["schemas"]["ImageSyncReport"];
export type ImageSyncFailure = components["schemas"]["ImageSyncFailure"];

export const imageSyncApi = {
  /** Chay xong moi tra ve — thu vien vai tram anh mat vai phut. */
  run: () => httpRequest<ImageSyncReport>("/api/tray/sync-images", { method: "POST" }),
};
