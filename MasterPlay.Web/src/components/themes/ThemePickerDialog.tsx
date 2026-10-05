import { useEffect, useMemo, useRef, useState, type KeyboardEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Check, Loader2, Plus, Search, X } from "lucide-react";
import type { TaxonomyItem, ThemeGroupBucket } from "@/lib/api/domain";
import { groupedThemesKey, taxonomyApi } from "@/lib/taxonomyApi";
import { useI18n } from "@/lib/i18n";
import { cn, iconSrc } from "@/lib/utils";
import { Dialog, DialogContent, DialogDescription, DialogTitle, IconButton } from "../ui";

/**
 * Bang chon theme cho MOT game — spec 2026-10-04-theme-groups §6.4.
 *
 * Luoi theo nhom, thu tu nhom/theme do server tra (client khong sap). Tim kiem
 * goi server (`grouped?q=`), khong loc o client. Chon/bo trong ban nhap; "Luu"
 * tra ve danh sach moi cho noi goi — noi goi tu lo viec ghi (PUT game), giu
 * nguyen luong luu san co cua no.
 */
export function ThemePickerDialog({
  open,
  onOpenChange,
  title,
  selected,
  saving = false,
  onSave,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  /** Theme dang gan cho game luc mo dialog. */
  selected: TaxonomyItem[];
  saving?: boolean;
  onSave: (next: TaxonomyItem[]) => void;
}) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [query, setQuery] = useState("");
  const [draft, setDraft] = useState<TaxonomyItem[]>(selected);
  const [confirmDiscard, setConfirmDiscard] = useState(false);
  const gridRef = useRef<HTMLDivElement>(null);

  /* Moi lan mo lai bat dau tu gia tri dang luu, khong giu ban nhap cu. */
  useEffect(() => {
    if (!open) return;
    setDraft(selected);
    setSearch("");
    setQuery("");
    setConfirmDiscard(false);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  useEffect(() => {
    const timer = window.setTimeout(() => setQuery(search.trim()), 200);
    return () => window.clearTimeout(timer);
  }, [search]);

  const grouped = useQuery({
    queryKey: groupedThemesKey("library", query),
    queryFn: () => taxonomyApi.groupedThemes("library", query || undefined),
    enabled: open,
    placeholderData: (previous) => previous,
  });

  const create = useMutation({
    mutationFn: (name: string) => taxonomyApi.create("library", "themes", { name }),
    onSuccess: async (created) => {
      setDraft((current) => (current.some((item) => item.id === created.id) ? current : [...current, created]));
      setSearch("");
      setQuery("");
      await queryClient.invalidateQueries({ queryKey: ["taxonomy"] });
    },
  });

  const draftIds = useMemo(() => new Set(draft.map((item) => item.id)), [draft]);
  const dirty = useMemo(() => {
    if (draft.length !== selected.length) return true;
    const initial = new Set(selected.map((item) => item.id));
    return draft.some((item) => !initial.has(item.id));
  }, [draft, selected]);

  const buckets = grouped.data?.groups ?? [];
  const term = search.trim();
  /* "Tao moi" chi khi server khong tra ve theme nao TRUNG KHOP ten (khoa do
     server tinh). So khop chinh xac, khong phai loc. */
  const exactMatch = buckets.some((bucket) =>
    bucket.themes.some((item) => item.key === term.toLowerCase().replace(/\s+/g, " ")),
  );
  const canCreate = term.length > 0 && query === term && !grouped.isFetching && !exactMatch;

  const toggle = (item: TaxonomyItem) =>
    setDraft((current) =>
      current.some((value) => value.id === item.id)
        ? current.filter((value) => value.id !== item.id)
        : [...current, item],
    );

  const requestClose = () => {
    if (saving) return;
    if (dirty) setConfirmDiscard(true);
    else onOpenChange(false);
  };

  /* Mui ten di chuyen giua cac o trong toan bo luoi (qua ca ranh gioi nhom). */
  const onGridKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    const keys = ["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown"];
    if (!keys.includes(event.key)) return;
    const tiles = Array.from(gridRef.current?.querySelectorAll<HTMLButtonElement>("[data-theme-tile]") ?? []);
    const index = tiles.indexOf(document.activeElement as HTMLButtonElement);
    if (index < 0) return;
    event.preventDefault();
    const current = tiles[index].getBoundingClientRect();
    if (event.key === "ArrowLeft") return tiles[Math.max(0, index - 1)]?.focus();
    if (event.key === "ArrowRight") return tiles[Math.min(tiles.length - 1, index + 1)]?.focus();
    // Len/xuong: o gan nhat theo chieu ngang o hang ke tiep.
    const down = event.key === "ArrowDown";
    const candidates = tiles.filter((tile) => {
      const rect = tile.getBoundingClientRect();
      return down ? rect.top > current.bottom - 4 : rect.bottom < current.top + 4;
    });
    if (!candidates.length) return;
    const rowTop = down
      ? Math.min(...candidates.map((tile) => tile.getBoundingClientRect().top))
      : Math.max(...candidates.map((tile) => tile.getBoundingClientRect().top));
    const row = candidates.filter((tile) => Math.abs(tile.getBoundingClientRect().top - rowTop) < 4);
    row.sort((a, b) =>
      Math.abs(a.getBoundingClientRect().left - current.left) - Math.abs(b.getBoundingClientRect().left - current.left));
    row[0]?.focus();
  };

  const error = grouped.error ?? create.error;

  return (
    <Dialog open={open} onOpenChange={(next) => (next ? onOpenChange(true) : requestClose())}>
      <DialogContent
        className="flex max-h-[min(86vh,820px)] max-w-[min(960px,94vw)] flex-col overflow-hidden p-0"
        /* Dialog nay mo BEN TRONG the chi tiet: chan lan chuot noi bot len
           `onWheel` "lat trang" cua the (su kien React di theo cay component). */
        onWheel={(event) => event.stopPropagation()}
        onEscapeKeyDown={(event) => {
          event.preventDefault();
          requestClose();
        }}
      >
        <header className="flex flex-wrap items-center gap-3 border-b border-border px-5 py-3.5">
          <div className="min-w-0 flex-1">
            <DialogTitle>{title}</DialogTitle>
            <DialogDescription className="sr-only">{t("themes.pickerHint")}</DialogDescription>
          </div>
          <div className="relative w-full sm:w-64">
            <Search size={14} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted-fg" />
            <input
              autoFocus
              aria-label={t("editor.taxonomySearch")}
              placeholder={t("editor.taxonomySearch")}
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === "Enter" && canCreate && !create.isPending) create.mutate(term);
                if (event.key === "ArrowDown") {
                  event.preventDefault();
                  gridRef.current?.querySelector<HTMLButtonElement>("[data-theme-tile]")?.focus();
                }
              }}
              className={cn(
                "h-9 w-full rounded-xl border border-border/80 bg-surface/55 pl-9 pr-3 text-sm font-medium text-fg",
                "outline-none transition focus:border-primary/80 focus:ring-2 focus:ring-primary/20",
              )}
            />
          </div>
          <IconButton size="compact" label={t("action.close")} icon={<X />} onClick={requestClose} />
        </header>

        <div
          ref={gridRef}
          onKeyDown={onGridKeyDown}
          className="scrollbar-thin min-h-[240px] flex-1 overflow-y-auto overscroll-contain px-5 pb-5"
        >
          {error ? (
            <p role="alert" className="mt-4 rounded-lg bg-danger/10 p-3 text-sm text-danger">
              {error instanceof Error ? error.message : String(error)}
            </p>
          ) : null}

          {grouped.isPending ? (
            <Loader2 size={20} className="mx-auto my-16 animate-spin text-muted-fg" />
          ) : buckets.length === 0 ? (
            <p className="py-16 text-center text-sm text-muted-fg">{t("editor.taxonomyNoMatch")}</p>
          ) : (
            buckets.map((bucket) => (
              <ThemeBucketSection
                key={bucket.group?.id ?? "ungrouped"}
                bucket={bucket}
                ungroupedLabel={t("themes.ungrouped")}
                selectedIds={draftIds}
                onToggle={toggle}
              />
            ))
          )}

          {canCreate ? (
            <button
              type="button"
              disabled={create.isPending}
              className="mt-5 flex items-center gap-2 rounded-lg border border-primary/25 bg-primary/10 px-3 py-2 text-sm font-bold text-primary disabled:opacity-60"
              onClick={() => create.mutate(term)}
            >
              {create.isPending ? <Loader2 size={14} className="animate-spin" /> : <Plus size={14} />}
              {t("editor.taxonomyCreate")} “{term}”
            </button>
          ) : null}
        </div>

        <footer className="flex flex-wrap items-center gap-3 border-t border-border px-5 py-3">
          <p className="min-w-0 flex-1 truncate text-xs text-muted-fg">
            {draft.length ? draft.map((item) => item.name).join(", ") : t("editor.none")}
          </p>
          {confirmDiscard ? (
            <>
              <span className="text-xs font-semibold text-warning">{t("themes.discardPrompt")}</span>
              <button
                type="button"
                className="rounded-lg border border-border px-3 py-1.5 text-sm font-semibold text-fg hover:bg-surface-2/60"
                onClick={() => setConfirmDiscard(false)}
              >
                {t("themes.keepEditing")}
              </button>
              <button
                type="button"
                className="rounded-lg border border-danger/40 bg-danger/10 px-3 py-1.5 text-sm font-semibold text-danger hover:bg-danger/20"
                onClick={() => onOpenChange(false)}
              >
                {t("themes.discard")}
              </button>
            </>
          ) : (
            <button
              type="button"
              disabled={saving || !dirty}
              className={cn(
                "flex items-center gap-2 rounded-lg px-4 py-2 text-sm font-bold transition",
                dirty
                  ? "border border-primary/40 bg-primary text-primary-fg hover:brightness-110"
                  : "border border-border/60 text-muted-fg",
                "disabled:cursor-default disabled:opacity-60",
              )}
              onClick={() => onSave(draft)}
            >
              {saving ? <Loader2 size={14} className="animate-spin" /> : <Check size={14} />}
              {t("action.save")} ({draft.length})
            </button>
          )}
        </footer>
      </DialogContent>
    </Dialog>
  );
}

function ThemeBucketSection({
  bucket,
  ungroupedLabel,
  selectedIds,
  onToggle,
}: {
  bucket: ThemeGroupBucket;
  ungroupedLabel: string;
  selectedIds: Set<number>;
  onToggle: (item: TaxonomyItem) => void;
}) {
  if (bucket.themes.length === 0) return null;
  const label = bucket.group?.displayName ?? ungroupedLabel;

  return (
    <section aria-label={label} className="pt-4">
      <h3 className="mb-2 flex items-center gap-2 text-fs-11 font-bold uppercase tracking-[0.12em] text-muted-fg">
        {label}
        <span className="font-semibold normal-case tracking-normal text-muted-fg/70">· {bucket.themes.length}</span>
      </h3>
      <div role="group" className="grid grid-cols-[repeat(auto-fill,minmax(112px,1fr))] gap-2">
        {bucket.themes.map((item) => {
          const on = selectedIds.has(item.id);
          const src = iconSrc(item.iconData);
          return (
            <button
              key={item.id}
              type="button"
              data-theme-tile=""
              role="checkbox"
              aria-checked={on}
              aria-label={item.name}
              onClick={() => onToggle(item)}
              className={cn(
                "relative flex min-h-[92px] flex-col items-center justify-center gap-2 rounded-xl border px-2 py-3 text-center transition",
                "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/60",
                on
                  ? "border-primary/80 bg-primary/12 text-fg"
                  : "border-border/70 bg-surface-2/40 text-fg/90 hover:border-border hover:bg-surface-2/70",
              )}
            >
              {on ? (
                <span className="absolute right-1.5 top-1.5 grid h-4 w-4 place-items-center rounded-full bg-primary text-primary-fg">
                  <Check size={11} strokeWidth={3} />
                </span>
              ) : null}
              {src ? (
                <img src={src} alt="" className="h-10 w-10 object-contain" />
              ) : (
                <span className="grid h-10 w-10 place-items-center rounded-lg bg-surface-2 text-base font-bold text-muted-fg">
                  {item.name.slice(0, 1).toUpperCase()}
                </span>
              )}
              <span className="line-clamp-2 text-xs font-semibold leading-tight">{item.name}</span>
            </button>
          );
        })}
      </div>
    </section>
  );
}
