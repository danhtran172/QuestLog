/**
 * Section Series cua Admin Console — spec 2026-09-14 §4.1, §4.3.
 *
 * Day la taxonomy metadata DUY NHAT co duong ghi. `metadata_genres` /
 * `metadata_themes` thuoc tien trinh sync nen khong co section tuong ung (§4.4).
 *
 * Xoa series con game dang gan bi backend chan (`SERIES_IN_USE`) — khong tu go
 * series khoi game. Loi do duoc bat tai cho va doi thanh goi y MERGE, vi merge
 * moi la thao tac dung: no chuyen lien ket sang series khac trong mot transaction.
 */
import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, Loader2, Merge, Pencil, Plus, Save, Trash2, X } from "lucide-react";
import { toast } from "sonner";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi, type AdminMetadataSeries } from "@/lib/adminApi";
import { useI18n } from "@/lib/i18n";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  IconButton,
} from "./ui";
import { ConfirmDialog } from "./ConfirmDialog";
import { editorInputClass } from "./GameEditorControls";

const SERIES_KEY = ["admin-metadata-series"];

function errorText(failure: unknown) {
  return failure instanceof ApiRequestError || failure instanceof Error
    ? failure.message
    : String(failure);
}

/** Tao moi (`series` = null) hoac doi ten. */
function SeriesNameDialog({
  open,
  series,
  onOpenChange,
}: {
  open: boolean;
  series: AdminMetadataSeries | null;
  onOpenChange: (open: boolean) => void;
}) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setName(series?.name ?? "");
    setError(null);
  }, [open, series]);

  const save = useMutation({
    mutationFn: () =>
      series ? adminApi.updateSeries(series.id, name.trim()) : adminApi.createSeries(name.trim()),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: SERIES_KEY });
      toast.success(series ? t("admin.series.saved") : t("admin.series.created"));
      onOpenChange(false);
    },
    onError: (failure) => setError(errorText(failure)),
  });

  return (
    <Dialog open={open} onOpenChange={(next) => (save.isPending ? null : onOpenChange(next))}>
      <DialogContent className="max-w-sm gap-0 overflow-hidden p-0">
        <DialogHeader className="border-b border-border px-5 py-4">
          <DialogTitle>{series ? t("admin.series.editTitle") : t("admin.series.createTitle")}</DialogTitle>
          <DialogDescription>{t("admin.series.sharedWarning")}</DialogDescription>
        </DialogHeader>
        <div className="space-y-3 p-5">
          {error ? (
            <p className="flex gap-2 rounded-xl border border-danger/40 bg-danger/10 p-3 text-xs text-danger">
              <AlertTriangle size={15} className="shrink-0" />
              {error}
            </p>
          ) : null}
          <input
            autoFocus
            className={editorInputClass}
            value={name}
            placeholder={t("admin.series.namePlaceholder")}
            onChange={(event) => setName(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === "Enter" && name.trim() && !save.isPending) save.mutate();
            }}
          />
        </div>
        <DialogFooter>
          <IconButton
            label={t("action.cancel")}
            icon={<X />}
            disabled={save.isPending}
            onClick={() => onOpenChange(false)}
          />
          <IconButton
            tone="add"
            label={t("action.save")}
            icon={save.isPending ? <Loader2 className="animate-spin" /> : <Save />}
            disabled={!name.trim() || save.isPending}
            onClick={() => save.mutate()}
          />
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function SeriesMergeDialog({
  source,
  options,
  onOpenChange,
}: {
  source: AdminMetadataSeries | null;
  options: AdminMetadataSeries[];
  onOpenChange: (open: boolean) => void;
}) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const [targetId, setTargetId] = useState<string>("");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setTargetId("");
    setError(null);
  }, [source]);

  const merge = useMutation({
    mutationFn: () => adminApi.mergeSeries(source!.id, Number(targetId)),
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: SERIES_KEY });
      await queryClient.invalidateQueries({ queryKey: ["admin-metadata-games"] });
      toast.success(
        t("admin.series.merged")
          .replace("{count}", String(result.movedGames))
          .replace("{target}", result.target.name),
      );
      onOpenChange(false);
    },
    onError: (failure) => setError(errorText(failure)),
  });

  const targets = options.filter((item) => item.id !== source?.id);

  return (
    <Dialog open={source != null} onOpenChange={(next) => (merge.isPending ? null : onOpenChange(next))}>
      <DialogContent className="max-w-sm gap-0 overflow-hidden p-0">
        <DialogHeader className="border-b border-border px-5 py-4">
          <DialogTitle>{t("admin.series.mergeTitle")}</DialogTitle>
          <DialogDescription>
            {t("admin.series.mergeDescription").replace("{name}", source?.name ?? "")}
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-3 p-5">
          {error ? (
            <p className="flex gap-2 rounded-xl border border-danger/40 bg-danger/10 p-3 text-xs text-danger">
              <AlertTriangle size={15} className="shrink-0" />
              {error}
            </p>
          ) : null}
          <select
            className={editorInputClass}
            value={targetId}
            onChange={(event) => setTargetId(event.target.value)}
          >
            <option value="">{t("admin.series.mergePick")}</option>
            {targets.map((item) => (
              <option key={item.id} value={item.id}>{item.name}</option>
            ))}
          </select>
          <p className="text-fs-11 leading-5 text-muted-fg">{t("admin.series.mergeHint")}</p>
        </div>
        <DialogFooter>
          <IconButton
            label={t("action.cancel")}
            icon={<X />}
            disabled={merge.isPending}
            onClick={() => onOpenChange(false)}
          />
          <IconButton
            tone="danger"
            label={t("admin.series.mergeAction")}
            icon={merge.isPending ? <Loader2 className="animate-spin" /> : <Merge />}
            disabled={!targetId || merge.isPending}
            onClick={() => merge.mutate()}
          />
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export function AdminSeriesSection({
  query,
  createSignal,
  onCreateHandled,
}: {
  query: string;
  /** Tang len moi lan bam `+` o header. */
  createSignal: number;
  onCreateHandled: () => void;
}) {
  const { t } = useI18n();
  const queryClient = useQueryClient();

  const [editing, setEditing] = useState<AdminMetadataSeries | null | undefined>(undefined);
  const [merging, setMerging] = useState<AdminMetadataSeries | null>(null);
  const [pendingDelete, setPendingDelete] = useState<AdminMetadataSeries | null>(null);

  useEffect(() => {
    if (createSignal > 0) {
      setEditing(null);
      onCreateHandled();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [createSignal]);

  const series = useQuery({
    queryKey: SERIES_KEY,
    queryFn: () => adminApi.listSeries({ limit: 200 }),
  });

  const remove = useMutation({
    mutationFn: (item: AdminMetadataSeries) => adminApi.deleteSeries(item.id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: SERIES_KEY });
      toast.success(t("admin.series.deleted"));
    },
    onError: (failure) => {
      // Backend chan xoa series con game dang gan. Doi thanh goi y merge — do moi
      // la thao tac dung, thay vi bat admin di go tay tung game.
      if (failure instanceof ApiRequestError && failure.code === "SERIES_IN_USE") {
        toast.error(t("admin.series.inUse"));
        return;
      }
      toast.error(errorText(failure));
    },
  });

  if (series.isPending) {
    return (
      <p className="flex items-center justify-center gap-2 py-16 text-sm text-muted-fg">
        <Loader2 size={16} className="animate-spin" /> {t("admin.series.loading")}
      </p>
    );
  }

  if (series.isError) {
    return (
      <div className="mx-auto mt-10 flex max-w-lg flex-col items-center gap-3 rounded-xl border border-danger/40 bg-danger/10 p-6 text-center">
        <AlertTriangle size={22} className="text-danger" />
        <p className="text-sm text-fg">{errorText(series.error)}</p>
        <button
          type="button"
          onClick={() => void series.refetch()}
          className="rounded-lg border border-border px-3 py-1.5 text-xs font-extrabold text-fg"
        >
          {t("error.retry")}
        </button>
      </div>
    );
  }

  const all = series.data;
  const needle = query.trim().toLowerCase();
  const items = needle ? all.filter((item) => item.name.toLowerCase().includes(needle)) : all;

  return (
    <>
      {items.length === 0 ? (
        <div className="flex flex-col items-center gap-3 py-16">
          <p className="text-sm text-muted-fg">
            {needle ? t("admin.series.noMatch") : t("admin.series.empty")}
          </p>
          {needle ? null : (
            <button
              type="button"
              onClick={() => setEditing(null)}
              className="flex items-center gap-1.5 rounded-lg border border-border px-3 py-1.5 text-xs font-extrabold text-fg transition-colors hover:bg-surface-2/60"
            >
              <Plus size={14} /> {t("admin.series.createTitle")}
            </button>
          )}
        </div>
      ) : (
        <div className="space-y-3">
          <div className="surface-card overflow-x-auto">
            <table className="w-full min-w-[520px] text-sm">
              <thead>
                <tr className="border-b border-border text-left text-fs-11 font-black uppercase tracking-[0.12em] text-muted-fg">
                  <th className="px-3 py-2">#</th>
                  <th className="px-3 py-2">{t("admin.series.name")}</th>
                  <th className="px-3 py-2" />
                </tr>
              </thead>
              <tbody>
                {items.map((item) => (
                  <tr key={item.id} className="border-b border-border/50 last:border-0 hover:bg-surface-2/40">
                    <td className="px-3 py-2 text-xs text-muted-fg">{item.id}</td>
                    <td className="px-3 py-2 font-bold text-fg">{item.name}</td>
                    <td className="px-3 py-2">
                      <div className="flex justify-end gap-1">
                        <IconButton
                          size="compact"
                          tone="edit"
                          label={t("action.edit")}
                          icon={<Pencil />}
                          onClick={() => setEditing(item)}
                        />
                        <IconButton
                          size="compact"
                          label={t("admin.series.mergeAction")}
                          icon={<Merge />}
                          disabled={all.length < 2}
                          onClick={() => setMerging(item)}
                        />
                        <IconButton
                          size="compact"
                          tone="danger"
                          label={t("action.delete")}
                          icon={remove.isPending ? <Loader2 className="animate-spin" /> : <Trash2 />}
                          disabled={remove.isPending}
                          onClick={() => setPendingDelete(item)}
                        />
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="text-xs text-muted-fg">
            {t("admin.series.count").replace("{count}", String(items.length))}
          </p>
        </div>
      )}

      <SeriesNameDialog
        open={editing !== undefined}
        series={editing ?? null}
        onOpenChange={(next) => { if (!next) setEditing(undefined); }}
      />
      <SeriesMergeDialog
        source={merging}
        options={all}
        onOpenChange={(next) => { if (!next) setMerging(null); }}
      />
      <ConfirmDialog
        open={pendingDelete !== null}
        onOpenChange={(next) => { if (!next) setPendingDelete(null); }}
        title={t("action.delete")}
        description={t("admin.series.deleteConfirm").replace("{name}", pendingDelete?.name ?? "")}
        confirmLabel={t("action.delete")}
        onConfirm={() => { if (pendingDelete) remove.mutate(pendingDelete); }}
      />
    </>
  );
}
