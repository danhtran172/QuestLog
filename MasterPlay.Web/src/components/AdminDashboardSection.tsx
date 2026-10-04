/**
 * Admin dashboard — spec 2026-09-14 §12.8.
 *
 * Dashboard thu vien tra loi "toi dang choi gi". Cai nay tra loi cau khac han:
 * "kho dung chung dang thieu gi va hong o dau". Khong tai dung so lieu, chi tai
 * dung component ve (components/charts.tsx).
 *
 * MOI CON SO DEU DO BACKEND TINH (`GET /api/admin/metadata/stats`). Cong don o
 * client se sai ngay khi kho vuot mot trang.
 *
 * Tab `health` moi la phan co gia tri: moi dong bam duoc va nhay thang sang
 * section Games da loc san. Rieng dong "chua co tag" KHONG bam duoc — no do tien
 * trinh sync quyet dinh, console khong hanh dong gi duoc (§4.4).
 */
import { useQuery } from "@tanstack/react-query";
import {
  Activity,
  AlertTriangle,
  BarChart3,
  CalendarClock,
  ChevronRight,
  Clock,
  Layers,
  Loader2,
  Star,
  Tags,
} from "lucide-react";
import { adminApi, type AdminMetadataStats } from "@/lib/adminApi";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { ColumnChart, Panel, RankedBars } from "./charts";

export type AdminDashboardTab = "overview" | "health";

/**
 * `filter` la khoa loc gui sang section Games. `null` nghia la dong nay chi de
 * theo doi, khong hanh dong duoc tu console.
 */
type HealthRow = {
  key: string;
  value: number;
  filter: string | null;
};

function StatTile({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="surface-card flex flex-col gap-0.5 p-3">
      <span className="text-[11px] font-bold uppercase tracking-[0.12em] text-muted-fg">{label}</span>
      <span className="text-xl font-black tabular-nums text-fg">{value}</span>
    </div>
  );
}

export function AdminDashboardSection({
  onOpenFiltered,
}: {
  onOpenFiltered: (missing: string) => void;
}) {
  const { t } = useI18n();

  const stats = useQuery({
    queryKey: ["admin-metadata-stats"],
    queryFn: () => adminApi.stats(),
  });

  if (stats.isPending) {
    return (
      <p className="flex items-center justify-center gap-2 py-16 text-sm text-muted-fg">
        <Loader2 size={16} className="animate-spin" /> {t("admin.dash.loading")}
      </p>
    );
  }

  if (stats.isError) {
    return (
      <div className="mx-auto mt-10 flex max-w-lg flex-col items-center gap-3 rounded-xl border border-danger/40 bg-danger/10 p-6 text-center">
        <AlertTriangle size={22} className="text-danger" />
        <p className="text-sm text-fg">
          {stats.error instanceof Error ? stats.error.message : String(stats.error)}
        </p>
        <button
          type="button"
          onClick={() => void stats.refetch()}
          className="rounded-lg border border-border px-3 py-1.5 text-xs font-extrabold text-fg"
        >
          {t("error.retry")}
        </button>
      </div>
    );
  }

  const data: AdminMetadataStats = stats.data;
  const { totals, health, activity } = data;

  /* Kho rong thi hien trang thai trung tinh, KHONG phai canh bao loi. */
  if (totals.games === 0) {
    return (
      <div className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <StatTile label={t("admin.dash.games")} value={totals.games} />
          <StatTile label={t("admin.dash.series")} value={totals.series} />
          <StatTile label={t("admin.dash.genres")} value={totals.genres} />
          <StatTile label={t("admin.dash.themes")} value={totals.themes} />
        </div>
        <p className="py-12 text-center text-sm text-muted-fg">{t("admin.dash.emptyCatalog")}</p>
      </div>
    );
  }

  const rows: HealthRow[] = [
    { key: "missingBanner", value: health.missingBanner, filter: "banner" },
    { key: "missingPoster", value: health.missingPoster, filter: "poster" },
    { key: "missingBackdrop", value: health.missingBackdrop, filter: "backdrop" },
    { key: "missingDeveloper", value: health.missingDeveloper, filter: "developer" },
    { key: "missingPublisher", value: health.missingPublisher, filter: "publisher" },
    { key: "missingPublishDate", value: health.missingPublishDate, filter: "publishDate" },
    { key: "missingMetaScore", value: health.missingMetaScore, filter: "metaScore" },
    { key: "missingAvgPlaytime", value: health.missingAvgPlaytime, filter: "avgPlaytime" },
    { key: "missingSummary", value: health.missingSummary, filter: "summary" },
    { key: "missingIgdbId", value: health.missingIgdbId, filter: "igdbId" },
    { key: "dlcWithoutBase", value: health.dlcWithoutBase, filter: "dlcWithoutBase" },
    // Khong bam duoc: thuoc sync, console khong sua duoc (§4.4).
    { key: "withoutTags", value: health.withoutTags, filter: null },
    { key: "duplicateNameGroups", value: health.duplicateNameGroups, filter: null },
    { key: "seriesWithOneGame", value: health.seriesWithOneGame, filter: null },
    { key: "seriesWithNoGames", value: health.seriesWithNoGames, filter: null },
  ];

  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <StatTile label={t("admin.dash.games")} value={totals.games} />
        <StatTile label={t("admin.dash.series")} value={totals.series} />
        <StatTile label={t("admin.dash.genres")} value={totals.genres} />
        <StatTile label={t("admin.dash.themes")} value={totals.themes} />
      </div>

      <div className="grid gap-3 lg:grid-cols-2">
        <Panel title={t("admin.dash.byYear")} icon={<CalendarClock size={13} />}>
          <ColumnChart data={data.byPublishYear} emptyLabel={t("admin.dash.noData")} />
        </Panel>
        <Panel title={t("admin.dash.metaScoreBands")} icon={<Star size={13} />}>
          <ColumnChart data={data.metaScoreBands} emptyLabel={t("admin.dash.noData")} />
        </Panel>
        <Panel title={t("admin.dash.playtimeBands")} icon={<Clock size={13} />}>
          <ColumnChart data={data.playtimeBands} emptyLabel={t("admin.dash.noData")} />
        </Panel>
        <Panel title={t("admin.dash.activity")} icon={<Activity size={13} />}>
          <div className="grid grid-cols-2 gap-3">
            <StatTile label={t("admin.dash.baseGames")} value={activity.baseGames} />
            <StatTile label={t("admin.dash.dlc")} value={activity.dlc} />
            <StatTile label={t("admin.dash.edited7d")} value={activity.edited7d} />
            <StatTile label={t("admin.dash.edited30d")} value={activity.edited30d} />
          </div>
        </Panel>
        <Panel
          title={t("admin.dash.topGenres")}
          icon={<Tags size={13} />}
          hint={t("admin.dash.syncOwned")}
        >
          <RankedBars
            data={data.topGenres.map((x) => ({ name: x.label, count: x.value }))}
            emptyLabel={t("admin.dash.noTagsYet")}
          />
        </Panel>
        <Panel
          title={t("admin.dash.topThemes")}
          icon={<Layers size={13} />}
          hint={t("admin.dash.syncOwned")}
        >
          <RankedBars
            data={data.topThemes.map((x) => ({ name: x.label, count: x.value }))}
            emptyLabel={t("admin.dash.noTagsYet")}
          />
        </Panel>
      </div>

      <Panel title={t("admin.dash.health")} icon={<BarChart3 size={13} />} hint={t("admin.dash.healthHint")}>
        <ul className="divide-y divide-border/50">
          {rows.map((row) => {
            const clickable = row.filter !== null && row.value > 0;
            return (
              <li key={row.key}>
                <button
                  type="button"
                  disabled={!clickable}
                  onClick={() => row.filter && onOpenFiltered(row.filter)}
                  className={cn(
                    "flex w-full items-center gap-3 px-1 py-2 text-left text-sm transition-colors",
                    clickable ? "hover:bg-surface-2/50" : "cursor-default",
                  )}
                >
                  <span className="min-w-0 flex-1 truncate text-fg">{t(`admin.health.${row.key}`)}</span>
                  <span
                    className={cn(
                      "w-12 text-right text-sm font-black tabular-nums",
                      row.value > 0 ? "text-warning" : "text-muted-fg/60",
                    )}
                  >
                    {row.value}
                  </span>
                  <ChevronRight
                    size={14}
                    aria-hidden="true"
                    className={cn("shrink-0", clickable ? "text-muted-fg" : "opacity-0")}
                  />
                </button>
              </li>
            );
          })}
        </ul>
      </Panel>
    </div>
  );
}
