import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Check, Loader2, Pencil, Plus, Search, X } from "lucide-react";
import type { TaxonomyItem } from "@/lib/api/domain";
import { api } from "@/lib/api/client";
import { useI18n } from "@/lib/i18n";
import { cn, iconSrc } from "@/lib/utils";
import { IconButton, Popover, PopoverContent, PopoverTrigger } from "./ui";
import { EditorField, editorInputClass } from "./GameEditorControls";
import { TaxonomyEditDialog, type TaxonomyKind } from "./TaxonomyEditDialog";
import { ThemePickerDialog } from "./themes/ThemePickerDialog";

type TaxonomyPickerProps = {
  kind: TaxonomyKind;
  label: string;
  items: TaxonomyItem[];
  value: number[];
  multiple: boolean;
  /** Nhan nam ben trai — dung cho cac khung cua the sua. */
  row?: boolean;
  onChange: (ids: number[]) => void;
};

/** Theme chon nhieu dung bang chon LON theo nhom (spec 2026-10-04 §6.4); con lai giu popover. */
export function TaxonomyPicker(props: TaxonomyPickerProps) {
  return props.kind === "themes" && props.multiple ? <ThemeTagField {...props} /> : <PopoverTaxonomyPicker {...props} />;
}

function ThemeTagField({ label, items, value, row = false, onChange }: TaxonomyPickerProps) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const selectedItems = useMemo(() => items.filter((item) => value.includes(item.id)), [items, value]);

  return (
    <EditorField label={label} wide={!row} row={row}>
      <div className={cn(editorInputClass, "flex h-auto min-h-9 flex-wrap items-center gap-1.5 px-2 py-1.5")}>
        {selectedItems.map((item) => (
          <span
            key={item.id}
            className="inline-flex items-center gap-1 rounded-md border border-border/70 bg-surface-2/70 px-1.5 py-0.5 text-xs font-semibold text-fg"
          >
            {iconSrc(item.iconData) ? <img src={iconSrc(item.iconData)} alt="" className="h-3 w-3 object-contain" /> : null}
            {item.name}
            <button
              type="button"
              aria-label={t("action.remove")}
              className="opacity-55 transition hover:opacity-100"
              onClick={() => onChange(value.filter((id) => id !== item.id))}
            >
              <X size={11} />
            </button>
          </span>
        ))}
        <button type="button" aria-label={label} className="flex min-w-8 flex-1 items-center gap-2 self-stretch text-left text-sm" onClick={() => setOpen(true)}>
          {selectedItems.length ? null : <span className="truncate text-muted-fg">{t("editor.none")}</span>}
          <Search size={14} className="ml-auto shrink-0 text-muted-fg" />
        </button>
      </div>
      <ThemePickerDialog
        open={open}
        onOpenChange={setOpen}
        title={label}
        selected={selectedItems}
        onSave={(next) => {
          onChange(next.map((item) => item.id));
          setOpen(false);
        }}
      />
    </EditorField>
  );
}

function PopoverTaxonomyPicker({
  kind,
  label,
  items,
  value,
  multiple,
  row = false,
  onChange,
}: TaxonomyPickerProps) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [query, setQuery] = useState("");
  const [editing, setEditing] = useState<TaxonomyItem | null>(null);

  useEffect(() => {
    const timer = window.setTimeout(() => setQuery(search.trim()), 240);
    return () => window.clearTimeout(timer);
  }, [search]);

  const results = useQuery({
    queryKey: ["taxonomy", kind, query],
    queryFn: () => api.taxonomy(kind, query || undefined),
    enabled: open,
  });
  const create = useMutation({
    mutationFn: () => api.createTaxonomy(kind, { id: 0, name: search.trim(), iconData: null, sortOrder: 0 }),
    onSuccess: async (created) => {
      await queryClient.invalidateQueries({ queryKey: ["taxonomy", kind] });
      onChange(multiple ? [...new Set([...value, created.id])] : [created.id]);
      setSearch("");
      if (!multiple) setOpen(false);
    },
  });

  const selectedItems = useMemo(() => items.filter((item) => value.includes(item.id)), [items, value]);
  const toggle = (id: number) => {
    if (multiple) onChange(value.includes(id) ? value.filter((current) => current !== id) : [...value, id]);
    else {
      onChange(value.includes(id) ? [] : [id]);
      setOpen(false);
    }
  };

  return (
    <EditorField label={label} wide={!row} row={row}>
      <Popover open={open} onOpenChange={setOpen}>
        {multiple ? (
          /* Chon nhieu: gia tri la chip NAM TRONG o nhap, kieu tag field —
             khong con hang chip roi rac ben duoi. */
          <div className={cn(editorInputClass, "flex h-auto min-h-9 flex-wrap items-center gap-1.5 px-2 py-1.5")}>
            {selectedItems.map((item) => (
              <span
                key={item.id}
                className="inline-flex items-center gap-1 rounded-md border border-border/70 bg-surface-2/70 px-1.5 py-0.5 text-xs font-semibold text-fg"
              >
                {iconSrc(item.iconData) ? <img src={iconSrc(item.iconData)} alt="" className="h-3 w-3 object-contain" /> : null}
                {item.name}
                <button
                  type="button"
                  aria-label={t("action.remove")}
                  className="opacity-55 transition hover:opacity-100"
                  onClick={() => toggle(item.id)}
                >
                  <X size={11} />
                </button>
              </span>
            ))}
            <PopoverTrigger asChild>
              <button type="button" aria-label={label} className="flex min-w-8 flex-1 items-center gap-2 self-stretch text-left text-sm">
                {selectedItems.length ? null : <span className="truncate text-muted-fg">{t("editor.none")}</span>}
                <Search size={14} className="ml-auto shrink-0 text-muted-fg" />
              </button>
            </PopoverTrigger>
          </div>
        ) : (
          <PopoverTrigger asChild>
            <button type="button" aria-label={label} className={cn(editorInputClass, "flex items-center justify-between text-left")}>
              <span className="truncate">{selectedItems[0]?.name ?? t("editor.none")}</span>
              <Search size={14} className="shrink-0 text-muted-fg" />
            </button>
          </PopoverTrigger>
        )}
        <PopoverContent align="start" className="w-[min(360px,calc(100vw-32px))] space-y-2 p-2">
          <div className="relative">
            <Search size={14} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted-fg" />
            <input autoFocus className={cn(editorInputClass, "pl-9")} value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t("editor.taxonomySearch")} />
          </div>
          {(results.error || create.error) ? (
            <p className="rounded-lg bg-danger/10 p-2 text-xs text-danger">{(results.error ?? create.error) instanceof Error ? (results.error ?? create.error as Error).message : String(results.error ?? create.error)}</p>
          ) : null}
          <div className="scrollbar-thin max-h-56 space-y-1 overflow-y-auto">
            {results.isPending ? <Loader2 className="mx-auto my-4 animate-spin text-muted-fg" /> : results.data?.map((item) => (
              <div key={item.id} className="flex items-center gap-1 rounded-lg hover:bg-surface-2/55">
                <button type="button" className="flex min-w-0 flex-1 items-center gap-2 px-2.5 py-2 text-left text-sm font-semibold" onClick={() => toggle(item.id)}>
                  {/* Chon MOT thi dau chon la hinh tron kieu radio, chon NHIEU
                      moi la o vuong tick — nhin la biet duoc chon may gia tri. */}
                  <span className={cn(
                    "grid h-4 w-4 shrink-0 place-items-center border",
                    multiple ? "rounded" : "rounded-full",
                    value.includes(item.id) ? "border-primary bg-primary text-primary-fg" : "border-border",
                  )}>
                    {value.includes(item.id) ? (multiple ? <Check size={11} /> : <span className="h-1.5 w-1.5 rounded-full bg-current" />) : null}
                  </span>
                  {iconSrc(item.iconData) ? <img src={iconSrc(item.iconData)} alt="" className="h-4 w-4 object-contain" /> : null}
                  <span className="truncate">{item.name}</span>
                </button>
                {/* Chi muc ca nhan sua duoc; muc chung do admin quan ly (server tra `editable`). */}
                {item.editable === false ? null : (
                  <IconButton size="compact" tone="edit" label={t("action.edit")} icon={<Pencil />} onClick={() => setEditing(item)} />
                )}
              </div>
            ))}
          </div>
          {search.trim() ? <button type="button" className="flex w-full items-center gap-2 rounded-lg border border-primary/25 bg-primary/10 px-3 py-2 text-sm font-bold text-primary" onClick={() => create.mutate()} disabled={create.isPending}>
            {create.isPending ? <Loader2 size={14} className="animate-spin" /> : <Plus size={14} />} {t("editor.taxonomyCreate")} “{search.trim()}”
          </button> : null}
        </PopoverContent>
      </Popover>
      <TaxonomyEditDialog kind={kind} item={editing} open={Boolean(editing)} onOpenChange={(next) => { if (!next) setEditing(null); }} onSaved={() => setEditing(null)} onDeleted={(id) => { onChange(value.filter((current) => current !== id)); setEditing(null); }} />
    </EditorField>
  );
}
