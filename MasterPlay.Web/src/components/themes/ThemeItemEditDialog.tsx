import { useEffect, useRef, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, ImagePlus, Loader2, Save, Trash2, X } from "lucide-react";
import type { TaxonomyItem } from "@/lib/api/domain";
import { taxonomyApi, type TaggableKind, type TaxonomyScope } from "@/lib/taxonomyApi";
import { useI18n } from "@/lib/i18n";
import { iconSrc } from "@/lib/utils";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogTitle, IconButton } from "../ui";
import { editorInputClass } from "../GameEditorControls";

const ICON_SIZE = 128;

/** Ve lai anh ve PNG 128x128 (giu ti le, can giua) — chi dinh dang HIEN THI, khong dong toi du lieu khac. */
async function toIconDataUrl(file: File): Promise<string> {
  const url = URL.createObjectURL(file);
  try {
    const image = await new Promise<HTMLImageElement>((resolve, reject) => {
      const element = new Image();
      element.onload = () => resolve(element);
      element.onerror = () => reject(new Error("decode"));
      element.src = url;
    });
    const canvas = document.createElement("canvas");
    canvas.width = canvas.height = ICON_SIZE;
    const scale = Math.min(ICON_SIZE / image.width, ICON_SIZE / image.height);
    const width = image.width * scale;
    const height = image.height * scale;
    canvas.getContext("2d")!.drawImage(image, (ICON_SIZE - width) / 2, (ICON_SIZE - height) / 2, width, height);
    return canvas.toDataURL("image/png");
  } finally {
    URL.revokeObjectURL(url);
  }
}

/**
 * Tao / sua / xoa mot theme (hoac genre o Admin Console). Chi admin dat duoc
 * icon — server cung bo qua iconData cua user thuong (spec §5.2).
 */
export function ThemeItemEditDialog({
  scope,
  kind = "themes",
  item,
  open,
  onOpenChange,
}: {
  scope: TaxonomyScope;
  kind?: TaggableKind;
  /** null = tao moi (chua phan nhom). */
  item: TaxonomyItem | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const fileRef = useRef<HTMLInputElement>(null);
  const [name, setName] = useState("");
  const [iconData, setIconData] = useState<string | null>(null);
  const [iconError, setIconError] = useState<string | null>(null);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const canSetIcon = scope === "admin";

  useEffect(() => {
    setName(item?.name ?? "");
    setIconData(item?.iconData ?? null);
    setIconError(null);
    setConfirmDelete(false);
  }, [item, open]);

  const done = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["taxonomy"] }),
      queryClient.invalidateQueries({ queryKey: ["game-detail"] }),
      queryClient.invalidateQueries({ queryKey: ["games"] }),
    ]);
    onOpenChange(false);
  };

  const save = useMutation({
    mutationFn: () =>
      item
        ? taxonomyApi.update(scope, kind, { ...item, name: name.trim(), iconData: canSetIcon ? iconData : null })
        : taxonomyApi.create(scope, kind, { name: name.trim(), iconData: canSetIcon ? iconData : null }),
    onSuccess: done,
  });
  const remove = useMutation({
    mutationFn: () => taxonomyApi.remove(scope, kind, item!.id),
    onSuccess: done,
  });
  const error = save.error ?? remove.error;
  const preview = iconSrc(iconData);
  const title = kind === "genres" ? t("tags.genres") : item ? t("themes.editTheme") : t("themes.addTheme");

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-sm" showClose={false}>
        <form
          className="space-y-4 p-5"
          onSubmit={(event) => {
            event.preventDefault();
            if (name.trim() && !save.isPending) save.mutate();
          }}
        >
          <div>
            <DialogTitle>{title}</DialogTitle>
            <DialogDescription>{confirmDelete ? t("themes.deleteThemeHint") : t("themes.themeName")}</DialogDescription>
          </div>
          {error || iconError ? (
            <p role="alert" className="flex gap-2 rounded-xl border border-danger/40 bg-danger/10 p-3 text-xs text-danger">
              <AlertTriangle size={15} className="shrink-0" />
              {iconError ?? (error instanceof Error ? error.message : String(error))}
            </p>
          ) : null}
          {confirmDelete ? (
            <p className="rounded-xl border border-danger/40 bg-danger/10 p-3 text-sm font-semibold">
              {t("themes.deleteTheme")}: {item?.name}
            </p>
          ) : (
            <div className="flex items-center gap-3">
              {canSetIcon ? (
                <button
                  type="button"
                  title={t("themes.setIcon")}
                  aria-label={t("themes.setIcon")}
                  className="grid h-12 w-12 shrink-0 place-items-center rounded-xl border border-dashed border-border hover:border-primary/70"
                  onClick={() => fileRef.current?.click()}
                >
                  {preview ? <img src={preview} alt="" className="h-9 w-9 object-contain" /> : <ImagePlus size={18} className="text-muted-fg" />}
                </button>
              ) : null}
              <input autoFocus className={editorInputClass} value={name} onChange={(e) => setName(e.target.value)} />
              {canSetIcon && preview ? (
                <IconButton size="compact" label={t("themes.clearIcon")} icon={<X />} onClick={() => setIconData(null)} />
              ) : null}
              <input
                ref={fileRef}
                type="file"
                accept="image/*"
                hidden
                onChange={async (event) => {
                  const file = event.target.files?.[0];
                  event.target.value = "";
                  if (!file) return;
                  if (!file.type.startsWith("image/")) {
                    setIconError(t("themes.iconInvalid"));
                    return;
                  }
                  try {
                    setIconData(await toIconDataUrl(file));
                    setIconError(null);
                  } catch {
                    setIconError(t("themes.iconInvalid"));
                  }
                }}
              />
            </div>
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
              label={t("themes.deleteTheme")}
              icon={remove.isPending ? <Loader2 className="animate-spin" /> : <Trash2 />}
              disabled={remove.isPending}
              onClick={() => remove.mutate()}
            />
          ) : (
            <>
              {item ? (
                <IconButton tone="danger" label={t("themes.deleteTheme")} icon={<Trash2 />} onClick={() => setConfirmDelete(true)} />
              ) : null}
              <IconButton
                tone="add"
                label={t("action.save")}
                icon={save.isPending ? <Loader2 className="animate-spin" /> : <Save />}
                disabled={save.isPending || !name.trim()}
                onClick={() => save.mutate()}
              />
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
