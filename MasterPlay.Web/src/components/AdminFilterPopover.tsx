/**
 * Bo loc cua Admin Console — spec §12.1.
 *
 * KHONG tai dung `FilterDialog` cua thu vien: cai do loc theo progress, ownership,
 * score, excited, playtime, finish year — toan userdata khong ton tai tren ban ghi
 * metadata. Bo loc o day nho hon han va chi cham vao field cua kho.
 *
 * KHONG co loc theo genre/theme du spec §12.1 co nhac: hai bang do thuoc tien
 * trinh sync va hien dang RONG (§4.4). Dung mot o chon khong bao gio co lua chon
 * la hua hao. Them lai khi sync chay.
 *
 * Moi phep loc chay o SERVER (`admin_list_metadata_games`). Component nay chi giu
 * lua chon va gui di — AGENTS.md §2 cam cai dat logic loc o client.
 */
import { Filter, RotateCcw } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import {
  adminApi,
  adminHealthLabelKey,
  countAdminFilters,
  EMPTY_ADMIN_FILTER,
  type AdminGameFilter,
} from "@/lib/adminApi";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import {
  IconButton,
  Popover,
  PopoverContent,
  PopoverTrigger,
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "./ui";

/** Khoa loc "thieu" — cung danh sach voi cac dong health cua dashboard. */
const MISSING_KEYS = [
  "banner",
  "backdrop",
  "poster",
  "developer",
  "publisher",
  "publishDate",
  "metaScore",
  "avgPlaytime",
  "summary",
  "igdbId",
  "dlcWithoutBase",
] as const;


/* Gia tri quy uoc cua <Select>: component chi nhan string, con bo loc dung
   `null`/`boolean`/`number`. Doi o RIA, khong cho chung ro ri vao state. */
const ANY = "__any";
const NO_SERIES = "__none";

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1 block text-fs-11 font-bold uppercase tracking-[0.1em] text-muted-fg">
        {label}
      </span>
      {children}
    </label>
  );
}

export function AdminFilterPopover({
  open,
  onOpenChange,
  filter,
  onChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  filter: AdminGameFilter;
  onChange: (filter: AdminGameFilter) => void;
}) {
  const { t } = useI18n();
  const active = countAdminFilters(filter);

  const series = useQuery({
    queryKey: ["admin-metadata-series"],
    queryFn: () => adminApi.listSeries({ limit: 200 }),
  });

  /* Nam co ban ghi lay tu `byPublishYear` cua dashboard — SERVER tinh tren toan
     kho. Suy tu trang dang xem se thieu nam ngay khi kho vuot mot trang. Cung
     khoa cache voi dashboard nen khong ton them mot lan goi. */
  const stats = useQuery({
    queryKey: ["admin-metadata-stats"],
    queryFn: () => adminApi.stats(),
  });
  const years = (stats.data?.byPublishYear ?? [])
    .map((point) => Number(point.label))
    .filter((year) => Number.isFinite(year))
    .sort((a, b) => b - a);

  const patch = (next: Partial<AdminGameFilter>) => onChange({ ...filter, ...next });

  return (
    <div className="relative shrink-0">
      <Popover open={open} onOpenChange={onOpenChange}>
        <PopoverTrigger asChild>
          <IconButton
            label={active > 0 ? `${t("action.filter")} (${active})` : t("action.filter")}
            tone="filter"
            icon={<Filter />}
          />
        </PopoverTrigger>
        <PopoverContent align="end" className="w-72 space-y-3">
          <div className="flex items-center justify-between gap-2 px-0.5">
            <span className="text-xs font-bold uppercase tracking-wide text-muted-fg">
              {t("action.filter")}
            </span>
            <IconButton
              size="compact"
              label={t("action.reset")}
              icon={<RotateCcw />}
              disabled={active === 0}
              onClick={() => onChange(EMPTY_ADMIN_FILTER)}
            />
          </div>

          <Field label={t("admin.filter.recordType")}>
            <Select
              value={filter.isDlc === null ? ANY : String(filter.isDlc)}
              onValueChange={(value) =>
                patch({ isDlc: value === ANY ? null : value === "true" })
              }
            >
              <SelectTrigger className="h-9 w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ANY}>{t("admin.filter.any")}</SelectItem>
                <SelectItem value="false">{t("admin.filter.baseGames")}</SelectItem>
                <SelectItem value="true">{t("admin.filter.dlcOnly")}</SelectItem>
              </SelectContent>
            </Select>
          </Field>

          <Field label={t("admin.game.series")}>
            <Select
              value={filter.noSeries ? NO_SERIES : filter.seriesId === null ? ANY : String(filter.seriesId)}
              onValueChange={(value) =>
                patch(
                  value === ANY
                    ? { seriesId: null, noSeries: false }
                    : value === NO_SERIES
                      ? { seriesId: null, noSeries: true }
                      : { seriesId: Number(value), noSeries: false },
                )
              }
            >
              <SelectTrigger className="h-9 w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ANY}>{t("admin.filter.any")}</SelectItem>
                <SelectItem value={NO_SERIES}>{t("admin.game.noSeries")}</SelectItem>
                {(series.data ?? []).map((item) => (
                  <SelectItem key={item.id} value={String(item.id)}>
                    {item.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </Field>

          <Field label={t("admin.filter.year")}>
            <Select
              value={filter.year === null ? ANY : String(filter.year)}
              onValueChange={(value) => patch({ year: value === ANY ? null : Number(value) })}
            >
              <SelectTrigger className="h-9 w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ANY}>{t("admin.filter.any")}</SelectItem>
                {years.map((year) => (
                  <SelectItem key={year} value={String(year)}>
                    {year}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </Field>

          <Field label={t("admin.filter.published")}>
            <Select
              value={filter.published === null ? ANY : String(filter.published)}
              onValueChange={(value) =>
                patch({ published: value === ANY ? null : value === "true" })
              }
            >
              <SelectTrigger className="h-9 w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ANY}>{t("admin.filter.any")}</SelectItem>
                <SelectItem value="true">{t("admin.filter.publishedOnly")}</SelectItem>
                <SelectItem value="false">{t("admin.filter.unpublishedOnly")}</SelectItem>
              </SelectContent>
            </Select>
          </Field>

          <Field label={t("admin.filter.missing")}>
            <Select
              value={filter.missing ?? ANY}
              onValueChange={(value) => patch({ missing: value === ANY ? null : value })}
            >
              <SelectTrigger className="h-9 w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ANY}>{t("admin.filter.any")}</SelectItem>
                {MISSING_KEYS.map((key) => (
                  <SelectItem key={key} value={key}>
                    {t(adminHealthLabelKey(key))}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </Field>
        </PopoverContent>
      </Popover>

      {active > 0 ? (
        <span
          className={cn(
            "pointer-events-none absolute -right-1 -top-1 z-10 grid min-h-4 min-w-4 place-items-center",
            "rounded-full bg-primary px-1 text-fs-9 font-black leading-none text-primary-fg shadow-sm ring-1 ring-bg",
          )}
        >
          {active}
        </span>
      ) : null}
    </div>
  );
}
