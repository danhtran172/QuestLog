/**
 * The cho mot ban ghi metadata — Admin Console, spec §12.1.
 *
 * KHONG tai dung `GameCard`: the do render progress, ownership, diem cua nguoi
 * choi, gio choi thuc, `isRunning`, `folderWarning` — toan field userdata khong
 * ton tai tren ban ghi metadata. Dung chung se phai truyen mot dong `undefined`
 * va ca hai ben cung kho doc.
 *
 * Giu dung ngon ngu thi giac cua the thu vien: cung `surface-card`, cung ty le
 * anh 270x126.2, cung kieu hover. Admin dang lam viec voi cung mot kho game,
 * khong co ly do de hai man hinh trong nhu hai san pham.
 *
 * ANH: `bannerUrl` (Steam header ngang) truoc, roi `backdropUrl`
 * (anh ngang cho hero). KHONG bao gio fallback sang `posterUrl` — cot do la
 * anh DOC, nhet vao khung ngang se meo (DOMAIN_RULES §12).
 *
 * META SCORE hien la SO TRAN, khong to mau theo dai. `metaScoreBand` do backend
 * tinh cho game thu vien; ban ghi metadata khong co field do, va tu suy dai o
 * client la vi pham AGENTS.md §2 (ngưỡng thuộc về server).
 */
import { CalendarDays, Clock, FileCheck2, FileX, Gamepad2, Layers, Pencil, Star, Trash2 } from "lucide-react";
import { cn } from "@/lib/utils";
import { useI18n } from "@/lib/i18n";
import { CARD_IMAGE_HEIGHT, CARD_WIDTH } from "@/lib/api/domain";
import { formatPublishDate } from "@/lib/format";
import { adminHealthLabelKey, type AdminMetadataGame } from "@/lib/adminApi";
import { IconButton, Tooltip } from "./ui";


export function AdminGameCardSkeleton() {
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

/** Mot o so lieu o chan the. Thieu du lieu thi van giu cho de luoi khong nhay. */
function Stat({ icon, value }: { icon: React.ReactNode; value: string | null }) {
  return (
    <span
      className={cn(
        "flex min-w-0 items-center gap-1 text-[12px]",
        value ? "text-muted-fg" : "text-muted-fg/40",
      )}
    >
      <span className="shrink-0" aria-hidden="true">{icon}</span>
      <span className="truncate tabular-nums">{value ?? "—"}</span>
    </span>
  );
}

export function AdminGameCard({
  game,
  seriesName,
  onEdit,
  onDelete,
  onTogglePublished,
  deleting,
  togglingPublished,
}: {
  game: AdminMetadataGame;
  /** Ban ghi chi giu `seriesId`; ten do section tra cuu roi truyen xuong. */
  seriesName: string | null;
  onEdit: () => void;
  onDelete: () => void;
  onTogglePublished: () => void;
  deleting: boolean;
  togglingPublished: boolean;
}) {
  const { t } = useI18n();
  const image = game.bannerUrl ?? game.backdropUrl;

  return (
    <article
      data-metadata-game-id={game.id}
      className={cn(
        "surface-card group relative flex w-full flex-col transition-transform duration-200",
        "hover:-translate-y-1 hover:border-primary/45",
        "hover:shadow-[0_18px_45px_-18px_color-mix(in_oklab,var(--color-primary)_55%,transparent)]",
      )}
    >
      <div
        role="button"
        tabIndex={0}
        aria-label={`${t("action.edit")}: ${game.name}`}
        onClick={onEdit}
        onKeyDown={(event) => {
          if (event.key !== "Enter" && event.key !== " ") return;
          event.preventDefault();
          onEdit();
        }}
        className="flex flex-1 cursor-pointer flex-col overflow-hidden rounded-[inherit] outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary/70"
      >
        <div
          className="relative w-full shrink-0 overflow-hidden bg-muted"
          style={{ aspectRatio: `${CARD_WIDTH} / ${CARD_IMAGE_HEIGHT}` }}
        >
          {image ? (
            <img
              src={image}
              alt=""
              loading="lazy"
              className="h-full w-full object-cover transition-transform duration-500 group-hover:scale-105"
            />
          ) : (
            <div className="grid h-full w-full place-items-center text-muted-fg">
              <Gamepad2 aria-hidden size={28} />
            </div>
          )}

          {game.isDlc ? (
            <span className="absolute left-2 top-2 rounded-full bg-primary/90 px-2 py-0.5 text-[10px] font-black uppercase tracking-wide text-primary-fg">
              DLC
            </span>
          ) : null}

          {/* id + revision la thong tin lam viec cua admin: bao cao loi, doi chieu
              audit va chan xung dot revision deu can no. */}
          <span className="absolute right-2 top-2 rounded-md bg-black/55 px-1.5 py-0.5 font-mono text-[10px] font-bold text-white">
            #{game.id} · r{game.revision}
          </span>
        </div>

        <div className="flex flex-1 flex-col gap-1.5 p-3">
          {/* Chua xuat ban thi phai thay ngay o luoi, khong phai mo ra moi biet.
              Neu bi chan vi truot tieu chi thi noi luon truot cai gi (spec §13). */}
          {!game.isPublished ? (
            <Tooltip
              label={
                game.gateFailures.length > 0
                  ? t("admin.publishing.blockedBecause").replace(
                      "{rules}",
                      game.gateFailures.map((key) => t(adminHealthLabelKey(key))).join(", "),
                    )
                  : t("admin.publishing.unpublishedManually")
              }
            >
              <span className="mb-0.5 flex w-fit items-center gap-1 rounded-full border border-warning/45 bg-warning/15 px-2 py-0.5 text-[10px] font-black uppercase tracking-wide text-warning">
                <FileX size={10} aria-hidden="true" />
                {t("admin.publishing.notPublished")}
              </span>
            </Tooltip>
          ) : null}

          <Tooltip label={game.name}>
            <h3 className="truncate text-sm font-bold leading-tight text-fg">{game.name}</h3>
          </Tooltip>

          <p className="h-4 truncate text-xs leading-4 text-muted-fg">
            {game.developer ?? game.publisher ?? ""}
          </p>

          <div className="mt-auto grid grid-cols-2 gap-x-3 gap-y-1.5 pt-3">
            <Stat icon={<CalendarDays size={12} />} value={formatPublishDate(game.publishDate)} />
            <Stat
              icon={<Star size={12} />}
              value={game.metaScore != null ? String(game.metaScore) : null}
            />
            <Stat
              icon={<Clock size={12} />}
              value={game.avgPlaytimeHours != null ? `${game.avgPlaytimeHours}h` : null}
            />
            <Stat icon={<Layers size={12} />} value={seriesName} />
          </div>
        </div>
      </div>

      {/* Ngoai vung bam-de-sua, neu khong bam Xoa se mo luon trinh sua. */}
      <div className="flex justify-end gap-1 border-t border-border/60 px-2 py-1.5">
        <IconButton
          size="compact"
          label={
            game.gateFailures.length > 0
              ? t("admin.publishing.lockedByRules")
              : game.manuallyUnpublished
                ? t("admin.publishing.publish")
                : t("admin.publishing.unpublish")
          }
          icon={game.manuallyUnpublished ? <FileX /> : <FileCheck2 />}
          /* Truot tieu chi thi bat/tat thu cong khong doi duoc gi — khoa lai va
             noi ro ly do, thay vi de bam mot nut khong co tac dung. */
          disabled={game.gateFailures.length > 0 || togglingPublished}
          onClick={onTogglePublished}
        />
        <IconButton
          size="compact"
          tone="edit"
          label={t("action.edit")}
          icon={<Pencil />}
          onClick={onEdit}
        />
        <IconButton
          size="compact"
          tone="danger"
          label={t("action.delete")}
          icon={<Trash2 />}
          disabled={deleting}
          onClick={onDelete}
        />
      </div>
    </article>
  );
}
