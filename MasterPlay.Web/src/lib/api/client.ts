/**
 * API client.
 *
 * NGUYEN TAC (AGENTS.md):
 *   - Moi loc/sort/phan trang deu CHAY O SERVER. Client chi gui query, nhan ket qua.
 *   - KHONG duoc nuot loi. Loi API phai nem ra de UI hien thi.
 *   - Auth: Supabase JWT qua header Authorization.
 */
import type { paths } from "./types.gen";
import type {
  ApiError,
  CatalogItem,
  BaseGameCandidate,
  Capabilities,
  DashboardStats,
  GameListResponse,
  LibraryGameImportItem,
  LibraryGameImportResponse,
  LibraryMetadataRefreshResponse,
  MetadataSearchResponse,
  SortRule,
  TaxonomyItem,
  ViewSortProfile,
  ViewSortProfiles,
} from "./domain";

export class ApiRequestError extends Error {
  readonly status: number;
  readonly code: string;
  readonly details: unknown;

  constructor(status: number, code: string, message: string, details?: unknown) {
    super(message);
    this.name = "ApiRequestError";
    this.status = status;
    this.code = code;
    this.details = details;
  }

  /** Endpoint native goi tren Hosted API — UI nen an nut thay vi hien loi. */
  get isUnsupportedOnHost(): boolean {
    return this.status === 501;
  }
}

let authToken: string | null = null;
export function setAuthToken(token: string | null) {
  authToken = token;
}

export function getAuthToken() {
  return authToken;
}

/**
 * Lam moi phien khi Host bao `401 SESSION_EXPIRED` giua chung (phien provider bi thu hoi
 * hoac het han). Tra `true` neu da co token moi; AuthProvider dang ky ham nay.
 */
type SessionRecovery = () => Promise<boolean>;
let recoverSession: SessionRecovery | null = null;
let pendingRecovery: Promise<boolean> | null = null;

export function setSessionRecovery(handler: SessionRecovery | null) {
  recoverSession = handler;
}

async function isSessionExpired(response: Response): Promise<boolean> {
  if (response.status !== 401) return false;
  try {
    const payload = (await response.clone().json()) as ApiError;
    return payload?.error?.code === "SESSION_EXPIRED";
  } catch {
    return false;
  }
}

/**
 * fetch kem Bearer hien tai. Gap `SESSION_EXPIRED` thi lam moi phien mot lan (cac request
 * dong thoi dung chung mot lan lam moi) roi gui lai voi token moi.
 */
export async function fetchWithSession(url: string, init: RequestInit): Promise<Response> {
  const send = () => {
    const headers = new Headers(init.headers);
    if (authToken) headers.set("Authorization", `Bearer ${authToken}`);
    return fetch(url, { ...init, credentials: "include", headers });
  };

  const response = await send();
  if (!recoverSession || !(await isSessionExpired(response))) return response;

  pendingRecovery ??= recoverSession().finally(() => {
    pendingRecovery = null;
  });
  return (await pendingRecovery) ? send() : response;
}

let language = "en-US";
export function setApiLanguage(lang: string) {
  language = lang;
}

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "";

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers);
  headers.set("Accept-Language", language);
  if (!headers.has("Content-Type") && init?.body) {
    headers.set("Content-Type", "application/json");
  }

  const response = await fetchWithSession(`${BASE_URL}${path}`, { ...init, headers });

  if (response.status === 204) return undefined as T;

  if (!response.ok) {
    let payload: ApiError | undefined;
    try {
      payload = (await response.json()) as ApiError;
    } catch {
      // body khong phai JSON — van phai bao loi, khong duoc im lang
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

type GamesQuery = NonNullable<paths["/api/games"]["get"]["parameters"]["query"]>;

/**
 * Bo qua gia tri rong de URL sach.
 * Mang -> chuoi ngan cach dau phay (khop `style: form, explode: false` trong openapi.yaml).
 */
function toSearchParams(query: Record<string, unknown>): string {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value == null || value === "" || value === false) continue;
    if (Array.isArray(value)) {
      if (value.length === 0) continue;
      params.set(key, value.join(","));
    } else {
      params.set(key, String(value));
    }
  }
  const qs = params.toString();
  return qs ? `?${qs}` : "";
}

export const api = {
  capabilities: () => request<Capabilities>("/api/capabilities"),

  games: (query: GamesQuery) =>
    request<GameListResponse>(`/api/games${toSearchParams(query as Record<string, unknown>)}`),

  viewSortProfiles: () =>
    request<ViewSortProfiles>("/api/library/sort-profiles"),

  updateViewSortProfile: (view: "all" | "planning" | "finished", rules: SortRule[]) =>
    request<ViewSortProfile>(`/api/library/sort-profiles/${view}`, {
      method: "PUT",
      body: JSON.stringify({ rules }),
    }),

  resetViewSortProfile: (view: "all" | "planning" | "finished") =>
    request<ViewSortProfile>(`/api/library/sort-profiles/${view}`, {
      method: "DELETE",
    }),

  dashboardStats: () => request<DashboardStats>("/api/dashboard/stats"),

  baseGameCandidates: (excludeId?: string, q?: string) =>
    request<BaseGameCandidate[]>(`/api/games/base-candidates${toSearchParams({ excludeId, q })}`),

  catalog: (kind: "progress" | "ownership") =>
    request<CatalogItem[]>(`/api/catalog/${kind}`),

  metadataGames: (query: { q: string; cursor?: string; pageSize?: number }) =>
    request<MetadataSearchResponse>(`/api/metadata/games${toSearchParams(query)}`),

  importGames: (items: LibraryGameImportItem[]) =>
    request<LibraryGameImportResponse>("/api/library/game-imports", {
      method: "POST",
      body: JSON.stringify({ items }),
    }),

  /** Keo thay doi metadata tu kho ve thu vien. Field nguoi dung da sua tay duoc giu nguyen. */
  refreshLibraryMetadata: (options?: { dryRun?: boolean; force?: boolean }) =>
    request<LibraryMetadataRefreshResponse>("/api/library/metadata-refresh", {
      method: "POST",
      body: JSON.stringify(options ?? {}),
    }),

  taxonomy: (kind: "series" | "genres" | "themes", q?: string) =>
    request<TaxonomyItem[]>(`/api/taxonomy/${kind}${toSearchParams({ q })}`),

  createTaxonomy: (kind: "series" | "genres" | "themes", item: TaxonomyItem) =>
    request<TaxonomyItem>(`/api/taxonomy/${kind}`, {
      method: "POST",
      body: JSON.stringify(item),
    }),

  updateTaxonomy: (kind: "series" | "genres" | "themes", item: TaxonomyItem) =>
    request<TaxonomyItem>(`/api/taxonomy/${kind}/${item.id}`, {
      method: "PUT",
      body: JSON.stringify(item),
    }),

  deleteTaxonomy: (kind: "series" | "genres" | "themes", id: number) =>
    request<void>(`/api/taxonomy/${kind}/${id}`, { method: "DELETE" }),
};
