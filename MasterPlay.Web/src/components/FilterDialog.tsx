import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Check, Filter as FilterIcon, Loader2, RotateCcw, Search, X } from "lucide-react";
import { cn, iconSrc } from "@/lib/utils";
import { useI18n } from "@/lib/i18n";
import {
  AVG_BANDS,
  COMPARE_OPS,
  COMPARE_OP_SYMBOL,
  META_BANDS,
  type AvgBand,
  type CompareOp,
  type MetaBand,
  type TaxonomyItem,
} from "@/lib/api/domain";
import { api } from "@/lib/api/client";
import { groupedThemesKey, taxonomyApi } from "@/lib/taxonomyApi";
import { playtimeHoursToSeconds } from "@/lib/format";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  IconButton,
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
  Switch,
  TextInput,
  Tooltip,
} from "./ui";

/**
 * Trang thai filter — CHI la state UI.
 * Moi nguong/phep so sanh deu chay o server (DOMAIN_RULES §5). Khong tinh o day.
 */
export type FilterState = {
  finishedYearOp: CompareOp;
  finishedYearValue: string;
  excitedOp: CompareOp;
  excitedValue: string;
  scoreOp: CompareOp;
  scoreValue: string;
  playtimeOp: CompareOp;
  playtimeHours: string;
  avgBands: AvgBand[];
  includeAvgNone: boolean;
  metaBands: MetaBand[];
  includeMetaNone: boolean;
  /** `systemKey` lay tu catalog — khong dung id (DOMAIN_RULES §5.8). */
  progressKeys: string[];
  ownershipKeys: string[];
  genreIds: number[];
  themeIds: number[];
  reverseFilter: boolean;
};

export const EMPTY_FILTER: FilterState = {
  finishedYearOp: "eq",
  finishedYearValue: "",
  excitedOp: "eq",
  excitedValue: "",
  scoreOp: "eq",
  scoreValue: "",
  playtimeOp: "eq",
  playtimeHours: "",
  avgBands: [],
  includeAvgNone: false,
  metaBands: [],
  includeMetaNone: false,
  progressKeys: [],
  ownershipKeys: [],
  genreIds: [],
  themeIds: [],
  reverseFilter: false,
};

/** Quy doi state UI -> tham so query gui len server. */
export function filterToQuery(f: FilterState) {
  const num = (s: string) => (s.trim() === "" ? undefined : Number(s));
  const hours = num(f.playtimeHours);
  return {
    finishedYearOp: f.finishedYearValue ? f.finishedYearOp : undefined,
    finishedYearValue: num(f.finishedYearValue),
    excitedOp: f.excitedValue ? f.excitedOp : undefined,
    excitedValue: num(f.excitedValue),
    scoreOp: f.scoreValue ? f.scoreOp : undefined,
    scoreValue: num(f.scoreValue),
    playtimeOp: f.playtimeHours ? f.playtimeOp : undefined,
    // Nguoi dung nhap GIO -> nhan 3600 TRUOC khi gui (DOMAIN_RULES §5.3).
    playtimeSeconds: hours == null ? undefined : playtimeHoursToSeconds(hours),
    avgBands: f.avgBands.length ? f.avgBands : undefined,
    includeAvgNone: f.includeAvgNone || undefined,
    metaBands: f.metaBands.length ? f.metaBands : undefined,
    includeMetaNone: f.includeMetaNone || undefined,
    progressKeys: f.progressKeys.length ? f.progressKeys : undefined,
    ownershipKeys: f.ownershipKeys.length ? f.ownershipKeys : undefined,
    genreIds: f.genreIds.length ? f.genreIds : undefined,
    themeIds: f.themeIds.length ? f.themeIds : undefined,
    reverseFilter: f.reverseFilter || undefined,
  };
}

function OpSelect({ value, onChange }: { value: CompareOp; onChange: (v: CompareOp) => void }) {
  const { t } = useI18n();
  return (
    <Select value={value} onValueChange={(next) => onChange(next as CompareOp)}>
      <SelectTrigger
        aria-label={t("filter.operator")}
        className="stone-slab h-8 w-16 shrink-0 rounded-lg px-2.5 font-mono font-bold"
      >
        <SelectValue />
      </SelectTrigger>
      <SelectContent className="min-w-20 rounded-xl">
        {COMPARE_OPS.map((op) => (
          <SelectItem key={op} value={op} className="font-mono font-bold">
            {COMPARE_OP_SYMBOL[op]}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

function Chip({
  active,
  onClick,
  children,
}: {
  active: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        "min-h-8 rounded-full border px-2.5 py-1 text-xs font-semibold outline-none transition-colors focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2 focus-visible:ring-offset-bg",
        active
          ? "border-primary/55 bg-primary/20 text-fg"
          : "border-border bg-surface-2/35 text-muted-fg hover:border-primary/35 hover:text-fg",
      )}
    >
      {children}
    </button>
  );
}

function FilterSection({
  title,
  description,
  children,
}: {
  title: string;
  description?: string;
  children: React.ReactNode;
}) {
  return (
    <section className="space-y-3">
      <div className="border-l-2 border-primary/55 pl-3">
        <h3 className="text-sm font-bold text-fg">{title}</h3>
        {description ? <p className="mt-0.5 text-xs text-muted-fg">{description}</p> : null}
      </div>
      {children}
    </section>
  );
}

/** Loi API phai hien ra, khong nuot (AGENTS.md §2). */
function QueryError({ error }: { error: unknown }) {
  if (!error) return null;
  return (
    <p className="rounded-lg bg-danger/10 p-2 text-xs text-danger">
      {error instanceof Error ? error.message : String(error)}
    </p>
  );
}

function ChipIcon({ iconData, colorCode }: { iconData?: string | null; colorCode?: string }) {
  const src = iconSrc(iconData);
  if (src) return <img src={src} alt="" className="h-3.5 w-3.5 object-contain" />;
  if (colorCode) return <span aria-hidden className="h-2 w-2 rounded-full" style={{ backgroundColor: colorCode }} />;
  return null;
}

/** Chip Progress / Ownership — ten, mau, icon va thu tu deu lay tu catalog API. */
function CatalogChips({
  kind,
  label,
  value,
  onChange,
}: {
  kind: "progress" | "ownership";
  label: string;
  value: string[];
  onChange: (next: string[]) => void;
}) {
  const catalog = useQuery({ queryKey: ["catalog", kind], queryFn: () => api.catalog(kind) });

  return (
    <div className="space-y-2">
      <p className="text-xs font-semibold text-fg">{label}</p>
      <QueryError error={catalog.error} />
      <div className="flex flex-wrap gap-1.5">
        {catalog.isPending ? <Loader2 size={16} className="animate-spin text-muted-fg" /> : null}
        {catalog.data?.map((item) => (
          <Chip
            key={item.systemKey}
            active={value.includes(item.systemKey)}
            onClick={() =>
              onChange(
                value.includes(item.systemKey)
                  ? value.filter((key) => key !== item.systemKey)
                  : [...value, item.systemKey],
              )
            }
          >
            <span className="inline-flex items-center gap-1.5">
              <ChipIcon iconData={item.iconData} colorCode={item.colorCode} />
              {item.displayName}
            </span>
          </Chip>
        ))}
      </div>
    </div>
  );
}

/**
 * Genre + Theme. Tim kiem chay o server (`?q=`), nhom theme do server sap xep —
 * client chi render. Quan he OR giua hai nhom cung do server ap dung (§5.7).
 */
function TaxonomyChips({
  genreIds,
  themeIds,
  onChange,
}: {
  genreIds: number[];
  themeIds: number[];
  onChange: (next: { genreIds: number[]; themeIds: number[] }) => void;
}) {
  const { t } = useI18n();
  const [search, setSearch] = useState("");
  const [term, setTerm] = useState("");

  useEffect(() => {
    const timer = window.setTimeout(() => setTerm(search.trim()), 240);
    return () => window.clearTimeout(timer);
  }, [search]);

  const genres = useQuery({
    queryKey: ["taxonomy", "genres", term],
    queryFn: () => api.taxonomy("genres", term || undefined),
  });
  const themes = useQuery({
    queryKey: groupedThemesKey("library", term),
    queryFn: () => taxonomyApi.groupedThemes("library", term || undefined),
  });
  // Ten cua muc dang chon khi o tim kiem dang an no — tra cuu tu danh sach day du.
  const allGenres = useQuery({
    queryKey: ["taxonomy", "genres", ""],
    queryFn: () => api.taxonomy("genres"),
    enabled: genreIds.length > 0,
  });
  const allThemes = useQuery({
    queryKey: groupedThemesKey("library", ""),
    queryFn: () => taxonomyApi.groupedThemes("library"),
    enabled: themeIds.length > 0,
  });

  const selected = useMemo(() => {
    const genreItems = (allGenres.data ?? []).filter((item) => genreIds.includes(item.id));
    const themeItems = (allThemes.data?.groups ?? [])
      .flatMap((bucket) => bucket.themes)
      .filter((item) => themeIds.includes(item.id));
    return [
      ...genreItems.map((item) => ({ kind: "genre" as const, item })),
      ...themeItems.map((item) => ({ kind: "theme" as const, item })),
    ];
  }, [allGenres.data, allThemes.data, genreIds, themeIds]);

  const toggleGenre = (id: number) =>
    onChange({
      genreIds: genreIds.includes(id) ? genreIds.filter((x) => x !== id) : [...genreIds, id],
      themeIds,
    });
  const toggleTheme = (id: number) =>
    onChange({
      genreIds,
      themeIds: themeIds.includes(id) ? themeIds.filter((x) => x !== id) : [...themeIds, id],
    });

  const renderChip = (item: TaxonomyItem, active: boolean, onClick: () => void) => (
    <Chip key={item.id} active={active} onClick={onClick}>
      <span className="inline-flex items-center gap-1.5">
        <ChipIcon iconData={item.iconData} />
        {item.name}
      </span>
    </Chip>
  );

  const buckets = (themes.data?.groups ?? []).filter((bucket) => bucket.themes.length > 0);
  const nothingFound =
    !genres.isPending && !themes.isPending && (genres.data?.length ?? 0) === 0 && buckets.length === 0;

  return (
    <div className="space-y-4 rounded-xl border border-border/70 bg-surface-2/25 p-3">
      <div className="relative">
        <Search size={14} className="pointer-events-none absolute left-2.5 top-1/2 -translate-y-1/2 text-muted-fg" />
        <TextInput
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          aria-label={t("filter.taxonomySearch")}
          placeholder={t("filter.taxonomySearch")}
          className="h-8 pl-8"
        />
      </div>

      {selected.length > 0 ? (
        <div className="flex flex-wrap items-center gap-1.5">
          <span className="text-xs font-semibold text-muted-fg">{t("filter.selected")}</span>
          {selected.map(({ kind, item }) => (
            <button
              key={`${kind}-${item.id}`}
              type="button"
              aria-label={`${t("action.remove")} ${item.name}`}
              onClick={() => (kind === "genre" ? toggleGenre(item.id) : toggleTheme(item.id))}
              className="inline-flex items-center gap-1 rounded-md border border-primary/45 bg-primary/15 px-1.5 py-0.5 text-xs font-semibold text-fg"
            >
              {item.name}
              <X size={11} className="opacity-60" />
            </button>
          ))}
        </div>
      ) : null}

      <QueryError error={genres.error ?? themes.error ?? allGenres.error ?? allThemes.error} />

      {nothingFound ? <p className="text-xs text-muted-fg">{t("editor.taxonomyNoMatch")}</p> : null}

      {genres.isPending || (genres.data?.length ?? 0) > 0 ? (
        <div className="space-y-2">
          <p className="text-xs font-semibold text-fg">{t("filter.genres")}</p>
          <div className="flex flex-wrap gap-1.5">
            {genres.isPending ? <Loader2 size={16} className="animate-spin text-muted-fg" /> : null}
            {genres.data?.map((item) => renderChip(item, genreIds.includes(item.id), () => toggleGenre(item.id)))}
          </div>
        </div>
      ) : null}

      {themes.isPending || buckets.length > 0 ? (
        <div className="space-y-3 border-t border-border/65 pt-3">
          <p className="text-xs font-semibold text-fg">{t("filter.themes")}</p>
          {themes.isPending ? <Loader2 size={16} className="animate-spin text-muted-fg" /> : null}
          {buckets.map((bucket) => (
            <div key={bucket.group?.id ?? "ungrouped"} className="space-y-1.5">
              <p className="text-fs-11 font-bold uppercase tracking-wide text-muted-fg">
                {bucket.group?.displayName ?? t("themes.ungrouped")}
              </p>
              <div className="flex flex-wrap gap-1.5">
                {bucket.themes.map((item) => renderChip(item, themeIds.includes(item.id), () => toggleTheme(item.id)))}
              </div>
            </div>
          ))}
        </div>
      ) : null}
    </div>
  );
}

export function FilterDialog({
  open,
  onOpenChange,
  value,
  onApply,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
  value: FilterState;
  onApply: (f: FilterState) => void;
}) {
  const { t } = useI18n();
  const [draft, setDraft] = useState<FilterState>(value);

  useEffect(() => {
    if (open) setDraft(value);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const set = <K extends keyof FilterState>(k: K, v: FilterState[K]) =>
    setDraft((d) => ({ ...d, [k]: v }));

  const toggle = <T,>(list: T[], item: T): T[] =>
    list.includes(item) ? list.filter((x) => x !== item) : [...list, item];

  const numericRows: {
    label: string;
    op: CompareOp;
    val: string;
    setOp: (v: CompareOp) => void;
    setVal: (v: string) => void;
    hint?: string;
  }[] = [
    {
      label: t("filter.finishedYear"),
      op: draft.finishedYearOp,
      val: draft.finishedYearValue,
      setOp: (v) => set("finishedYearOp", v),
      setVal: (v) => set("finishedYearValue", v),
    },
    {
      label: t("filter.excited"),
      op: draft.excitedOp,
      val: draft.excitedValue,
      setOp: (v) => set("excitedOp", v),
      setVal: (v) => set("excitedValue", v),
      hint: "1–10",
    },
    {
      label: t("filter.score"),
      op: draft.scoreOp,
      val: draft.scoreValue,
      setOp: (v) => set("scoreOp", v),
      setVal: (v) => set("scoreValue", v),
      hint: "0–10",
    },
    {
      label: t("filter.playtime"),
      op: draft.playtimeOp,
      val: draft.playtimeHours,
      setOp: (v) => set("playtimeOp", v),
      setVal: (v) => set("playtimeHours", v),
      hint: t("filter.hours"),
    },
  ];

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl gap-0 overflow-hidden p-0">
        <DialogHeader className="flex-row items-center justify-between gap-4 border-b border-border px-5 py-4">
          <div className="min-w-0">
            <DialogTitle className="flex items-center gap-2">
              <FilterIcon size={17} /> {t("action.filter")}
            </DialogTitle>
            <DialogDescription>{t("filter.hint")}</DialogDescription>
          </div>
          <div className="stone-slab flex shrink-0 items-center gap-2 rounded-lg px-2.5 py-1.5">
            <span className="text-xs font-semibold text-fg">{t("filter.reverse")}</span>
            <Switch
              checked={draft.reverseFilter}
              aria-label={t("filter.reverse")}
              onCheckedChange={(v) => set("reverseFilter", v)}
            />
          </div>
        </DialogHeader>

        <div className="scrollbar-thin max-h-[64vh] space-y-7 overflow-y-auto p-5">
          <FilterSection title={t("filter.numericConditions")} description={t("filter.numericHint")}>
            {/* Gia tri toi da 4-5 ky tu (nam, diem, gio) nen o nhap hep co dinh
                va xep 2 cot tu sm tro len cho gon. */}
            <div className="grid gap-2 sm:grid-cols-2">
              {numericRows.map((row) => (
                <div
                  key={row.label}
                  className="grid grid-cols-[minmax(0,1fr)_4rem_5.5rem] items-center gap-2 rounded-xl border border-border/70 bg-surface-2/25 px-3 py-2"
                >
                  <span className="min-w-0 text-sm font-semibold text-fg">{row.label}</span>
                  <OpSelect value={row.op} onChange={row.setOp} />
                  <TextInput
                    value={row.val}
                    onChange={(e) => row.setVal(e.target.value)}
                    inputMode="decimal"
                    maxLength={6}
                    aria-label={row.label}
                    placeholder={row.hint ?? ""}
                    className="h-8 min-w-0 px-2.5"
                  />
                </div>
              ))}
            </div>
          </FilterSection>

          <FilterSection title={t("filter.metadata")} description={t("filter.metadataHint")}>
            <div className="space-y-4 rounded-xl border border-border/70 bg-surface-2/25 p-3">
              <div className="space-y-2">
                <p className="text-xs font-semibold text-fg">{t("filter.avgLength")}</p>
                <div className="flex flex-wrap gap-1.5">
                  {AVG_BANDS.map((b) => (
                    <Tooltip key={b} label={t(`filter.avgRange.${b}`)}>
                      <Chip
                        active={draft.avgBands.includes(b)}
                        onClick={() => set("avgBands", toggle(draft.avgBands, b))}
                      >
                        {t(`avg.${b.charAt(0).toLowerCase() + b.slice(1)}`)}
                      </Chip>
                    </Tooltip>
                  ))}
                  {/* "None" la CO RIENG, khong gop vao mang chip (DOMAIN_RULES §5.4) */}
                  <Tooltip label={t("filter.avgRange.none")}>
                    <Chip
                      active={draft.includeAvgNone}
                      onClick={() => set("includeAvgNone", !draft.includeAvgNone)}
                    >
                      {t("avg.none")}
                    </Chip>
                  </Tooltip>
                </div>
              </div>

              <div className="space-y-2 border-t border-border/65 pt-3">
                <p className="text-xs font-semibold text-fg">MetaScore</p>
                <div className="flex flex-wrap gap-1.5">
                  {META_BANDS.map((b) => (
                    <Tooltip key={b} label={t(`filter.metaRange.${b}`)}>
                      <Chip
                        active={draft.metaBands.includes(b)}
                        onClick={() => set("metaBands", toggle(draft.metaBands, b))}
                      >
                        {t(`meta.${b.charAt(0).toLowerCase() + b.slice(1)}`)}
                      </Chip>
                    </Tooltip>
                  ))}
                  <Tooltip label={t("filter.metaRange.none")}>
                    <Chip
                      active={draft.includeMetaNone}
                      onClick={() => set("includeMetaNone", !draft.includeMetaNone)}
                    >
                      {t("avg.none")}
                    </Chip>
                  </Tooltip>
                </div>
              </div>
            </div>
          </FilterSection>

          <FilterSection title={t("filter.status")} description={t("filter.statusHint")}>
            <div className="space-y-4 rounded-xl border border-border/70 bg-surface-2/25 p-3">
              <CatalogChips
                kind="progress"
                label={t("filter.progress")}
                value={draft.progressKeys}
                onChange={(next) => set("progressKeys", next)}
              />
              <div className="border-t border-border/65 pt-3">
                <CatalogChips
                  kind="ownership"
                  label={t("filter.ownership")}
                  value={draft.ownershipKeys}
                  onChange={(next) => set("ownershipKeys", next)}
                />
              </div>
            </div>
          </FilterSection>

          <FilterSection title={t("filter.taxonomy")} description={t("filter.taxonomyHint")}>
            <TaxonomyChips
              genreIds={draft.genreIds}
              themeIds={draft.themeIds}
              onChange={(next) => setDraft((d) => ({ ...d, ...next }))}
            />
          </FilterSection>
        </div>

        <DialogFooter className="justify-between">
          <IconButton
            label={t("action.clear")}
            icon={<RotateCcw />}
            onClick={() => setDraft(EMPTY_FILTER)}
          />
          <div className="flex items-center gap-2">
            <IconButton
              label={t("action.cancel")}
              icon={<X />}
              onClick={() => onOpenChange(false)}
            />
            <IconButton
              tone="add"
              label={t("action.apply")}
              icon={<Check />}
              onClick={() => {
                onApply(draft);
                onOpenChange(false);
              }}
            />
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
