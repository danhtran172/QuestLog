/**
 * Sap xep cua Admin Console — spec §12.1.
 *
 * Chi sau khoa thuan metadata. Bo `default`, `status`, `playTime`, `finishTime`,
 * `excited`, `score`, `ownership`, `progress`: tat ca deu la userdata, khong ton
 * tai tren ban ghi metadata.
 *
 * Sap xep chay o SERVER. Component nay chi giu lua chon — AGENTS.md §2 cam cai
 * dat logic sort o client. Lua chon la state cua PHIEN, khong ghi `/api/settings`
 * va khong dung view sort profile: profile gan voi view cua thu vien.
 *
 * Lay dung mat cua cum sort ben thu vien (App.tsx): bang bao huong + khoa o ben
 * trai, nut mo popover de len.
 */
import { ArrowDownWideNarrow, ArrowUpDown, ArrowUpNarrowWide, Check, RotateCcw } from "lucide-react";
import {
  ADMIN_SORT_KEYS,
  DEFAULT_ADMIN_SORT,
  type AdminSortKey,
} from "@/lib/adminApi";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { IconButton, Popover, PopoverContent, PopoverTrigger, Tooltip } from "./ui";

export function AdminSortPopover({
  open,
  onOpenChange,
  sort,
  descending,
  onChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  sort: AdminSortKey;
  descending: boolean;
  onChange: (sort: AdminSortKey, descending: boolean) => void;
}) {
  const { t } = useI18n();
  /* `id desc` la mac dinh — ban ghi moi nhat len dau, dung voi viec bien tap. */
  const isDefault = sort === DEFAULT_ADMIN_SORT && descending;

  return (
    <div className="flex shrink-0 items-center">
      {!isDefault ? (
        <Tooltip
          label={`${t("action.sort")}: ${t(`admin.sort.${sort}`)} · ${descending ? t("sort.desc") : t("sort.asc")}`}
          side="bottom"
        >
          {/* Canh phai VUONG va thut vao duoi nut tron ben canh — giong cum sort
              cua thu vien, de hai cho khong trong nhu hai he thong khac nhau. */}
          <span className="stone-slab -mr-4 flex h-9 shrink-0 items-center gap-1.5 rounded-l-xl rounded-r-none !border-r-0 py-0 pl-3 pr-6 text-xs font-bold text-fg">
            {descending ? (
              <ArrowDownWideNarrow size={14} className="shrink-0 text-primary" />
            ) : (
              <ArrowUpNarrowWide size={14} className="shrink-0 text-primary" />
            )}
            <span className="truncate">{t(`admin.sort.${sort}`)}</span>
          </span>
        </Tooltip>
      ) : null}

      <Popover open={open} onOpenChange={onOpenChange}>
        <PopoverTrigger asChild>
          <IconButton
            label={`${t("action.sort")}: ${t(`admin.sort.${sort}`)}`}
            tone="edit"
            icon={<ArrowUpDown />}
            className="relative z-10"
          />
        </PopoverTrigger>
        <PopoverContent align="end" className="w-56">
          <div className="mb-2 rounded-lg border border-primary/20 bg-primary/10 px-2.5 py-2">
            <p className="text-[11px] font-semibold text-muted-fg">{t("sort.current")}</p>
            <div className="mt-0.5 flex items-center justify-between gap-2">
              <span className="min-w-0 truncate text-sm font-bold text-fg">{t(`admin.sort.${sort}`)}</span>
              <IconButton
                size="compact"
                label={descending ? t("sort.desc") : t("sort.asc")}
                icon={descending ? <ArrowDownWideNarrow /> : <ArrowUpNarrowWide />}
                onClick={() => onChange(sort, !descending)}
              />
            </div>
          </div>
          <div className="mb-1 flex items-center justify-between gap-2 px-1.5">
            <span className="text-xs font-semibold text-muted-fg">{t("sort.choose")}</span>
            <div className="flex items-center gap-1">
              <IconButton
                size="compact"
                label={t("action.clearSort")}
                icon={<RotateCcw />}
                disabled={isDefault}
                onClick={() => {
                  onChange(DEFAULT_ADMIN_SORT, true);
                  onOpenChange(false);
                }}
              />
            </div>
          </div>
          <div className="space-y-0.5">
            {ADMIN_SORT_KEYS.map((key) => (
              <button
                key={key}
                type="button"
                aria-current={sort === key ? "true" : undefined}
                onClick={() => {
                  onChange(key, descending);
                  onOpenChange(false);
                }}
                className={cn(
                  "flex w-full items-center justify-between rounded-lg px-2.5 py-2 text-sm font-semibold outline-none transition-colors focus-visible:ring-2 focus-visible:ring-primary",
                  sort === key
                    ? "bg-primary/18 text-fg"
                    : "text-muted-fg hover:bg-surface-2/70 hover:text-fg",
                )}
              >
                <span>{t(`admin.sort.${key}`)}</span>
                {sort === key ? <Check size={15} className="text-primary" aria-hidden="true" /> : null}
              </button>
            ))}
          </div>
        </PopoverContent>
      </Popover>
    </div>
  );
}
