import { Flame, Loader2, Save, Trophy, X, type LucideIcon } from "lucide-react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";
import { toast } from "sonner";
import type { GameDetail, UserDataPatch } from "@/lib/api/domain";
import {
  RATING_MAX,
  formatExcited,
  formatPlaytimeSeconds,
  formatScore,
  isRatingInputDraft,
  isValidRating,
} from "@/lib/format";
import { gameDetailApi } from "@/lib/gameDetailApi";
import { gameUserDataPatch } from "@/lib/gameUserData";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { ExcitedFire, GalleryScoreChip, PlayerPlaytimeIcon } from "./Pills";
import {
  InlineEditButton,
  InlineNumberInput,
  NUMBER_INPUT_NO_SPINNER,
} from "./InlineEditControls";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogTitle,
  IconButton,
} from "./ui";
import { RatingScaleHint } from "./RatingScaleHint";

type EditableStat = "excited" | "score";

function DetailStat({
  label,
  valueContent,
  action,
  icon: Icon,
  iconContent,
  iconClassName,
  onValueDoubleClick,
}: {
  label: string;
  valueContent: ReactNode;
  action?: ReactNode;
  icon?: LucideIcon;
  iconContent?: ReactNode;
  iconClassName?: string;
  onValueDoubleClick?: () => void;
}) {
  return (
    <div className="group/inline-edit flex flex-col rounded-lg border border-border bg-surface-2/30 p-2.5">
      <div className="flex items-center justify-center gap-1 text-xs font-semibold text-muted-fg">
        <span className="inline-flex min-w-0 items-center gap-1">
          {iconContent ?? (Icon ? <Icon size={12} className={iconClassName} aria-hidden /> : null)}
          <span className="truncate">{label}</span>
        </span>
        {action}
      </div>
      <div
        className={cn(
          "flex min-h-9 flex-1 items-center justify-center pt-0.5",
          onValueDoubleClick && "cursor-text select-none",
        )}
        onDoubleClick={onValueDoubleClick}
      >
        {valueContent}
      </div>
    </div>
  );
}

export function GameDetailQuickStats({
  game,
  onSaved,
}: {
  game: GameDetail;
  onSaved: (game: GameDetail) => void;
}) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const [editingStat, setEditingStat] = useState<EditableStat | null>(null);
  const [inlineValue, setInlineValue] = useState("");
  const [playtimeOpen, setPlaytimeOpen] = useState(false);
  const [playtimeHours, setPlaytimeHours] = useState("0");
  const [playtimeMinutes, setPlaytimeMinutes] = useState("0");

  const saveUserData = useMutation({
    mutationFn: (patch: UserDataPatch) => gameDetailApi.updateUserData(game.id, patch),
    onSuccess: async (saved) => {
      queryClient.setQueryData(["game-detail", saved.id], saved);
      setEditingStat(null);
      setPlaytimeOpen(false);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["games"] }),
        queryClient.invalidateQueries({ queryKey: ["play-queue"] }),
        queryClient.invalidateQueries({ queryKey: ["dashboard-stats"] }),
      ]);
      onSaved(saved);
    },
    onError: (error) => toast.error(error instanceof Error ? error.message : String(error)),
  });

  const startInlineEdit = (field: EditableStat) => {
    setEditingStat(field);
    setInlineValue(game[field]?.toString() ?? "");
    saveUserData.reset();
  };

  /* De trong = xoa diem (luu null). Dang trong ma van trong thi khong goi API. */
  const submitInlineEdit = (field: EditableStat, rawValue: string) => {
    let value: number | null = null;
    if (rawValue.trim()) {
      value = Number(rawValue);
      if (!isValidRating(value)) return;
    }
    if (value === (game[field] ?? null)) {
      setEditingStat(null);
      return;
    }
    saveUserData.mutate(gameUserDataPatch(
      game,
      field === "excited" ? { excited: value } : { score: value },
    ));
  };

  const inlineInput = (field: EditableStat) => (
    <InlineNumberInput
      autoFocus
      min="0"
      max={RATING_MAX}
      step="0.5"
      aria-label={`${t("action.edit")} ${t(field === "excited" ? "sort.excited" : "sort.score")}`}
      value={inlineValue}
      disabled={saveUserData.isPending}
      onChange={(event) => {
        if (isRatingInputDraft(event.target.value)) setInlineValue(event.target.value);
      }}
      onCommit={(value) => submitInlineEdit(field, value)}
      onCancel={() => {
        setEditingStat(null);
        setInlineValue("");
        saveUserData.reset();
      }}
      className={cn(
        // 64 x 43 px khop footprint cua GalleryScoreChip size={54} trong Detail.
        "h-[43px] w-16 rounded-2xl border border-border bg-surface px-2 text-center text-2xl font-black tabular-nums text-fg outline-none transition",
        "focus:border-primary focus:ring-2 focus:ring-primary/25 disabled:cursor-wait disabled:opacity-70",
      )}
    />
  );

  const startPlaytimeEdit = () => {
    const seconds = Math.max(0, game.totalPlaytimeSeconds ?? 0);
    setEditingStat(null);
    setPlaytimeHours(Math.floor(seconds / 3600).toString());
    setPlaytimeMinutes(Math.floor((seconds % 3600) / 60).toString());
    saveUserData.reset();
    setPlaytimeOpen(true);
  };

  const submitPlaytime = () => {
    // Ca hai o deu trong = chua co gio choi -> luu null, khong phai 0.
    if (!playtimeHours.trim() && !playtimeMinutes.trim()) {
      saveUserData.mutate(gameUserDataPatch(game, { totalPlaytimeSeconds: null }));
      return;
    }
    const hours = Number(playtimeHours || 0);
    const minutes = Number(playtimeMinutes || 0);
    if (!Number.isFinite(hours) || hours < 0 || !Number.isFinite(minutes) || minutes < 0 || minutes > 59) return;

    // Popup chi sua gio/phut; giu lai phan giay hien co de khong lam mat du lieu ngam.
    const trailingSeconds = Math.max(0, game.totalPlaytimeSeconds ?? 0) % 60;
    saveUserData.mutate(gameUserDataPatch(game, {
      totalPlaytimeSeconds: Math.round(hours * 3600 + minutes * 60 + trailingSeconds),
    }));
  };

  const editAction = (field: EditableStat, label: string) => editingStat !== field ? (
    <InlineEditButton
      label={`${t("action.edit")} ${label}`}
      disabled={saveUserData.isPending}
      onClick={() => startInlineEdit(field)}
    />
  ) : undefined;

  const score = formatScore(game.score);

  return (
    <>
      <div className="grid grid-cols-[0.85fr_1.3fr_0.85fr] gap-2 text-sm">
        <DetailStat
          label={t("sort.excited")}
          icon={Flame}
          iconClassName="text-field-excited"
          action={editAction("excited", t("sort.excited"))}
          onValueDoubleClick={editingStat !== "excited" && !saveUserData.isPending
            ? () => startInlineEdit("excited")
            : undefined}
          valueContent={editingStat === "excited" ? inlineInput("excited") : formatExcited(game.excited) ? (
            <ExcitedFire value={game.excited} showIcon={false} className="text-2xl leading-tight" />
          ) : (
            <span className="text-2xl font-black text-muted-fg">—</span>
          )}
        />
        <DetailStat
          label={t("card.playtime")}
          iconContent={<PlayerPlaytimeIcon className="text-field-playtime" />}
          action={!game.isDlc ? (
            <InlineEditButton
              label={`${t("action.edit")} ${t("card.playtime")}`}
              disabled={saveUserData.isPending}
              onClick={startPlaytimeEdit}
            />
          ) : undefined}
          onValueDoubleClick={!game.isDlc && !saveUserData.isPending ? startPlaytimeEdit : undefined}
          valueContent={!game.isDlc && game.totalPlaytimeSeconds ? (
            <p className="truncate text-2xl font-black leading-tight">
              {formatPlaytimeSeconds(game.totalPlaytimeSeconds)}
            </p>
          ) : (
            <span className="text-2xl font-black text-muted-fg">—</span>
          )}
        />
        <DetailStat
          label={t("sort.score")}
          icon={Trophy}
          iconClassName="text-field-score"
          action={editAction("score", t("sort.score"))}
          onValueDoubleClick={editingStat !== "score" && !saveUserData.isPending
            ? () => startInlineEdit("score")
            : undefined}
          valueContent={editingStat === "score" ? inlineInput("score") : score ? (
            <GalleryScoreChip
              score={game.score}
              band={game.scoreMaterialBand}
              size={54}
              className="rounded-2xl px-3.5 text-2xl"
            />
          ) : (
            <span className="text-2xl font-black text-muted-fg">—</span>
          )}
        />
      </div>
      {editingStat ? <RatingScaleHint className="mx-auto mt-2" /> : null}

      <Dialog
        open={playtimeOpen}
        onOpenChange={(nextOpen) => {
          if (saveUserData.isPending) return;
          setPlaytimeOpen(nextOpen);
          if (!nextOpen) saveUserData.reset();
        }}
      >
        <DialogContent className="max-w-sm gap-0 overflow-hidden p-0">
          <form
            onSubmit={(event) => {
              event.preventDefault();
              submitPlaytime();
            }}
          >
            <div className="space-y-4 p-5 pr-14">
              <div>
                <DialogTitle>{t("action.edit")} {t("card.playtime")}</DialogTitle>
                <DialogDescription>{t("editor.description")}</DialogDescription>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <label className="grid gap-1.5 text-xs font-bold text-muted-fg">
                  {t("editor.playedHours")}
                  <input
                    autoFocus
                    type="number"
                    min="0"
                    step="1"
                    value={playtimeHours}
                    disabled={saveUserData.isPending}
                    onFocus={(event) => event.currentTarget.select()}
                    onChange={(event) => setPlaytimeHours(event.target.value)}
                    className={cn(
                      NUMBER_INPUT_NO_SPINNER,
                      "h-10 rounded-lg border border-border bg-surface px-3 text-center text-lg font-black tabular-nums text-fg outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/25 disabled:cursor-wait disabled:opacity-70",
                    )}
                  />
                </label>
                <label className="grid gap-1.5 text-xs font-bold text-muted-fg">
                  {t("editor.playedMinutes")}
                  <input
                    type="number"
                    min="0"
                    max="59"
                    step="1"
                    value={playtimeMinutes}
                    disabled={saveUserData.isPending}
                    onChange={(event) => setPlaytimeMinutes(event.target.value)}
                    className={cn(
                      NUMBER_INPUT_NO_SPINNER,
                      "h-10 rounded-lg border border-border bg-surface px-3 text-center text-lg font-black tabular-nums text-fg outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/25 disabled:cursor-wait disabled:opacity-70",
                    )}
                  />
                </label>
              </div>
            </div>
            <DialogFooter>
              <IconButton
                label={t("action.close")}
                icon={<X />}
                disabled={saveUserData.isPending}
                onClick={() => {
                  setPlaytimeOpen(false);
                  saveUserData.reset();
                }}
              />
              <IconButton
                type="submit"
                label={t("action.save")}
                tone="add"
                icon={saveUserData.isPending ? <Loader2 className="animate-spin" /> : <Save />}
                disabled={saveUserData.isPending || !playtimeHours || !playtimeMinutes}
              />
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </>
  );
}
