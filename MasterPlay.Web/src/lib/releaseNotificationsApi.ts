import type { components } from "@/lib/api/types.gen";
import { httpRequest } from "@/lib/http";

export type ReleaseNotificationFeed = components["schemas"]["ReleaseNotificationFeed"];
export type ReleaseNotification = components["schemas"]["ReleaseNotification"];

/** Mui gio IANA cua trinh duyet; server dung no de dem ngay (DOMAIN_RULES §22). */
export function browserTimeZone(): string | undefined {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || undefined;
  } catch {
    // Trinh duyet khong co Intl day du: server dung mui gio cua may chay Host.
    return undefined;
  }
}

export const releaseNotificationsApi = {
  feed: (timeZone: string | undefined) =>
    httpRequest<ReleaseNotificationFeed>(
      timeZone
        ? `/api/notifications/releases?timeZone=${encodeURIComponent(timeZone)}`
        : "/api/notifications/releases",
    ),
  markRead: (ids: string[]) =>
    httpRequest<void>("/api/notifications/releases/read", {
      method: "POST",
      body: JSON.stringify({ ids }),
    }),
};
