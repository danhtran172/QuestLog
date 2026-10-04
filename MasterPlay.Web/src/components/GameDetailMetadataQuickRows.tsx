import { Gauge, Info } from "lucide-react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";
import { toast } from "sonner";
import type { GameDetail } from "@/lib/api/domain";
import { formatAvgPlaytimeHours } from "@/lib/format";
import { gameDetailApi } from "@/lib/gameDetailApi";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { CommunityPlaytimeIcon, MetaChip } from "./Pills";
import { InlineEditButton, InlineNumberInput } from "./InlineEditControls";
import { IconButton } from "./ui";

type EditableMetadataField = "avgPlaytimeHours" | "metaScore";

function MetadataRow({
  label,
  icon,
  editAction,
  value,
  externalAction,
  onValueDoubleClick,
}: {
  label: string;
  icon: ReactNode;
  editAction: ReactNode;
  value: ReactNode;
  externalAction?: ReactNode;
  onValueDoubleClick?: () => void;
}) {
  return (
    <div className="group/inline-edit contents">
      <dt className="flex items-center gap-1.5 font-semibold text-muted-fg">
        <span className="grid shrink-0 place-items-center">{icon}</span>
        <span>{label}:</span>
        {editAction}
      </dt>
      <dd
        className={cn(
          "min-w-0 break-words font-semibold text-fg",
          onValueDoubleClick && "cursor-text select-none",
        )}
        onDoubleClick={onValueDoubleClick}
      >
        {value}
      </dd>
      <div className="-my-1 justify-self-end">{externalAction}</div>
    </div>
  );
}

export function GameDetailMetadataQuickRows({
  game,
  hltbUrl,
  metacriticUrl,
  onSaved,
}: {
  game: GameDetail;
  hltbUrl?: string;
  metacriticUrl?: string;
  onSaved: (game: GameDetail) => void;
}) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const [editingField, setEditingField] = useState<EditableMetadataField | null>(null);
  const [inlineValue, setInlineValue] = useState("");

  const saveMetadata = useMutation({
    mutationFn: ({ field, value }: { field: EditableMetadataField; value: number }) => {
      const latest = queryClient.getQueryData<GameDetail>(["game-detail", game.id]) ?? game;
      return gameDetailApi.update(game.id, { ...latest, [field]: value });
    },
    onSuccess: async (saved) => {
      queryClient.setQueryData(["game-detail", saved.id], saved);
      setEditingField(null);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["games"] }),
        queryClient.invalidateQueries({ queryKey: ["play-queue"] }),
        queryClient.invalidateQueries({ queryKey: ["dashboard-stats"] }),
      ]);
      onSaved(saved);
    },
    onError: (error) => toast.error(error instanceof Error ? error.message : String(error)),
  });

  const startEditing = (field: EditableMetadataField) => {
    setEditingField(field);
    setInlineValue(game[field]?.toString() ?? "");
    saveMetadata.reset();
  };

  const saveInlineValue = (field: EditableMetadataField, rawValue: string) => {
    if (!rawValue.trim()) return;
    const value = Number(rawValue);
    if (!Number.isFinite(value)) return;
    saveMetadata.mutate({ field, value });
  };

  const cancelEditing = () => {
    setEditingField(null);
    setInlineValue("");
    saveMetadata.reset();
  };

  const editAction = (field: EditableMetadataField, label: string) => editingField !== field ? (
    <InlineEditButton
      label={`${t("action.edit")} ${label}`}
      disabled={saveMetadata.isPending}
      onClick={() => startEditing(field)}
    />
  ) : null;

  return (
    <>
      <MetadataRow
        label={t("sort.avgPlaytime")}
        icon={<CommunityPlaytimeIcon />}
        editAction={editAction("avgPlaytimeHours", t("sort.avgPlaytime"))}
        onValueDoubleClick={editingField !== "avgPlaytimeHours" && !saveMetadata.isPending
          ? () => startEditing("avgPlaytimeHours")
          : undefined}
        value={editingField === "avgPlaytimeHours" ? (
          <InlineNumberInput
            autoFocus
            required
            min="0"
            step="1"
            aria-label={`${t("action.edit")} ${t("sort.avgPlaytime")}`}
            value={inlineValue}
            disabled={saveMetadata.isPending}
            onChange={(event) => setInlineValue(event.target.value)}
            onCommit={(value) => saveInlineValue("avgPlaytimeHours", value)}
            onCancel={cancelEditing}
            className="h-7 w-20 rounded-lg border border-border bg-surface px-2 text-center text-sm font-bold tabular-nums text-fg outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/25 disabled:cursor-wait disabled:opacity-70"
          />
        ) : game.avgPlaytimeHours ? (
          <span className="tabular-nums">{formatAvgPlaytimeHours(game.avgPlaytimeHours)}</span>
        ) : (
          <span className="text-muted-fg">—</span>
        )}
        externalAction={hltbUrl?.trim() ? (
          <IconButton
            size="mini"
            label={t("detail.openHltb")}
            tone="open"
            icon={<Info />}
            onClick={() => window.open(hltbUrl, "_blank", "noopener,noreferrer")}
          />
        ) : undefined}
      />
      <MetadataRow
        label={t("sort.metaScore")}
        icon={<Gauge size={13} className="text-emerald-400" />}
        editAction={editAction("metaScore", t("sort.metaScore"))}
        onValueDoubleClick={editingField !== "metaScore" && !saveMetadata.isPending
          ? () => startEditing("metaScore")
          : undefined}
        value={editingField === "metaScore" ? (
          <InlineNumberInput
            autoFocus
            required
            min="0"
            max="100"
            step="1"
            aria-label={`${t("action.edit")} ${t("sort.metaScore")}`}
            value={inlineValue}
            disabled={saveMetadata.isPending}
            onChange={(event) => setInlineValue(event.target.value)}
            onCommit={(value) => saveInlineValue("metaScore", value)}
            onCancel={cancelEditing}
            className="h-7 w-[50px] rounded-lg border border-border bg-surface px-1.5 text-center text-sm font-black tabular-nums text-fg outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/25 disabled:cursor-wait disabled:opacity-70"
          />
        ) : (
          <MetaChip value={game.metaScore} band={game.metaScoreBand} className="w-full max-w-[50px]" />
        )}
        externalAction={metacriticUrl?.trim() ? (
          <IconButton
            size="mini"
            label={t("detail.openMetacritic")}
            tone="open"
            icon={<Info />}
            onClick={() => window.open(metacriticUrl, "_blank", "noopener,noreferrer")}
          />
        ) : undefined}
      />
    </>
  );
}
