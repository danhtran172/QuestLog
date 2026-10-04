/**
 * Domain types — TAI XUAT tu types.gen.ts (sinh tu ../docs/openapi.yaml).
 *
 * KHONG dinh nghia type domain moi o day. Neu thieu field -> sua openapi.yaml
 * phia backend roi chay `npm run gen:api`. Xem AGENTS.md muc 2.
 */
import type { components } from "./types.gen";

type S = components["schemas"];

export type GameListItem = S["GameListItem"];
export type GameDetail = S["GameDetail"];
export type GameListResponse = S["GameListResponse"];
export type CatalogItem = S["CatalogItem"];
export type TaxonomyItem = S["TaxonomyItem"];
export type ThemeGroup = S["ThemeGroup"];
export type ThemeGroupBucket = S["ThemeGroupBucket"];
export type GroupedThemes = S["GroupedThemes"];
export type ThemeOrderRequest = S["ThemeOrderRequest"];
export type ThemeGroupWrite = S["ThemeGroupWrite"];
export type BaseGameCandidate = S["BaseGameCandidate"];
export type MetadataGameCandidate = S["MetadataGameCandidate"];
export type GameSyncSource = S["GameSyncSource"];
export type GameMetadataPreview = S["GameMetadataPreview"];
export type GameMetadataPreviewRequest = S["GameMetadataPreviewRequest"];
export type MetadataSearchResponse = S["MetadataSearchResponse"];
export type LibraryGameImportItem = S["LibraryGameImportItem"];
export type LibraryGameImportResponse = S["LibraryGameImportResponse"];
export type LibraryMetadataRefreshResponse = S["LibraryMetadataRefreshResponse"];
export type UserDataPatch = S["UserDataPatch"];
export type LaunchTarget = S["LaunchTarget"];
export type GameFinishEntry = S["GameFinishEntry"];
export type FinishGameRequest = S["FinishGameRequest"];
export type UpdateFinishTimeRequest = S["UpdateFinishTimeRequest"];
export type OwnershipInfo = S["OwnershipInfo"];
export type CardHints = S["CardHints"];
export type Capabilities = S["Capabilities"];
export type Settings = S["Settings"];
export type DashboardStats = S["DashboardStats"];
export type ApiError = S["Error"];
export type ProfileSortKey = S["ProfileSortKey"];
export type SortDirection = S["SortDirection"];
export type SortRule = S["SortRule"];
export type ViewSortProfile = S["ViewSortProfile"];
export type ViewSortProfiles = S["ViewSortProfiles"];
export type UpdateViewSortProfile = S["UpdateViewSortProfile"];

export type Tab = S["Tab"];
export type RegularTab = Exclude<Tab, "console">;
export type SortKey = S["SortKey"];
export type SearchScope = S["SearchScope"];
export type CompareOp = S["CompareOp"];
export type AvgBand = S["AvgBand"];
export type MetaBand = S["MetaBand"];
export type ScoreMaterialBand = S["ScoreMaterialBand"];
export type ProgressKey = S["ProgressKey"];
export type OwnershipKey = S["OwnershipKey"];

/** Cac tab thu vien thuong — giu nguyen phim tat Ctrl+1..4. */
export const TABS: readonly RegularTab[] = ["all", "planning", "finished", "queue"] as const;

/** Console la view mac dinh rieng cua mot Console Mode session. */
export const CONSOLE_TABS: readonly Tab[] = ["console", ...TABS] as const;

/** 12 khoa sort, DUNG THU TU — DOMAIN_RULES §7.1. */
export const SORT_KEYS: readonly SortKey[] = [
  "default",
  "status",
  "name",
  "playTime",
  "publish",
  "finishTime",
  "metaScore",
  "excited",
  "score",
  "ownership",
  "progress",
  "avgPlaytime",
] as const;

export const PROFILE_SORT_KEYS: readonly ProfileSortKey[] = SORT_KEYS.filter(
  (key): key is ProfileSortKey => key !== "default",
);

/** 6 pham vi search — DOMAIN_RULES §6.3. */
export const SEARCH_SCOPES: readonly SearchScope[] = [
  "all",
  "name",
  "series",
  "publisher",
  "developer",
  "releaseYear",
] as const;

/** 5 toan tu so sanh — DOMAIN_RULES §5.1. */
export const COMPARE_OPS: readonly CompareOp[] = ["gt", "gte", "eq", "lte", "lt"] as const;

export const COMPARE_OP_SYMBOL: Record<CompareOp, string> = {
  gt: ">",
  gte: ">=",
  eq: "=",
  lte: "<=",
  lt: "<",
};

export const AVG_BANDS: readonly AvgBand[] = [
  "VeryShort",
  "Short",
  "Medium",
  "Long",
  "VeryLong",
] as const;

export const META_BANDS: readonly MetaBand[] = [
  "VeryLow",
  "Low",
  "Medium",
  "High",
  "VeryHigh",
] as const;

/** Kich thuoc card — khop CHINH XAC voi WPF (MainWindow.xaml:314,341). */
export const CARD_WIDTH = 270;
export const CARD_IMAGE_HEIGHT = 126.2;

/** DOMAIN_RULES §16. */
export const PAGE_SIZE = 30;

/** DOMAIN_RULES §6.1. */
export const SEARCH_DEBOUNCE_MS = 280;
