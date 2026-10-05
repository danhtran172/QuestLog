import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { GripVertical, Search, Tags } from "lucide-react";
import { cn, iconSrc } from "@/lib/utils";
import { useI18n } from "@/lib/i18n";
import { api } from "@/lib/api/client";
import type { CatalogItem, TaxonomyItem } from "@/lib/api/domain";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "./ui";
import { ThemeGroupBoard } from "./themes/ThemeGroupBoard";

/**
 * Manage tags / catalog.
 *
 * KHAC ban Lovable o cho quan trong: Lovable chi co 3 danh muc
 * (Series / Genres / Themes). WPF co 4, va con thieu ca truong SortOrder.
 *
 * Bo sung PROGRESS va OWNERSHIP (A15) vi `sortOrder` cua hai bang nay
 * chinh la NGUON THU HANG SORT sau khi bo magic number (DOMAIN_RULES §3.2).
 * Thieu chung la nguoi dung mat kha nang sap xep von dang co o WPF.
 */
type TabKey = "series" | "genres" | "themes" | "progress" | "ownership";

const TABS: TabKey[] = ["series", "genres", "themes", "progress", "ownership"];

export function ManageTagsDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
}) {
  const { t } = useI18n();
  const [tab, setTab] = useState<TabKey>("series");
  const [q, setQ] = useState("");

  const isCatalog = tab === "progress" || tab === "ownership";
  /* Tab Themes: bang nhom co keo tha (spec 2026-10-04-theme-groups §6.3). */
  const isThemes = tab === "themes";

  const { data, isPending, isError, error } = useQuery<Array<CatalogItem | TaxonomyItem>>({
    queryKey: ["catalog-or-taxonomy", tab],
    queryFn: () =>
      isCatalog
        ? api.catalog(tab as "progress" | "ownership")
        : api.taxonomy(tab as "series" | "genres"),
    enabled: open && !isThemes,
  });

  // Chuan hoa hai kieu (CatalogItem | TaxonomyItem) ve mot shape de render chung.
  // `systemKey` chi co o CatalogItem (Progress/Ownership).
  type Row = {
    id: number;
    label: string;
    iconData?: string | null;
    sortOrder: number;
    systemKey?: string;
  };

  // `data` la union CUA HAI MANG (CatalogItem[] | TaxonomyItem[]) — TS khong tu
  // thu hep duoc khi goi .map, nen gom ve mang cua union truoc.
  const raw = (data ?? []) as Array<CatalogItem | TaxonomyItem>;

  const rows: Row[] = raw.map((x) => ({
    id: x.id,
    label: "displayName" in x ? x.displayName : x.name,
    iconData: x.iconData,
    sortOrder: x.sortOrder,
    systemKey: "systemKey" in x ? x.systemKey : undefined,
  }));

  const items = rows.filter((x) =>
    q.trim() ? x.label.toLowerCase().includes(q.trim().toLowerCase()) : true,
  );

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className={cn("gap-0 overflow-hidden p-0", isThemes ? "max-w-4xl" : "max-w-2xl")}>
        <DialogHeader className="border-b border-border px-5 py-4">
          <DialogTitle className="flex items-center gap-2">
            <Tags size={18} /> {t("menu.manageTags")}
          </DialogTitle>
          <DialogDescription>{t("tags.hint")}</DialogDescription>
        </DialogHeader>

        <div className="flex items-center gap-1 border-b border-border px-3 py-2">
          {TABS.map((k) => (
            <button
              key={k}
              onClick={() => setTab(k)}
              className={cn(
                "rounded-lg px-2.5 py-1.5 text-xs font-semibold transition-colors",
                tab === k
                  ? "glass-surface bg-primary/40 text-fg"
                  : "text-muted-fg hover:bg-surface-2/70",
              )}
            >
              {t(`tags.${k}`)}
            </button>
          ))}
        </div>

        {isThemes ? (
          <div className="scrollbar-thin max-h-[70vh] overflow-y-auto p-4">
            <ThemeGroupBoard scope="library" />
          </div>
        ) : (
        <div className="p-4">
          <div className="stone-slab mb-3 flex h-9 items-center rounded-lg">
            <Search size={14} className="ml-3 text-muted-fg" />
            <input
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder={t("search.placeholder")}
              className="h-9 w-full bg-transparent px-2 text-sm outline-none placeholder:text-muted-fg"
            />
          </div>

          <div className="scrollbar-thin h-[46vh] space-y-1.5 overflow-y-auto pr-1">
            {isError ? (
              <p className="py-10 text-center text-sm text-danger">
                {error instanceof Error ? error.message : String(error)}
              </p>
            ) : isPending ? (
              Array.from({ length: 8 }).map((_, i) => (
                <div key={i} className="surface-card h-11 animate-pulse" />
              ))
            ) : items.length === 0 ? (
              <p className="py-10 text-center text-sm text-muted-fg">{t("empty.noResults")}</p>
            ) : (
              items.map((item) => (
                <div key={item.id} className="surface-card flex items-center gap-3 px-3 py-2">
                  <GripVertical size={14} className="shrink-0 text-muted-fg" />

                  {iconSrc(item.iconData) ? (
                    <img src={iconSrc(item.iconData)} alt="" className="h-4 w-4 object-contain" />
                  ) : null}

                  <span className="flex-1 truncate text-sm font-semibold">{item.label}</span>

                  {/* systemKey la BAT BIEN — hien de thay ro cai gi sua duoc, cai gi khong */}
                  {item.systemKey && (
                    <code className="rounded bg-surface-2 px-1.5 py-0.5 text-fs-10 text-muted-fg">
                      {item.systemKey}
                    </code>
                  )}

                  <span className="w-10 text-right text-fs-11 text-muted-fg">#{item.sortOrder}</span>
                </div>
              ))
            )}
          </div>

          <p className="mt-3 text-xs text-muted-fg">{t("tags.readonlyNote")}</p>
        </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
