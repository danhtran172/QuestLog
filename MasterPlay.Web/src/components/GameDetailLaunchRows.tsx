import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Cpu, FileSearch, Loader2, Rocket, ToggleRight } from "lucide-react";
import { useState, type ReactNode } from "react";
import { toast } from "sonner";
import type { GameDetail } from "@/lib/api/domain";
import { gameDetailApi } from "@/lib/gameDetailApi";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { InlineEditButton, InlineTextInput } from "./InlineEditControls";
import { IconButton, Switch, Tooltip } from "./ui";

type EditingField = "exeName" | "launchTarget";

const INLINE_TEXT_INPUT =
  "h-7 w-full min-w-0 rounded-lg border border-border bg-surface px-2 font-mono text-xs font-bold text-fg outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/25 disabled:cursor-wait disabled:opacity-70";

function launchTargetOf(path: string): GameDetail["launchTarget"] {
  const value = path.trim();
  if (!value) return null;
  return { kind: value.toLowerCase().endsWith(".lnk") ? "shortcut" : "exe", path: value };
}

function LaunchRow({
  label,
  icon,
  editAction,
  value,
  action,
  onValueDoubleClick,
}: {
  label: string;
  icon: ReactNode;
  editAction?: ReactNode;
  value: ReactNode;
  action?: ReactNode;
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
        className={cn("min-w-0 break-words font-semibold text-fg", onValueDoubleClick && "cursor-text select-none")}
        onDoubleClick={onValueDoubleClick}
      >
        {value}
      </dd>
      {/* Cot thu ba luon ton tai du khong co nut, khong thi luoi lech hang. */}
      <div className="-my-1 justify-self-end">{action}</div>
    </div>
  );
}

/**
 * Nhom KHOI CHAY cua the chi tiet, sua duoc tai cho nhu hang Genre/Theme:
 * nut but canh nhan hoac nhay dup vao gia tri, Enter/ra ngoai la luu, Esc la bo.
 *
 * - Ten tien trinh: luu dung chuoi nguoi dung go; luc HIEN THI moi them `.exe` neu thieu.
 * - File khoi chay rieng: cong tac o HANG RIENG. Bat thi mo hop chon file (neu
 *   may co file picker), khong thi cho go duong dan; tat thi bo file di.
 */
export function GameDetailLaunchRows({
  game,
  canPickFile,
  onSaved,
}: {
  game: GameDetail;
  canPickFile: boolean;
  onSaved: (game: GameDetail) => void;
}) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<EditingField | null>(null);
  const [value, setValue] = useState("");

  const save = useMutation({
    mutationFn: (patch: Partial<GameDetail>) => {
      const latest = queryClient.getQueryData<GameDetail>(["game-detail", game.id]) ?? game;
      return gameDetailApi.update(game.id, { ...latest, ...patch });
    },
    onSuccess: async (saved) => {
      setEditing(null);
      queryClient.setQueryData(["game-detail", saved.id], saved);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["games"] }),
        queryClient.invalidateQueries({ queryKey: ["play-queue"] }),
      ]);
      onSaved(saved);
    },
    onError: (error) => toast.error(error instanceof Error ? error.message : String(error)),
  });

  /* Hop chon file cua Host: huy chon thi khong doi gi ca. */
  const pickFile = useMutation({
    mutationFn: () => gameDetailApi.pickFile("exe-or-shortcut"),
    onSuccess: (result) => {
      const target = launchTargetOf(result.path ?? "");
      if (target) save.mutate({ launchTarget: target });
    },
    onError: (error) => toast.error(error instanceof Error ? error.message : String(error)),
  });

  const busy = save.isPending || pickFile.isPending;
  const exeName = game.exeName?.trim() ?? "";
  const processName = exeName
    ? exeName.toLowerCase().endsWith(".exe") ? exeName : `${exeName}.exe`
    : null;
  const launchPath = game.launchTarget?.path?.trim() ?? "";

  const startEditing = (field: EditingField) => {
    if (busy) return;
    setEditing(field);
    setValue(field === "exeName" ? exeName : launchPath);
  };

  const commit = (field: EditingField, raw: string) => {
    const next = raw.trim();
    if (field === "exeName") {
      if (next === exeName) return setEditing(null);
      save.mutate({ exeName: next || null });
    } else {
      if (next === launchPath) return setEditing(null);
      save.mutate({ launchTarget: launchTargetOf(next) });
    }
  };

  const editAction = (field: EditingField, label: string) => editing === field ? null : (
    <InlineEditButton label={`${t("action.edit")} ${label}`} disabled={busy} onClick={() => startEditing(field)} />
  );

  const customLaunchOn = Boolean(game.launchTarget) || editing === "launchTarget";

  return (
    <>
      <LaunchRow
        label={t("detail.process")}
        icon={<Cpu size={13} className="text-field-process" />}
        editAction={editAction("exeName", t("detail.process"))}
        onValueDoubleClick={editing === "exeName" ? undefined : () => startEditing("exeName")}
        value={editing === "exeName" ? (
          <InlineTextInput
            autoFocus
            aria-label={`${t("action.edit")} ${t("detail.process")}`}
            spellCheck={false}
            value={value}
            disabled={save.isPending}
            onChange={(event) => setValue(event.target.value)}
            onCommit={(next) => commit("exeName", next)}
            onCancel={() => setEditing(null)}
            className={INLINE_TEXT_INPUT}
          />
        ) : processName ? (
          <span className="break-all font-mono">{processName}</span>
        ) : <span className="text-muted-fg">—</span>}
      />

      <LaunchRow
        label={t("editor.hasCustomLaunch")}
        icon={<ToggleRight size={13} className="text-field-custom-launch" />}
        value={(
          <Switch
            checked={customLaunchOn}
            aria-label={t("editor.hasCustomLaunch")}
            disabled={busy}
            onCheckedChange={(enabled) => {
              if (!enabled) {
                setEditing(null);
                if (game.launchTarget) save.mutate({ launchTarget: null });
              } else if (canPickFile) {
                pickFile.mutate();
              } else {
                startEditing("launchTarget");
              }
            }}
          />
        )}
        action={busy ? <Loader2 size={14} className="animate-spin text-muted-fg" /> : undefined}
      />

      {customLaunchOn ? (
        <LaunchRow
          label={t("detail.launchTarget")}
          icon={<Rocket size={13} className="text-field-launch-target" />}
          editAction={editAction("launchTarget", t("detail.launchTarget"))}
          onValueDoubleClick={editing === "launchTarget" ? undefined : () => startEditing("launchTarget")}
          value={editing === "launchTarget" ? (
            <InlineTextInput
              autoFocus
              aria-label={`${t("action.edit")} ${t("detail.launchTarget")}`}
              spellCheck={false}
              value={value}
              disabled={save.isPending}
              onChange={(event) => setValue(event.target.value)}
              onCommit={(next) => commit("launchTarget", next)}
              onCancel={() => setEditing(null)}
              className={INLINE_TEXT_INPUT}
            />
          ) : launchPath ? (
            <Tooltip label={launchPath} className="max-w-80 break-all font-mono font-medium">
              <span className="break-all font-mono">{launchPath}</span>
            </Tooltip>
          ) : <span className="text-muted-fg">—</span>}
          action={canPickFile ? (
            <IconButton
              size="mini"
              label={t("editor.pickLaunchTarget")}
              tone="open"
              icon={pickFile.isPending ? <Loader2 className="animate-spin" /> : <FileSearch />}
              disabled={busy}
              onClick={() => pickFile.mutate()}
            />
          ) : undefined}
        />
      ) : null}
    </>
  );
}
