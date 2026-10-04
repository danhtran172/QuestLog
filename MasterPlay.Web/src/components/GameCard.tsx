import { AlertTriangle, CalendarDays, Gamepad2, Package, Play, Trophy } from "lucide-react";
import type { KeyboardEvent as ReactKeyboardEvent } from "react";
import { cn } from "@/lib/utils";
import { CardBush } from "@/jungle/CardDecor";
import { useI18n } from "@/lib/i18n";
import { CARD_IMAGE_HEIGHT, CARD_WIDTH, type CardHints, type GameListItem } from "@/lib/api/domain";
import { formatFinishTime, formatPlaytimeSeconds, formatPublishDate } from "@/lib/format";
import {
  CatalogPill,
  CommunityAveragePlaytime,
  ExcitedFire,
  GalleryScoreChip,
  MetaChip,
  PlayerPlaytimeIcon,
} from "./Pills";
import { GameCoverFlags } from "./GameCoverFlags";
import { IconButton, Tooltip } from "./ui";

export function GameCardSkeleton() {
  return (
    <div className="surface-card w-full overflow-hidden">
      <div
        className="animate-pulse bg-muted"
        style={{ aspectRatio: `${CARD_WIDTH} / ${CARD_IMAGE_HEIGHT}` }}
        aria-hidden
      />
      <div className="space-y-2 p-3">
        <div className="h-4 w-3/4 animate-pulse rounded bg-muted" />
        <div className="h-3 w-1/2 animate-pulse rounded bg-muted" />
        <div className="h-3 w-2/3 animate-pulse rounded bg-muted" />
      </div>
    </div>
  );
}

/**
 * Game card.
 *
 * Kich thuoc 270 x 126.2 khop CHINH XAC voi WPF (MainWindow.xaml:314,341).
 *
 * ANH: dung `bannerPath` (Steam header ngang 460x215).
 * `posterPath` chi dung cho be mat doc nhu Console mode.
 * KHONG dung o card; no danh cho hero o man chi tiet.
 *
 * `hints` quyet dinh field nao hien — do BACKEND tinh theo sort/filter dang bat
 * (DOMAIN_RULES §17). Client KHONG tu suy.
 */
export function GameCard({
  game,
  hints,
  onOpen,
  onPlay,
}: {
  game: GameListItem;
  hints?: CardHints;
  onOpen: () => void;
  onPlay: () => void;
}) {
  const { t } = useI18n();
  const publishDate = formatPublishDate(game.publishDate);
  const finishTime = formatFinishTime(game.lastFinishedAt);
  // Gia tri diem phai luon hien neu API tra ve score. `scoreMaterialBand` chi
  // quyet dinh hieu ung; backend cu/chua restart co the tam thoi chua tra field nay.
  const hasScore = game.score != null && game.score !== 0;
  const hasMetaScore = game.metaScore != null && game.metaScore > 0 && game.metaScoreBand != null;
  const hasExcited = game.excited != null && game.excited !== 0;
  const hasPlayerPlaytime = game.totalPlaytimeSeconds != null && game.totalPlaytimeSeconds > 0;
  const hasAveragePlaytime = game.avgPlaytimeHours != null && game.avgPlaytimeHours > 0;

  const handleOpenKeyDown = (event: ReactKeyboardEvent<HTMLDivElement>) => {
    if (event.key !== "Enter" && event.key !== " ") return;
    event.preventDefault();
    onOpen();
  };

  return (
    /*
      Lop bao chi ton tai de BUI LA nam duoc ben ngoai card.
      Luc hover, card duoc dat `scale` va `z-index` — ca hai deu tao ngan xep
      moi, va con z-index am trong ngan xep do lai duoc ve SAU nen cua chinh
      no. Nen muon bui la thuc su nam sau card thi no phai la ANH EM dung
      truoc card, khong the la con.
    */
    <div className="j-card-wrap">
      <CardBush seed={game.id} />
      <article
      data-game-id={game.id}
      className={cn(
        // Rong 100% cot luoi (cot toi thieu 270px roi gian ra lap day) —
        // neu de cung 270px va canh giua thi phan thua bi don het ra hai mep.
        "game-card surface-card group relative flex w-full flex-col",
        "hover:-translate-y-1 hover:scale-[1.015] hover:border-primary/45",
        "hover:shadow-[0_18px_45px_-18px_color-mix(in_oklab,var(--color-primary)_55%,transparent)]",
      )}
    >
      <div
        role="button"
        data-game-primary="true"
        tabIndex={0}
        aria-label={`${t("action.details")}: ${game.name}`}
        onClick={onOpen}
        onKeyDown={handleOpenKeyDown}
        /*
          `relative` la thu BAT BUOC: vet sang ben duoi la absolute, ma
          `overflow-hidden` chi cat duoc phan tu absolute khi chinh no la khoi
          chua (containing block) cua phan tu do. Thieu `relative` thi khoi
          chua nhay len `article`, vet sang truot han ra NGOAI card mot chieu
          rong card — o cot cuoi ben phai no day trang sinh thanh cuon ngang.
        */
        className="relative flex flex-1 cursor-pointer flex-col overflow-hidden rounded-[inherit] outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary/70"
      >
        {/* Vet sang duoc clip ben trong card khi tan la tach ra. */}
        <span className="pointer-events-none absolute inset-0 z-20 -translate-x-full bg-gradient-to-r from-transparent via-fg/8 to-transparent opacity-0 transition-all duration-700 group-hover:translate-x-full group-hover:opacity-100" />

        {/* Giu dung ty le 270x126.2 cua WPF, nhung theo chieu rong that cua cot
            thay vi khoa cung chieu cao -> card rong ra thi anh gian theo. */}
        <div
          className="relative w-full shrink-0 overflow-hidden bg-muted"
          style={{ aspectRatio: `${CARD_WIDTH} / ${CARD_IMAGE_HEIGHT}` }}
        >
          {game.bannerPath ? (
            <img
              src={game.bannerPath}
              alt=""
              loading="lazy"
              className="h-full w-full object-cover transition-transform duration-500 group-hover:scale-105"
            />
          ) : (
            <div className="grid h-full w-full place-items-center text-muted-fg">
              <Gamepad2 aria-hidden size={28} />
            </div>
          )}

          {/* Lop phu mo nhe khi hover */}
          <div className="absolute inset-0 bg-primary-deep/15 opacity-0 backdrop-blur-[1.5px] transition-opacity duration-200 group-hover:opacity-100" />

          <GameCoverFlags isDlc={game.isDlc} isUpcoming={game.isUpcoming} publishDate={game.publishDate} />

          {game.isRunning ? (
            <span
              className={cn(
                "absolute top-2 inline-flex items-center gap-1 rounded-full bg-success/90 px-2 py-0.5 text-[10px] font-bold text-primary-fg",
                game.isDlc ? "left-12" : "left-2",
              )}
            >
              ● {t("action.play")}
            </span>
          ) : null}

          {game.folderWarning ? (
            <Tooltip label={game.folderWarning}>
              <span className="absolute right-2 top-2 grid h-6 w-6 place-items-center rounded-full bg-warning/90 text-primary-fg">
                <AlertTriangle aria-hidden size={13} />
              </span>
            </Tooltip>
          ) : null}
        </div>

        <div className="flex flex-1 flex-col gap-1.5 p-3">
          <Tooltip label={game.name}>
            <h3 className="rune-etch truncate text-sm font-bold leading-tight">
              {game.name}
            </h3>
          </Tooltip>

          <p className="h-4 truncate text-xs leading-4 text-muted-fg">
            {game.publisher ?? ""}
          </p>

          <div className="flex min-h-6 items-center gap-2 pt-0.5">
            <div className="flex min-w-0 flex-1 flex-wrap items-center gap-1">
              <CatalogPill item={game.progress} />
              {hints?.showOwnership !== false ? <CatalogPill item={game.ownership} /> : null}
            </div>
            {hasScore || hasMetaScore ? (
              <div className="ml-auto flex shrink-0 items-center gap-1.5">
                {hasScore ? (
                  <GalleryScoreChip
                    score={game.score}
                    band={game.scoreMaterialBand}
                    className="text-[12px]"
                  />
                ) : null}
                {hasMetaScore ? (
                  <MetaChip
                    value={game.metaScore}
                    band={game.metaScoreBand}
                    className="text-[12px]"
                  />
                ) : null}
              </div>
            ) : null}
          </div>

          <div className="mt-auto grid grid-rows-[1rem_1rem] gap-y-2.5 pt-3 text-[12px] text-muted-fg">
            <div className="grid min-h-4 grid-cols-2 items-center gap-x-3">
              {game.isDlc && game.baseGameName?.trim() ? (
                <span className="inline-flex min-w-0 items-center gap-1">
                  <Package aria-hidden size={13} className="shrink-0 text-indigo-400" />
                  <span className="shrink-0">{t("detail.baseGame")}:</span>
                  <span className="min-w-0 truncate rounded-md border border-border bg-surface-2/60 px-1.5 py-0.5 font-semibold text-fg">
                    {game.baseGameName}
                  </span>
                </span>
              ) : publishDate ? (
                <span className="inline-flex items-center gap-1">
                  <CalendarDays aria-hidden size={13} className="text-violet-400" />
                  {publishDate}
                </span>
              ) : null}
              {hasExcited ? (
                <span className="col-start-2 justify-self-end">
                  <ExcitedFire value={game.excited} size={15} className="text-[12px]" />
                </span>
              ) : null}
            </div>

            <div className="grid min-h-4 grid-cols-2 items-center gap-x-3">
              {hasAveragePlaytime ? (
                <CommunityAveragePlaytime
                  hours={game.avgPlaytimeHours}
                  className="text-[12px]"
                />
              ) : null}
              {hasPlayerPlaytime ? (
                <Tooltip label={t("card.playtime")}>
                  <span className="col-start-2 inline-flex items-center gap-1 justify-self-end">
                    <PlayerPlaytimeIcon className="text-sky-400" />
                    {formatPlaytimeSeconds(game.totalPlaytimeSeconds)}
                  </span>
                </Tooltip>
              ) : null}
            </div>
          </div>

          <div className="mt-2.5 flex h-8 shrink-0 items-center pr-10 text-[12px] text-muted-fg">
            {finishTime ? (
              <span className="inline-flex items-center gap-1">
                <Trophy aria-hidden size={13} className="text-success" />
                {t("card.finishTime")} {finishTime}
              </span>
            ) : null}
          </div>
        </div>
      </div>

      {game.canLaunch ? (
        <div className="absolute bottom-3 right-3 z-30">
          {/* `canLaunch` do BACKEND quyet dinh — client khong tu suy tu exeName. */}
          <IconButton
            onClick={() => {
              onPlay();
            }}
            label={t("action.play")}
            icon={<Play className="fill-current" />}
            tone="play"
          />
        </div>
      ) : null}
      </article>
    </div>
  );
}
