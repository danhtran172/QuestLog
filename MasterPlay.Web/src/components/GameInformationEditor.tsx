import { useEffect, useMemo, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, ArrowLeft, Check, Loader2, Save, X } from "lucide-react";
import type { GameDetail } from "@/lib/api/domain";
import { api } from "@/lib/api/client";
import { gameDetailApi } from "@/lib/gameDetailApi";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogTitle,
  IconButton,
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
  Switch,
} from "./ui";
import {
  EditorHero,
  EditorPanel,
  EditorRow,
  editorInputClass,
} from "./GameEditorControls";
import { TaxonomyPicker } from "./TaxonomyPicker";

type InformationDraft = {
  name: string;
  publisher: string;
  developer: string;
  publishDate: string;
  isDlc: boolean;
  seriesId: string;
  baseGameId: string;
};

type Props = {
  game: GameDetail;
  onBack: () => void;
  onClose: () => void;
  onSaved: (game: GameDetail) => void;
  registerCloseGuard?: (guard: (() => boolean) | null) => void;
};

const NONE = "__none__";

function toDraft(game: GameDetail): InformationDraft {
  return {
    name: game.name,
    publisher: game.publisher ?? "",
    developer: game.developer ?? "",
    publishDate: game.publishDate ?? "",
    isDlc: game.isDlc,
    seriesId: game.series?.id.toString() ?? NONE,
    baseGameId: game.baseGameId ?? NONE,
  };
}

export function GameInformationEditor({ game, onBack, onClose, onSaved, registerCloseGuard }: Props) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const [draft, setDraft] = useState(() => toDraft(game));
  const [pendingExit, setPendingExit] = useState<"close" | "back" | null>(null);
  const baseDraft = useMemo(() => toDraft(game), [game]);
  const dirty = useMemo(
    () => (Object.keys(baseDraft) as Array<keyof InformationDraft>).some((key) => draft[key] !== baseDraft[key]),
    [baseDraft, draft],
  );
  const dirtyRef = useRef(dirty);
  dirtyRef.current = dirty;
  const pendingExitRef = useRef(pendingExit);
  pendingExitRef.current = pendingExit;

  useEffect(() => {
    registerCloseGuard?.(() => {
      if (!dirtyRef.current) return false;
      setPendingExit("close");
      return true;
    });
    return () => registerCloseGuard?.(null);
  }, [registerCloseGuard]);
  const series = useQuery({ queryKey: ["taxonomy", "series"], queryFn: () => api.taxonomy("series") });
  const baseGames = useQuery({
    queryKey: ["base-game-candidates", game.id],
    queryFn: () => api.baseGameCandidates(game.id),
    enabled: draft.isDlc,
  });

  const payload = useMemo<GameDetail>(() => ({
    ...game,
    name: draft.name.trim(),
    publisher: draft.publisher.trim() || null,
    developer: draft.developer.trim() || null,
    publishDate: draft.publishDate || null,
    isDlc: draft.isDlc,
    series: draft.isDlc
      ? null
      : series.data?.find((item) => String(item.id) === draft.seriesId) ?? null,
    baseGameId: draft.isDlc && draft.baseGameId !== NONE ? draft.baseGameId : null,
  }), [draft, game, series.data]);

  const save = useMutation({
    mutationFn: () => gameDetailApi.update(game.id, payload),
    onSuccess: async (saved) => {
      queryClient.setQueryData(["game-detail", saved.id], saved);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["games"] }),
        queryClient.invalidateQueries({ queryKey: ["play-queue"] }),
        queryClient.invalidateQueries({ queryKey: ["dashboard-stats"] }),
      ]);
      const exit = pendingExitRef.current;
      setPendingExit(null);
      onSaved(saved);
      if (exit === "close") onClose();
    },
  });

  const set = <K extends keyof InformationDraft>(key: K, value: InformationDraft[K]) =>
    setDraft((current) => ({ ...current, [key]: value }));
  const referenceError = draft.isDlc ? baseGames.error : series.error;
  const error = save.error ?? referenceError;
  const referencesPending = draft.isDlc ? baseGames.isPending : series.isPending;

  return (
    <form
      noValidate
      className="flex min-h-0 flex-1 flex-col overflow-hidden"
      onSubmit={(event) => {
        event.preventDefault();
        if (draft.name.trim()) save.mutate();
      }}
    >
      <DialogTitle className="sr-only">{t("detail.gameInfo")}</DialogTitle>
      <DialogDescription className="sr-only">{t("editor.description")}</DialogDescription>

      <div className="min-h-0 flex-1 overflow-y-auto">
        {error ? (
          <div className="sticky top-0 z-20 mx-5 mt-3 flex gap-2 rounded-lg border border-danger/45 bg-danger/20 p-2.5 text-xs text-danger shadow-lg backdrop-blur-xl">
            <AlertTriangle size={14} className="mt-0.5 shrink-0" />
            <span className="min-w-0 break-words">{error instanceof Error ? error.message : String(error)}</span>
          </div>
        ) : null}

        <EditorHero
          name={draft.name}
          publisher={draft.publisher}
          imageSrc={game.backdropPath || game.bannerPath || ""}
          fallbackName={t("editor.editTitle")}
        />

        <div className="mx-auto w-full max-w-2xl px-5 pb-20 pt-5 sm:px-6">
          <EditorPanel className="space-y-3">
            <EditorRow label={t("editor.name")}>
              <input
                autoFocus
                className={cn(editorInputClass, "h-8")}
                value={draft.name}
                onChange={(event) => set("name", event.target.value)}
              />
            </EditorRow>
            <EditorRow label={t("detail.publisher")}>
              <input className={cn(editorInputClass, "h-8")} value={draft.publisher} onChange={(event) => set("publisher", event.target.value)} />
            </EditorRow>
            <EditorRow label={t("editor.developer")}>
              <input className={cn(editorInputClass, "h-8")} value={draft.developer} onChange={(event) => set("developer", event.target.value)} />
            </EditorRow>
            <EditorRow label={t("sort.publish")}>
              <input type="date" className={cn(editorInputClass, "h-8")} value={draft.publishDate} onChange={(event) => set("publishDate", event.target.value)} />
            </EditorRow>
            <EditorRow label={t("editor.dlc")}>
              <Switch checked={draft.isDlc} onCheckedChange={(value) => set("isDlc", value)} />
            </EditorRow>
            {draft.isDlc ? (
              <EditorRow label={t("editor.baseGame")}>
                <Select value={draft.baseGameId} onValueChange={(value) => set("baseGameId", value)}>
                  <SelectTrigger className={cn(editorInputClass, "h-8")} aria-label={t("editor.baseGame")}>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent className="max-h-72 min-w-64">
                    <SelectItem value={NONE}>{t("editor.none")}</SelectItem>
                    {baseGames.data?.map((candidate) => (
                      <SelectItem key={candidate.id} value={candidate.id}>{candidate.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </EditorRow>
            ) : (
              <TaxonomyPicker
                kind="series"
                label={t("sort.series")}
                items={series.data ?? []}
                value={draft.seriesId === NONE ? [] : [Number(draft.seriesId)]}
                multiple={false}
                row
                onChange={(value) => set("seriesId", value[0] ? String(value[0]) : NONE)}
              />
            )}
          </EditorPanel>
        </div>
      </div>

      <div className="absolute bottom-3 right-3 z-30 flex items-center gap-2">
        <IconButton
          label={t("action.back")}
          icon={<ArrowLeft />}
          onClick={() => (dirty ? setPendingExit("back") : onBack())}
          disabled={save.isPending}
        />
        <IconButton
          type="submit"
          tone="add"
          label={t("action.save")}
          icon={save.isPending ? <Loader2 className="animate-spin" /> : <Check />}
          disabled={referencesPending || save.isPending || !draft.name.trim()}
        />
      </div>

      <Dialog open={pendingExit !== null} onOpenChange={(next) => { if (!next) setPendingExit(null); }}>
        <DialogContent className="max-w-sm">
          <div className="space-y-2 p-5 pr-14">
            <DialogTitle>{t("editor.unsavedTitle")}</DialogTitle>
            <DialogDescription>{t("editor.unsavedDescription")}</DialogDescription>
          </div>
          <DialogFooter>
            <IconButton
              tone="danger"
              label={t("editor.discardChanges")}
              icon={<X />}
              disabled={save.isPending}
              onClick={() => {
                const exit = pendingExit;
                setPendingExit(null);
                if (exit === "close") onClose();
                else onBack();
              }}
            />
            <IconButton
              tone="add"
              label={t("action.save")}
              icon={save.isPending ? <Loader2 className="animate-spin" /> : <Save />}
              disabled={referencesPending || save.isPending || !draft.name.trim()}
              onClick={() => save.mutate()}
            />
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </form>
  );
}
