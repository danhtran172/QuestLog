import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, Loader2, Pencil, Save, X } from "lucide-react";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import type { GameDetail } from "@/lib/api/domain";
import { gameDetailApi } from "@/lib/gameDetailApi";
import { useI18n } from "@/lib/i18n";
import { OwnershipInfoDialog, type OwnershipInfoValues } from "./OwnershipInfoDialog";
import { DialogFooter, IconButton } from "./ui";

function valuesOf(game?: GameDetail): OwnershipInfoValues {
  const info = game?.ownershipInfo;
  return {
    steamAccount: info?.steamAccount ?? "",
    steamPassword: info?.steamPassword ?? "",
    ubisoftAccount: info?.ubisoftAccount ?? "",
    ubisoftPassword: info?.ubisoftPassword ?? "",
    supportDocumentUrl: info?.supportDocumentUrl ?? "",
    downloadUrl: info?.downloadUrl ?? "",
    note: info?.note ?? "",
  };
}

/**
 * Thong tin so huu o THE CHI TIET: mac dinh chi xem + copy; nut but o goc tren
 * ben phai chuyen sang sua tai cho, Luu ghi ngay (`PUT /api/games/{id}`) ma
 * khong phai mo ca form Edit.
 */
export function GameDetailOwnershipDialog({
  game,
  open,
  onOpenChange,
  onSaved,
}: {
  game?: GameDetail;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: (game: GameDetail) => void;
}) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState<OwnershipInfoValues>(() => valuesOf(game));
  const saved = valuesOf(game);

  /* Moi lan mo lai la xem, khong giu nua chung ban nhap cu. */
  useEffect(() => {
    if (open) return;
    setEditing(false);
  }, [open]);

  const save = useMutation({
    mutationFn: (values: OwnershipInfoValues) => {
      const latest = queryClient.getQueryData<GameDetail>(["game-detail", game!.id]) ?? game!;
      const text = (value: string) => value.trim() || null;
      return gameDetailApi.update(latest.id, {
        ...latest,
        ownershipInfo: {
          steamAccount: text(values.steamAccount),
          steamPassword: text(values.steamPassword),
          ubisoftAccount: text(values.ubisoftAccount),
          ubisoftPassword: text(values.ubisoftPassword),
          supportDocumentUrl: text(values.supportDocumentUrl),
          downloadUrl: text(values.downloadUrl),
          note: text(values.note),
        },
      });
    },
    onSuccess: async (result) => {
      setEditing(false);
      queryClient.setQueryData(["game-detail", result.id], result);
      await queryClient.invalidateQueries({ queryKey: ["games"] });
      onSaved(result);
    },
  });

  const startEditing = () => {
    setDraft(saved);
    save.reset();
    setEditing(true);
  };

  const cancelEditing = () => {
    setEditing(false);
    save.reset();
  };

  const copy = async (value: string) => {
    try {
      await navigator.clipboard.writeText(value);
      toast.success(t("detail.copied"));
    } catch {
      toast.error(t("detail.copyFailed"));
    }
  };

  const dirty = (Object.keys(draft) as Array<keyof OwnershipInfoValues>)
    .some((key) => draft[key].trim() !== saved[key].trim());

  return (
    <OwnershipInfoDialog
      open={open}
      onOpenChange={(next) => {
        /* Dang sua ma con thay doi: Esc / bam ra ngoai chi thoat che do sua,
           khong dong hop va vut ban nhap. */
        if (!next && editing && dirty) return cancelEditing();
        onOpenChange(next);
      }}
      layout={game?.ownership?.infoLayout}
      publisher={game?.publisher}
      values={editing ? draft : saved}
      onChange={editing ? (patch) => setDraft((current) => ({ ...current, ...patch })) : undefined}
      onCopy={editing ? undefined : (value) => void copy(value)}
      headerAction={editing ? undefined : (
        <IconButton
          size="compact"
          tone="edit"
          label={`${t("action.edit")} ${t("detail.ownershipInfo")}`}
          icon={<Pencil />}
          disabled={!game}
          onClick={startEditing}
        />
      )}
      footer={editing ? (
        <DialogFooter className="flex-wrap">
          {save.error ? (
            <p role="alert" className="mr-auto flex min-w-0 items-center gap-1.5 text-xs text-danger">
              <AlertTriangle size={13} className="shrink-0" />
              <span className="break-words">{save.error instanceof Error ? save.error.message : String(save.error)}</span>
            </p>
          ) : null}
          <IconButton
            label={t("action.cancel")}
            icon={<X />}
            disabled={save.isPending}
            onClick={cancelEditing}
          />
          <IconButton
            tone="add"
            label={t("action.save")}
            icon={save.isPending ? <Loader2 className="animate-spin" /> : <Save />}
            disabled={save.isPending || !dirty}
            onClick={() => save.mutate(draft)}
          />
        </DialogFooter>
      ) : undefined}
    />
  );
}
