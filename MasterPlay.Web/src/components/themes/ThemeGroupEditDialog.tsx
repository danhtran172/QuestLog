import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, Loader2, Save, Trash2, X } from "lucide-react";
import type { ThemeGroup } from "@/lib/api/domain";
import { taxonomyApi, type TaxonomyScope } from "@/lib/taxonomyApi";
import { useI18n } from "@/lib/i18n";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogTitle, IconButton } from "../ui";
import { editorInputClass } from "../GameEditorControls";

/**
 * Tao / sua / xoa nhom theme. Admin (nhom chung) nhap du hai ten EN + VI; user
 * (nhom ca nhan) nhap mot ten — server dien ten con lai (spec §4.2 I6).
 */
export function ThemeGroupEditDialog({
  scope,
  group,
  open,
  onOpenChange,
}: {
  scope: TaxonomyScope;
  /** null = tao moi. */
  group: ThemeGroup | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const bilingual = scope === "admin";
  const [nameEn, setNameEn] = useState("");
  const [nameVi, setNameVi] = useState("");
  const [name, setName] = useState("");
  const [confirmDelete, setConfirmDelete] = useState(false);

  useEffect(() => {
    setNameEn(group?.nameEn ?? "");
    setNameVi(group?.nameVi ?? "");
    setName(group?.displayName ?? "");
    setConfirmDelete(false);
  }, [group, open]);

  const done = async () => {
    await queryClient.invalidateQueries({ queryKey: ["taxonomy"] });
    onOpenChange(false);
  };

  const body = bilingual
    ? { nameEn: nameEn.trim(), nameVi: nameVi.trim() }
    : { nameEn: name.trim(), nameVi: name.trim() };
  const valid = bilingual ? Boolean(body.nameEn && body.nameVi) : Boolean(body.nameEn);

  const save = useMutation({
    mutationFn: () => (group ? taxonomyApi.updateGroup(scope, group.id, body) : taxonomyApi.createGroup(scope, body)),
    onSuccess: done,
  });
  const remove = useMutation({
    mutationFn: () => taxonomyApi.removeGroup(scope, group!.id),
    onSuccess: done,
  });
  const error = save.error ?? remove.error;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-sm" showClose={false}>
        <form
          className="space-y-4 p-5"
          onSubmit={(event) => {
            event.preventDefault();
            if (valid && !save.isPending) save.mutate();
          }}
        >
          <div>
            <DialogTitle>{group ? t("themes.editGroup") : t("themes.addGroup")}</DialogTitle>
            <DialogDescription>{confirmDelete ? t("themes.deleteGroupHint") : t("tags.themes")}</DialogDescription>
          </div>
          {error ? (
            <p role="alert" className="flex gap-2 rounded-xl border border-danger/40 bg-danger/10 p-3 text-xs text-danger">
              <AlertTriangle size={15} className="shrink-0" />
              {error instanceof Error ? error.message : String(error)}
            </p>
          ) : null}
          {confirmDelete ? (
            <p className="rounded-xl border border-danger/40 bg-danger/10 p-3 text-sm font-semibold">
              {t("themes.deleteGroup")}: {group?.displayName}
            </p>
          ) : bilingual ? (
            <>
              <label className="block space-y-1 text-xs font-semibold text-muted-fg">
                {t("themes.groupNameEn")}
                <input autoFocus className={editorInputClass} value={nameEn} onChange={(e) => setNameEn(e.target.value)} />
              </label>
              <label className="block space-y-1 text-xs font-semibold text-muted-fg">
                {t("themes.groupNameVi")}
                <input className={editorInputClass} value={nameVi} onChange={(e) => setNameVi(e.target.value)} />
              </label>
            </>
          ) : (
            <label className="block space-y-1 text-xs font-semibold text-muted-fg">
              {t("themes.groupName")}
              <input autoFocus className={editorInputClass} value={name} onChange={(e) => setName(e.target.value)} />
            </label>
          )}
          <button type="submit" hidden />
        </form>
        <DialogFooter>
          <IconButton
            label={t("action.cancel")}
            icon={<X />}
            onClick={() => (confirmDelete ? setConfirmDelete(false) : onOpenChange(false))}
          />
          {confirmDelete ? (
            <IconButton
              tone="danger"
              label={t("themes.deleteGroup")}
              icon={remove.isPending ? <Loader2 className="animate-spin" /> : <Trash2 />}
              disabled={remove.isPending}
              onClick={() => remove.mutate()}
            />
          ) : (
            <>
              {group ? (
                <IconButton tone="danger" label={t("themes.deleteGroup")} icon={<Trash2 />} onClick={() => setConfirmDelete(true)} />
              ) : null}
              <IconButton
                tone="add"
                label={t("action.save")}
                icon={save.isPending ? <Loader2 className="animate-spin" /> : <Save />}
                disabled={save.isPending || !valid}
                onClick={() => save.mutate()}
              />
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
