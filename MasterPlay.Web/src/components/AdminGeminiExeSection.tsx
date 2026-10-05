/** Operational console for the independent Gemini executable-candidate worker. */
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Bot, Loader2, Play, RefreshCw } from "lucide-react";
import { toast } from "sonner";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi } from "@/lib/adminApi";
import { useI18n } from "@/lib/i18n";
import { Switch } from "./ui";

function errorText(error: unknown) {
  return error instanceof ApiRequestError ? error.message : String(error);
}

function date(value: string | null | undefined) {
  if (!value) return "—";
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? "—" : new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(parsed);
}

export function AdminGeminiExeSection() {
  const { t } = useI18n();
  const client = useQueryClient();
  const status = useQuery({ queryKey: ["admin-gemini-exe-status"], queryFn: () => adminApi.geminiExeStatus(), refetchInterval: 30_000 });
  const runs = useQuery({ queryKey: ["admin-gemini-exe-runs"], queryFn: () => adminApi.listGeminiExeRuns() });
  const games = useQuery({ queryKey: ["admin-gemini-exe-games"], queryFn: () => adminApi.listGeminiExeGames() });
  const refresh = () => Promise.all(["status", "runs", "games"].map((name) =>
    client.invalidateQueries({ queryKey: [`admin-gemini-exe-${name}`] }),
  ));
  const toggle = useMutation({
    mutationFn: (enabled: boolean) => adminApi.setGeminiExeEnabled(enabled),
    onSuccess: async () => { await refresh(); toast.success(t("admin.geminiExe.saved")); },
    onError: (error) => toast.error(errorText(error)),
  });
  const run = useMutation({
    mutationFn: adminApi.triggerGeminiExe,
    onSuccess: async () => { await refresh(); toast.success(t("admin.geminiExe.queued")); },
    onError: (error) => toast.error(errorText(error)),
  });
  if (status.isPending) return <div className="surface-card flex items-center gap-2 p-5 text-muted-fg"><Loader2 className="animate-spin" size={18} />{t("admin.geminiExe.loading")}</div>;
  if (status.isError || !status.data) return <div className="surface-card p-5 text-danger"><p className="font-bold">{t("admin.geminiExe.error")}</p>{status.error ? <p className="mt-1 text-sm">{errorText(status.error)}</p> : null}</div>;
  const value = status.data;
  const active = !!value.leaseUntil && new Date(value.leaseUntil).getTime() > Date.now();
  return <section className="space-y-5">
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div><h2 className="flex items-center gap-2 text-xl font-black"><Bot size={21} />{t("admin.geminiExe.title")}</h2><p className="mt-1 max-w-2xl text-sm text-muted-fg">{t("admin.geminiExe.description")}</p></div>
      <div className="flex items-center gap-2"><Switch checked={value.enabled} disabled={toggle.isPending} onCheckedChange={(next) => toggle.mutate(next)} aria-label={t("admin.geminiExe.enabled")} /><button type="button" disabled={!value.enabled || active || run.isPending} onClick={() => run.mutate()} className="flex items-center gap-2 rounded-lg bg-primary px-3 py-2 text-sm font-bold text-primary-fg disabled:opacity-50"><Play size={15} />{t("admin.geminiExe.runNow")}</button></div>
    </div>
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
      {[[t("admin.geminiExe.enabled"), value.enabled ? t("admin.geminiExe.running") : t("admin.geminiExe.paused")], [t("admin.geminiExe.queue"), value.queueDepth], [t("admin.geminiExe.nextRun"), date(value.nextRunAt)], [t("admin.geminiExe.lease"), active ? date(value.leaseUntil) : "—"]].map(([label, content]) => <div className="surface-card p-3" key={String(label)}><span className="text-fs-11 font-bold uppercase tracking-wider text-muted-fg">{label}</span><div className="mt-1 truncate text-lg font-black">{content}</div></div>)}
    </div>
    <div className="grid gap-5 xl:grid-cols-2">
      <div className="surface-card overflow-hidden"><div className="flex items-center justify-between border-b border-border p-3"><h3 className="font-extrabold">{t("admin.geminiExe.runs")}</h3><button type="button" onClick={() => refresh()} className="text-muted-fg"><RefreshCw size={15} /></button></div><div className="divide-y divide-border">{runs.isError ? <p className="p-4 text-sm text-danger">{errorText(runs.error)}</p> : runs.data?.items.length ? runs.data.items.map((item) => <div className="p-3 text-sm" key={item.id}><div className="flex justify-between gap-2 font-bold"><span>{item.status}</span><span className="text-muted-fg">{date(item.started_at)}</span></div><div className="mt-1 text-muted-fg">{item.candidates} {t("admin.geminiExe.candidates")} · {item.no_candidates} {t("admin.geminiExe.noCandidate")} · {item.errors} {t("admin.geminiExe.errors")}</div></div>) : <p className="p-4 text-sm text-muted-fg">{t("admin.geminiExe.emptyRuns")}</p>}</div></div>
      <div className="surface-card overflow-hidden"><div className="border-b border-border p-3"><h3 className="font-extrabold">{t("admin.geminiExe.candidates")}</h3></div><div className="max-h-[360px] divide-y divide-border overflow-y-auto">{games.isError ? <p className="p-4 text-sm text-danger">{errorText(games.error)}</p> : games.data?.items.length ? games.data.items.map((item) => <div className="p-3 text-sm" key={item.gameId}><div className="flex justify-between gap-2 font-bold"><span className="truncate">{item.name}</span><code className="shrink-0 text-primary">{item.fileName ?? "—"}</code></div><div className="mt-1 text-xs text-muted-fg">{item.status ?? t("admin.geminiExe.pending")} · {date(item.enrichedAt)}</div></div>) : <p className="p-4 text-sm text-muted-fg">{t("admin.geminiExe.emptyCandidates")}</p>}</div></div>
    </div>
  </section>;
}
