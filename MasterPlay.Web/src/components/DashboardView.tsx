/**
 * DASHBOARD — mot MAN HINH day du, khong phai popup.
 *
 * Hai tab:
 *   "overview": thong ke thu vien (hoan thanh theo nam, phan bo progress /
 *               ownership, the loai / series / hang, diem toi cham so voi
 *               Metacritic, nam phat hanh, sap ra mat).
 *   "health":   nhung cho du lieu con thieu — moi dong la mot viec can lam.
 *
 * MOI CON SO DEU DO BACKEND TINH (`GET /api/dashboard/stats`). O day chi ve.
 * Cong don o client se sai ngay khi thu vien co phan trang — day la ly do
 * endpoint kia ton tai.
 *
 * Bieu do ve bang SVG/CSS thuan, khong keo them thu vien chart nao: cac dang
 * can dung (cot, cot ngang, vanh khuyen) deu don gian, va lam vay thi mau sac
 * dung dung he token cua app.
 */
import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  ArrowLeft,
  BarChart3,
  CalendarClock,
  CheckCircle2,
  Clock,
  Gamepad2,
  ListOrdered,
  PlayCircle,
  RotateCcw,
  ShieldAlert,
  Star,
  Trophy,
} from "lucide-react";
import { api } from "@/lib/api/client";
import type { DashboardStats } from "@/lib/api/domain";
import { formatPlaytimeSeconds, formatScore } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { IconButton, Tooltip } from "./ui";
/* Panel / Empty / ColumnChart / RankedBars dung chung voi Admin dashboard —
   xem components/charts.tsx (spec 2026-09-14 §12.8). */
import { ColumnChart, Empty, Panel, RankedBars } from "./charts";

type DashboardTab = "overview" | "health";

/* ── Manh ghep dung chung ── */

const DONUT_FALLBACK = ["#6366f1", "#22d3ee", "#f59e0b", "#f472b6", "#4ade80", "#94a3b8"];

/**
 * Vanh khuyen. Mau lay tu catalog (`colorCode`) — nguoi dung doi mau Progress /
 * Ownership trong Settings thi bieu do doi theo, khong hardcode.
 */
function Donut({
  data,
  emptyLabel,
}: {
  data: { name: string; count: number; colorCode?: string | null }[];
  emptyLabel: string;
}) {
  const total = data.reduce((sum, d) => sum + d.count, 0);
  if (total === 0) return <Empty label={emptyLabel} />;

  const radius = 52;
  const circumference = 2 * Math.PI * radius;
  let offset = 0;

  return (
    <div className="flex flex-wrap items-center gap-4">
      <svg viewBox="0 0 140 140" className="h-32 w-32 shrink-0 -rotate-90">
        {data.map((d, index) => {
          const length = (d.count / total) * circumference;
          const dash = `${length} ${circumference - length}`;
          const element = (
            <circle
              key={d.name || index}
              cx="70"
              cy="70"
              r={radius}
              fill="none"
              strokeWidth="16"
              stroke={d.colorCode || DONUT_FALLBACK[index % DONUT_FALLBACK.length]}
              strokeDasharray={dash}
              strokeDashoffset={-offset}
            />
          );
          offset += length;
          return element;
        })}
      </svg>

      <ul className="min-w-0 flex-1 space-y-1">
        {data.map((d, index) => (
          <li key={d.name || index} className="flex items-center gap-2 text-xs">
            <span
              className="h-2.5 w-2.5 shrink-0 rounded-[3px]"
              style={{ background: d.colorCode || DONUT_FALLBACK[index % DONUT_FALLBACK.length] }}
            />
            <span className="min-w-0 flex-1 truncate font-semibold text-fg">{d.name || "—"}</span>
            <span className="shrink-0 font-black tabular-nums text-muted-fg">{d.count}</span>
            <span className="w-9 shrink-0 text-right tabular-nums text-muted-fg/70">
              {Math.round((d.count / total) * 100)}%
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

/* ── Tab 1: tong quan ── */

function OverviewTab({ data }: { data: DashboardStats }) {
  const { t } = useI18n();
  const noData = t("dashboard.noData");

  const tiles = [
    { icon: Gamepad2, label: t("dashboard.total"), value: String(data.totalGames ?? 0) },
    { icon: CheckCircle2, label: t("dashboard.done"), value: String(data.finishedGames ?? 0) },
    { icon: PlayCircle, label: t("dashboard.playing"), value: String(data.playingGames ?? 0) },
    { icon: ListOrdered, label: t("dashboard.queue"), value: String(data.queuedGames ?? 0) },
    { icon: Clock, label: t("dashboard.playtime"), value: formatPlaytimeSeconds(data.totalPlaytimeSeconds) },
    { icon: Star, label: t("dashboard.avgScore"), value: formatScore(data.averageScore) ?? "—" },
  ];

  const critics = data.scoreVsCritics;
  const gap =
    critics?.averageMine != null && critics?.averageCritic != null
      ? critics.averageMine - critics.averageCritic
      : null;

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-6">
        {tiles.map((tile) => (
          <div key={tile.label} className="surface-card p-3">
            <p className="flex items-center gap-1.5 text-[11px] font-semibold text-muted-fg">
              <tile.icon size={13} /> {tile.label}
            </p>
            <p className="mt-1 truncate text-xl font-black">{tile.value}</p>
          </div>
        ))}
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <Panel
          title={t("dashboard.finishesByYear")}
          icon={<Trophy size={13} className="text-success" />}
          hint={`${(data.finishesByYear ?? []).reduce((sum, y) => sum + y.count, 0)}`}
        >
          <ColumnChart
            data={(data.finishesByYear ?? []).map((y) => ({ label: String(y.year), value: y.count }))}
            emptyLabel={noData}
          />
        </Panel>

        <Panel title={t("dashboard.gamesByReleaseYear")} icon={<CalendarClock size={13} className="text-violet-400" />}>
          <ColumnChart
            data={(data.gamesByReleaseYear ?? []).map((y) => ({ label: String(y.year), value: y.count }))}
            emptyLabel={noData}
          />
        </Panel>

        <Panel title={t("dashboard.progressBreakdown")}>
          <Donut data={data.progressBreakdown ?? []} emptyLabel={noData} />
        </Panel>

        <Panel title={t("dashboard.ownershipBreakdown")}>
          <Donut data={data.ownershipBreakdown ?? []} emptyLabel={noData} />
        </Panel>

        <Panel
          title={t("dashboard.scoreVsCritics")}
          icon={<Star size={13} className="text-amber-400" />}
          hint={critics?.pairedGames ? `${critics.pairedGames} ${t("lib.items")}` : undefined}
        >
          {critics && critics.pairedGames ? (
            <div className="space-y-3">
              <div className="grid grid-cols-3 gap-2 text-center">
                <div className="rounded-lg border border-border bg-surface-2/30 p-2">
                  <p className="text-[11px] font-semibold text-muted-fg">{t("dashboard.mine")}</p>
                  <p className="text-lg font-black text-primary">{critics.averageMine?.toFixed(2)}</p>
                </div>
                <div className="rounded-lg border border-border bg-surface-2/30 p-2">
                  <p className="text-[11px] font-semibold text-muted-fg">{t("dashboard.critics")}</p>
                  <p className="text-lg font-black text-fg">{critics.averageCritic?.toFixed(2)}</p>
                </div>
                <div className="rounded-lg border border-border bg-surface-2/30 p-2">
                  <p className="text-[11px] font-semibold text-muted-fg">{t("dashboard.gap")}</p>
                  <p
                    className={cn(
                      "text-lg font-black",
                      gap == null ? "text-muted-fg" : gap >= 0 ? "text-success" : "text-danger",
                    )}
                  >
                    {gap == null ? "—" : `${gap >= 0 ? "+" : ""}${gap.toFixed(2)}`}
                  </p>
                </div>
              </div>

              {/* Mot thanh chia ba khuc: cham cao hon / bang / thap hon critics. */}
              <div className="flex h-4 overflow-hidden rounded-md">
                <Tooltip label={`${t("dashboard.higher")}: ${critics.higher}`}>
                  <span className="bg-success/70" style={{ width: `${(critics.higher! / critics.pairedGames) * 100}%` }} />
                </Tooltip>
                <Tooltip label={`${t("dashboard.equal")}: ${critics.equal}`}>
                  <span className="bg-muted-fg/50" style={{ width: `${(critics.equal! / critics.pairedGames) * 100}%` }} />
                </Tooltip>
                <Tooltip label={`${t("dashboard.lower")}: ${critics.lower}`}>
                  <span className="bg-danger/70" style={{ width: `${(critics.lower! / critics.pairedGames) * 100}%` }} />
                </Tooltip>
              </div>
              <div className="flex justify-between text-[11px] font-semibold">
                <span className="text-success">{t("dashboard.higher")} {critics.higher}</span>
                <span className="text-muted-fg">{t("dashboard.equal")} {critics.equal}</span>
                <span className="text-danger">{t("dashboard.lower")} {critics.lower}</span>
              </div>
            </div>
          ) : (
            <Empty label={noData} />
          )}
        </Panel>

        <Panel title={t("dashboard.scoreDistribution")} icon={<BarChart3 size={13} className="text-primary" />}>
          <ColumnChart
            data={(data.scoreDistribution ?? []).map((s) => ({ label: String(s.score), value: s.count }))}
            emptyLabel={noData}
          />
        </Panel>

        <Panel title={t("dashboard.topGenres")}>
          <RankedBars data={data.topGenres ?? []} emptyLabel={noData} />
        </Panel>

        <Panel title={t("dashboard.topSeries")}>
          <RankedBars data={data.topSeries ?? []} emptyLabel={noData} />
        </Panel>

        <Panel title={t("dashboard.topPublishers")}>
          <RankedBars data={data.topPublishers ?? []} emptyLabel={noData} />
        </Panel>

        <Panel title={t("dashboard.topDevelopers")}>
          <RankedBars data={data.topDevelopers ?? []} emptyLabel={noData} />
        </Panel>

        <Panel
          title={t("dashboard.upcoming")}
          icon={<CalendarClock size={13} className="text-sky-400" />}
          className="xl:col-span-2"
        >
          {(data.upcoming ?? []).length === 0 ? (
            <Empty label={noData} />
          ) : (
            <ul className="grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
              {(data.upcoming ?? []).map((game) => (
                <li
                  key={game.id}
                  className="flex items-center gap-2 rounded-lg border border-border bg-surface-2/30 p-2"
                >
                  <span className="h-10 w-[72px] shrink-0 overflow-hidden rounded bg-muted">
                    {game.bannerPath ? (
                      <img src={game.bannerPath} alt="" loading="lazy" className="h-full w-full object-cover" />
                    ) : null}
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-xs font-bold text-fg">{game.name}</span>
                    <span className="block text-[11px] text-muted-fg">{game.publishDate}</span>
                  </span>
                  <span className="shrink-0 text-right">
                    <span className="block text-sm font-black tabular-nums text-primary">{game.daysLeft}</span>
                    <span className="block text-[10px] text-muted-fg">{t("dashboard.days")}</span>
                  </span>
                </li>
              ))}
            </ul>
          )}
        </Panel>
      </div>
    </div>
  );
}

/* ── Tab 2: suc khoe du lieu ── */

type HealthKey =
  | "missingSeries"
  | "missingGenres"
  | "missingSlug"
  | "missingBanner"
  | "missingPoster"
  | "missingPublishDate"
  | "missingProgress"
  | "missingOwnership"
  | "doneWithoutFinish"
  | "disabledGames";

/** `disabledGames` chi la thong tin, khong phai loi — nen khong to do. */
const INFO_ONLY: HealthKey[] = ["disabledGames"];

const HEALTH_ORDER: HealthKey[] = [
  "missingSlug",
  "missingGenres",
  "missingSeries",
  "missingPublishDate",
  "doneWithoutFinish",
  "missingProgress",
  "missingOwnership",
  "missingPoster",
  "missingBanner",
  "disabledGames",
];

function HealthTab({ data }: { data: DashboardStats }) {
  const { t } = useI18n();
  const health = data.dataHealth;
  const total = health?.activeGames ?? 0;

  const rows = useMemo(
    () =>
      HEALTH_ORDER.map((key) => ({
        key,
        count: (health?.[key] as number | undefined) ?? 0,
        info: INFO_ONLY.includes(key),
      })),
    [health],
  );

  const problems = rows.filter((row) => !row.info && row.count > 0);
  const clean = rows.filter((row) => !row.info && row.count === 0).length;

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <div className="surface-card p-3">
          <p className="text-[11px] font-semibold text-muted-fg">{t("dashboard.total")}</p>
          <p className="mt-1 text-xl font-black">{total}</p>
        </div>
        <div className="surface-card p-3">
          <p className="text-[11px] font-semibold text-muted-fg">{t("dashboard.healthChecks")}</p>
          <p className="mt-1 text-xl font-black">{rows.length - INFO_ONLY.length}</p>
        </div>
        <div className="surface-card p-3">
          <p className="text-[11px] font-semibold text-muted-fg">{t("dashboard.healthClean")}</p>
          <p className="mt-1 text-xl font-black text-success">{clean}</p>
        </div>
        <div className="surface-card p-3">
          <p className="text-[11px] font-semibold text-muted-fg">{t("dashboard.healthTodo")}</p>
          <p className={cn("mt-1 text-xl font-black", problems.length > 0 ? "text-warning" : "text-success")}>
            {problems.length}
          </p>
        </div>
      </div>

      <Panel title={t("dashboard.tabHealth")} icon={<ShieldAlert size={13} className="text-warning" />}>
        <p className="mb-3 text-xs text-muted-fg">{t("dashboard.healthIntro")}</p>
        <ul className="space-y-2">
          {rows.map((row) => {
            const ratio = total > 0 ? row.count / total : 0;
            const ok = row.count === 0;
            return (
              <li key={row.key} className="grid grid-cols-[minmax(0,1fr)_88px_56px] items-center gap-3">
                <span className="min-w-0">
                  <span className="block truncate text-sm font-bold text-fg">
                    {t(`dashboard.health.${row.key}`)}
                  </span>
                  <span className="block truncate text-[11px] text-muted-fg">
                    {t(`dashboard.healthWhy.${row.key}`)}
                  </span>
                </span>
                <span className="h-2 overflow-hidden rounded-full bg-surface-2">
                  <span
                    className={cn(
                      "block h-full rounded-full",
                      ok ? "bg-success/60" : row.info ? "bg-muted-fg/50" : "bg-warning/70",
                    )}
                    style={{ width: ok ? "100%" : `${Math.max(4, ratio * 100)}%` }}
                  />
                </span>
                <span
                  className={cn(
                    "text-right text-sm font-black tabular-nums",
                    ok ? "text-success" : row.info ? "text-muted-fg" : "text-warning",
                  )}
                >
                  {row.count}
                </span>
              </li>
            );
          })}
        </ul>
      </Panel>
    </div>
  );
}

/* ── Man hinh ── */

export function DashboardView({ onClose }: { onClose: () => void }) {
  const { t } = useI18n();
  const [tab, setTab] = useState<DashboardTab>("overview");

  const { data, isPending, isError, error, refetch } = useQuery({
    queryKey: ["dashboard"],
    queryFn: () => api.dashboardStats(),
  });

  const tabs: { key: DashboardTab; label: string }[] = [
    { key: "overview", label: t("dashboard.tabOverview") },
    { key: "health", label: t("dashboard.tabHealth") },
  ];

  return (
    <div className="animate-in fade-in space-y-4 duration-300">
      <header className="flex flex-wrap items-center gap-3">
        <IconButton onClick={onClose} label={t("action.back")} icon={<ArrowLeft />} tooltipSide="bottom" />
        <h2 className="flex items-center gap-2 text-lg font-black">
          <BarChart3 size={18} className="text-primary" /> {t("dashboard.title")}
        </h2>

        <nav className="stone-slab ml-auto flex items-center gap-1 rounded-xl p-1">
          {tabs.map((item) => (
            <button
              key={item.key}
              type="button"
              onClick={() => setTab(item.key)}
              className={cn(
                "rounded-lg px-3 py-1.5 text-sm font-semibold transition-colors duration-200",
                tab === item.key
                  ? "glass-surface bg-primary/45 text-fg"
                  : "text-muted-fg hover:bg-surface-2/60 hover:text-fg",
              )}
            >
              {item.label}
            </button>
          ))}
        </nav>
      </header>

      {isError ? (
        <div className="flex flex-col items-center gap-3 py-24 text-center">
          <h3 className="text-lg font-bold">{t("error.title")}</h3>
          <p className="max-w-md text-sm text-muted-fg">
            {error instanceof Error ? error.message : String(error)}
          </p>
          <IconButton onClick={() => void refetch()} label={t("error.retry")} tone="edit" icon={<RotateCcw />} />
        </div>
      ) : isPending ? (
        <div className="grid gap-4 xl:grid-cols-2">
          {Array.from({ length: 6 }).map((_, index) => (
            <div key={index} className="surface-card h-56 animate-pulse" />
          ))}
        </div>
      ) : tab === "overview" ? (
        <OverviewTab data={data} />
      ) : (
        <HealthTab data={data} />
      )}
    </div>
  );
}
