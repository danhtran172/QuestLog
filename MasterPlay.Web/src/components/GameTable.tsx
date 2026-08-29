/**
 * Bang game — dung cho HAI cho:
 *
 *   variant "queue": Play Queue. Keo tha doi thu tu, co tay cam keo.
 *   variant "list":  che do xem dang danh sach cua ba tab dau. KHONG keo tha,
 *                    Publish dat len dau cum du lieu va them cot Finish in o
 *                    cuoi.
 *
 * Hai cho dung CHUNG mot bo o de khong bao gio lech nhau ve mau ma, khoang
 * cach hay cach can le; chi thu tu cot va chuyen keo tha la khac.
 */
import { useState } from "react";
import { toast } from "sonner";
import { Gamepad2, GripVertical, ListOrdered, RotateCcw } from "lucide-react";
import { cn } from "@/lib/utils";
import { formatFinishTime, formatPublishDate } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import type { GameListItem } from "@/lib/api/domain";
import { CatalogPill, CommunityAveragePlaytime, ExcitedFire, MetaChip } from "./Pills";
import { GameCoverFlags } from "./GameCoverFlags";
import { IconButton, Tooltip } from "./ui";

export type GameTableVariant = "queue" | "list";

/** Play Queue chi hien tung nay hang. Thua thi cat o phan HIEN THI. */
export const QUEUE_MAX_ROWS = 10;

/**
 * Game dang choi la mot khoi rieng, luon nam tren dau hang cho va chi duoc doi
 * cho VOI NHAU. Backend moi la noi chot quy tac nay (`/api/queue` sap xep
 * Playing len dau, `PUT /api/queue/order` tu choi thu tu vi pham); o day chan
 * tu trong luc keo de nguoi dung khong tha ra roi moi bi bao loi.
 */
function isPlaying(game: GameListItem): boolean {
  return game.progress?.systemKey === "Playing";
}

type GameTableProps = {
  variant: GameTableVariant;
  /** Tieu de o thanh tren bang. */
  title: string;
  games: GameListItem[];
  isLoading: boolean;
  error: string | null;
  onRetry: () => void;
  onOpen: (game: GameListItem) => void;
  /** Chi variant "queue" — keo tha doi thu tu. */
  onReorder?: (gameIds: string[]) => Promise<GameListItem[]>;
  isSaving?: boolean;
  /** So thu tu cua hang dau tien. List co phan trang nen khong phai luon la 1. */
  startNumber?: number;
  /**
   * Chi HIEN tung nay hang. Keo tha van tinh tren ca danh sach: `PUT
   * /api/queue/order` bat buoc phai nhan DU moi game trong hang cho, gui thieu
   * la 400 QUEUE_ORDER_MISMATCH.
   */
  maxRows?: number;
};

/* Thu tu cot khac nhau giua hai variant, nen moi variant mot bo luoi. */
const QUEUE_GRID =
  "lg:grid-cols-[40px_44px_92px_minmax(0,1fr)_120px_110px_90px_92px_112px_120px]";
const LIST_GRID =
  "lg:grid-cols-[44px_92px_minmax(0,1fr)_90px_120px_110px_92px_112px_120px_92px]";
const QUEUE_BASE = "grid-cols-[40px_44px_92px_minmax(0,1fr)]";
const LIST_BASE = "grid-cols-[44px_92px_minmax(0,1fr)]";

function rowGrid(variant: GameTableVariant) {
  return variant === "queue" ? `${QUEUE_BASE} ${QUEUE_GRID}` : `${LIST_BASE} ${LIST_GRID}`;
}

function RowSkeleton({ index, variant }: { index: number; variant: GameTableVariant }) {
  return (
    <li
      aria-hidden
      className={`grid items-center gap-2 border-b border-border px-4 py-2 last:border-0 ${rowGrid(variant)}`}
    >
      {variant === "queue" ? <GripVertical size={16} className="text-muted-fg/40" /> : null}
      <span className="text-sm font-black tabular-nums text-primary/35">
        {String(index + 1).padStart(2, "0")}
      </span>
      <span className="h-12 w-[88px] animate-pulse rounded-md bg-muted" />
      <span className="space-y-2">
        <span className="block h-3.5 w-44 max-w-full animate-pulse rounded bg-muted" />
        <span className="block h-3 w-28 max-w-full animate-pulse rounded bg-muted/70" />
      </span>
      {Array.from({ length: variant === "queue" ? 6 : 7 }).map((_, column) => (
        <span
          key={column}
          className="mx-auto hidden h-5 w-16 animate-pulse rounded bg-muted/70 lg:block"
        />
      ))}
    </li>
  );
}

export function GameTable({
  variant,
  title,
  games,
  isLoading,
  error,
  onRetry,
  onOpen,
  onReorder,
  isSaving = false,
  startNumber = 1,
  maxRows,
}: GameTableProps) {
  const { t } = useI18n();
  const [dragId, setDragId] = useState<string | null>(null);
  const [overId, setOverId] = useState<string | null>(null);
  const draggable = variant === "queue";

  const finishDrag = () => {
    setDragId(null);
    setOverId(null);
  };

  /** Chi cho doi cho giua hai game CUNG khoi: Playing voi Playing, con lai voi con lai. */
  const canDropOn = (target: GameListItem) => {
    if (!dragId || dragId === target.id) return false;
    const source = games.find((game) => game.id === dragId);
    if (!source) return false;
    return isPlaying(source) === isPlaying(target);
  };

  const dropBefore = async (target: GameListItem) => {
    const targetId = target.id;
    if (!onReorder || !dragId || dragId === targetId || isSaving) {
      finishDrag();
      return;
    }

    if (!canDropOn(target)) {
      // Noi ro ly do — tha xong ma khong co gi xay ra thi nguoi dung tuong hong.
      toast.error(t("queue.playingLocked"));
      finishDrag();
      return;
    }

    const gameIds = games.map((game) => game.id);
    const sourceIndex = gameIds.indexOf(dragId);
    const targetIndex = gameIds.indexOf(targetId);
    if (sourceIndex < 0 || targetIndex < 0) {
      finishDrag();
      return;
    }

    const [movedId] = gameIds.splice(sourceIndex, 1);
    gameIds.splice(targetIndex, 0, movedId);

    try {
      await onReorder(gameIds);
      toast.success(t("queue.saved"));
    } catch (saveError) {
      toast.error(
        saveError instanceof Error ? saveError.message : t("queue.saveError"),
      );
    } finally {
      finishDrag();
    }
  };

  if (error) {
    return (
      <section className="surface-card flex flex-col items-center gap-3 p-16 text-center">
        <ListOrdered className="text-muted-fg" size={32} />
        <h2 className="text-lg font-bold">{t("queue.error")}</h2>
        <p className="max-w-md text-sm text-muted-fg">{error}</p>
        <IconButton
          onClick={onRetry}
          label={t("error.retry")}
          tone="edit"
          icon={<RotateCcw />}
        />
      </section>
    );
  }

  if (!isLoading && games.length === 0) {
    return (
      <section className="surface-card flex flex-col items-center gap-2 p-16 text-center">
        <ListOrdered className="text-muted-fg" size={32} />
        <h2 className="text-lg font-bold">{t("empty.queue")}</h2>
        <p className="text-sm text-muted-fg">{t("empty.queueDesc")}</p>
      </section>
    );
  }

  const headProgress = <span key="progress" className="text-center">{t("filter.progress")}</span>;
  const headExcited = <span key="excited" className="text-center">{t("filter.excited")}</span>;
  const headPublish = <span key="publish" className="text-center">{t("sort.publish")}</span>;
  const headMeta = <span key="meta" className="text-center">{t("sort.metaScore")}</span>;
  const headAvg = <span key="avg" className="text-center">{t("queue.avgPlaytime")}</span>;
  const headOwnership = <span key="ownership" className="text-center">{t("filter.ownership")}</span>;
  const headFinish = <span key="finish" className="text-center">{t("queue.finishTime")}</span>;

  return (
    <section className="surface-card overflow-hidden" aria-labelledby="game-table-title">
      <header className="flex items-center justify-between gap-4 border-b border-border px-4 py-2.5">
        <h2 id="game-table-title" className="text-sm font-bold">
          {title}
        </h2>
        {draggable ? (
          <p className="text-right text-xs text-muted-fg">{t("queue.hint")}</p>
        ) : null}
      </header>

      <div
        role="row"
        className={`hidden gap-2 border-b border-border bg-surface-2/40 px-4 py-2 text-[11px] font-bold uppercase tracking-wide text-muted-fg lg:grid ${rowGrid(variant)}`}
      >
        {draggable ? <span /> : null}
        <span>#</span>
        <span />
        <span>{t("queue.name")}</span>
        {variant === "queue"
          ? [headProgress, headExcited, headPublish, headMeta, headAvg, headOwnership]
          : [headPublish, headProgress, headExcited, headMeta, headAvg, headOwnership, headFinish]}
      </div>

      <ul aria-label={title}>
        {isLoading
          ? Array.from({ length: 7 }).map((_, index) => (
              <RowSkeleton key={index} index={index} variant={variant} />
            ))
          : (maxRows != null ? games.slice(0, maxRows) : games).map((game, index) => {
              const publishDate = formatPublishDate(game.publishDate);
              const finishTime = formatFinishTime(game.lastFinishedAt);
              const subtitle = game.publisher?.trim() ?? "";

              const cellProgress = (
                <span key="progress" className="hidden min-w-0 text-center lg:block">
                  <CatalogPill item={game.progress} />
                </span>
              );
              const cellExcited = (
                <span key="excited" className="hidden text-center lg:block">
                  <ExcitedFire value={game.excited} showIcon={false} />
                </span>
              );
              const cellPublish = (
                <span key="publish" className="hidden text-center text-xs text-muted-fg lg:block">
                  {publishDate}
                </span>
              );
              const cellMeta = (
                <span key="meta" className="hidden text-center lg:block">
                  {game.metaScore != null && game.metaScore > 0 && game.metaScoreBand ? (
                    /* `mx-auto w-fit`: MetaChip la `display:grid` nen trong o bang no
                       keo dai het chieu ngang cot va khong chiu `text-center`. */
                    <MetaChip
                      value={game.metaScore}
                      band={game.metaScoreBand}
                      className="mx-auto w-fit"
                    />
                  ) : null}
                </span>
              );
              const cellAvg = (
                <span key="avg" className="hidden text-center lg:block">
                  {game.avgPlaytimeHours != null ? (
                    <CommunityAveragePlaytime hours={game.avgPlaytimeHours} showIcon={false} />
                  ) : null}
                </span>
              );
              const cellOwnership = (
                <span key="ownership" className="hidden min-w-0 text-center lg:block">
                  <CatalogPill item={game.ownership} />
                </span>
              );
              const cellFinish = (
                <span key="finish" className="hidden text-center text-xs text-muted-fg lg:block">
                  {finishTime}
                </span>
              );

              const open = () => onOpen(game);

              return (
                <li
                  key={game.id}
                  data-game-id={game.id}
                  data-gamepad-focusable="true"
                  data-game-primary="true"
                  draggable={draggable && !isSaving}
                  onDragStart={draggable ? (event) => {
                    setDragId(game.id);
                    event.dataTransfer.effectAllowed = "move";
                    event.dataTransfer.setData("text/plain", game.id);
                  } : undefined}
                  onDragOver={draggable ? (event) => {
                    if (isSaving) return;
                    if (!canDropOn(game)) {
                      // Khong goi preventDefault -> con tro hien dau cam, khong
                      // to sang hang dich.
                      event.dataTransfer.dropEffect = "none";
                      if (overId === game.id) setOverId(null);
                      return;
                    }
                    event.preventDefault();
                    event.dataTransfer.dropEffect = "move";
                    setOverId(game.id);
                  } : undefined}
                  onDragEnd={draggable ? finishDrag : undefined}
                  onDrop={draggable ? (event) => {
                    event.preventDefault();
                    void dropBefore(game);
                  } : undefined}
                  /* Hang keo duoc thi mot cu bam la bat dau keo, nen mo the phai
                     bang nhay doi. Hang khong keo duoc thi bam mot cai la mo,
                     giong the ngoai luoi. */
                  onClick={draggable ? undefined : open}
                  onDoubleClick={open}
                  role="button"
                  tabIndex={0}
                  onKeyDown={(event) => {
                    if (event.key === "Enter" || event.key === " ") {
                      event.preventDefault();
                      open();
                    }
                  }}
                  className={cn(
                    `grid items-center gap-2 border-b border-border px-4 py-2 transition-colors last:border-0 hover:bg-surface-2/50 ${rowGrid(variant)}`,
                    draggable
                      ? isSaving ? "cursor-wait" : "cursor-grab active:cursor-grabbing"
                      : "cursor-pointer outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary/70",
                    dragId === game.id && "opacity-50",
                    overId === game.id &&
                      dragId !== game.id &&
                      "bg-primary/10 ring-1 ring-inset ring-primary/40",
                  )}
                >
                  {draggable ? <GripVertical size={16} className="text-muted-fg" /> : null}
                  <span className="text-sm font-black tabular-nums text-primary">
                    {String(startNumber + index).padStart(2, "0")}
                  </span>

                  <div className="relative h-12 w-[88px] overflow-hidden rounded-md bg-muted">
                    {game.gImagePath ? (
                      <img
                        src={game.gImagePath}
                        alt=""
                        loading="lazy"
                        className="h-full w-full object-cover"
                      />
                    ) : (
                      <span className="grid h-full w-full place-items-center text-muted-fg">
                        <Gamepad2 size={20} />
                      </span>
                    )}
                    <GameCoverFlags
                      isDlc={game.isDlc}
                      isUpcoming={game.isUpcoming}
                      publishDate={game.publishDate}
                      size="thumbnail"
                    />
                  </div>

                  <div className="min-w-0">
                    <Tooltip label={game.name}>
                      <p className="truncate text-sm font-bold">{game.name}</p>
                    </Tooltip>
                    <p className="h-4 truncate text-xs leading-4 text-muted-fg">{subtitle}</p>
                    <div className="mt-1 flex flex-wrap gap-1 lg:hidden">
                      <CatalogPill item={game.progress} />
                      <CatalogPill item={game.ownership} />
                    </div>
                  </div>

                  {variant === "queue"
                    ? [cellProgress, cellExcited, cellPublish, cellMeta, cellAvg, cellOwnership]
                    : [cellPublish, cellProgress, cellExcited, cellMeta, cellAvg, cellOwnership, cellFinish]}
                </li>
              );
            })}
      </ul>
    </section>
  );
}
