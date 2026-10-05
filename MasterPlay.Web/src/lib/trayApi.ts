import type { components } from "./api/types.gen";
import { httpRequest } from "./http";

/** Types originate from the backend-owned OpenAPI contract. */
export type TrayStatus = components["schemas"]["TrayStatus"];
export type TrayServiceName = components["schemas"]["TrayServiceStatus"]["name"];

/** Host chuyen lenh sang tien trinh Tray qua named pipe (xem TrayControl.cs). */
export const trayApi = {
  status: () => httpRequest<TrayStatus>("/api/tray/status"),
  setService: (name: TrayServiceName, enabled: boolean) =>
    httpRequest<void>(`/api/tray/services/${name}`, {
      method: "POST",
      body: JSON.stringify({ enabled }),
    }),
  scanNow: () => httpRequest<void>("/api/tray/scan-now", { method: "POST" }),
  openLogs: () => httpRequest<void>("/api/tray/open-logs", { method: "POST" }),
};
