import { AlertTriangle, Loader2, Trophy } from "lucide-react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import type { GameDetail } from "@/lib/api/domain";
import { gameDetailApi } from "@/lib/gameDetailApi";
import {
  finishTimeDraftFromNow,
  isValidFinishTimeDraft,
  toFinishTimeRequest,
  type FinishTimeDraft,
} from "@/lib/finishTime";
import { RATING_MAX, isRatingInputDraft, isValidRating } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { FinishTimeFields } from "./FinishTimeFields";
import { NUMBER_INPUT_NO_SPINNER } from "./InlineEditControls";
import { RatingScaleHint } from "./RatingScaleHint";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogTitle,
  IconButton,
} from "./ui";

/**
 * Ghi MOT moc hoan thanh.
 *
 * `POST /api/games/{id}/finishes` luon doi mot diem: no vua them moc vua dat
 * lai diem cua game va day Progress sang Done. Nen o day khong co duong "them
 * moc ma khong cham diem" — cai co duoc la DIEN SAN diem dang co, de lan hoan
 * thanh thu hai tro xuong chi con mot cu bam neu khong muon doi gi.
 *
 * Thoi diem cung dien san (hom nay) nhung sua duoc ngay tai day, va xem truoc
 * duoc — game hoan thanh tu nam ngoai la chuyen thuong, bat ghi "hom nay" roi
 * di sua lai la bat lam hai lan.
 */
export function FinishGameDialog({
  gameId,
  open,
  onOpenChange,
  defaultScore,
  onSaved,
}: {
  gameId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Diem dang co, dung de dien san. `null` = chua cham bao gio. */
  defaultScore: number | null;
  onSaved: (game: GameDetail) => void;
}) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const [score, setScore] = useState("");
  const [time, setTime] = useState<FinishTimeDraft>(() => finishTimeDraftFromNow());

  /* Dien lai moi lan mo: diem co the da doi ke tu lan mo truoc, va "hom nay"
     cua lan mo truoc co the da la hom qua. */
  useEffect(() => {
    if (!open) return;
    setScore(defaultScore == null ? "" : String(defaultScore));
    setTime(finishTimeDraftFromNow());
  }, [open, defaultScore]);

  const finish = useMutation({
    mutationFn: (value: number) => gameDetailApi.finish(gameId, {
      score: value,
      ...toFinishTimeRequest(time),
    }),
    onSuccess: async (saved) => {
      queryClient.setQueryData(["game-detail", saved.id], saved);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["games"] }),
        queryClient.invalidateQueries({ queryKey: ["play-queue"] }),
        queryClient.invalidateQueries({ queryKey: ["dashboard-stats"] }),
      ]);
      onOpenChange(false);
      onSaved(saved);
    },
  });

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        onOpenChange(next);
        if (!next) finish.reset();
      }}
    >
      <DialogContent className="max-w-sm">
        <form
          onSubmit={(event) => {
            event.preventDefault();
            const value = Number(score);
            if (isValidRating(value) && isValidFinishTimeDraft(time)) finish.mutate(value);
          }}
        >
          <div className="space-y-4 p-5 pr-14">
            <div>
              <DialogTitle>{t("finish.title")}</DialogTitle>
              {/* Da co diem thi cau hoi that su la "co doi diem khong", chu
                  khong phai "cham diem di" — hai tinh huong doc khac han. */}
              <DialogDescription>
                {t(defaultScore == null ? "finish.description" : "finish.keepScore")}
              </DialogDescription>
            </div>
            {finish.error ? (
              <div className="flex gap-2 rounded-lg border border-danger/40 bg-danger/10 p-3 text-sm text-danger">
                <AlertTriangle size={16} className="mt-0.5 shrink-0" />
                <span>{finish.error.message}</span>
              </div>
            ) : null}
            <RatingScaleHint />
            <label className="grid gap-1.5 text-xs font-bold text-muted-fg">
              {t("finish.score")}
              <input
                autoFocus
                required
                type="number"
                min="0"
                max={RATING_MAX}
                step="0.5"
                value={score}
                onChange={(event) => {
                  if (isRatingInputDraft(event.target.value)) setScore(event.target.value);
                }}
                onFocus={(event) => event.currentTarget.select()}
                className={cn(
                  NUMBER_INPUT_NO_SPINNER,
                  "h-9 rounded-lg border border-border bg-surface px-3 text-sm font-medium text-fg outline-none transition focus:border-primary",
                )}
              />
            </label>
            <FinishTimeFields draft={time} onChange={setTime} disabled={finish.isPending} />
          </div>
          <DialogFooter>
            <IconButton
              type="submit"
              label={t("action.finish")}
              tone="finish"
              icon={finish.isPending ? <Loader2 className="animate-spin" /> : <Trophy />}
              disabled={finish.isPending || !score || !isValidFinishTimeDraft(time)}
            />
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
