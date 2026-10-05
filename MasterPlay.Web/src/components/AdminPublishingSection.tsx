/**
 * TIEU CHI XUAT BAN — spec §13.
 *
 * MAC DINH moi ban ghi DUOC xuat ban. Bang nay liet ke cac tieu chi co the BAT;
 * ban ghi nao truot mot tieu chi dang bat thi tu dong khong xuat ban nua — ke ca
 * ban ghi da co trong thu vien nguoi khac.
 *
 * HAI KHAI NIEM DOC LAP:
 *   `manuallyUnpublished` — y dinh cua admin, bat/tat o tung ban ghi.
 *   `isPublished`         — ket qua suy ra, DB tinh. Khong ai ghi tay.
 *
 * Cot "SE CHAN" la phan quan trong nhat man hinh nay: no dem so ban ghi truot
 * tung tieu chi KE CA khi tieu chi dang tat. Bat mot cong tac roi moi phat hien
 * no chan mat 95 ban ghi la qua muon — kho dung chung khong co undo.
 */
import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, FileX, Loader2, ShieldCheck } from "lucide-react";
import { toast } from "sonner";
import { ApiRequestError } from "@/lib/api/client";
import {
  adminApi,
  PUBLISH_RULE_KEYS,
  type AdminPublishState,
  type PublishRuleKey,
} from "@/lib/adminApi";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { Switch } from "./ui";

function Tile({ label, value, tone }: { label: string; value: number; tone?: "warn" }) {
  return (
    <div className="surface-card flex flex-col gap-0.5 p-3">
      <span className="text-fs-11 font-bold uppercase tracking-[0.12em] text-muted-fg">{label}</span>
      <span
        className={cn(
          "text-xl font-black tabular-nums",
          tone === "warn" && value > 0 ? "text-warning" : "text-fg",
        )}
      >
        {value}
      </span>
    </div>
  );
}

export function AdminPublishingSection({
  onOpenUnpublished,
}: {
  /** Nhay sang Games da loc san ban ghi dang bi an. */
  onOpenUnpublished: () => void;
}) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  /* Khoa dang luu — de chi o do quay, khong khoa ca bang. */
  const [saving, setSaving] = useState<PublishRuleKey | null>(null);

  const state = useQuery({
    queryKey: ["admin-metadata-publishing"],
    queryFn: () => adminApi.publishRules(),
  });

  const save = useMutation({
    mutationFn: (patch: Partial<Record<PublishRuleKey, boolean>>) =>
      adminApi.setPublishRules(patch),
    onSuccess: async (result) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["admin-metadata-publishing"] }),
        queryClient.invalidateQueries({ queryKey: ["admin-metadata-games"] }),
        queryClient.invalidateQueries({ queryKey: ["admin-metadata-stats"] }),
      ]);
      toast.success(
        t("admin.publishing.saved")
          .replace("{count}", String(result.recomputed))
          .replace("{unpublished}", String(result.unpublishedTotal)),
      );
    },
    onError: (failure) =>
      toast.error(failure instanceof ApiRequestError ? failure.message : String(failure)),
    onSettled: () => setSaving(null),
  });

  if (state.isPending) {
    return (
      <p className="flex items-center justify-center gap-2 py-16 text-sm text-muted-fg">
        <Loader2 size={16} className="animate-spin" /> {t("admin.publishing.loading")}
      </p>
    );
  }

  if (state.isError) {
    return (
      <div className="mx-auto mt-10 flex max-w-lg flex-col items-center gap-3 rounded-xl border border-danger/40 bg-danger/10 p-6 text-center">
        <AlertTriangle size={22} className="text-danger" />
        <p className="text-sm text-fg">
          {state.error instanceof Error ? state.error.message : String(state.error)}
        </p>
        <button
          type="button"
          onClick={() => void state.refetch()}
          className="rounded-lg border border-border px-3 py-1.5 text-xs font-extrabold text-fg"
        >
          {t("error.retry")}
        </button>
      </div>
    );
  }

  const data: AdminPublishState = state.data;

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <div className="flex items-start gap-2 rounded-xl border border-warning/40 bg-warning/10 p-3 text-xs leading-5 text-fg">
        <AlertTriangle size={16} className="mt-0.5 shrink-0 text-warning" />
        <span>{t("admin.publishing.warning")}</span>
      </div>

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Tile label={t("admin.publishing.total")} value={data.total} />
        <Tile label={t("admin.publishing.unpublishedTotal")} value={data.unpublishedTotal} tone="warn" />
        <Tile label={t("admin.publishing.failingGate")} value={data.failingGate} tone="warn" />
        <Tile label={t("admin.publishing.manuallyUnpublished")} value={data.manuallyUnpublished} tone="warn" />
      </div>

      {data.unpublishedTotal > 0 ? (
        <button
          type="button"
          onClick={onOpenUnpublished}
          className="flex w-full items-center gap-2 rounded-xl border border-primary/35 bg-primary/10 px-3 py-2 text-xs font-bold text-fg transition-colors hover:bg-primary/15"
        >
          <FileX size={14} aria-hidden="true" />
          {t("admin.publishing.reviewUnpublished").replace("{count}", String(data.unpublishedTotal))}
        </button>
      ) : null}

      <div className="surface-card overflow-hidden">
        <div className="flex items-center gap-2 border-b border-border px-4 py-2.5">
          <ShieldCheck size={14} className="text-primary" aria-hidden="true" />
          <span className="text-xs font-black uppercase tracking-[0.12em] text-muted-fg">
            {t("admin.publishing.rulesTitle")}
          </span>
        </div>

        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-border text-left text-fs-11 font-black uppercase tracking-[0.12em] text-muted-fg">
              <th className="px-4 py-2">{t("admin.publishing.condition")}</th>
              <th className="px-4 py-2 text-right">{t("admin.publishing.wouldBlock")}</th>
              <th className="px-4 py-2 text-right">{t("admin.publishing.active")}</th>
            </tr>
          </thead>
          <tbody>
            {PUBLISH_RULE_KEYS.map((key) => {
              const on = data.rules[key];
              const affected = data.wouldBlock[key] ?? 0;
              return (
                <tr key={key} className="border-b border-border/50 last:border-0">
                  <td className="px-4 py-2.5 font-semibold text-fg">
                    {t(`admin.publishing.rule.${key}`)}
                  </td>
                  <td
                    className={cn(
                      "px-4 py-2.5 text-right text-sm font-black tabular-nums",
                      /* To mau khi CO anh huong that: dieu kien dang bat va dang
                         an ban ghi, hoac dang tat nhung bat len se an ngay. */
                      affected > 0 ? "text-warning" : "text-muted-fg/60",
                    )}
                  >
                    {affected}
                  </td>
                  <td className="px-4 py-2.5">
                    <div className="flex justify-end">
                      {saving === key ? (
                        <Loader2 size={16} className="animate-spin text-muted-fg" />
                      ) : (
                        <Switch
                          checked={on}
                          aria-label={t(`admin.publishing.rule.${key}`)}
                          onCheckedChange={(next) => {
                            setSaving(key);
                            save.mutate({ [key]: next } as Partial<Record<PublishRuleKey, boolean>>);
                          }}
                        />
                      )}
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <p className="px-1 text-fs-11 leading-5 text-muted-fg">{t("admin.publishing.footnote")}</p>
    </div>
  );
}
