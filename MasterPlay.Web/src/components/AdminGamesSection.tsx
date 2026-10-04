/**
 * Section Games cua Admin Console — spec 2026-09-14 §4.1, §12.1.
 *
 * HAI CHE DO XEM, giong thu vien: the (card) va danh sach (list). Dung state
 * o AdminConsoleView, khong ghi Settings — giong het `layout` cua thu vien va
 * cung ly do: day la trang thai UI cua phien, khong phai cau hinh dung chung
 * voi Tray (AGENTS.md §2).
 *
 * KHONG tai dung GameCard/GameTable: hai cai do render progress/ownership/
 * score/playtime — toan field userdata khong ton tai tren ban ghi metadata.
 *
 * HAI CACH PHAN TRANG, moi che do mot cach:
 *   - The: KEYSET + nut "Tai them". Con tro la CHUOI THO do server phat ra —
 *     client khong doc va khong dung lai no.
 *   - Danh sach: SO TRANG (`page`), nhay thang toi trang bat ky. Moi trang deu
 *     co `totalItems` nen ve duoc "Trang x / y".
 *
 * Loc va sap xep chay HOAN TOAN o server (`admin_list_metadata_games`).
 */
import { useEffect, useState } from "react";
import {
  keepPreviousData,
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  AlertTriangle,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  Eye,
  FileCheck2,
  FileX,
  Gamepad2,
  Loader2,
  Pencil,
  Trash2,
} from "lucide-react";
import { toast } from "sonner";
import { ApiRequestError } from "@/lib/api/client";
import {
  adminApi,
  adminHealthLabelKey,
  adminListParams,
  EMPTY_ADMIN_FILTER,
  type AdminGameFilter,
  type AdminMetadataGame,
  type AdminSortKey,
} from "@/lib/adminApi";
import { useI18n } from "@/lib/i18n";
import { formatPublishDate } from "@/lib/format";
import { cn } from "@/lib/utils";
import type { AdminLayout } from "@/lib/workspace";
import { AdminGameCard, AdminGameCardSkeleton } from "./AdminGameCard";
import { AdminPublishedStatus, unpublishedReason } from "./AdminPublishedStatus";
import {
  IconButton,
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
  Tooltip,
} from "./ui";

const CARD_PAGE_SIZE = 30;
/* Server chan tren o 100 (`admin_list_metadata_games`). */
const LIST_PAGE_SIZES = [25, 50, 100] as const;
const DEFAULT_LIST_PAGE_SIZE = 50;

/**
 * Cac o so trang can ve: luon co trang dau, trang cuoi va hai trang quanh trang
 * hien tai; khoang bi bo duoc thay bang dau "…".
 */
function pageWindow(page: number, totalPages: number): (number | "gap")[] {
  const sorted = [...new Set([1, totalPages, page - 1, page, page + 1])]
    .filter((value) => value >= 1 && value <= totalPages)
    .sort((a, b) => a - b);
  const result: (number | "gap")[] = [];
  sorted.forEach((value, index) => {
    const previous = sorted[index - 1];
    if (previous !== undefined && value - previous > 1) result.push("gap");
    result.push(value);
  });
  return result;
}

function AdminPagination({
  page,
  totalPages,
  onPageChange,
  disabled,
}: {
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
  disabled: boolean;
}) {
  const { t } = useI18n();
  const [jump, setJump] = useState("");

  const go = (next: number) => onPageChange(Math.min(Math.max(next, 1), totalPages));

  return (
    <nav aria-label={t("admin.pagination.label")} className="flex flex-wrap items-center gap-1">
      <IconButton
        size="compact"
        label={t("admin.pagination.first")}
        icon={<ChevronsLeft />}
        disabled={disabled || page <= 1}
        onClick={() => go(1)}
      />
      <IconButton
        size="compact"
        label={t("admin.pagination.previous")}
        icon={<ChevronLeft />}
        disabled={disabled || page <= 1}
        onClick={() => go(page - 1)}
      />
      {pageWindow(page, totalPages).map((item, index) =>
        item === "gap" ? (
          <span key={`gap-${index}`} className="px-1 text-xs text-muted-fg" aria-hidden="true">…</span>
        ) : (
          <button
            key={item}
            type="button"
            disabled={disabled}
            aria-current={item === page ? "page" : undefined}
            onClick={() => go(item)}
            className={cn(
              "grid h-8 min-w-8 place-items-center rounded-lg border px-2 text-xs font-extrabold tabular-nums transition-colors disabled:opacity-40",
              item === page
                ? "border-primary bg-primary text-primary-fg"
                : "border-border text-fg hover:bg-surface-2/60",
            )}
          >
            {item}
          </button>
        ),
      )}
      <IconButton
        size="compact"
        label={t("admin.pagination.next")}
        icon={<ChevronRight />}
        disabled={disabled || page >= totalPages}
        onClick={() => go(page + 1)}
      />
      <IconButton
        size="compact"
        label={t("admin.pagination.last")}
        icon={<ChevronsRight />}
        disabled={disabled || page >= totalPages}
        onClick={() => go(totalPages)}
      />
      <form
        className="ml-2 flex items-center gap-1.5"
        onSubmit={(event) => {
          event.preventDefault();
          const target = Number(jump);
          if (jump !== "" && Number.isInteger(target)) go(target);
          setJump("");
        }}
      >
        <label className="text-xs text-muted-fg" htmlFor="admin-page-jump">
          {t("admin.pagination.goTo")}
        </label>
        <input
          id="admin-page-jump"
          inputMode="numeric"
          value={jump}
          disabled={disabled}
          onChange={(event) => setJump(event.target.value.replace(/\D/g, ""))}
          placeholder={String(page)}
          className="stone-slab h-8 w-14 rounded-lg px-2 text-center text-xs tabular-nums outline-none"
        />
      </form>
    </nav>
  );
}

export function AdminGamesSection({
  query,
  filter,
  sort,
  descending,
  layout,
  onFilterChange,
  onEdit,
  onPreview,
}: {
  query: string;
  filter: AdminGameFilter;
  sort: AdminSortKey;
  descending: boolean;
  layout: AdminLayout;
  onFilterChange: (filter: AdminGameFilter) => void;
  onEdit: (game: AdminMetadataGame) => void;
  /** Mo Preview tai vi tri `index` (tu 0) cua danh sach dang loc. */
  onPreview: (index: number) => void;
}) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const params = adminListParams(query, filter, sort, descending);

  const [pageSize, setPageSize] = useState<number>(DEFAULT_LIST_PAGE_SIZE);
  /* Trang gan voi CHINH bo tham so da sinh ra no: doi tim kiem / loc / sap xep
     thi ve trang 1 ngay trong lan render do. Dung effect de reset se lot mot lan
     goi "trang 7 cua bo loc moi" truoc khi kip ve trang 1. */
  const listKey = JSON.stringify([query, filter, sort, descending, pageSize]);
  const [pageState, setPageState] = useState({ key: listKey, page: 1 });
  const page = pageState.key === listKey ? pageState.page : 1;
  const setPage = (next: number) => setPageState({ key: listKey, page: next });

  const cards = useInfiniteQuery({
    /* Moi tham so anh huong ket qua deu phai nam trong key, neu khong doi bo loc
       se hien lai trang cu cho toi khi refetch xong. */
    queryKey: ["admin-metadata-games", "cursor", query, filter, sort, descending],
    queryFn: ({ pageParam }) =>
      adminApi.listGames({ ...params, cursor: pageParam, pageSize: CARD_PAGE_SIZE }),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => last.nextCursor,
    enabled: layout === "card",
  });

  const rows = useQuery({
    queryKey: ["admin-metadata-games", "page", query, filter, sort, descending, page, pageSize],
    queryFn: () => adminApi.listGames({ ...params, page, pageSize }),
    /* Giu trang cu tren man hinh trong luc tai trang moi — bang khong nhay ve
       spinner moi lan bam so trang. */
    placeholderData: keepPreviousData,
    enabled: layout === "list",
  });

  const listTotal = rows.data?.totalItems ?? 0;
  const totalPages = Math.max(1, Math.ceil(listTotal / pageSize));

  /* Xoa ban ghi cuoi cua trang cuoi thi trang do rong — lui ve trang cuoi con co. */
  useEffect(() => {
    if (layout === "list" && rows.data && !rows.isPlaceholderData && page > totalPages) {
      setPageState({ key: listKey, page: totalPages });
    }
  }, [layout, rows.data, rows.isPlaceholderData, page, totalPages, listKey]);

  /* Ban ghi chi giu `seriesId`. Lay ten mot lan roi tra cuu tai cho — cung khoa
     cache voi section Series nen sua ten o do la day nay cap nhat theo. */
  const series = useQuery({
    queryKey: ["admin-metadata-series"],
    queryFn: () => adminApi.listSeries({ limit: 200 }),
  });
  const seriesById = new Map((series.data ?? []).map((item) => [item.id, item.name]));
  const seriesNameOf = (game: AdminMetadataGame) =>
    game.seriesId != null ? seriesById.get(game.seriesId) ?? null : null;

  /* Dang loc thi phai noi ro va cho go — neu khong admin se tuong kho chi co
     tung nay ban ghi. Chip liet ke TUNG dieu kien loc dang bat, khong chi mot cai. */
  const chips: string[] = [];
  if (filter.missing) chips.push(t(adminHealthLabelKey(filter.missing)));
  if (filter.isDlc !== null) {
    chips.push(t(filter.isDlc ? "admin.filter.dlcOnly" : "admin.filter.baseGames"));
  }
  if (filter.noSeries) chips.push(t("admin.game.noSeries"));
  else if (filter.seriesId !== null) {
    chips.push(seriesById.get(filter.seriesId) ?? t("admin.game.series"));
  }
  if (filter.year !== null) chips.push(String(filter.year));
  if (filter.published !== null) {
    chips.push(t(filter.published ? "admin.filter.publishedOnly" : "admin.filter.unpublishedOnly"));
  }

  const filterChip = chips.length > 0 ? (
    <div className="flex flex-wrap items-center gap-2 rounded-xl border border-primary/35 bg-primary/10 px-3 py-2 text-xs">
      {chips.map((chip) => (
        <span key={chip} className="font-bold text-fg">{chip}</span>
      ))}
      <button
        type="button"
        onClick={() => onFilterChange(EMPTY_ADMIN_FILTER)}
        className="ml-auto rounded-lg border border-border px-2 py-1 font-extrabold text-fg transition-colors hover:bg-surface-2/60"
      >
        {t("admin.games.clearFilter")}
      </button>
    </div>
  ) : null;

  const remove = useMutation({
    mutationFn: (game: AdminMetadataGame) => adminApi.deleteGame(game.id),
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: ["admin-metadata-games"] });
      // DLC tro toi ban ghi vua xoa se tu mo coi (FK `on delete set null`). Im lang
      // ve viec do la cach chac chan de sau nay khong ai truy nguoc duoc.
      toast.success(
        result.orphanedDlcCount > 0
          ? t("admin.game.deletedWithOrphans").replace("{count}", String(result.orphanedDlcCount))
          : t("admin.game.deleted"),
      );
    },
    onError: (failure) =>
      toast.error(failure instanceof ApiRequestError ? failure.message : String(failure)),
  });

  const setPublished = useMutation({
    mutationFn: ({ id, published }: { id: number; published: boolean }) =>
      adminApi.setGamePublished(id, published),
    onSuccess: async (game) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["admin-metadata-games"] }),
        queryClient.invalidateQueries({ queryKey: ["admin-metadata-publishing"] }),
      ]);
      toast.success(game.manuallyUnpublished ? t("admin.publishing.nowUnpublished") : t("admin.publishing.nowPublished"));
    },
    onError: (failure) =>
      toast.error(failure instanceof ApiRequestError ? failure.message : String(failure)),
  });

  const confirmDelete = (game: AdminMetadataGame) => {
    if (window.confirm(t("admin.game.deleteConfirm").replace("{name}", game.name))) {
      remove.mutate(game);
    }
  };

  const active = layout === "card" ? cards : rows;

  if (active.isPending) {
    return layout === "card" ? (
      <div className="grid gap-4 [grid-template-columns:repeat(auto-fill,minmax(270px,1fr))]">
        {Array.from({ length: 12 }).map((_, index) => (
          <AdminGameCardSkeleton key={index} />
        ))}
      </div>
    ) : (
      <p className="flex items-center justify-center gap-2 py-16 text-sm text-muted-fg">
        <Loader2 size={16} className="animate-spin" /> {t("admin.games.loading")}
      </p>
    );
  }

  if (active.isError) {
    return (
      <div className="mx-auto mt-10 flex max-w-lg flex-col items-center gap-3 rounded-xl border border-danger/40 bg-danger/10 p-6 text-center">
        <AlertTriangle size={22} className="text-danger" />
        <p className="text-sm text-fg">
          {active.error instanceof Error ? active.error.message : String(active.error)}
        </p>
        <button
          type="button"
          onClick={() => void active.refetch()}
          className="rounded-lg border border-border px-3 py-1.5 text-xs font-extrabold text-fg"
        >
          {t("error.retry")}
        </button>
      </div>
    );
  }

  const items = layout === "card"
    ? (cards.data?.pages ?? []).flatMap((item) => item.items)
    : rows.data?.items ?? [];

  if (items.length === 0) {
    return (
      <div className="space-y-3">
        {filterChip}
        <p className="py-16 text-center text-sm text-muted-fg">
          {chips.length > 0
            ? t("admin.games.noneMatchFilter")
            : query
              ? t("admin.games.noMatch")
              : t("admin.games.empty")}
        </p>
      </div>
    );
  }

  /* Vi tri dau cua trang dang xem trong danh sach dang loc. */
  const offset = (Math.min(page, totalPages) - 1) * pageSize;
  /* Tong so chi co o trang DAU cua kieu con tro — dem lai o moi lan "Tai them"
     la quet ca bang de tra ve dung con so vua tra. */
  const cardTotal = cards.data?.pages[0]?.totalItems ?? null;

  return (
    <div className="space-y-3">
      {filterChip}

      {layout === "card" ? (
        <div className="animate-in fade-in grid gap-4 duration-300 [grid-template-columns:repeat(auto-fill,minmax(270px,1fr))]">
          {items.map((game) => (
            <AdminGameCard
              key={game.id}
              game={game}
              seriesName={seriesNameOf(game)}
              onEdit={() => onEdit(game)}
              onDelete={() => confirmDelete(game)}
              onTogglePublished={() =>
                setPublished.mutate({ id: game.id, published: game.manuallyUnpublished })}
              deleting={remove.isPending}
              togglingPublished={setPublished.isPending}
            />
          ))}
        </div>
      ) : (
        <div
          className={cn(
            "surface-card overflow-x-auto transition-opacity",
            rows.isPlaceholderData && "opacity-60",
          )}
        >
          <table className="w-full min-w-[1100px] text-sm">
            <thead>
              <tr className="border-b border-border text-left text-[11px] font-black uppercase tracking-[0.12em] text-muted-fg">
                <th className="px-3 py-2">#</th>
                {/* Cot anh de trong ve mat thi giac, nhung van phai co ten cho
                    trinh doc man hinh — o khong ten la mot cot khong giai thich duoc. */}
                <th className="px-3 py-2">
                  <span className="sr-only">{t("admin.game.bannerColumn")}</span>
                </th>
                <th className="px-3 py-2">{t("admin.game.name")}</th>
                <th className="px-3 py-2">{t("admin.game.status")}</th>
                <th className="px-3 py-2">{t("admin.game.developer")}</th>
                <th className="px-3 py-2">{t("admin.game.series")}</th>
                <th className="px-3 py-2">{t("admin.game.publishDate")}</th>
                <th className="px-3 py-2 text-right">{t("admin.game.metaScore")}</th>
                <th className="px-3 py-2 text-right">rev</th>
                <th className="px-3 py-2" />
              </tr>
            </thead>
            <tbody>
              {items.map((game, rowIndex) => {
                /* Cung thu tu uu tien voi the: hai cot deu la anh NGANG.
                   `posterUrl` la anh doc, nhet vao day se meo. */
                const image = game.bannerUrl ?? game.backdropUrl;
                return (
                  <tr
                    key={game.id}
                    className="border-b border-border/50 last:border-0 hover:bg-surface-2/40"
                  >
                    <td className="px-3 py-2 text-xs text-muted-fg">{game.id}</td>
                    <td className="px-3 py-1.5">
                      <div className="relative h-12 w-[88px] overflow-hidden rounded-md bg-muted">
                        {image ? (
                          <img
                            src={image}
                            alt=""
                            loading="lazy"
                            className="h-full w-full object-cover"
                          />
                        ) : (
                          <span className="grid h-full w-full place-items-center text-muted-fg">
                            <Gamepad2 size={18} aria-hidden="true" />
                          </span>
                        )}
                      </div>
                    </td>
                    <td className="px-3 py-2">
                      <Tooltip label={game.name}>
                        <span className="font-bold text-fg">{game.name}</span>
                      </Tooltip>
                      {game.isDlc ? (
                        <span className="ml-2 rounded-full border border-primary/35 px-1.5 py-0.5 text-[10px] font-black uppercase text-primary">
                          DLC
                        </span>
                      ) : null}
                    </td>
                    <td className="px-3 py-2">
                      <AdminPublishedStatus game={game} />
                    </td>
                    <td className="px-3 py-2 text-muted-fg">{game.developer ?? "—"}</td>
                    <td className="px-3 py-2 text-muted-fg">{seriesNameOf(game) ?? "—"}</td>
                    <td className="px-3 py-2 text-muted-fg">
                      {formatPublishDate(game.publishDate) ?? "—"}
                    </td>
                    <td className="px-3 py-2 text-right tabular-nums text-muted-fg">
                      {game.metaScore ?? "—"}
                    </td>
                    <td className="px-3 py-2 text-right text-xs tabular-nums text-muted-fg">
                      {game.revision}
                    </td>
                    <td className="px-3 py-2">
                      <div className="flex justify-end gap-1">
                        <IconButton
                          size="compact"
                          tone="open"
                          label={t("admin.preview.openRecord")}
                          icon={<Eye />}
                          onClick={() => onPreview(offset + rowIndex)}
                        />
                        <IconButton
                          size="compact"
                          label={
                            game.gateFailures.length > 0
                              ? unpublishedReason(game, t)
                              : game.manuallyUnpublished
                                ? t("admin.publishing.publish")
                                : t("admin.publishing.unpublish")
                          }
                          icon={game.manuallyUnpublished ? <FileX /> : <FileCheck2 />}
                          /* Truot dieu kien thi bat/tat thu cong khong doi duoc gi —
                             khoa lai va noi ro ly do thay vi de bam vo ich. */
                          disabled={game.gateFailures.length > 0 || setPublished.isPending}
                          onClick={() => setPublished.mutate({ id: game.id, published: game.manuallyUnpublished })}
                        />
                        <IconButton
                          size="compact"
                          tone="edit"
                          label={t("action.edit")}
                          icon={<Pencil />}
                          onClick={() => onEdit(game)}
                        />
                        <IconButton
                          size="compact"
                          tone="danger"
                          label={t("action.delete")}
                          icon={remove.isPending ? <Loader2 className="animate-spin" /> : <Trash2 />}
                          disabled={remove.isPending}
                          onClick={() => confirmDelete(game)}
                        />
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {layout === "card" ? (
        <div className="flex items-center justify-between gap-3 text-xs text-muted-fg">
          <span>
            {cardTotal != null
              ? t("admin.games.loadedOf")
                  .replace("{count}", String(items.length))
                  .replace("{total}", String(cardTotal))
              : t("admin.games.loaded").replace("{count}", String(items.length))}
          </span>
          {cards.hasNextPage ? (
            <button
              type="button"
              disabled={cards.isFetchingNextPage}
              onClick={() => void cards.fetchNextPage()}
              className="rounded-lg border border-border px-3 py-1.5 text-xs font-extrabold text-fg transition-colors hover:bg-surface-2/60 disabled:opacity-60"
            >
              {cards.isFetchingNextPage ? t("admin.games.loading") : t("admin.games.loadMore")}
            </button>
          ) : null}
        </div>
      ) : (
        <div className="flex flex-wrap items-center justify-between gap-3 text-xs text-muted-fg">
          <span className="tabular-nums">
            {t("admin.pagination.range")
              .replace("{from}", String(offset + 1))
              .replace("{to}", String(offset + items.length))
              .replace("{total}", String(listTotal))}
          </span>
          <AdminPagination
            page={Math.min(page, totalPages)}
            totalPages={totalPages}
            onPageChange={setPage}
            disabled={rows.isPlaceholderData}
          />
          <label className="flex items-center gap-2">
            {t("admin.pagination.pageSize")}
            <Select value={String(pageSize)} onValueChange={(value) => setPageSize(Number(value))}>
              <SelectTrigger className="h-8 w-20">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {LIST_PAGE_SIZES.map((size) => (
                  <SelectItem key={size} value={String(size)}>{size}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </label>
        </div>
      )}
    </div>
  );
}
