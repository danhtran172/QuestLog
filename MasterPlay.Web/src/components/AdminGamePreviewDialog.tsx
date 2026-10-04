/**
 * Preview mode cua Admin Console — duyet tung ban ghi metadata, xem MOI thu cung
 * luc tren mot popup.
 *
 * Bo cuc lay tu the chi tiet game cua thu vien (GameDetailDialog), nhung HAI
 * TRANG cua the do dat CANH NHAU thanh hai cot thay vi truot len xuong: admin
 * dang soat du lieu, phai thay het mot luc moi so sanh duoc. Popup cao va tu
 * cuon ben trong thay vi ep "khong cuon" nhu the cua thu vien.
 *
 *   Cot trai  — Tong quan: anh hero, thong tin game, tom tat.
 *   Cot phai  — Xuat ban, ba vai tro anh, thong tin ban ghi.
 *
 * Danh sach di qua la CHINH danh sach cua console: cung tim kiem, bo loc va sap
 * xep, lay theo SO TRANG tu server. Chuyen ban ghi = doi vi tri `index` trong
 * danh sach do; client khong loc hay sap xep gi (AGENTS.md §2).
 *
 * "Thieu" o day chi la kiem tra o TRONG de to mau — khong phai tieu chi. Tieu chi
 * xuat ban do server tinh (`gateFailures`); o nao vua trong vua dang chan xuat
 * ban thi to dam hon de admin biet dien cai nao truoc.
 *
 * "Duyet" = xuat ban ban ghi (`PUT .../published`). Ban ghi truot tieu chi thi
 * nut khoa lai va noi ro thieu gi — bat tay khong doi duoc ket qua.
 */
import { useEffect, useRef, useState, type KeyboardEvent, type ReactNode } from "react";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  AlertTriangle,
  CalendarDays,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  Clock,
  Eye,
  FileCheck2,
  FileX,
  Fingerprint,
  Gamepad2,
  Hash,
  Library,
  Loader2,
  Package,
  Pencil,
  Search,
  Star,
  Building2,
  Users,
  X,
} from "lucide-react";
import { toast } from "sonner";
import { ApiRequestError } from "@/lib/api/client";
import {
  adminApi,
  adminHealthLabelKey,
  adminListParams,
  countAdminFilters,
  type AdminGameFilter,
  type AdminMetadataGame,
  type AdminSortKey,
} from "@/lib/adminApi";
import { SEARCH_DEBOUNCE_MS } from "@/lib/api/domain";
import { formatAvgPlaytimeHours, formatPublishDate } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { AdminFilterPopover } from "./AdminFilterPopover";
import { AdminPublishedStatus, unpublishedReason } from "./AdminPublishedStatus";
import { Dialog, DialogContent, DialogDescription, DialogTitle, IconButton, Tooltip } from "./ui";

/** So ban ghi lay moi lan — chuyen trong cung mot trang khong goi lai server. */
const PREVIEW_PAGE_SIZE = 20;

export type AdminPreviewStart = {
  query: string;
  filter: AdminGameFilter;
  sort: AdminSortKey;
  descending: boolean;
  /** Vi tri (tu 0) cua ban ghi mo dau trong danh sach dang loc. */
  index: number;
};

/** Khoa o du lieu — cung bo khoa voi bo loc "thieu" va `gateFailures`. */
type FieldKey =
  | "banner"
  | "backdrop"
  | "poster"
  | "developer"
  | "publisher"
  | "publishDate"
  | "metaScore"
  | "avgPlaytime"
  | "summary"
  | "igdbId"
  | "dlcWithoutBase";

const blank = (value: string | null) => value == null || value.trim() === "";

/** O nao dang TRONG. Chi de to mau, khong quyet dinh duoc xuat ban. */
const EMPTY_CHECKS: Record<FieldKey, (game: AdminMetadataGame) => boolean> = {
  banner: (game) => blank(game.bannerUrl),
  backdrop: (game) => blank(game.backdropUrl),
  poster: (game) => blank(game.posterUrl),
  developer: (game) => blank(game.developer),
  publisher: (game) => blank(game.publisher),
  publishDate: (game) => game.publishDate == null,
  metaScore: (game) => game.metaScore == null,
  avgPlaytime: (game) => game.avgPlaytimeHours == null,
  summary: (game) => blank(game.summary),
  igdbId: (game) => game.igdbId == null,
  dlcWithoutBase: (game) => game.isDlc && game.baseMetadataGameId == null,
};

type FieldState = "ok" | "missing" | "blocking";

function fieldState(game: AdminMetadataGame, key: FieldKey): FieldState {
  if (game.gateFailures.includes(key)) return "blocking";
  return EMPTY_CHECKS[key](game) ? "missing" : "ok";
}

const HERO_IMAGE_FADE = {
  maskImage: "linear-gradient(to bottom, black 0%, black 55%, rgb(0 0 0 / 0.85) 65%, transparent 100%)",
  WebkitMaskImage: "linear-gradient(to bottom, black 0%, black 55%, rgb(0 0 0 / 0.85) 65%, transparent 100%)",
} as const;

function isTyping(target: EventTarget | null) {
  const element = target as HTMLElement | null;
  return !!element
    && (element.isContentEditable || ["INPUT", "SELECT", "TEXTAREA"].includes(element.tagName));
}

function Panel({ title, children, className }: { title: string; children: ReactNode; className?: string }) {
  return (
    <section className={cn("rounded-xl border border-border bg-surface-2/30 p-3", className)}>
      <h3 className="mb-2 text-xs font-black uppercase tracking-[0.14em] text-muted-fg">{title}</h3>
      {children}
    </section>
  );
}

/** Nhan "Thieu" — vang khi chi trong, do khi dang chan xuat ban. */
function MissingBadge({ state }: { state: Exclude<FieldState, "ok"> }) {
  const { t } = useI18n();
  const badge = (
    <span
      className={cn(
        "inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-[10px] font-black uppercase tracking-wide",
        state === "blocking"
          ? "border-danger/50 bg-danger/15 text-danger"
          : "border-warning/50 bg-warning/15 text-warning",
      )}
    >
      <AlertTriangle size={10} aria-hidden="true" />
      {t("admin.preview.missing")}
    </span>
  );
  return state === "blocking" ? <Tooltip label={t("admin.preview.blocksPublishing")}>{badge}</Tooltip> : badge;
}

/** Mot hang thong tin. O trong thi CA HANG duoc to, khong chi mot dau gach. */
function Row({
  label,
  icon,
  state = "ok",
  children,
}: {
  label: string;
  icon: ReactNode;
  state?: FieldState;
  children?: ReactNode;
}) {
  return (
    <div
      className={cn(
        "grid grid-cols-[150px_minmax(0,1fr)] items-start gap-3 rounded-lg px-2 py-1.5 text-sm",
        state === "missing" && "bg-warning/10 ring-1 ring-inset ring-warning/35",
        state === "blocking" && "bg-danger/10 ring-1 ring-inset ring-danger/40",
      )}
    >
      <dt className="flex items-center gap-1.5 font-semibold text-muted-fg">
        <span className="grid shrink-0 place-items-center" aria-hidden="true">{icon}</span>
        {label}
      </dt>
      <dd className="min-w-0 break-words font-semibold text-fg">
        {state === "ok" ? children : <MissingBadge state={state} />}
      </dd>
    </div>
  );
}

/** Mot vai tro anh: khung dung ty le cua vai tro do, trong thi la o gach dut. */
function ImageSlot({
  label,
  url,
  state,
  aspect,
  className,
}: {
  label: string;
  url: string | null;
  state: FieldState;
  aspect: string;
  className?: string;
}) {
  return (
    <figure className={cn("min-w-0 space-y-1.5", className)}>
      <figcaption className="flex items-center justify-between gap-2 text-[11px] font-black uppercase tracking-[0.12em] text-muted-fg">
        {label}
        {state !== "ok" ? <MissingBadge state={state} /> : null}
      </figcaption>
      <div
        className={cn(
          "overflow-hidden rounded-lg bg-muted",
          state === "missing" && "border-2 border-dashed border-warning/60 bg-warning/5",
          state === "blocking" && "border-2 border-dashed border-danger/60 bg-danger/5",
        )}
        style={{ aspectRatio: aspect }}
      >
        {url ? (
          <img src={url} alt="" loading="lazy" className="h-full w-full object-cover" />
        ) : (
          <div className="grid h-full w-full place-items-center text-muted-fg/60">
            <Gamepad2 size={26} aria-hidden="true" />
          </div>
        )}
      </div>
      {url ? (
        <Tooltip label={url} className="max-w-96 break-all font-mono">
          <p className="truncate font-mono text-[10px] text-muted-fg">{url}</p>
        </Tooltip>
      ) : null}
    </figure>
  );
}

export function AdminGamePreviewDialog({
  open,
  onOpenChange,
  start,
  onEdit,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Trang thai console luc mo — Preview giu ban sao rieng tu do. */
  start: AdminPreviewStart;
  onEdit: (game: AdminMetadataGame) => void;
}) {
  const { t, lang } = useI18n();
  const queryClient = useQueryClient();

  /* Ban sao RIENG cua tim kiem/bo loc: loc trong Preview khong lam bang ben
     duoi nhay theo, dong Preview la ve dung cho dang xem. */
  const [rawQuery, setRawQuery] = useState(start.query);
  const [query, setQuery] = useState(start.query);
  const [filter, setFilter] = useState<AdminGameFilter>(start.filter);
  const [index, setIndex] = useState(start.index);
  const [filterOpen, setFilterOpen] = useState(false);

  useEffect(() => {
    if (!open) return;
    setRawQuery(start.query);
    setQuery(start.query);
    setFilter(start.filter);
    setIndex(start.index);
    setFilterOpen(false);
  }, [open, start]);

  useEffect(() => {
    if (rawQuery === query) return;
    const timer = setTimeout(() => {
      setQuery(rawQuery);
      setIndex(0);
    }, SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [rawQuery, query]);

  const params = adminListParams(query, filter, start.sort, start.descending);
  const pageNumber = Math.floor(index / PREVIEW_PAGE_SIZE) + 1;
  const pageKey = (page: number) =>
    ["admin-metadata-games", "page", query, filter, start.sort, start.descending, page, PREVIEW_PAGE_SIZE];

  const list = useQuery({
    queryKey: pageKey(pageNumber),
    queryFn: () => adminApi.listGames({ ...params, page: pageNumber, pageSize: PREVIEW_PAGE_SIZE }),
    placeholderData: keepPreviousData,
    enabled: open,
  });

  const total = list.data?.totalItems ?? 0;
  const current = list.data && !list.isPlaceholderData
    ? list.data.items[index % PREVIEW_PAGE_SIZE] ?? null
    : null;

  /* Giu ban ghi vua xem tren man hinh trong luc tai trang ke — khong nhay ve
     khung trong moi lan qua ranh gioi trang. */
  const lastShown = useRef<AdminMetadataGame | null>(null);
  if (current) lastShown.current = current;
  const game = current ?? (list.isFetching ? lastShown.current : null);

  /* Duyet xong ma ban ghi roi khoi bo loc (vd. dang loc "chua xuat ban") thi
     danh sach ngan lai; dung lai o cuoi neu vi tri vuot qua tong. */
  useEffect(() => {
    if (!list.data || list.isPlaceholderData) return;
    if (total > 0 && index >= total) setIndex(total - 1);
  }, [list.data, list.isPlaceholderData, total, index]);

  /* Lay truoc trang ke khi sap cham ranh gioi, de bam "sau" khong phai doi. */
  useEffect(() => {
    if (!open || !list.data || list.isPlaceholderData) return;
    const position = index % PREVIEW_PAGE_SIZE;
    const neighbor = position >= PREVIEW_PAGE_SIZE - 3 ? pageNumber + 1 : position <= 2 ? pageNumber - 1 : null;
    if (neighbor == null || neighbor < 1 || (neighbor - 1) * PREVIEW_PAGE_SIZE >= total) return;
    void queryClient.prefetchQuery({
      queryKey: pageKey(neighbor),
      queryFn: () => adminApi.listGames({ ...params, page: neighbor, pageSize: PREVIEW_PAGE_SIZE }),
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, list.data, list.isPlaceholderData, index, pageNumber, total]);

  const series = useQuery({
    queryKey: ["admin-metadata-series"],
    queryFn: () => adminApi.listSeries({ limit: 200 }),
    enabled: open,
  });
  const seriesName = game?.seriesId != null
    ? (series.data ?? []).find((item) => item.id === game.seriesId)?.name ?? `#${game.seriesId}`
    : null;

  const baseId = game?.isDlc ? game.baseMetadataGameId : null;
  const baseGame = useQuery({
    queryKey: ["admin-metadata-game", baseId],
    queryFn: () => adminApi.getGame(baseId!),
    enabled: open && baseId != null,
    staleTime: 60_000,
  });

  const setPublished = useMutation({
    mutationFn: ({ id, published }: { id: number; published: boolean }) =>
      adminApi.setGamePublished(id, published),
    onSuccess: async (saved) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["admin-metadata-games"] }),
        queryClient.invalidateQueries({ queryKey: ["admin-metadata-publishing"] }),
      ]);
      toast.success(saved.manuallyUnpublished ? t("admin.publishing.nowUnpublished") : t("admin.publishing.nowPublished"));
    },
    onError: (failure) =>
      toast.error(failure instanceof ApiRequestError ? failure.message : String(failure)),
  });

  const canPrevious = index > 0;
  const canNext = index < total - 1;
  const move = (delta: number) => setIndex((value) => Math.min(Math.max(value + delta, 0), Math.max(total - 1, 0)));

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.altKey || event.ctrlKey || event.metaKey || isTyping(event.target)) return;
    /* Su kien React noi bot qua CAY component, ke ca tu popover bo loc (nam o
       portal khac): mui ten trong menu do khong duoc lat ban ghi. */
    if (!event.currentTarget.contains(event.target as Node)) return;
    if (event.key === "ArrowLeft" && canPrevious) {
      event.preventDefault();
      move(-1);
    } else if (event.key === "ArrowRight" && canNext) {
      event.preventDefault();
      move(1);
    }
  };

  const missing = game
    ? (Object.keys(EMPTY_CHECKS) as FieldKey[]).filter((key) => fieldState(game, key) !== "ok")
    : [];
  const state = (key: FieldKey) => (game ? fieldState(game, key) : "ok");
  const locked = (game?.gateFailures.length ?? 0) > 0;

  const formatUpdatedAt = (value: string | null) => {
    if (!value) return null;
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleString(lang);
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        className="flex h-[94vh] w-[min(1360px,96vw)] max-w-none flex-col gap-0 overflow-hidden p-0"
        onKeyDown={handleKeyDown}
      >
        <DialogTitle className="sr-only">{t("admin.preview.title")}</DialogTitle>
        <DialogDescription className="sr-only">{t("admin.preview.keyboardHint")}</DialogDescription>

        {/* Thanh tren: tim kiem, loc, chuyen ban ghi. */}
        <div className="flex flex-wrap items-center gap-2 border-b border-border px-4 py-3">
          <span className="flex shrink-0 items-center gap-2 pr-1 text-xs font-black uppercase tracking-[0.14em] text-primary">
            <Eye size={15} aria-hidden="true" />
            {t("admin.preview.title")}
          </span>

          <div className="relative min-w-48 flex-1">
            <Search
              size={15}
              aria-hidden="true"
              className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted-fg"
            />
            <input
              value={rawQuery}
              onChange={(event) => setRawQuery(event.target.value)}
              placeholder={t("admin.search.placeholder")}
              aria-label={t("action.search")}
              className="stone-slab h-9 w-full rounded-lg pl-9 pr-3 text-sm outline-none"
            />
          </div>

          <AdminFilterPopover
            open={filterOpen}
            onOpenChange={setFilterOpen}
            filter={filter}
            onChange={(next) => {
              setFilter(next);
              setIndex(0);
            }}
          />

          <div className="flex shrink-0 items-center gap-1">
            <IconButton
              label={t("admin.preview.previous")}
              shortcut="←"
              icon={<ChevronLeft />}
              disabled={!canPrevious}
              onClick={() => move(-1)}
            />
            <span className="min-w-20 text-center text-xs font-extrabold tabular-nums text-fg" aria-live="polite">
              {total > 0
                ? t("admin.preview.position")
                    .replace("{index}", String(Math.min(index + 1, total)))
                    .replace("{total}", String(total))
                : "0 / 0"}
            </span>
            <IconButton
              label={t("admin.preview.next")}
              shortcut="→"
              icon={<ChevronRight />}
              disabled={!canNext}
              onClick={() => move(1)}
            />
          </div>

          <IconButton label={t("action.close")} icon={<X />} onClick={() => onOpenChange(false)} />
        </div>

        {/* Tom tat o thieu — doc mot dong la biet ban ghi nay can lam gi. */}
        {game ? (
          <div
            className={cn(
              "flex flex-wrap items-center gap-2 border-b px-4 py-2 text-xs",
              missing.length === 0
                ? "border-success/30 bg-success/10 text-success"
                : locked
                  ? "border-danger/30 bg-danger/10 text-fg"
                  : "border-warning/30 bg-warning/10 text-fg",
            )}
          >
            {missing.length === 0 ? (
              <span className="flex items-center gap-1.5 font-bold">
                <CheckCircle2 size={14} aria-hidden="true" />
                {t("admin.preview.complete")}
              </span>
            ) : (
              <>
                <span className="flex items-center gap-1.5 font-bold">
                  <AlertTriangle size={14} className={locked ? "text-danger" : "text-warning"} aria-hidden="true" />
                  {t("admin.preview.missingCount").replace("{count}", String(missing.length))}
                </span>
                {missing.map((key) => (
                  <span
                    key={key}
                    className={cn(
                      "rounded-full border px-2 py-0.5 font-bold",
                      state(key) === "blocking"
                        ? "border-danger/45 bg-danger/15 text-danger"
                        : "border-warning/45 bg-warning/15 text-warning",
                    )}
                  >
                    {t(adminHealthLabelKey(key))}
                  </span>
                ))}
              </>
            )}
            {countAdminFilters(filter) > 0 || query ? (
              <span className="ml-auto text-muted-fg">{t("admin.preview.filtered")}</span>
            ) : null}
          </div>
        ) : null}

        <div className={cn("min-h-0 flex-1 overflow-y-auto transition-opacity", !current && game && "opacity-60")}>
          {list.isError ? (
            <div className="mx-auto mt-16 flex max-w-lg flex-col items-center gap-3 rounded-xl border border-danger/40 bg-danger/10 p-6 text-center">
              <AlertTriangle size={22} className="text-danger" />
              <p className="text-sm text-fg">
                {list.error instanceof Error ? list.error.message : String(list.error)}
              </p>
              <button
                type="button"
                onClick={() => void list.refetch()}
                className="rounded-lg border border-border px-3 py-1.5 text-xs font-extrabold text-fg"
              >
                {t("error.retry")}
              </button>
            </div>
          ) : !game ? (
            list.isFetching || list.isPending ? (
              <p className="flex items-center justify-center gap-2 py-24 text-sm text-muted-fg">
                <Loader2 size={16} className="animate-spin" /> {t("admin.games.loading")}
              </p>
            ) : (
              <p className="py-24 text-center text-sm text-muted-fg">
                {countAdminFilters(filter) > 0 ? t("admin.games.noneMatchFilter") : query ? t("admin.games.noMatch") : t("admin.games.empty")}
              </p>
            )
          ) : (
            <div className="grid gap-4 p-4 lg:grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)]">
              {/* ── Cot trai: Tong quan ── */}
              <div className="flex min-w-0 flex-col gap-4">
                <h2 className="text-[11px] font-black uppercase tracking-[0.16em] text-muted-fg">
                  {t("admin.preview.overview")}
                </h2>

                {/* Hero: cung thu tu anh voi the chi tiet — backdrop roi banner. */}
                <div className="relative aspect-[41/20] w-full overflow-hidden rounded-xl border border-border bg-[var(--color-bg)]">
                  {game.backdropUrl || game.bannerUrl ? (
                    <img
                      src={game.backdropUrl ?? game.bannerUrl ?? undefined}
                      alt=""
                      className="h-full w-full object-cover"
                      style={HERO_IMAGE_FADE}
                    />
                  ) : (
                    <div className="grid h-full place-items-center text-muted-fg"><Gamepad2 size={44} /></div>
                  )}
                  {game.isDlc ? (
                    <span className="absolute left-3 top-3 rounded-full bg-primary/90 px-2 py-0.5 text-[10px] font-black uppercase tracking-wide text-primary-fg">
                      DLC
                    </span>
                  ) : null}
                  <div className="absolute inset-x-0 bottom-0 px-5 pb-3 pt-16">
                    <h3 className="truncate text-3xl font-black text-white drop-shadow-lg">{game.name}</h3>
                    {(game.publisher ?? game.developer)?.trim() ? (
                      <p className="mt-1 truncate text-sm text-white/70">{game.publisher?.trim() || game.developer}</p>
                    ) : null}
                  </div>
                </div>

                <Panel title={t("admin.preview.gameInfo")}>
                  <dl className="space-y-1">
                    {game.isDlc ? (
                      <Row
                        label={t("admin.preview.baseGame")}
                        icon={<Package size={13} className="text-indigo-400" />}
                        state={state("dlcWithoutBase")}
                      >
                        {baseGame.data ? `${baseGame.data.name} · #${baseId}` : `#${baseId}`}
                      </Row>
                    ) : (
                      <Row label={t("admin.game.series")} icon={<Library size={13} className="text-violet-400" />}>
                        {seriesName ?? <span className="text-muted-fg">{t("admin.game.noSeries")}</span>}
                      </Row>
                    )}
                    <Row label={t("admin.game.publishDate")} icon={<CalendarDays size={13} className="text-sky-400" />} state={state("publishDate")}>
                      {formatPublishDate(game.publishDate) ?? game.publishDate}
                    </Row>
                    <Row label={t("admin.game.developer")} icon={<Users size={13} className="text-cyan-400" />} state={state("developer")}>
                      {game.developer}
                    </Row>
                    <Row label={t("admin.game.publisher")} icon={<Building2 size={13} className="text-teal-400" />} state={state("publisher")}>
                      {game.publisher}
                    </Row>
                    <Row label={t("admin.game.metaScore")} icon={<Star size={13} className="text-amber-400" />} state={state("metaScore")}>
                      <span className="tabular-nums">{game.metaScore}</span>
                    </Row>
                    <Row label={t("admin.preview.avgPlaytime")} icon={<Clock size={13} className="text-emerald-400" />} state={state("avgPlaytime")}>
                      <span className="tabular-nums">{formatAvgPlaytimeHours(game.avgPlaytimeHours)}</span>
                    </Row>
                  </dl>
                </Panel>

                <Panel
                  title={t("admin.game.summary")}
                  className={cn(
                    state("summary") === "missing" && "border-warning/45 bg-warning/10",
                    state("summary") === "blocking" && "border-danger/45 bg-danger/10",
                  )}
                >
                  {state("summary") === "ok" ? (
                    <p className="whitespace-pre-line text-sm leading-6 text-fg">{game.summary}</p>
                  ) : (
                    <MissingBadge state={state("summary") as Exclude<FieldState, "ok">} />
                  )}
                </Panel>
              </div>

              {/* ── Cot phai: Anh & xuat ban ── */}
              <div className="flex min-w-0 flex-col gap-4">
                <h2 className="text-[11px] font-black uppercase tracking-[0.16em] text-muted-fg">
                  {t("admin.preview.imagesAndPublishing")}
                </h2>

                <Panel title={t("admin.preview.publishing")}>
                  <div className="flex flex-wrap items-center gap-3">
                    <AdminPublishedStatus game={game} />
                    {!game.isPublished ? (
                      <span className="text-xs text-muted-fg">{unpublishedReason(game, t)}</span>
                    ) : null}
                  </div>
                </Panel>

                <Panel title={t("admin.preview.images")}>
                  <div className="grid grid-cols-[minmax(0,1fr)_140px] gap-3">
                    <div className="flex min-w-0 flex-col gap-3">
                      <ImageSlot
                        label={t("admin.game.bannerColumn")}
                        url={game.bannerUrl}
                        state={state("banner")}
                        aspect="460 / 215"
                      />
                      <ImageSlot
                        label={t("admin.game.backdropColumn")}
                        url={game.backdropUrl}
                        state={state("backdrop")}
                        aspect="16 / 9"
                      />
                    </div>
                    <ImageSlot
                      label={t("admin.game.posterColumn")}
                      url={game.posterUrl}
                      state={state("poster")}
                      aspect="2 / 3"
                    />
                  </div>
                </Panel>

                <Panel title={t("admin.preview.record")}>
                  <dl className="space-y-1">
                    <Row label="ID" icon={<Hash size={13} className="text-muted-fg" />}>
                      <span className="font-mono">#{game.id}</span>
                    </Row>
                    <Row label={t("admin.game.igdbId")} icon={<Fingerprint size={13} className="text-fuchsia-400" />} state={state("igdbId")}>
                      <span className="font-mono">{game.igdbId}</span>
                    </Row>
                    <Row label={t("admin.preview.revision")} icon={<Hash size={13} className="text-muted-fg" />}>
                      <span className="font-mono">{game.revision}</span>
                    </Row>
                    <Row label={t("admin.preview.updatedAt")} icon={<CalendarDays size={13} className="text-muted-fg" />}>
                      {formatUpdatedAt(game.updatedAt) ?? <span className="text-muted-fg">—</span>}
                    </Row>
                  </dl>
                </Panel>
              </div>
            </div>
          )}
        </div>

        {/* Chan: sua va duyet. */}
        <div className="flex flex-wrap items-center gap-2 border-t border-border px-4 py-3">
          <span className="mr-auto text-[11px] text-muted-fg">{t("admin.preview.keyboardHint")}</span>
          <IconButton
            tone="edit"
            label={t("action.edit")}
            icon={<Pencil />}
            disabled={!current}
            onClick={() => current && onEdit(current)}
          />
          {game?.isPublished ? (
            <button
              type="button"
              disabled={!current || setPublished.isPending}
              onClick={() => current && setPublished.mutate({ id: current.id, published: false })}
              className="flex h-9 items-center gap-1.5 rounded-lg border border-border px-3 text-xs font-extrabold text-fg transition-colors hover:bg-surface-2/60 disabled:opacity-50"
            >
              {setPublished.isPending ? <Loader2 size={14} className="animate-spin" /> : <FileX size={14} />}
              {t("admin.publishing.unpublish")}
            </button>
          ) : (
            <Tooltip label={locked && game ? unpublishedReason(game, t) : t("admin.preview.approveHint")}>
              {/* Boc span: nut disabled khong nhan su kien chuot, tooltip se khong hien. */}
              <span className="inline-flex">
                <button
                  type="button"
                  disabled={!current || locked || setPublished.isPending}
                  onClick={() => current && setPublished.mutate({ id: current.id, published: true })}
                  className="flex h-9 items-center gap-1.5 rounded-lg bg-primary px-4 text-xs font-extrabold text-primary-fg transition-opacity hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-45"
                >
                  {setPublished.isPending ? <Loader2 size={14} className="animate-spin" /> : <FileCheck2 size={14} />}
                  {t("admin.preview.approve")}
                </button>
              </span>
            </Tooltip>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
