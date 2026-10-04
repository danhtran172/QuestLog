import { AlertTriangle, Loader2, Plus, Save, Trash2, X } from "lucide-react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState, type ReactNode } from "react";
import type { GameDetail, GameFinishEntry } from "@/lib/api/domain";
import { gameDetailApi } from "@/lib/gameDetailApi";
import {
  finishTimeDraftEquals,
  finishTimeDraftFromEntry,
  isValidFinishTimeDraft,
  toFinishTimeRequest,
  type FinishTimeDraft,
} from "@/lib/finishTime";
import { formatFinishTime, formatScore } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { useExitOnOutsidePointer } from "@/lib/useExitOnOutsidePointer";
import { cn } from "@/lib/utils";
import { FinishGameDialog } from "./FinishGameDialog";
import { FinishTimeFields } from "./FinishTimeFields";
import { InlineEditButton } from "./InlineEditControls";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogTitle,
  IconButton,
} from "./ui";

/** Mot moc hoan thanh = mot pill "thang/nam" kem diem cua lan do. */
function FinishPill({ finish, className }: { finish: GameFinishEntry; className?: string }) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 rounded-md border border-border/70 bg-surface-2/70 px-1.5 py-0.5 text-xs font-semibold text-fg",
        className,
      )}
    >
      <span className="tabular-nums">{formatFinishTime(finish.finishedAt, finish.hasMonth) ?? "—"}</span>
      <span className="tabular-nums text-muted-fg">{formatScore(finish.score) ?? "—"}</span>
    </span>
  );
}

/**
 * Hang Finish history cua the chi tiet, sua duoc tai cho — cung khuon voi hang
 * Genre/Theme: nut but canh nhan, nhay dup vao hang, bam ra ngoai la xong.
 *
 * Khac Genre/Theme mot cho: sua mot moc chi sua duoc THOI DIEM. Diem cua mot
 * lan hoan thanh la bat bien sau khi ghi — no la diem TAI LUC DO. Muon doi danh
 * gia hien tai thi sua `game.score` (DOMAIN_RULES §9.4).
 *
 * Xoa mot moc khong dung toi diem game; xoa het moc thi backend bo trong
 * Progress (§9.6). Vi the buoc hoi lai noi ro hau qua do.
 *
 * Thu tu moc la moi nhat truoc, va do SERVER sap (`OrderByDescending`). Khong
 * sap lai o client — DOMAIN_RULES §5, §7.
 */
export function GameDetailFinishHistoryRow({
  game,
  label,
  icon,
  onSaved,
}: {
  game: GameDetail;
  label: string;
  icon: ReactNode;
  onSaved: (game: GameDetail) => void;
}) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [addOpen, setAddOpen] = useState(false);
  const [timeTarget, setTimeTarget] = useState<GameFinishEntry | null>(null);
  const [time, setTime] = useState<FinishTimeDraft>({ year: "", month: "" });
  const [removalTarget, setRemovalTarget] = useState<GameFinishEntry | null>(null);
  const rowRef = useRef<HTMLDivElement>(null);

  const finishes = game.finishes ?? [];
  /* Diem de dien san khi them moc moi: diem cua lan hoan thanh gan nhat, hoac
     diem game dang mang neu chua hoan thanh lan nao. */
  const previousScore = finishes[0]?.score ?? game.score ?? null;

  useEffect(() => {
    setTime(timeTarget ? finishTimeDraftFromEntry(timeTarget) : { year: "", month: "" });
  }, [timeTarget]);

  const refreshAfterSave = async (saved: GameDetail) => {
    queryClient.setQueryData(["game-detail", saved.id], saved);
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["games"] }),
      /* Xoa moc cuoi co the bo trong Progress -> game roi khoi tab Finished va
         co the roi vao queue. Lam moi ca hai. */
      queryClient.invalidateQueries({ queryKey: ["play-queue"] }),
      queryClient.invalidateQueries({ queryKey: ["dashboard-stats"] }),
    ]);
    onSaved(saved);
  };

  const saveTime = useMutation({
    mutationFn: ({ finishId, draft }: { finishId: number; draft: FinishTimeDraft }) =>
      gameDetailApi.updateFinishTime(game.id, finishId, toFinishTimeRequest(draft)),
    onSuccess: async (saved) => {
      setTimeTarget(null);
      await refreshAfterSave(saved);
    },
  });

  const removeFinish = useMutation({
    mutationFn: (finishId: number) => gameDetailApi.deleteFinish(game.id, finishId),
    onSuccess: async (saved) => {
      setRemovalTarget(null);
      await refreshAfterSave(saved);
    },
  });

  const busy = saveTime.isPending || removeFinish.isPending;

  const stopEditing = () => {
    setEditing(false);
    setTimeTarget(null);
    setRemovalTarget(null);
  };

  useExitOnOutsidePointer(
    editing && !addOpen && !timeTarget && !removalTarget,
    rowRef,
    stopEditing,
  );

  const startEditingOnDoubleClick = editing || busy ? undefined : () => setEditing(true);
  const timeChanged = timeTarget !== null &&
    isValidFinishTimeDraft(time) &&
    !finishTimeDraftEquals(time, finishTimeDraftFromEntry(timeTarget));
  /* Xoa moc CUOI CUNG keo theo hau qua khac han: game khong con la Done. */
  const removingLast = removalTarget !== null && finishes.length === 1;

  return (
    <div ref={rowRef} className="group/inline-edit contents">
      <dt
        className="flex items-center gap-1.5 font-semibold text-muted-fg"
        onDoubleClick={startEditingOnDoubleClick}
      >
        <span className="grid shrink-0 place-items-center">{icon}</span>
        <span>{label}:</span>
        {editing ? null : (
          <InlineEditButton
            label={`${t("action.edit")} ${label}`}
            disabled={busy}
            onClick={() => setEditing(true)}
          />
        )}
      </dt>

      <dd
        className={cn("min-w-0 break-words font-semibold text-fg", !editing && "cursor-text select-none")}
        onDoubleClick={startEditingOnDoubleClick}
      >
        {editing ? (
          <span className="flex flex-wrap items-center gap-1.5">
            {finishes.map((finish) => {
              const when = formatFinishTime(finish.finishedAt, finish.hasMonth) ?? "";

              return (
                <span
                  key={finish.id}
                  className="inline-flex items-center gap-1 rounded-md border border-dashed border-border/70 bg-surface-2/70 px-1.5 py-0.5 text-xs font-semibold text-fg"
                >
                  <button
                    type="button"
                    aria-label={`${t("editor.finishTime")} ${when}`}
                    title={t("editor.finishTime")}
                    disabled={busy}
                    className="inline-flex items-center gap-1.5 rounded outline-none transition hover:text-primary focus-visible:ring-1 focus-visible:ring-primary/70 disabled:cursor-wait disabled:opacity-45"
                    onClick={() => setTimeTarget(finish)}
                  >
                    <span className="tabular-nums">{when || "—"}</span>
                    <span className="tabular-nums text-muted-fg">{formatScore(finish.score) ?? "—"}</span>
                  </button>
                  <button
                    type="button"
                    aria-label={`${t("action.delete")} ${when}`}
                    title={`${t("action.delete")} ${when}`}
                    disabled={busy}
                    className={cn(
                      "opacity-55 transition hover:opacity-100",
                      "focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-primary/70",
                      "disabled:cursor-wait disabled:opacity-30",
                    )}
                    onClick={() => setRemovalTarget(finish)}
                  >
                    <X size={11} />
                  </button>
                </span>
              );
            })}

            <button
              type="button"
              aria-label={t("detail.addFinish")}
              title={t("detail.addFinish")}
              disabled={busy}
              className={cn(
                "inline-grid h-[22px] w-[22px] place-items-center rounded-md border border-dashed border-border text-muted-fg transition",
                "hover:border-primary/70 hover:text-primary",
                "focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-primary/70",
                "disabled:cursor-wait disabled:opacity-45",
              )}
              onClick={() => setAddOpen(true)}
            >
              <Plus size={12} />
            </button>
          </span>
        ) : finishes.length ? (
          <span className="flex flex-wrap items-center gap-1.5">
            {finishes.map((finish) => (
              <FinishPill key={finish.id} finish={finish} />
            ))}
          </span>
        ) : (
          <span className="text-muted-fg">—</span>
        )}
      </dd>

      <div className="-my-1 justify-self-end">
        {/* Chi THOAT che do sua — moi thay doi da luu ngay luc thuc hien. */}
        {editing ? (
          <IconButton
            size="mini"
            label={t("action.done")}
            icon={busy ? <Loader2 className="animate-spin" /> : <X />}
            disabled={busy}
            onClick={stopEditing}
          />
        ) : null}
      </div>

      <FinishGameDialog
        gameId={game.id}
        open={addOpen}
        onOpenChange={setAddOpen}
        defaultScore={previousScore}
        onSaved={onSaved}
      />

      <Dialog open={timeTarget !== null} onOpenChange={(next) => { if (!next) setTimeTarget(null); }}>
        <DialogContent className="max-w-sm">
          <form
            onSubmit={(event) => {
              event.preventDefault();
              if (timeTarget && timeChanged) {
                saveTime.mutate({ finishId: timeTarget.id, draft: time });
              }
            }}
          >
            <div className="space-y-4 p-5 pr-14">
              <div>
                <DialogTitle>{t("editor.finishTime")}</DialogTitle>
                {/* Noi truoc rang diem la bat bien, de nguoi dung khong di tim
                    nut sua diem cho met. */}
                <DialogDescription>{t("detail.finishTimeOnly")}</DialogDescription>
              </div>
              {saveTime.error ? (
                <div className="flex gap-2 rounded-lg border border-danger/40 bg-danger/10 p-3 text-sm text-danger">
                  <AlertTriangle size={16} className="mt-0.5 shrink-0" />
                  <span>{saveTime.error.message}</span>
                </div>
              ) : null}
              <FinishTimeFields autoFocus draft={time} onChange={setTime} disabled={saveTime.isPending} />
            </div>
            <DialogFooter>
              <IconButton
                label={t("action.cancel")}
                icon={<X />}
                disabled={saveTime.isPending}
                onClick={() => setTimeTarget(null)}
              />
              <IconButton
                type="submit"
                tone="add"
                label={t("editor.saveFinishTime")}
                icon={saveTime.isPending ? <Loader2 className="animate-spin" /> : <Save />}
                disabled={saveTime.isPending || !timeChanged}
              />
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={removalTarget !== null} onOpenChange={(next) => { if (!next) setRemovalTarget(null); }}>
        <DialogContent className="max-w-sm">
          <div className="space-y-3 p-5 pr-14">
            <DialogTitle>{t("detail.finishRemoveTitle")}</DialogTitle>
            <DialogDescription>
              <strong className="font-bold tabular-nums text-fg">
                {removalTarget ? formatFinishTime(removalTarget.finishedAt, removalTarget.hasMonth) : null}
              </strong>
              {" — "}
              {t("detail.finishRemoveHint")}
            </DialogDescription>
            {/* Xoa moc cuoi cung con keo Progress ra khoi Done. Noi thang, chu
                khong de nguoi dung phat hien sau khi game bien khoi tab. */}
            {removingLast ? (
              <p className="flex gap-2 rounded-lg border border-warning/40 bg-warning/10 p-3 text-xs font-semibold text-warning">
                <AlertTriangle size={14} className="mt-0.5 shrink-0" />
                {t("detail.finishRemoveLast")}
              </p>
            ) : null}
            {removeFinish.error ? (
              <div className="flex gap-2 rounded-lg border border-danger/40 bg-danger/10 p-3 text-sm text-danger">
                <AlertTriangle size={16} className="mt-0.5 shrink-0" />
                <span>{removeFinish.error.message}</span>
              </div>
            ) : null}
          </div>
          <DialogFooter>
            <IconButton
              label={t("action.cancel")}
              icon={<X />}
              disabled={removeFinish.isPending}
              onClick={() => setRemovalTarget(null)}
            />
            <IconButton
              tone="danger"
              label={t("action.delete")}
              icon={removeFinish.isPending ? <Loader2 className="animate-spin" /> : <Trash2 />}
              disabled={removeFinish.isPending}
              onClick={() => removalTarget && removeFinish.mutate(removalTarget.id)}
            />
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
