/**
 * Crawler operations console. It only presents state and ordering emitted by
 * the server; quotas, prioritisation and cursor ordering remain in Postgres.
 */
import { useState } from "react";
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  AlertTriangle,
  CheckCircle2,
  Clock3,
  Database,
  ExternalLink,
  ListRestart,
  Loader2,
  Play,
  RefreshCw,
} from "lucide-react";
import { toast } from "sonner";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi, type AdminCrawlerRun } from "@/lib/adminApi";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { Switch } from "./ui";

const PAGE_SIZE = 25;

function formatDate(value: string | null | undefined) {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium", timeStyle: "short",
  }).format(date);
}

function formatUnixDate(value: number) {
  return formatDate(new Date(value * 1000).toISOString());
}

function formatBytes(bytes: number) {
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function statusTone(status: AdminCrawlerRun["status"]) {
  if (status === "completed") return "text-success";
  if (status === "running") return "text-primary";
  if (status === "partial" || status === "interrupted") return "text-warning";
  return "text-danger";
}

function Tile({ label, value, icon }: { label: string; value: string | number; icon?: React.ReactNode }) {
  return (
    <div className="surface-card flex min-w-0 flex-col gap-1 p-3">
      <span className="flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-[0.12em] text-muted-fg">
        {icon}{label}
      </span>
      <span className="truncate text-xl font-black tabular-nums text-fg">{value}</span>
    </div>
  );
}

export function AdminCrawlerSection() {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const [view, setView] = useState<"runs" | "tracked">("runs");
  const [selectedRun, setSelectedRun] = useState<string | null>(null);

  const status = useQuery({
    queryKey: ["admin-crawler-status"],
    queryFn: () => adminApi.crawlerStatus(),
    refetchInterval: 30_000,
  });
  const runs = useInfiniteQuery({
    queryKey: ["admin-crawler-runs"],
    queryFn: ({ pageParam }) => adminApi.listCrawlerRuns({ cursor: pageParam, pageSize: PAGE_SIZE }),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => last.nextCursor,
  });
  const tracked = useInfiniteQuery({
    queryKey: ["admin-crawler-tracked"],
    queryFn: ({ pageParam }) => adminApi.listCrawlerGames({ cursor: pageParam, pageSize: PAGE_SIZE }),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => last.nextCursor,
    enabled: view === "tracked",
  });
  const items = useInfiniteQuery({
    queryKey: ["admin-crawler-run-items", selectedRun],
    queryFn: ({ pageParam }) => adminApi.listCrawlerRunItems(selectedRun!, { cursor: pageParam, pageSize: PAGE_SIZE }),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => last.nextCursor,
    enabled: selectedRun !== null,
  });

  const invalidate = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["admin-crawler-status"] }),
      queryClient.invalidateQueries({ queryKey: ["admin-crawler-runs"] }),
    ]);
  };
  const toggle = useMutation({
    mutationFn: (enabled: boolean) => adminApi.setCrawlerEnabled(enabled),
    onSuccess: async (next) => {
      await invalidate();
      toast.success(t(next.enabled ? "admin.crawler.resumed" : "admin.crawler.paused"));
    },
    onError: (error) => toast.error(error instanceof ApiRequestError ? error.message : String(error)),
  });
  const runNow = useMutation({
    mutationFn: () => adminApi.triggerCrawler(),
    onSuccess: async () => {
      toast.success(t("admin.crawler.queued"));
      await invalidate();
    },
    onError: (error) => toast.error(error instanceof ApiRequestError ? error.message : String(error)),
  });

  if (status.isPending) {
    return <p className="flex items-center justify-center gap-2 py-16 text-sm text-muted-fg"><Loader2 size={16} className="animate-spin" />{t("admin.crawler.loading")}</p>;
  }
  if (status.isError) {
    return (
      <div className="mx-auto mt-10 flex max-w-lg flex-col items-center gap-3 rounded-xl border border-danger/40 bg-danger/10 p-6 text-center">
        <AlertTriangle size={22} className="text-danger" />
        <p className="text-sm text-fg">{status.error instanceof Error ? status.error.message : String(status.error)}</p>
        <button type="button" onClick={() => void status.refetch()} className="rounded-lg border border-border px-3 py-1.5 text-xs font-extrabold text-fg">{t("error.retry")}</button>
      </div>
    );
  }

  const data = status.data;
  const allRuns = runs.data?.pages.flatMap((page) => page.items) ?? [];
  const runItems = items.data?.pages.flatMap((page) => page.items) ?? [];
  const trackedGames = tracked.data?.pages.flatMap((page) => page.items) ?? [];
  const databasePercent = data.databaseCapBytes > 0 ? Math.min(100, (data.databaseBytes / data.databaseCapBytes) * 100) : 0;
  const selected = allRuns.find((run) => run.id === selectedRun) ?? data.latestRun;

  return (
    <div className="mx-auto max-w-6xl space-y-4">
      <div className="surface-card flex flex-wrap items-center gap-3 p-4">
        <div className="min-w-0 flex-1">
          <h2 className="text-base font-extrabold text-fg">{t("admin.crawler.title")}</h2>
          <p className="mt-0.5 text-xs text-muted-fg">{t("admin.crawler.description")}</p>
        </div>
        <div className="flex items-center gap-2">
          <Switch checked={data.enabled} disabled={toggle.isPending} aria-label={t("admin.crawler.enabled")} onCheckedChange={(value) => toggle.mutate(value)} />
          <span className={cn("text-xs font-extrabold", data.enabled ? "text-success" : "text-warning")}>{t(data.enabled ? "admin.crawler.enabled" : "admin.crawler.pausedState")}</span>
        </div>
        <button type="button" disabled={!data.enabled || runNow.isPending} onClick={() => runNow.mutate()} className="flex items-center gap-1.5 rounded-lg bg-primary px-3 py-2 text-xs font-extrabold text-primary-fg disabled:cursor-not-allowed disabled:opacity-45">
          {runNow.isPending ? <Loader2 size={14} className="animate-spin" /> : <Play size={14} />}{t("admin.crawler.runNow")}
        </button>
      </div>

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Tile label={t("admin.crawler.queue")} value={data.queueDepth} icon={<ListRestart size={13} />} />
        <Tile label={t("admin.crawler.tracked")} value={data.trackedGames} icon={<CheckCircle2 size={13} />} />
        <Tile label={t("admin.crawler.nextRun")} value={formatDate(data.nextRunAt)} icon={<Clock3 size={13} />} />
        <Tile label={t("admin.crawler.checkpoint")} value={formatUnixDate(data.releaseCursorAt)} icon={<RefreshCw size={13} />} />
      </div>

      <div className="surface-card p-4">
        <div className="flex flex-wrap items-center justify-between gap-2 text-xs">
          <span className="flex items-center gap-1.5 font-bold text-fg"><Database size={14} className="text-primary" />{t("admin.crawler.database")}</span>
          <span className="font-bold tabular-nums text-muted-fg">{formatBytes(data.databaseBytes)} / {formatBytes(data.databaseCapBytes)} · {t("admin.crawler.headroom").replace("{value}", formatBytes(data.discoveryHeadroomBytes))}</span>
        </div>
        <div className="mt-2 h-2 overflow-hidden rounded-full bg-surface-2"><div className={cn("h-full rounded-full", databasePercent >= 90 ? "bg-danger" : "bg-primary")} style={{ width: `${databasePercent}%` }} /></div>
        <p className="mt-2 text-[11px] text-muted-fg">
          {data.cron?.schedule ? `${t("admin.crawler.schedule")}: ${data.cron.schedule}` : t("admin.crawler.scheduleUnavailable")}
          {data.leaseUntil ? ` · ${t("admin.crawler.leaseUntil")}: ${formatDate(data.leaseUntil)}` : ""}
        </p>
      </div>

      <div className="flex gap-2 border-b border-border">
        {(["runs", "tracked"] as const).map((key) => <button key={key} type="button" onClick={() => setView(key)} className={cn("border-b-2 px-3 py-2 text-xs font-extrabold", view === key ? "border-primary text-primary" : "border-transparent text-muted-fg")}>{t(`admin.crawler.tab.${key}`)}</button>)}
      </div>

      {view === "runs" ? (
        <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
          <div className="surface-card overflow-hidden">
            <div className="border-b border-border px-4 py-2.5 text-xs font-black uppercase tracking-[0.12em] text-muted-fg">{t("admin.crawler.runs")}</div>
            {runs.isPending ? <p className="p-5 text-sm text-muted-fg">{t("admin.crawler.loading")}</p> : null}
            {runs.isError ? <p className="p-5 text-sm text-danger">{runs.error instanceof Error ? runs.error.message : String(runs.error)}</p> : null}
            {allRuns.map((run) => <button key={run.id} type="button" onClick={() => setSelectedRun(run.id)} className={cn("grid w-full grid-cols-[minmax(0,1fr)_auto] gap-x-3 border-b border-border/50 px-4 py-3 text-left last:border-0", selectedRun === run.id && "bg-primary/10")}>
              <span className="min-w-0"><span className="block truncate text-sm font-bold text-fg">{formatDate(run.startedAt)}</span><span className="mt-0.5 block text-[11px] text-muted-fg">{t("admin.crawler.runCounts").replace("{inserted}", String(run.inserted)).replace("{refreshed}", String(run.refreshed)).replace("{errors}", String(run.errors))}</span></span>
              <span className={cn("text-xs font-black capitalize", statusTone(run.status))}>{t(`admin.crawler.status.${run.status}`)}</span>
            </button>)}
            {!runs.isPending && allRuns.length === 0 ? <p className="p-5 text-sm text-muted-fg">{t("admin.crawler.noRuns")}</p> : null}
            {runs.hasNextPage ? <button type="button" disabled={runs.isFetchingNextPage} onClick={() => void runs.fetchNextPage()} className="m-3 rounded-lg border border-border px-3 py-1.5 text-xs font-bold text-fg">{runs.isFetchingNextPage ? t("admin.crawler.loading") : t("admin.crawler.more")}</button> : null}
          </div>
          <div className="surface-card overflow-hidden">
            <div className="border-b border-border px-4 py-2.5 text-xs font-black uppercase tracking-[0.12em] text-muted-fg">{selected ? `${t("admin.crawler.changedGames")} · ${formatDate(selected.startedAt)}` : t("admin.crawler.changedGames")}</div>
            {selectedRun === null ? <p className="p-5 text-sm text-muted-fg">{t("admin.crawler.selectRun")}</p> : null}
            {items.isPending ? <p className="p-5 text-sm text-muted-fg">{t("admin.crawler.loading")}</p> : null}
            {items.isError ? <p className="p-5 text-sm text-danger">{items.error instanceof Error ? items.error.message : String(items.error)}</p> : null}
            {runItems.map((game) => <div key={`${game.gameId}-${game.action}`} className="flex items-center gap-3 border-b border-border/50 px-4 py-2.5 last:border-0"><span className="min-w-0 flex-1 truncate text-sm font-semibold text-fg">{game.name}</span><span className={cn("text-[11px] font-black uppercase", game.action === "inserted" ? "text-success" : "text-primary")}>{t(`admin.crawler.action.${game.action}`)}</span></div>)}
            {selectedRun !== null && !items.isPending && runItems.length === 0 ? <p className="p-5 text-sm text-muted-fg">{t("admin.crawler.noAudit")}</p> : null}
            {items.hasNextPage ? <button type="button" disabled={items.isFetchingNextPage} onClick={() => void items.fetchNextPage()} className="m-3 rounded-lg border border-border px-3 py-1.5 text-xs font-bold text-fg">{items.isFetchingNextPage ? t("admin.crawler.loading") : t("admin.crawler.more")}</button> : null}
          </div>
        </div>
      ) : (
        <div className="surface-card overflow-hidden">
          <div className="border-b border-border px-4 py-2.5 text-xs font-black uppercase tracking-[0.12em] text-muted-fg">{t("admin.crawler.trackedGames")}</div>
          {tracked.isPending ? <p className="p-5 text-sm text-muted-fg">{t("admin.crawler.loading")}</p> : null}
          {tracked.isError ? <p className="p-5 text-sm text-danger">{tracked.error instanceof Error ? tracked.error.message : String(tracked.error)}</p> : null}
          {trackedGames.map((game) => <div key={game.gameId} className="grid grid-cols-[minmax(0,1fr)_auto_auto] gap-4 border-b border-border/50 px-4 py-2.5 text-sm last:border-0"><span className="min-w-0 truncate font-semibold text-fg">{game.name}</span><span className="text-xs text-muted-fg">{formatDate(game.checkedAt)}</span>{game.queued ? <span className="rounded-full bg-warning/15 px-2 py-0.5 text-[10px] font-black uppercase text-warning">{t("admin.crawler.queuedBadge")}</span> : <span />}</div>)}
          {!tracked.isPending && trackedGames.length === 0 ? <p className="p-5 text-sm text-muted-fg">{t("admin.crawler.noTracked")}</p> : null}
          {tracked.hasNextPage ? <button type="button" disabled={tracked.isFetchingNextPage} onClick={() => void tracked.fetchNextPage()} className="m-3 rounded-lg border border-border px-3 py-1.5 text-xs font-bold text-fg">{tracked.isFetchingNextPage ? t("admin.crawler.loading") : t("admin.crawler.more")}</button> : null}
        </div>
      )}

      <p className="flex items-center gap-1.5 px-1 text-[11px] leading-5 text-muted-fg"><ExternalLink size={12} />{t("admin.crawler.footnote")}</p>
    </div>
  );
}
