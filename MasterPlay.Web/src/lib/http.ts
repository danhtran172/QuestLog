import type { ApiError } from "@/lib/api/domain";
import { ApiRequestError, fetchWithSession } from "@/lib/api/client";

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "";

export async function httpRequest<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers);
  headers.set("Accept-Language", document.documentElement.lang === "vi-VN" ? "vi-VN" : "en-US");
  if (init?.body) headers.set("Content-Type", "application/json");

  const response = await fetchWithSession(`${BASE_URL}${path}`, { ...init, headers });
  if (response.status === 204) return undefined as T;

  if (!response.ok) {
    let payload: ApiError | undefined;
    try {
      payload = (await response.json()) as ApiError;
    } catch {
      // Non-JSON errors still surface below with their HTTP status.
    }
    throw new ApiRequestError(
      response.status,
      payload?.error?.code ?? "UNKNOWN",
      payload?.error?.message ?? `HTTP ${response.status} ${response.statusText}`,
      payload?.error?.details,
    );
  }

  return (await response.json()) as T;
}
