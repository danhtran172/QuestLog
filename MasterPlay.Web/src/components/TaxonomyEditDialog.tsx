import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, Loader2, Save, Trash2, X } from "lucide-react";
import type { TaxonomyItem } from "@/lib/api/domain";
import { api } from "@/lib/api/client";
import { useI18n } from "@/lib/i18n";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogTitle, IconButton } from "./ui";
import { editorInputClass } from "./GameEditorControls";

export type TaxonomyKind = "series" | "genres" | "themes";

export function TaxonomyEditDialog({
  kind,
  item,
  open,
  onOpenChange,
  onSaved,
  onDeleted,
}: {
  kind: TaxonomyKind;
  item: TaxonomyItem | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: (item: TaxonomyItem) => void;
  onDeleted: (id: number) => void;
}) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const [name, setName] = useState("");
  const [confirmDelete, setConfirmDelete] = useState(false);

  useEffect(() => {
    setName(item?.name ?? "");
    setConfirmDelete(false);
  }, [item, open]);

  const save = useMutation({
    mutationFn: () => api.updateTaxonomy(kind, { ...item!, name: name.trim() }),
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: ["taxonomy", kind] });
      onSaved(saved);
      onOpenChange(false);
    },
  });
  const remove = useMutation({
    mutationFn: () => api.deleteTaxonomy(kind, item!.id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["taxonomy", kind] });
      onDeleted(item!.id);
      onOpenChange(false);
    },
  });
  const error = save.error ?? remove.error;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-sm" showClose={false}>
        <div className="space-y-4 p-5">
          <div>
            <DialogTitle>{t("editor.taxonomyEdit")}</DialogTitle>
            <DialogDescription>{t(`editor.${kind}`)}</DialogDescription>
          </div>
          {error ? (
            <p className="flex gap-2 rounded-xl border border-danger/40 bg-danger/10 p-3 text-xs text-danger">
              <AlertTriangle size={15} className="shrink-0" />
              {error instanceof Error ? error.message : String(error)}
            </p>
          ) : null}
          {confirmDelete ? (
            <p className="rounded-xl border border-danger/40 bg-danger/10 p-3 text-sm font-semibold">
              {t("editor.taxonomyDeleteConfirm")}
            </p>
          ) : (
            <input autoFocus className={editorInputClass} value={name} onChange={(event) => setName(event.target.value)} />
          )}
        </div>
        <DialogFooter>
          <IconButton label={t("action.cancel")} icon={<X />} onClick={() => confirmDelete ? setConfirmDelete(false) : onOpenChange(false)} />
          {confirmDelete ? (
            <IconButton tone="danger" label={t("action.delete")} icon={remove.isPending ? <Loader2 className="animate-spin" /> : <Trash2 />} onClick={() => remove.mutate()} disabled={remove.isPending} />
          ) : (
            <>
              <IconButton tone="danger" label={t("action.delete")} icon={<Trash2 />} onClick={() => setConfirmDelete(true)} />
              <IconButton tone="add" label={t("action.save")} icon={save.isPending ? <Loader2 className="animate-spin" /> : <Save />} onClick={() => save.mutate()} disabled={save.isPending || !name.trim()} />
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
