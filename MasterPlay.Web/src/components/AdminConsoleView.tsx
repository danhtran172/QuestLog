/**
 * ADMIN CONSOLE — spec 2026-09-14, giai doan 1 (mode shell).
 *
 * Console THAY TOAN BO vung thu vien: grid/table/queue/dashboard ca nhan bi an
 * cho toi khi thoat mode.
 *
 * Giai doan nay CHI dung vo: band signature, dieu huong section, va cho danh san
 * cho noi dung. KHONG co du lieu gia — moi section noi ro no se chua gi va giai
 * doan nao giao. Dat placeholder bang du lieu bia se vi pham AGENTS.md §3.2.
 *
 * Bang metadata la component RIENG (chua viet), khong tai dung GameTable: GameTable
 * render progress/ownership/score/playtime — toan field userdata khong ton tai tren
 * ban ghi metadata.
 */
import { useEffect, useState } from "react";
import {
  BarChart3,
  Bot,
  Eye,
  FileCheck2,
  Gamepad2,
  LayoutGrid,
  Library,
  LogOut,
  Plus,
  PlugZap,
  Rows3,
  Search,
  ShieldCheck,
  Tags,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { useI18n } from "@/lib/i18n";
import { SEARCH_DEBOUNCE_MS } from "@/lib/api/domain";
import {
  ADMIN_SECTIONS,
  DEFAULT_ADMIN_LAYOUT,
  type AdminLayout,
  type AdminSection,
} from "@/lib/workspace";
import {
  DEFAULT_ADMIN_SORT,
  EMPTY_ADMIN_FILTER,
  type AdminGameFilter,
  type AdminMetadataGame,
  type AdminSortKey,
} from "@/lib/adminApi";
import { AdminGamesSection } from "./AdminGamesSection";
import { AdminSeriesSection } from "./AdminSeriesSection";
import { AdminDashboardSection } from "./AdminDashboardSection";
import { AdminPublishingSection } from "./AdminPublishingSection";
import { AdminIntegrationsSection } from "./AdminIntegrationsSection";
import { AdminCrawlerSection } from "./AdminCrawlerSection";
import { AdminGeminiExeSection } from "./AdminGeminiExeSection";
import { AdminTaxonomySection } from "./AdminTaxonomySection";
import { AdminGameEditorDialog } from "./AdminGameEditorDialog";
import { AdminGamePreviewDialog, type AdminPreviewStart } from "./AdminGamePreviewDialog";
import { AdminSortPopover } from "./AdminSortPopover";
import { AdminFilterPopover } from "./AdminFilterPopover";
import { IconButton, Tooltip } from "./ui";

const SECTION_ICONS: Record<AdminSection, typeof Gamepad2> = {
  games: Gamepad2,
  series: Library,
  taxonomy: Tags,
  publishing: FileCheck2,
  integrations: PlugZap,
  crawler: Bot,
  geminiExe: Bot,
  dashboard: BarChart3,
};

/**
 * Cho danh san cho noi dung that.
 *
 * `phase` la so giai doan trong spec §10 — de nguoi dung (va nguoi review) biet
 * day la trang thai dung thiet ke chu khong phai man hinh hong.
 */
function SectionPlaceholder({
  icon,
  title,
  description,
  phase,
}: {
  icon: React.ReactNode;
  title: string;
  description: string;
  phase: string;
}) {
  return (
    <div className="surface-card mx-auto mt-10 flex max-w-xl flex-col items-center gap-3 p-8 text-center">
      <span className="grid h-12 w-12 place-items-center rounded-xl border border-primary/25 bg-primary/12 text-primary">
        {icon}
      </span>
      <h2 className="text-base font-extrabold text-fg">{title}</h2>
      <p className="text-sm leading-6 text-muted-fg">{description}</p>
      <span className="mt-1 rounded-full border border-border bg-surface-2/40 px-3 py-1 text-fs-11 font-bold uppercase tracking-[0.12em] text-muted-fg">
        {phase}
      </span>
    </div>
  );
}

export function AdminConsoleView({
  section,
  onSectionChange,
  username,
  onExit,
}: {
  section: AdminSection;
  onSectionChange: (section: AdminSection) => void;
  username: string;
  onExit: () => void;
}) {
  const { t } = useI18n();
  const SectionIcon = SECTION_ICONS[section];

  const [rawQuery, setRawQuery] = useState("");
  const [query, setQuery] = useState("");
  /* `undefined` = dong. `null` = tao moi. Co ban ghi = sua. */
  const [editing, setEditing] = useState<AdminMetadataGame | null | undefined>(undefined);
  /* Series giu editor cua rieng no, nen nut `+` o header chi gui mot tin hieu. */
  const [seriesCreateSignal, setSeriesCreateSignal] = useState(0);
  /* Loc va sap xep — state cua PHIEN, khong persist. Bam mot dong health o
     dashboard se ghi thang vao `filter.missing`, nen hai duong do dung CHUNG mot
     o state thay vi hai o roi phai giu chung dong bo. */
  const [filter, setFilter] = useState<AdminGameFilter>(EMPTY_ADMIN_FILTER);
  const [sort, setSort] = useState<AdminSortKey>(DEFAULT_ADMIN_SORT);
  const [descending, setDescending] = useState(true);
  const [sortOpen, setSortOpen] = useState(false);
  const [filterOpen, setFilterOpen] = useState(false);
  /* The hay danh sach. State cua phien, khong persist — giong `layout` cua thu vien. */
  const [layout, setLayout] = useState<AdminLayout>(DEFAULT_ADMIN_LAYOUT);
  /* Preview mode: chup lai tim kiem / loc / sap xep luc mo, cong vi tri bat dau.
     `null` = dong. */
  const [previewStart, setPreviewStart] = useState<AdminPreviewStart | null>(null);
  const openPreview = (index: number) =>
    setPreviewStart({ query, filter, sort, descending, index });

  useEffect(() => {
    const timer = setTimeout(() => setQuery(rawQuery), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [rawQuery]);

  /* Doi section thi bo o tim kiem: truy van cua Games khong co nghia o Series. */
  useEffect(() => setRawQuery(""), [section]);

  const searchable = section === "games" || section === "series";
  const creatable = section === "games" || section === "series";
  /* Series von da la bang, dashboard khong co danh sach ban ghi — chi Games moi
     co hai cach trinh bay de chon. */
  const switchable = section === "games";

  const startCreate = () => {
    if (section === "games") setEditing(null);
    else if (section === "series") setSeriesCreateSignal((current) => current + 1);
  };

  /* `\`` mo luong tao ban ghi metadata (luong C, spec §12.7). State editor nam o
     day nen listener cung o day; App.tsx chi chan default cho phim nay. */
  useEffect(() => {
    if (!creatable) return;
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== "`") return;
      const target = event.target as HTMLElement | null;
      const typing = !!target
        && (target.tagName === "INPUT" || target.tagName === "TEXTAREA" || target.isContentEditable);
      if (typing) return;
      /* Dang mo dialog thi khong mo chong len. */
      if (document.querySelector("[role='dialog'][data-state='open']")) return;
      event.preventDefault();
      startCreate();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [creatable, section]);

  return (
    <div className="relative z-10 min-h-screen">
      {/*
        BAND — tin hieu khong the bo qua du da cuon sau. Chu "ADMIN CONSOLE" la
        phan bat buoc; mau xanh chi la lop tin hieu thu hai (spec §3).
      */}
      <div
        className="sticky top-0 z-50 flex flex-wrap items-center gap-3 px-4 py-2"
        style={{ backgroundColor: "var(--admin-band)", color: "var(--admin-band-fg)" }}
      >
        <span className="flex items-center gap-2 text-xs font-black uppercase tracking-[0.18em]">
          <ShieldCheck size={16} aria-hidden="true" />
          {t("admin.console.title")}
        </span>
        <span className="min-w-0 flex-1 truncate text-xs font-semibold opacity-80">
          {t("admin.console.bandHint")} · {username}
        </span>
        <button
          type="button"
          onClick={onExit}
          className="flex shrink-0 items-center gap-1.5 rounded-lg border border-current/30 px-2.5 py-1 text-xs font-extrabold transition-colors hover:bg-black/15"
        >
          <LogOut size={14} aria-hidden="true" />
          {t("admin.console.exit")}
        </button>
      </div>

      <header className="glass-panel sticky top-[38px] z-40 border-b">
        <div className="flex flex-wrap items-center gap-3 px-4 py-2.5">
          <nav className="stone-slab flex shrink-0 items-center gap-1 rounded-xl p-1">
            {ADMIN_SECTIONS.map((key, index) => {
              const Icon = SECTION_ICONS[key];
              return (
                <Tooltip
                  key={key}
                  label={t(`admin.section.${key}`)}
                  shortcut={`Ctrl+${index + 1}`}
                  side="bottom"
                >
                  <button
                    type="button"
                    /* Bam tab thu cong thi bo bo loc. Nhay tu dong health cua
                       dashboard thi KHONG — do chinh la muc dich cua cu bam do. */
                    onClick={() => { setFilter(EMPTY_ADMIN_FILTER); onSectionChange(key); }}
                    aria-current={section === key ? "page" : undefined}
                    className={cn(
                      "flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-sm font-bold transition-colors",
                      section === key
                        ? "bg-primary text-primary-fg"
                        : "text-muted-fg hover:bg-surface-2/70",
                    )}
                  >
                    <Icon size={15} aria-hidden="true" />
                    {t(`admin.section.${key}`)}
                  </button>
                </Tooltip>
              );
            })}
          </nav>

          {switchable ? (
            <AdminSortPopover
              open={sortOpen}
              onOpenChange={setSortOpen}
              sort={sort}
              descending={descending}
              onChange={(nextSort, nextDesc) => { setSort(nextSort); setDescending(nextDesc); }}
            />
          ) : null}

          {switchable ? (
            <AdminFilterPopover
              open={filterOpen}
              onOpenChange={setFilterOpen}
              filter={filter}
              onChange={setFilter}
            />
          ) : null}

          {/* Chuyen The <-> Danh sach. Lay dung mat cua nut cung chuc nang ben
              thu vien (App.tsx): o vuong `stone-slab h-9 w-9 rounded-xl`, khong
              dung he `IconButton` vi no thuoc thanh cong cu. Nhan/icon la che do
              SE CHUYEN SANG, khong phai che do dang bat. */}
          {switchable ? (
            <Tooltip label={t(layout === "card" ? "layout.list" : "layout.card")} side="bottom">
              <button
                type="button"
                onClick={() => setLayout((current) => (current === "card" ? "list" : "card"))}
                aria-pressed={layout === "list"}
                aria-label={t(layout === "card" ? "layout.list" : "layout.card")}
                className={cn(
                  "stone-slab grid h-9 w-9 shrink-0 place-items-center rounded-xl outline-none transition-colors",
                  "hover:text-fg focus-visible:ring-2 focus-visible:ring-primary/40",
                  layout === "list" ? "text-primary" : "text-muted-fg",
                )}
              >
                {layout === "card" ? <Rows3 size={16} /> : <LayoutGrid size={16} />}
              </button>
            </Tooltip>
          ) : null}

          {/* Section chua co noi dung that thi disable that su, khong de bam roi
              khong co gi xay ra. */}
          <div className="relative min-w-48 flex-1">
            <Search
              size={15}
              aria-hidden="true"
              className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted-fg"
            />
            <input
              disabled={!searchable}
              value={rawQuery}
              onChange={(event) => setRawQuery(event.target.value)}
              placeholder={section === "series" ? t("admin.series.searchPlaceholder") : t("admin.search.placeholder")}
              className="stone-slab h-9 w-full rounded-lg pl-9 pr-3 text-sm outline-none disabled:opacity-50"
            />
          </div>

          {/* Preview mode: duyet tung ban ghi cua danh sach dang loc, bat dau
              tu ban ghi dau tien. */}
          {switchable ? (
            <IconButton
              onClick={() => openPreview(0)}
              label={t("admin.preview.open")}
              icon={<Eye />}
              tone="open"
            />
          ) : null}

          <IconButton
            disabled={!creatable}
            onClick={startCreate}
            label={section === "series" ? t("admin.series.createTitle") : t("admin.action.create")}
            shortcut="`"
            icon={<Plus />}
            tone="add"
          />
        </div>
      </header>

      <main className="px-4 py-5 pb-20">
        {section === "games" ? (
          <AdminGamesSection
            query={query}
            filter={filter}
            sort={sort}
            descending={descending}
            layout={layout}
            onFilterChange={setFilter}
            onEdit={setEditing}
            onPreview={openPreview}
          />
        ) : section === "series" ? (
          <AdminSeriesSection
            query={query}
            createSignal={seriesCreateSignal}
            onCreateHandled={() => setSeriesCreateSignal(0)}
          />
        ) : section === "publishing" ? (
          <AdminPublishingSection
            onOpenUnpublished={() => {
              setFilter({ ...EMPTY_ADMIN_FILTER, published: false });
              onSectionChange("games");
            }}
          />
        ) : section === "taxonomy" ? (
          <AdminTaxonomySection />
        ) : section === "integrations" ? (
          <AdminIntegrationsSection />
        ) : section === "crawler" ? (
          <AdminCrawlerSection />
        ) : section === "geminiExe" ? (
          <AdminGeminiExeSection />
        ) : section === "dashboard" ? (
          <AdminDashboardSection
            onOpenFiltered={(missing) => {
              /* Thay TOAN BO bo loc, khong chong them: dong health la mot cau hoi
                 tron ven ("ban ghi nao thieu anh card"), khong phai mot dieu kien
                 cong vao thu dang loc do. */
              setFilter({ ...EMPTY_ADMIN_FILTER, missing });
              onSectionChange("games");
            }}
          />
        ) : (
          <SectionPlaceholder
            icon={<SectionIcon size={22} aria-hidden="true" />}
            title={t(`admin.section.${section}`)}
            description={t(`admin.placeholder.${section}`)}
            phase={t("admin.placeholder.phase")}
          />
        )}
      </main>

      <AdminGameEditorDialog
        open={editing !== undefined}
        game={editing ?? null}
        onOpenChange={(next) => { if (!next) setEditing(undefined); }}
      />

      {previewStart ? (
        <AdminGamePreviewDialog
          open
          start={previewStart}
          onOpenChange={(next) => { if (!next) setPreviewStart(null); }}
          onEdit={setEditing}
        />
      ) : null}
    </div>
  );
}
