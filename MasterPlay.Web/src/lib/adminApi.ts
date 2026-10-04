/**
 * Client cho Admin Console — spec 2026-09-14 §7.
 *
 * Dat o `src/lib/` chu KHONG phai `src/lib/api/`: thu muc do la hop dong sinh tu
 * `openapi.yaml` va backend so huu (AGENTS.md §2). Cung cho voi `gameDetailApi.ts`.
 *
 * KHONG co ham nao cho metadata genres/themes: hai bang do thuoc tien trinh sync,
 * khong co duong ghi (spec §4.4).
 */
import { httpRequest } from "./http";
import type { components } from "./api/types.gen";

/* Crawler contracts are generated from docs/openapi.yaml. Keep cursors opaque:
   the database owns their shape and ordering. */
export type AdminCrawlerStatus = components["schemas"]["AdminCrawlerStatus"];
export type AdminCrawlerRun = components["schemas"]["AdminCrawlerRun"];
export type AdminCrawlerRunPage = components["schemas"]["AdminCrawlerRunPage"];
export type AdminCrawlerRunItemPage = components["schemas"]["AdminCrawlerRunItemPage"];
export type AdminCrawlerTrackedGamePage = components["schemas"]["AdminCrawlerTrackedGamePage"];

export type AdminMetadataGame = {
  id: number;
  igdbId: number | null;
  name: string;
  developer: string | null;
  publisher: string | null;
  summary: string | null;
  /** ISO date "YYYY-MM-DD". */
  publishDate: string | null;
  isDlc: boolean;
  baseMetadataGameId: number | null;
  posterUrl: string | null;
  backdropUrl: string | null;
  bannerUrl: string | null;
  metaScore: number | null;
  avgPlaytimeHours: number | null;
  seriesId: number | null;
  /** Optimistic concurrency — bat buoc gui lai khi luu. */
  revision: number;
  updatedAt: string | null;
  /** Y DINH cua admin: co chu dong GO xuat ban ban ghi nay khong. */
  manuallyUnpublished: boolean;
  /** KET QUA suy ra: nguoi dung co tim thay ban ghi nay khong. Read-only. */
  isPublished: boolean;
  /** Tieu chi dang truot. Rong nghia la dat het. */
  gateFailures: string[];
};

/** Khoa tieu chi xuat ban — spec §13. Cung bo voi cac dong health cua dashboard. */
export const PUBLISH_RULE_KEYS = [
  "requireBanner",
  "requirePoster",
  "requireBackdrop",
  "requireDeveloper",
  "requirePublisher",
  "requirePublishDate",
  "requireMetaScore",
  "requireAvgPlaytime",
  "requireSummary",
  "requireIgdbId",
  "requireDlcBase",
] as const;

export type PublishRuleKey = (typeof PUBLISH_RULE_KEYS)[number];

export type AdminPublishRules = Record<PublishRuleKey, boolean> & {
  updatedAt: string | null;
};

export type AdminPublishState = {
  rules: AdminPublishRules;
  total: number;
  /** Tong so ban ghi CHUA xuat ban — do ca hai nguyen nhan. */
  unpublishedTotal: number;
  manuallyUnpublished: number;
  failingGate: number;
  /**
   * So ban ghi truot TUNG tieu chi, tinh ca tieu chi dang TAT.
   *
   * Day la con so quan trong nhat man hinh nay: admin phai thay "bat cai nay se
   * chan bao nhieu ban ghi" TRUOC khi bat, chu khong phai sau.
   */
  wouldBlock: Record<PublishRuleKey, number>;
};

export type AdminPublishRulesResult = {
  rules: AdminPublishRules;
  /** So ban ghi doi trang thai sau khi quet lai. */
  recomputed: number;
  unpublishedTotal: number;
};

export type AdminMetadataGamePage = {
  items: AdminMetadataGame[];
  /**
   * Con tro THO cho trang sau — gui lai nguyen si o `cursor`. `null` la het.
   *
   * KHONG doc cau truc ben trong: no do DB phat ra va se doi khi doi khoa sap
   * xep. Coi no la mo la cach duy nhat de doi cach phan trang ma khong pha client.
   */
  nextCursor: string | null;
  pageSize: number;
  /**
   * Tong so ban ghi khop. Co o trang dau cua kieu con tro va o MOI trang cua
   * kieu so trang (`page`); cac trang con tro sau la `null`.
   */
  totalItems: number | null;
};

/**
 * Khoa sap xep cua Admin Console — spec §12.1.
 *
 * CHI cac khoa thuan metadata. Khong co `status`, `playTime`, `finishTime`,
 * `excited`, `score`, `ownership`, `progress`: chung la userdata, khong ton tai
 * tren ban ghi metadata.
 *
 * Danh sach that nam o `admin_list_metadata_games`; day la ban sao de dung menu,
 * va server tu choi moi gia tri ngoai danh sach.
 */
export const ADMIN_SORT_KEYS = [
  "id",
  "name",
  "publishDate",
  "metaScore",
  "avgPlaytime",
  "updatedAt",
] as const;

export type AdminSortKey = (typeof ADMIN_SORT_KEYS)[number];

export const DEFAULT_ADMIN_SORT: AdminSortKey = "id";

/** `null` o moi truong nghia la "khong loc theo tieu chi nay". */
export type AdminGameFilter = {
  /** Khoa loc "thieu" — cung danh sach voi cac dong health cua dashboard. */
  missing: string | null;
  /** `true` chi DLC, `false` chi ban goc, `null` ca hai. */
  isDlc: boolean | null;
  seriesId: number | null;
  /** Chi ban ghi khong thuoc series nao. Loai tru `seriesId`. */
  noSeries: boolean;
  year: number | null;
  /** `true` chi ban ghi DA xuat ban, `false` chi ban ghi chua xuat ban. */
  published: boolean | null;
};

export const EMPTY_ADMIN_FILTER: AdminGameFilter = {
  missing: null,
  isDlc: null,
  seriesId: null,
  noSeries: false,
  year: null,
  published: null,
};

/**
 * Khoa loc "thieu" / khoa tieu chi truot (`gateFailures`) -> khoa i18n cua dong
 * health tuong ung. Hai danh sach dung chung bo khoa nen dung lai nhan da co.
 *
 * Hau het la `missing<Field>`, rieng `dlcWithoutBase` khong phai dang "thieu" nen
 * khong ghep tien to duoc.
 */
export function adminHealthLabelKey(key: string): string {
  if (key === "dlcWithoutBase") return "admin.health.dlcWithoutBase";
  return `admin.health.missing${key.charAt(0).toUpperCase()}${key.slice(1)}`;
}

/** Bo loc + tim kiem + sap xep cua console -> tham so `listGames`. */
export function adminListParams(
  query: string,
  filter: AdminGameFilter,
  sort: AdminSortKey,
  descending: boolean,
) {
  return {
    q: query || undefined,
    missing: filter.missing ?? undefined,
    isDlc: filter.isDlc ?? undefined,
    seriesId: filter.seriesId ?? undefined,
    noSeries: filter.noSeries || undefined,
    year: filter.year ?? undefined,
    published: filter.published ?? undefined,
    sort,
    desc: descending,
  };
}

export function countAdminFilters(filter: AdminGameFilter): number {
  return (
    (filter.missing ? 1 : 0)
    + (filter.isDlc !== null ? 1 : 0)
    + (filter.seriesId !== null || filter.noSeries ? 1 : 0)
    + (filter.year !== null ? 1 : 0)
    + (filter.published !== null ? 1 : 0)
  );
}

/**
 * PUT la THAY THE toan bo: field vang mat duoc hieu la null, khong phai "giu nguyen".
 *
 * Khong co genre/theme — hai field do read-only o moi duong ghi.
 */
export type AdminMetadataGameWrite = {
  igdbId: number | null;
  name: string;
  developer: string | null;
  publisher: string | null;
  summary: string | null;
  publishDate: string | null;
  isDlc: boolean;
  baseMetadataGameId: number | null;
  posterUrl: string | null;
  backdropUrl: string | null;
  bannerUrl: string | null;
  metaScore: number | null;
  avgPlaytimeHours: number | null;
  seriesId: number | null;
  revision?: number;
  confirmDuplicate?: boolean;
};

/** `error.details` cua `GAME_NAME_SIMILAR`. */
export type AdminGameDuplicate = {
  id: number;
  name: string;
  publishDate: string | null;
};

export type AdminMetadataSeries = { id: number; name: string };

export type AdminSeriesMergeResult = {
  target: AdminMetadataSeries;
  movedGames: number;
};

export type AdminGameDeleteResult = { orphanedDlcCount: number };

export type AdminChartPoint = { label: string; value: number };

/** Moi con so o day do backend tinh — xem `GET /api/admin/metadata/stats`. */
export type AdminMetadataStats = {
  totals: { games: number; series: number; genres: number; themes: number };
  byPublishYear: AdminChartPoint[];
  metaScoreBands: AdminChartPoint[];
  playtimeBands: AdminChartPoint[];
  topGenres: AdminChartPoint[];
  topThemes: AdminChartPoint[];
  activity: { dlc: number; baseGames: number; edited7d: number; edited30d: number };
  health: {
    missingBanner: number;
    missingPoster: number;
    missingBackdrop: number;
    missingDeveloper: number;
    missingPublisher: number;
    missingPublishDate: number;
    missingMetaScore: number;
    missingAvgPlaytime: number;
    missingSummary: number;
    missingIgdbId: number;
    dlcWithoutBase: number;
    /** Thuoc tien trinh sync — khong hanh dong duoc tu console. */
    withoutTags: number;
    duplicateNameGroups: number;
    seriesWithOneGame: number;
    seriesWithNoGames: number;
  };
};

/**
 * Bo qua `undefined`, `null` va chuoi rong — nhung GIU `false`: `desc=false` va
 * `isDlc=false` deu la lua chon that, khong phai "khong chon".
 */
const query = (params: Record<string, string | number | boolean | undefined | null>) => {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== "") search.set(key, String(value));
  }
  const text = search.toString();
  return text ? `?${text}` : "";
};

/**
 * Ba field tieu chi xuat ban chi co tu migration 202609140006 tro di.
 *
 * Host chay ban cu se tra ban ghi KHONG co chung, va `game.gateFailures.length`
 * tren `undefined` lam trang CA man hinh — app khong co error boundary nen mot
 * loi render giet toan bo cay React. Chuan hoa tai day de lech phien ban chi lam
 * mat mot huy hieu, khong lam mat ca console.
 *
 * Mac dinh la "da xuat ban, khong truot tieu chi": backend chua biet den tieu chi
 * xuat ban thi khong ban ghi nao dang bi chan.
 */
function normalizeGame(game: AdminMetadataGame): AdminMetadataGame {
  return {
    ...game,
    manuallyUnpublished: game.manuallyUnpublished ?? false,
    isPublished: game.isPublished ?? true,
    gateFailures: game.gateFailures ?? [],
  };
}

export const adminApi = {
  stats: () => httpRequest<AdminMetadataStats>("/api/admin/metadata/stats"),

  crawlerStatus: () => httpRequest<AdminCrawlerStatus>("/api/admin/metadata/crawler"),

  setCrawlerEnabled: (enabled: boolean) =>
    httpRequest<AdminCrawlerStatus>("/api/admin/metadata/crawler/enabled", {
      method: "PUT",
      body: JSON.stringify({ enabled }),
    }),

  triggerCrawler: () =>
    httpRequest<components["schemas"]["AdminCrawlerRunRequest"]>("/api/admin/metadata/crawler/run", {
      method: "POST",
    }),

  listCrawlerRuns: (params: { cursor?: string | null; pageSize?: number } = {}) =>
    httpRequest<AdminCrawlerRunPage>(`/api/admin/metadata/crawler/runs${query(params)}`),

  listCrawlerRunItems: (runId: string, params: { cursor?: string | null; pageSize?: number } = {}) =>
    httpRequest<AdminCrawlerRunItemPage>(
      `/api/admin/metadata/crawler/runs/${encodeURIComponent(runId)}/games${query(params)}`,
    ),

  listCrawlerGames: (params: { cursor?: string | null; pageSize?: number } = {}) =>
    httpRequest<AdminCrawlerTrackedGamePage>(`/api/admin/metadata/crawler/games${query(params)}`),

  listGames: (params: {
    q?: string;
    /** Khoa loc tu dong health cua dashboard. Server chi nhan gia tri trong danh sach dong. */
    missing?: string;
    sort?: AdminSortKey;
    desc?: boolean;
    isDlc?: boolean;
    seriesId?: number;
    noSeries?: boolean;
    year?: number;
    /** `true` chi ban ghi DA xuat ban, `false` chi ban ghi chua xuat ban. */
    published?: boolean;
    /** Con tro tho tu trang truoc, gui lai nguyen si. */
    cursor?: string | null;
    /**
     * So trang (tu 1) — che do danh sach va Preview. Moi trang deu co
     * `totalItems`. Bi bo qua neu co `cursor`.
     */
    page?: number;
    pageSize?: number;
  }) =>
    httpRequest<AdminMetadataGamePage>(`/api/admin/metadata/games${query(params)}`)
      .then((page) => ({ ...page, items: page.items.map(normalizeGame) })),

  getGame: (id: number) =>
    httpRequest<AdminMetadataGame>(`/api/admin/metadata/games/${id}`).then(normalizeGame),

  createGame: (input: AdminMetadataGameWrite) =>
    httpRequest<AdminMetadataGame>("/api/admin/metadata/games", {
      method: "POST",
      body: JSON.stringify(input),
    }).then(normalizeGame),

  updateGame: (id: number, input: AdminMetadataGameWrite) =>
    httpRequest<AdminMetadataGame>(`/api/admin/metadata/games/${id}`, {
      method: "PUT",
      body: JSON.stringify(input),
    }).then(normalizeGame),

  setGamePublished: (id: number, published: boolean) =>
    httpRequest<AdminMetadataGame>(`/api/admin/metadata/games/${id}/published`, {
      method: "PUT",
      body: JSON.stringify({ published }),
    }).then(normalizeGame),

  publishRules: () => httpRequest<AdminPublishState>("/api/admin/metadata/publishing"),

  /** Field bo trong = GIU NGUYEN, khong phai `false`. */
  setPublishRules: (rules: Partial<Record<PublishRuleKey, boolean>>) =>
    httpRequest<AdminPublishRulesResult>("/api/admin/metadata/publishing", {
      method: "PUT",
      body: JSON.stringify(rules),
    }),

  deleteGame: (id: number) =>
    httpRequest<AdminGameDeleteResult>(`/api/admin/metadata/games/${id}`, {
      method: "DELETE",
    }),

  listSeries: (params: { q?: string; limit?: number } = {}) =>
    httpRequest<AdminMetadataSeries[]>(`/api/admin/metadata/series${query(params)}`),

  createSeries: (name: string) =>
    httpRequest<AdminMetadataSeries>("/api/admin/metadata/series", {
      method: "POST",
      body: JSON.stringify({ name }),
    }),

  updateSeries: (id: number, name: string) =>
    httpRequest<AdminMetadataSeries>(`/api/admin/metadata/series/${id}`, {
      method: "PUT",
      body: JSON.stringify({ name }),
    }),

  deleteSeries: (id: number) =>
    httpRequest<void>(`/api/admin/metadata/series/${id}`, { method: "DELETE" }),

  mergeSeries: (sourceId: number, targetId: number) =>
    httpRequest<AdminSeriesMergeResult>("/api/admin/metadata/series/merge", {
      method: "POST",
      body: JSON.stringify({ sourceId, targetId }),
    }),
};

/** Ban ghi rong cho luong tao moi. */
export function emptyMetadataGame(): AdminMetadataGameWrite {
  return {
    igdbId: null,
    name: "",
    developer: null,
    publisher: null,
    summary: null,
    publishDate: null,
    isDlc: false,
    baseMetadataGameId: null,
    posterUrl: null,
    backdropUrl: null,
    bannerUrl: null,
    metaScore: null,
    avgPlaytimeHours: null,
    seriesId: null,
  };
}

export function toWriteModel(game: AdminMetadataGame): AdminMetadataGameWrite {
  return {
    igdbId: game.igdbId,
    name: game.name,
    developer: game.developer,
    publisher: game.publisher,
    summary: game.summary,
    publishDate: game.publishDate,
    isDlc: game.isDlc,
    baseMetadataGameId: game.baseMetadataGameId,
    posterUrl: game.posterUrl,
    backdropUrl: game.backdropUrl,
    bannerUrl: game.bannerUrl,
    metaScore: game.metaScore,
    avgPlaytimeHours: game.avgPlaytimeHours,
    seriesId: game.seriesId,
    revision: game.revision,
  };
}
