/**
 * Cai dat DOMAIN_RULES bang ham thuan — KHONG phu thuoc framework.
 *
 * Muc dich KHONG chi la de UI chay duoc khi chua co backend. Day la
 * DAC TA CHAY DUOC: backend that (MasterPlay.Host) phai cho ra ket qua
 * giong het cac ham nay. Bo test parity o Giai doan 1 se doi chieu
 * API that voi chinh logic o day.
 *
 * PHAI XOA khi backend that san sang (Giai doan 3).
 */
import type {
  CardHints,
  Capabilities,
  GameListItem,
  GameListResponse,
  SortKey,
  Tab,
} from "../lib/api/domain";
import { MOCK_GAMES } from "./data";

const DEFAULT_PAGE_SIZE = 30;

/** DOMAIN_RULES §4 — dung systemKey, khong dung id. */
function applyTab(games: GameListItem[], tab: Tab): GameListItem[] {
  if (tab === "planning") return games.filter((g) => g.progress?.systemKey !== "Done");
  if (tab === "finished") return games.filter((g) => g.progress?.systemKey === "Done");
  if (tab === "queue") {
    return games.filter((g) =>
      ["Playing", "NextPlay", "Intended"].includes(g.progress?.systemKey ?? ""),
    );
  }
  return games;
}

/**
 * DOMAIN_RULES §6.2 — initials dung CONTAINS (khong phai startsWith),
 * tach theo ' ' '-' '_' '.'.
 * Vi du: "God of War" -> "gow"; go "ow" van KHOP.
 */
export function matchesInitials(text: string | null | undefined, term: string): boolean {
  if (!text || !term) return false;
  const words = text.split(/[ \-_.]+/).filter(Boolean);
  if (words.length === 0) return false;
  const initials = words.map((w) => w[0]!.toLowerCase()).join("");
  return initials.includes(term);
}

const contains = (v: string | null | undefined, term: string) =>
  (v ?? "").toLowerCase().includes(term);

/** DOMAIN_RULES §6.3, §6.4. */
function applySearch(games: GameListItem[], q: string, scope: string): GameListItem[] {
  const term = q.trim().toLowerCase();
  if (!term) return games;

  if (scope === "releaseYear") {
    // So BANG DUNG NAM. Khong parse duoc -> tra RONG (khong phai tra tat ca).
    // Go "02" phai ra RONG, khong duoc ra 2020/2021/2022 (mismatch M26).
    if (!/^\d+$/.test(term)) return [];
    const year = Number.parseInt(term, 10);
    return games.filter((g) => g.publishDate?.slice(0, 4) === String(year));
  }

  return games.filter((g) => {
    switch (scope) {
      case "name":
        return contains(g.name, term) || matchesInitials(g.name, term);
      case "series":
        return contains(g.series?.name, term) || matchesInitials(g.series?.name, term);
      case "publisher":
        return contains(g.publisher, term) || matchesInitials(g.publisher, term);
      case "developer":
        return contains(g.developer, term) || matchesInitials(g.developer, term);
      default:
        return (
          contains(g.name, term) ||
          contains(g.series?.name, term) ||
          matchesInitials(g.name, term) ||
          matchesInitials(g.series?.name, term)
        );
    }
  });
}

const LAST = Number.MAX_SAFE_INTEGER;
const num = (v: number | null | undefined, fallback: number) => v ?? fallback;
const cmp = (a: number, b: number) => (a < b ? -1 : a > b ? 1 : 0);
const cmpStr = (a: string, b: string) => a.localeCompare(b);

type Comparator = (a: GameListItem, b: GameListItem) => number;

function chain(...comparators: Comparator[]): Comparator {
  return (a, b) => {
    for (const c of comparators) {
      const r = c(a, b);
      if (r !== 0) return r;
    }
    return 0;
  };
}

const byName: Comparator = (a, b) => cmpStr(a.name, b.name);
/** Thu hang theo sortOrder TU DB, khong theo id hardcode (DOMAIN_RULES §3.2). */
const byProgressOrder: Comparator = (a, b) =>
  cmp(num(a.progress?.sortOrder, LAST), num(b.progress?.sortOrder, LAST));
const byOwnershipOrder: Comparator = (a, b) =>
  cmp(num(a.ownership?.sortOrder, LAST), num(b.ownership?.sortOrder, LAST));
const byExcitedDesc: Comparator = (a, b) => cmp(num(b.excited, -1), num(a.excited, -1));
const byPublishDesc: Comparator = (a, b) => cmpStr(b.publishDate ?? "", a.publishDate ?? "");
const byReadyDesc: Comparator = (a, b) => cmp(Number(b.ready), Number(a.ready));
const byScoreDesc: Comparator = (a, b) => cmp(num(b.score, -1), num(a.score, -1));
const byMetaDesc: Comparator = (a, b) => cmp(num(b.metaScore, -1), num(a.metaScore, -1));
const byFinishTimeDesc: Comparator = (a, b) =>
  cmpStr(b.lastFinishedAt ?? "", a.lastFinishedAt ?? "");
const byAvgAsc: Comparator = (a, b) =>
  cmp(num(a.avgPlaytimeHours, LAST), num(b.avgPlaytimeHours, LAST));

/** DOMAIN_RULES §7.2 — default sort theo tab, DU MOI KHOA PHU (mismatch M19-M21). */
function defaultComparator(tab: Tab): Comparator {
  if (tab === "planning") return chain(byProgressOrder, byExcitedDesc, byAvgAsc, byName);
  if (tab === "finished")
    return chain(byProgressOrder, byScoreDesc, byFinishTimeDesc, byPublishDesc, byMetaDesc);
  return chain(byReadyDesc, byPublishDesc, byExcitedDesc, byName, byProgressOrder, byOwnershipOrder);
}

function comparatorFor(sort: SortKey, tab: Tab): Comparator {
  switch (sort) {
    case "status":
      return chain(byReadyDesc, byPublishDesc, byName);
    case "name":
      return byName;
    case "playTime":
      return (a, b) => cmp(num(b.totalPlaytimeSeconds, -1), num(a.totalPlaytimeSeconds, -1));
    case "publish":
      return byPublishDesc;
    case "finishTime":
      return chain(byFinishTimeDesc, byName);
    case "metaScore":
      return byMetaDesc;
    case "excited":
      return byExcitedDesc;
    case "score":
      return byScoreDesc;
    case "ownership":
      return byOwnershipOrder;
    case "progress":
      return byProgressOrder;
    case "avgPlaytime":
      return (a, b) => cmp(num(b.avgPlaytimeHours, -1), num(a.avgPlaytimeHours, -1));
    default:
      return defaultComparator(tab);
  }
}

/** DOMAIN_RULES §17. */
function buildCardHints(sort: SortKey, tab: Tab, scope: string): CardHints {
  return {
    showDeveloper: scope === "developer",
    showPublisher: scope === "publisher" && tab !== "all",
    showFinishTime: tab !== "finished",
    showPlaytime: sort === "playTime" && tab !== "all",
    showExcited: sort === "excited" && tab !== "planning",
    showScore: sort === "score" && tab !== "finished",
    showOwnership: sort === "ownership" && tab !== "planning",
    showMetaScore: true,
    showAvgPlaytime: true,
    showPublishDate: true,
    showSeries: tab === "all",
    showProgress: true,
  };
}

export function getCapabilities(): Capabilities {
  // Mock dong vai Hosted API: khong co kha nang native.
  return {
    mode: "hosted",
    features: {
      launch: false,
      filePicker: false,
      revealFolder: false,
      tray: false,
      scanner: false,
      controller: false,
      imageSync: false,
      backup: false,
    },
  };
}

export function queryGames(params: URLSearchParams): GameListResponse {
  const tab = (params.get("tab") ?? "all") as Tab;
  const q = params.get("q") ?? "";
  const scope = params.get("scope") ?? "all";
  const sort = (params.get("sort") ?? "default") as SortKey;
  const reverseSort = params.get("reverseSort") === "true";
  const page = Number(params.get("page") ?? "1");
  const pageSize = Number(params.get("pageSize") ?? String(DEFAULT_PAGE_SIZE));

  let result = applyTab(MOCK_GAMES, tab);
  result = applySearch(result, q, scope);
  result = [...result].sort(comparatorFor(sort, tab));

  // DOMAIN_RULES §7.3 — dao TOAN BO list SAU khi sort xong,
  // KHONG nhan huong vao tung comparator (mismatch M18).
  if (reverseSort) result.reverse();

  const totalItems = result.length;
  const totalPages = Math.max(1, Math.ceil(totalItems / pageSize));

  // Tab queue tra toan bo, khong phan trang (DOMAIN_RULES §8.1).
  const items = tab === "queue" ? result : result.slice((page - 1) * pageSize, page * pageSize);

  return {
    items,
    page,
    pageSize,
    totalItems,
    totalPages,
    activeFilterCount: 0,
    cardHints: buildCardHints(sort, tab, scope),
  };
}
