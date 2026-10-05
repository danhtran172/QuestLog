/**
 * Editor ban ghi metadata dung chung — spec 2026-09-14 §12.3, §12.7.
 *
 * Khac han GameEditorForm cua thu vien: o day KHONG co score, excited, progress,
 * ownership, playtime, finish, launcher hay folder. Ban ghi metadata khong co
 * userdata va khong co device data.
 *
 * Genre va theme KHONG xuat hien: chung thuoc tien trinh sync (§4.4).
 *
 * Hai tinh huong loi duoc xu ly tai cho thay vi chi hien toast, vi bao "that bai"
 * thoi thi nguoi dung khong hanh dong duoc:
 *   - `GAME_NAME_SIMILAR`: liet ke ban ghi dang trung, cho xac nhan tao tiep.
 *   - `METADATA_REVISION_CONFLICT`: liet ke dung nhung field da lech, cho nap ban moi.
 */
import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, Loader2, Save, X } from "lucide-react";
import { toast } from "sonner";
import { api, ApiRequestError } from "@/lib/api/client";
import {
  adminApi,
  emptyMetadataGame,
  toWriteModel,
  type AdminGameDuplicate,
  type AdminMetadataGame,
  type AdminMetadataGameWrite,
} from "@/lib/adminApi";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  IconButton,
  Switch,
} from "./ui";
import { editorInputClass } from "./GameEditorControls";

/** Field so: o trong nghia la null, khong phai 0. */
function numberOrNull(value: string): number | null {
  const trimmed = value.trim();
  if (trimmed === "") return null;
  const parsed = Number(trimmed);
  return Number.isFinite(parsed) ? parsed : null;
}

function Row({ label, hint, children }: { label: string; hint?: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1 block text-xs font-bold uppercase tracking-[0.1em] text-muted-fg">{label}</span>
      {children}
      {hint ? <span className="mt-1 block text-fs-11 text-muted-fg/80">{hint}</span> : null}
    </label>
  );
}

/** Nhung field dang lech giua ban dang sua va ban tren server. */
function diffFields(mine: AdminMetadataGameWrite, latest: AdminMetadataGame): string[] {
  const theirs = toWriteModel(latest);
  return (Object.keys(theirs) as (keyof AdminMetadataGameWrite)[])
    .filter((key) => key !== "revision")
    .filter((key) => (mine[key] ?? null) !== (theirs[key] ?? null))
    .map(String);
}

export function AdminGameEditorDialog({
  open,
  game,
  onOpenChange,
}: {
  open: boolean;
  /** `null` la tao moi. */
  game: AdminMetadataGame | null;
  onOpenChange: (open: boolean) => void;
}) {
  const { t } = useI18n();
  const queryClient = useQueryClient();

  const [form, setForm] = useState<AdminMetadataGameWrite>(emptyMetadataGame);
  const [duplicates, setDuplicates] = useState<AdminGameDuplicate[] | null>(null);
  const [conflict, setConflict] = useState<AdminMetadataGame | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setForm(game ? toWriteModel(game) : emptyMetadataGame());
    setDuplicates(null);
    setConflict(null);
    setError(null);
  }, [game, open]);

  const series = useQuery({
    queryKey: ["admin-metadata-series"],
    queryFn: () => adminApi.listSeries({ limit: 200 }),
    enabled: open,
  });

  const patch = (next: Partial<AdminMetadataGameWrite>) => setForm((current) => ({ ...current, ...next }));

  const save = useMutation({
    mutationFn: (input: AdminMetadataGameWrite) =>
      game ? adminApi.updateGame(game.id, input) : adminApi.createGame(input),
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: ["admin-metadata-games"] });
      toast.success(game ? t("admin.game.saved") : t("admin.game.created"));
      onOpenChange(false);

      /* Nguoi vua sua kho thuong cung co game do trong thu vien cua chinh ho.
         `force` de bo qua cua chan 10 phut: khong co no thi ho phai doi gan het
         mot tuan tra moi thay ket qua cua chinh minh (DOMAIN_RULES §20).
         Chay nen — day la hieu ung phu, khong duoc lam hong buoc luu vua xong. */
      if (game) {
        void api
          .refreshLibraryMetadata({ force: true })
          .then(async (report) => {
            if (report.skipped || report.updated === 0) return;
            await queryClient.invalidateQueries({ queryKey: ["games"] });
            await queryClient.invalidateQueries({ queryKey: ["gameDetail"] });
          })
          .catch((failure) => {
            toast.error(
              t("library.metadataRefresh.failed").replace(
                "{message}",
                failure instanceof Error ? failure.message : String(failure),
              ),
            );
          });
      }
      return saved;
    },
    onError: (failure) => {
      setDuplicates(null);
      setConflict(null);
      if (!(failure instanceof ApiRequestError)) {
        setError(failure instanceof Error ? failure.message : String(failure));
        return;
      }
      if (failure.code === "GAME_NAME_SIMILAR") {
        setDuplicates((failure.details as AdminGameDuplicate[] | undefined) ?? []);
        setError(null);
        return;
      }
      if (failure.code === "METADATA_REVISION_CONFLICT") {
        setConflict((failure.details as AdminMetadataGame | undefined) ?? null);
        setError(null);
        return;
      }
      setError(failure.message);
    },
  });

  const conflictDiff = useMemo(
    () => (conflict ? diffFields(form, conflict) : []),
    [conflict, form],
  );

  const busy = save.isPending;
  const canSave = form.name.trim().length > 0 && !busy;

  return (
    <Dialog open={open} onOpenChange={(next) => (busy ? null : onOpenChange(next))}>
      <DialogContent className="max-w-3xl gap-0 overflow-hidden p-0">
        <DialogHeader className="border-b border-border px-5 py-4">
          <DialogTitle>{game ? t("admin.game.editTitle") : t("admin.game.createTitle")}</DialogTitle>
          <DialogDescription>{t("admin.game.sharedWarning")}</DialogDescription>
        </DialogHeader>

        <div className="max-h-[65vh] space-y-4 overflow-y-auto p-5">
          {error ? (
            <p className="flex gap-2 rounded-xl border border-danger/40 bg-danger/10 p-3 text-xs text-danger">
              <AlertTriangle size={15} className="shrink-0" />
              {error}
            </p>
          ) : null}

          {/* Trung ten la CANH BAO chu khong phai chan cung — spec §12.7.4. */}
          {duplicates ? (
            <div className="rounded-xl border border-warning/45 bg-warning/10 p-3">
              <p className="flex gap-2 text-xs font-bold text-fg">
                <AlertTriangle size={15} className="shrink-0 text-warning" />
                {t("admin.game.duplicateTitle")}
              </p>
              <ul className="mt-2 space-y-1 text-xs text-muted-fg">
                {duplicates.map((item) => (
                  <li key={item.id}>
                    #{item.id} — {item.name}
                    {item.publishDate ? ` (${item.publishDate})` : ""}
                  </li>
                ))}
              </ul>
              <p className="mt-2 text-fs-11 leading-5 text-muted-fg">{t("admin.game.duplicateHint")}</p>
              <button
                type="button"
                disabled={busy}
                onClick={() => save.mutate({ ...form, confirmDuplicate: true })}
                className="mt-2 rounded-lg border border-warning/50 px-3 py-1.5 text-xs font-extrabold text-fg transition-colors hover:bg-warning/15"
              >
                {t("admin.game.duplicateConfirm")}
              </button>
            </div>
          ) : null}

          {/* Xung dot revision: hien DUNG nhung field da lech, khong bat doc lai ca ban ghi. */}
          {conflict ? (
            <div className="rounded-xl border border-danger/45 bg-danger/10 p-3">
              <p className="flex gap-2 text-xs font-bold text-fg">
                <AlertTriangle size={15} className="shrink-0 text-danger" />
                {t("admin.game.conflictTitle")}
              </p>
              <p className="mt-1 text-fs-11 leading-5 text-muted-fg">
                {t("admin.game.conflictHint").replace("{revision}", String(conflict.revision))}
              </p>
              {conflictDiff.length > 0 ? (
                <ul className="mt-2 space-y-0.5 text-xs text-muted-fg">
                  {conflictDiff.map((field) => (
                    <li key={field}>• {field}</li>
                  ))}
                </ul>
              ) : null}
              <button
                type="button"
                disabled={busy}
                onClick={() => {
                  setForm(toWriteModel(conflict));
                  setConflict(null);
                }}
                className="mt-2 rounded-lg border border-danger/50 px-3 py-1.5 text-xs font-extrabold text-fg transition-colors hover:bg-danger/15"
              >
                {t("admin.game.conflictReload")}
              </button>
            </div>
          ) : null}

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="sm:col-span-2">
              <Row label={t("admin.game.name")}>
                <input
                  autoFocus
                  className={editorInputClass}
                  value={form.name}
                  onChange={(event) => patch({ name: event.target.value })}
                />
              </Row>
            </div>

            <Row label={t("admin.game.developer")}>
              <input
                className={editorInputClass}
                value={form.developer ?? ""}
                onChange={(event) => patch({ developer: event.target.value || null })}
              />
            </Row>
            <Row label={t("admin.game.publisher")}>
              <input
                className={editorInputClass}
                value={form.publisher ?? ""}
                onChange={(event) => patch({ publisher: event.target.value || null })}
              />
            </Row>

            <Row label={t("admin.game.publishDate")} hint="YYYY-MM-DD">
              <input
                className={editorInputClass}
                placeholder="2004-11-16"
                value={form.publishDate ?? ""}
                onChange={(event) => patch({ publishDate: event.target.value || null })}
              />
            </Row>
            <Row label={t("admin.game.series")}>
              <select
                className={editorInputClass}
                value={form.seriesId ?? ""}
                onChange={(event) => patch({ seriesId: numberOrNull(event.target.value) })}
              >
                <option value="">{t("admin.game.noSeries")}</option>
                {(series.data ?? []).map((item) => (
                  <option key={item.id} value={item.id}>{item.name}</option>
                ))}
              </select>
            </Row>

            <Row label={t("admin.game.metaScore")} hint="0-100">
              <input
                className={editorInputClass}
                inputMode="numeric"
                value={form.metaScore ?? ""}
                onChange={(event) => patch({ metaScore: numberOrNull(event.target.value) })}
              />
            </Row>
            <Row label={t("admin.game.avgPlaytime")}>
              <input
                className={editorInputClass}
                inputMode="numeric"
                value={form.avgPlaytimeHours ?? ""}
                onChange={(event) => patch({ avgPlaytimeHours: numberOrNull(event.target.value) })}
              />
            </Row>

            <Row label={t("admin.game.igdbId")}>
              <input
                className={editorInputClass}
                inputMode="numeric"
                value={form.igdbId ?? ""}
                onChange={(event) => patch({ igdbId: numberOrNull(event.target.value) })}
              />
            </Row>
            <div className="flex items-end gap-3 pb-1">
              <Switch
                checked={form.isDlc}
                onCheckedChange={(checked) =>
                  patch({ isDlc: checked, baseMetadataGameId: checked ? form.baseMetadataGameId : null })
                }
              />
              <span className="text-sm font-bold text-fg">{t("admin.game.isDlc")}</span>
            </div>

            {form.isDlc ? (
              <div className="sm:col-span-2">
                <Row label={t("admin.game.baseGameId")} hint={t("admin.game.baseGameHint")}>
                  <input
                    className={editorInputClass}
                    inputMode="numeric"
                    value={form.baseMetadataGameId ?? ""}
                    onChange={(event) => patch({ baseMetadataGameId: numberOrNull(event.target.value) })}
                  />
                </Row>
              </div>
            ) : null}

            <div className="sm:col-span-2">
              <Row label={t("admin.game.summary")}>
                <textarea
                  rows={4}
                  className={cn(editorInputClass, "h-auto py-2")}
                  value={form.summary ?? ""}
                  onChange={(event) => patch({ summary: event.target.value || null })}
                />
              </Row>
            </div>

            {/* Ba vai tro anh — DOMAIN_RULES §12. */}
            <div className="sm:col-span-2">
              <Row label={t("admin.game.banner")} hint={t("admin.game.bannerHint")}>
                <input
                  className={editorInputClass}
                  value={form.bannerUrl ?? ""}
                  onChange={(event) => patch({ bannerUrl: event.target.value || null })}
                />
              </Row>
            </div>
            <Row label={t("admin.game.poster")}>
              <input
                className={editorInputClass}
                value={form.posterUrl ?? ""}
                onChange={(event) => patch({ posterUrl: event.target.value || null })}
              />
            </Row>
            <Row label={t("admin.game.backdrop")}>
              <input
                className={editorInputClass}
                value={form.backdropUrl ?? ""}
                onChange={(event) => patch({ backdropUrl: event.target.value || null })}
              />
            </Row>
          </div>

          <p className="rounded-xl border border-border bg-surface-2/30 p-3 text-fs-11 leading-5 text-muted-fg">
            {t("admin.game.tagsReadOnly")}
          </p>
        </div>

        <DialogFooter>
          {game ? (
            <span className="mr-auto pl-1 text-fs-11 text-muted-fg">
              #{game.id} · rev {form.revision ?? game.revision}
            </span>
          ) : null}
          <IconButton
            label={t("action.cancel")}
            icon={<X />}
            disabled={busy}
            onClick={() => onOpenChange(false)}
          />
          <IconButton
            tone="add"
            label={t("action.save")}
            icon={busy ? <Loader2 className="animate-spin" /> : <Save />}
            disabled={!canSave}
            onClick={() => save.mutate(form)}
          />
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
