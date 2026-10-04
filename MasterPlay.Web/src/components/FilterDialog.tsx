import { useEffect, useState } from "react";
import { Check, Filter as FilterIcon, RotateCcw, X } from "lucide-react";
import { cn } from "@/lib/utils";
import { useI18n } from "@/lib/i18n";
import {
  AVG_BANDS,
  COMPARE_OPS,
  COMPARE_OP_SYMBOL,
  META_BANDS,
  type AvgBand,
  type CompareOp,
  type MetaBand,
} from "@/lib/api/domain";
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
            <div className="space-y-2">
              {numericRows.map((row) => (
                <div
                  key={row.label}
                  className="grid grid-cols-[minmax(0,1fr)_4rem_6.5rem] items-center gap-2 rounded-xl border border-border/70 bg-surface-2/25 px-3 py-2 sm:grid-cols-[10rem_4rem_1fr]"
                >
                  <span className="min-w-0 text-sm font-semibold text-fg">{row.label}</span>
                  <OpSelect value={row.op} onChange={row.setOp} />
                  <TextInput
                    value={row.val}
                    onChange={(e) => row.setVal(e.target.value)}
                    inputMode="decimal"
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
