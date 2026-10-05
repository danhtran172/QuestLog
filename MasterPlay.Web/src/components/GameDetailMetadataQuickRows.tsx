import { Gauge } from "lucide-react";
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
import { InfoButton } from "./ui";

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
    mutationFn: ({ field, value }: { field: EditableMetadataField; value: number | null }) => {
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

  /* De trong = xoa gia tri (luu null). Khong doi gi thi khong goi API. */
  const saveInlineValue = (field: EditableMetadataField, rawValue: string) => {
    let value: number | null = null;
    if (rawValue.trim()) {
      value = Number(rawValue);
      if (!Number.isFinite(value)) return;
    }
    if (value === (game[field] ?? null)) {
      setEditingField(null);
      return;
    }
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
          <InfoButton
            size="mini"
            label={t("detail.openHltb")}
            tone="open"
            onClick={() => window.open(hltbUrl, "_blank", "noopener,noreferrer")}
          />
        ) : undefined}
      />
      <MetadataRow
        label={t("sort.metaScore")}
        icon={<Gauge size={13} className="text-field-metascore" />}
        editAction={editAction("metaScore", t("sort.metaScore"))}
        onValueDoubleClick={editingField !== "metaScore" && !saveMetadata.isPending
          ? () => startEditing("metaScore")
          : undefined}
        value={editingField === "metaScore" ? (
          <InlineNumberInput
            autoFocus
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
        /* Luon hien nut. Backend tra "" khi game thieu slug RAWG -> disable va
           noi ro ly do o tooltip. `undefined` = link chua tai xong. */
        externalAction={(
          <InfoButton
            size="mini"
            label={t(metacriticUrl === "" ? "detail.metacriticNoSlug" : "detail.openMetacritic")}
            tone="open"
            disabled={!metacriticUrl?.trim()}
            onClick={() => metacriticUrl && window.open(metacriticUrl, "_blank", "noopener,noreferrer")}
          />
        )}
      />
    </>
  );
}
