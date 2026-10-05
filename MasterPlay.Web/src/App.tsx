import { lazy, Suspense, useCallback, useEffect, useRef, useState } from "react";
import {
  ArrowDownWideNarrow,
  ArrowUpDown,
  ArrowUpNarrowWide,
  BarChart3,
  Check,
  ChevronLeft,
  ChevronRight,
  Filter,
  Gamepad2,
  Keyboard,
  LayoutGrid,
  ListOrdered,
  Loader2,
  Menu,
  Plus,
  RotateCcw,
  Rows3,
  Search,
  Settings as SettingsIcon,
  ShieldCheck,
  Tags,
  UserRound,
  X,
} from "lucide-react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import { cn } from "@/lib/utils";
import { useI18n } from "@/lib/i18n";
import { useGames } from "@/lib/useGames";
import { useLibraryMetadataRefresh } from "@/lib/useLibraryMetadataRefresh";
import { usePlayQueue } from "@/lib/usePlayQueue";
import { useControllerExperience } from "@/lib/useControllerExperience";
import { useAppModeMemory } from "@/lib/useAppModeMemory";
import { toggleFullscreen } from "@/lib/desktopBridge";
import { useViewSortProfiles, type ConfigurableView } from "@/lib/useViewSortProfiles";
import { useAuth } from "@/lib/auth";
import {
  ADMIN_SECTIONS,
  DEFAULT_ADMIN_SECTION,
  type AdminSection,
  type WorkspaceMode,
} from "@/lib/workspace";
import { api } from "@/lib/api/client";
import { gameDetailApi } from "@/lib/gameDetailApi";
import {
  PAGE_SIZE,
  SEARCH_DEBOUNCE_MS,
  SEARCH_SCOPES,
  SORT_KEYS,
  TABS,
  type SearchScope,
  type SortKey,
  type Tab,
} from "@/lib/api/domain";
import { GameCard, GameCardSkeleton } from "@/components/GameCard";
import { ControllerPrompts } from "@/components/ControllerPrompts";
import { GameTable, QUEUE_MAX_ROWS } from "@/components/GameTable";
import { GameDetailDialog } from "@/components/GameDetailDialog";
import { AddGameMethodDialog } from "@/components/AddGameMethodDialog";
import { MetadataGamePickerDialog } from "@/components/MetadataGamePickerDialog";
import { ViewSortProfileEditor } from "@/components/ViewSortProfileEditor";
import { GuideTour } from "@/components/GuideTour";
import { VIEW_GUIDE, useViewModesGuide } from "@/components/viewModesGuide";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
  IconButton,
  Popover,
  PopoverAnchor,
  PopoverContent,
  PopoverTrigger,
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
  Tooltip,
} from "@/components/ui";
import { AdminConsoleView } from "@/components/AdminConsoleView";
import { SettingsDialog } from "@/components/SettingsDialog";
import { DashboardView } from "@/components/DashboardView";
import { ManageTagsDialog } from "@/components/ManageTagsDialog";
import { AccountDialog } from "@/components/AccountDialog";
import { TrayPanel } from "@/components/TrayPanel";
import { ReleaseNotificationsBell } from "@/components/ReleaseNotificationsBell";
import {
  EMPTY_FILTER,
  FilterDialog,
  filterToQuery,
  type FilterState,
} from "@/components/FilterDialog";

const SHORTCUTS: [string, string][] = [
  ["F5", "shortcut.reload"],
  ["F6", "shortcut.resetLibrary"],
  ["F10", "shortcut.console"],
  ["F11", "shortcut.fullscreen"],
  ["`", "shortcut.addGame"],
  ["Ctrl+1..4", "shortcut.tabs"],
  ["Ctrl+K", "shortcut.search"],
  ["Esc", "shortcut.closeSurface"],
];

/* Cung bang tren nhung cho tay cam — khop voi controllerInput.ts. */
const GAMEPAD_SHORTCUTS: [string, string][] = [
  ["A", "controller.select"],
  ["B", "shortcut.closeSurface"],
  ["X", "action.play"],
  ["Y", "shortcut.reload"],
  ["LB/RB", "shortcut.tabs"],
  ["LB+RB+A", "shortcut.console"],
  ["R3", "shortcut.addGame"],
  ["L3", "controller.changeLayout"],
  ["View", "settings.title"],
  ["RS", "controller.scroll"],
];

const ConsoleLibraryView = lazy(() => import("@/components/ConsoleLibraryView").then(
  (module) => ({ default: module.ConsoleLibraryView }),
));

export default function App() {
  const { t } = useI18n();
  const { account, status: authStatus, logout } = useAuth();

  const [tab, setTab] = useState<Tab>("all");
  const [rawQuery, setRawQuery] = useState("");
  const [query, setQuery] = useState("");
  const [debouncing, setDebouncing] = useState(false);
  const [scope, setScope] = useState<SearchScope>("all");
  const [sort, setSort] = useState<SortKey>("default");
  const [reverseSort, setReverseSort] = useState(false);
  const [page, setPage] = useState(1);
  const [sortOpen, setSortOpen] = useState(false);
  const [sortProfileView, setSortProfileView] = useState<ConfigurableView | null>(null);
  const [shortcutsOpen, setShortcutsOpen] = useState(false);
  const [filterOpen, setFilterOpen] = useState(false);
  const [filters, setFilters] = useState<FilterState>(EMPTY_FILTER);
  /* Che do hien thi cua ba tab dau. KHONG luu localStorage — DOMAIN_RULES §15
     bat moi thiet lap phai di qua config dung chung voi Tray. */
  const [layout, setLayout] = useState<"card" | "list">("card");

  /* Workspace mode — spec 2026-09-14 §2. State cua PHIEN, khong persist: reload
     hay dang nhap lai deu ve `library`. */
  const [workspace, setWorkspace] = useState<WorkspaceMode>("library");
  const [adminSection, setAdminSection] = useState<AdminSection>(DEFAULT_ADMIN_SECTION);

  const isAdmin = authStatus === "signedIn" && account?.role === "admin";
  const adminConsoleActive = workspace === "adminConsole" && isAdmin;

  /* Kho dung chung sua metadata thi thu vien doi theo — tru field nguoi dung da
     sua tay. Guest cung co thu vien noi vao kho nen cung phai chay. */
  useLibraryMetadataRefresh(authStatus === "signedIn" || authStatus === "guest");

  const [settingsOpen, setSettingsOpen] = useState(false);
  const [accountOpen, setAccountOpen] = useState(false);
  const [addMethodOpen, setAddMethodOpen] = useState(false);
  const [metadataPickerOpen, setMetadataPickerOpen] = useState(false);
  /* Dashboard la mot MAN HINH, khong phai popup — bat len thi <main> doi noi dung. */
  const [dashboardOpen, setDashboardOpen] = useState(false);
  const [tagsOpen, setTagsOpen] = useState(false);
  const [trayOpen, setTrayOpen] = useState(false);
  const [detailOpen, setDetailOpen] = useState(false);
  const [selectedGameId, setSelectedGameId] = useState<string | null>(null);
  const [detailMode, setDetailMode] = useState<"view" | "edit" | "add">("view");

  const searchRef = useRef<HTMLInputElement>(null);
  const gridRef = useRef<HTMLDivElement>(null);
  const tabBeforeConsoleRef = useRef<Tab>("all");

  // Debounce 280ms — DOMAIN_RULES §6.1.
  useEffect(() => {
    setDebouncing(true);
    const timer = setTimeout(() => {
      setQuery(rawQuery);
      setDebouncing(false);
      setPage(1);
    }, SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [rawQuery]);

  // Cham trang thai o footer: xanh khi Local API co kha nang tray, xam khi khong.
  const { data: caps } = useQuery({
    queryKey: ["capabilities"],
    queryFn: () => api.capabilities(),
    staleTime: Infinity,
  });
  const trayRunning = caps?.features?.tray === true;
  const launchGame = useMutation({
    mutationFn: (gameId: string) => gameDetailApi.launch(gameId),
    onError: (launchError) => toast.error(
      launchError instanceof Error ? launchError.message : String(launchError),
    ),
  });

  const { data, isPending, isError, error, refetch } = useGames(
    {
      tab,
      q: tab === "console" ? "" : query,
      scope,
      sort: tab === "console" ? "default" : sort,
      reverseSort: tab === "console" ? false : reverseSort,
      page,
      filter: tab === "console" ? undefined : filterToQuery(filters),
    },
    /* Admin Console thay toan bo vung thu vien — khong fetch thu vien o nen. */
    tab !== "queue" && !adminConsoleActive,
  );
  const queue = usePlayQueue(tab === "queue" && !adminConsoleActive);
  const viewSortProfiles = useViewSortProfiles();

  const refreshCurrentView = useCallback(() => {
    if (tab === "queue") return queue.refetch();
    return refetch();
  }, [queue.refetch, refetch, tab]);

  const resetAll = useCallback(() => {
    setRawQuery("");
    setQuery("");
    setScope("all");
    setSort("default");
    setReverseSort(false);
    setFilters(EMPTY_FILTER);
    setPage(1);
    setSortOpen(false);
    setFilterOpen(false);
    setSortProfileView(null);
  }, []);

  useEffect(() => {
    if (tab !== "queue") return;
    setSortOpen(false);
    setFilterOpen(false);
    setSortProfileView(null);
  }, [tab]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const el = e.target as HTMLElement | null;
      const typing =
        !!el && (el.tagName === "INPUT" || el.tagName === "TEXTAREA" || el.isContentEditable);

      if (e.key === "F5") {
        e.preventDefault();
        void refreshCurrentView();
      } else if (e.key === "F6") {
        e.preventDefault();
        /* Reset library khong co nghia trong Admin Console — spec §12.5. */
        if (!adminConsoleActive) resetAll();
      } else if (e.key === "F11") {
        e.preventDefault();
        if (e.repeat) return;
        void toggleFullscreen().catch((fullscreenError) => {
          toast.error(fullscreenError instanceof Error ? fullscreenError.message : t("controller.fullscreenError"));
        });
      } else if (e.key === "k" && (e.ctrlKey || e.metaKey)) {
        // A18 — WPF co Ctrl+K, ban Lovable thieu.
        e.preventDefault();
        if (adminConsoleActive) return;
        searchRef.current?.focus();
        searchRef.current?.select();
      } else if (e.key === "`" && !typing) {
        e.preventDefault();
        /* Trong Admin Console, `\`` se mo editor metadata (luong C, spec §12.7).
           Editor do thuoc giai doan 3 — chua co gi de mo, nen khong lam gi ca
           thay vi mo nham editor game cua thu vien. */
        if (adminConsoleActive) return;
        setSelectedGameId(null);
        setDetailMode("add");
        setDetailOpen(true);
      } else if (
        e.ctrlKey
        /* So phim khop so muc that — doi ADMIN_SECTIONS la phim tat theo ngay. */
        && /^[1-9]$/.test(e.key)
        && Number(e.key) <= (adminConsoleActive ? ADMIN_SECTIONS.length : TABS.length)
      ) {
        e.preventDefault();
        const index = Number(e.key) - 1;
        if (adminConsoleActive) {
          const nextSection = ADMIN_SECTIONS[index];
          if (nextSection) setAdminSection(nextSection);
          return;
        }
        const next = TABS[index];
        if (next) {
          setTab(next);
          setPage(1);
        }
      } else if (e.key === "Escape") {
        setShortcutsOpen(false);
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [adminConsoleActive, refreshCurrentView, resetAll, t]);

  // A19 — scroll len dau khi doi trang (ban Lovable thieu).
  const goToPage = useCallback((next: number) => {
    setPage(next);
    gridRef.current?.scrollIntoView({ block: "start", behavior: "smooth" });
  }, []);

  const items = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;
  const totalItems = tab === "queue"
    ? Math.min(queue.data?.length ?? 0, QUEUE_MAX_ROWS)
    : (data?.totalItems ?? 0);
  /* Khoang dang hien o footer. Queue khong phan trang nen luon la tron bo. */
  const firstItem = totalItems === 0 ? 0 : tab === "queue" ? 1 : (page - 1) * PAGE_SIZE + 1;
  const lastItem = tab === "queue" ? totalItems : Math.min(page * PAGE_SIZE, totalItems);
  const previousGameCount = tab === "queue" ? 0 : Math.max(0, firstItem - 1);
  const remainingGameCount = tab === "queue" ? 0 : Math.max(0, totalItems - lastItem);
  const activeFilterCount = data?.activeFilterCount ?? 0;
  /* Tab "Tat ca" khong tim, khong loc ma van rong = thu vien that su chua co game
     (user moi). Cac tab khac rong khong noi len dieu do. */
  const libraryIsEmpty = tab === "all" && !query && activeFilterCount === 0;

  const openDetail = useCallback((gameId: string) => {
    setSelectedGameId(gameId);
    setDetailMode("view");
    setDetailOpen(true);
  }, []);

  const openAdd = useCallback(() => {
    setAddMethodOpen(true);
  }, []);

  const openManualAdd = useCallback(() => {
    setSelectedGameId(null);
    setDetailMode("add");
    setDetailOpen(true);
  }, []);

  const controllerGames = tab === "queue" ? (queue.data ?? []) : items;
  const controllerLoading = tab === "queue" ? queue.isPending : isPending;
  const controllerErrorValue = tab === "queue" && queue.isError ? queue.error : isError ? error : null;
  const controllerError = controllerErrorValue
    ? controllerErrorValue instanceof Error
      ? controllerErrorValue.message
      : String(controllerErrorValue)
    : null;
  const hasControllerSurface =
    settingsOpen || accountOpen || addMethodOpen || metadataPickerOpen || tagsOpen || trayOpen ||
    detailOpen || filterOpen || sortOpen || sortProfileView != null || shortcutsOpen;

  const closeTopControllerSurface = useCallback(() => {
    if (metadataPickerOpen) setMetadataPickerOpen(false);
    else if (addMethodOpen) setAddMethodOpen(false);
    else if (detailOpen) setDetailOpen(false);
    else if (settingsOpen) setSettingsOpen(false);
    else if (accountOpen) setAccountOpen(false);
    else if (tagsOpen) setTagsOpen(false);
    else if (trayOpen) setTrayOpen(false);
    else if (filterOpen) setFilterOpen(false);
    else if (sortProfileView != null) setSortProfileView(null);
    else if (sortOpen) setSortOpen(false);
    else if (shortcutsOpen) setShortcutsOpen(false);
  }, [
    accountOpen,
    addMethodOpen,
    detailOpen,
    filterOpen,
    metadataPickerOpen,
    settingsOpen,
    shortcutsOpen,
    sortOpen,
    sortProfileView,
    tagsOpen,
    trayOpen,
  ]);

  const controller = useControllerExperience({
    games: controllerGames,
    loading: controllerLoading,
    error: controllerError,
    tab,
    page,
    totalPages,
    layout,
    hasOverlay: hasControllerSurface,
    /* Console (controller) va Admin Console khong duoc chong nhau — spec §2.1. */
    suspended: adminConsoleActive,
    t,
    onTabChange: (nextTab) => {
      setTab(nextTab);
      setPage(1);
      setDashboardOpen(false);
    },
    onPageChange: setPage,
    onLayoutChange: setLayout,
    onCloseOverlay: closeTopControllerSurface,
    onOpenSettings: () => setSettingsOpen(true),
    onOpenGame: openDetail,
    onAddGame: openAdd,
    onLaunchGame: (gameId) => launchGame.mutate(gameId),
    onRefresh: () => void refreshCurrentView(),
    onLeaveDashboard: () => setDashboardOpen(false),
    onEnterConsoleSession: () => {
      if (tab !== "console") tabBeforeConsoleRef.current = tab;
      setTab("console");
      setPage(1);
      setDashboardOpen(false);
    },
    onExitConsoleSession: () => {
      setTab(tabBeforeConsoleRef.current);
      setPage(1);
      setDashboardOpen(false);
    },
  });
  /* Dang dung tay cam: goi y phim tat ban phim doi sang nut tay cam. */
  const usingGamepad = controller.inputModality === "gamepad";
  const showConsole = controller.showConsole;
  /* Mo lai app thi vao lai mode cuoi cung (desktop | console) — luu o config. */
  useAppModeMemory({
    consoleSession: controller.consoleSession,
    canRestore: !adminConsoleActive,
    onRestoreConsole: controller.restoreConsoleSession,
  });
  /* Ca hai surface fullscreen deu an chrome thu vien. */
  const hideLibraryChrome = showConsole || adminConsoleActive;

  /* Huong dan cac tab: chi bat khi thu vien da co game va khong popup/man hinh
     nao dang che thanh tab — vd vua Luu game dau tien thi doi the game dong lai. */
  const viewModesGuide = useViewModesGuide({
    hasGames: (tab === "queue" ? (queue.data?.length ?? 0) : totalItems) > 0,
    idle: (authStatus === "signedIn" || authStatus === "guest") &&
      !hideLibraryChrome && !dashboardOpen && !detailOpen && !addMethodOpen && !metadataPickerOpen &&
      !settingsOpen && !accountOpen && !filterOpen && !tagsOpen && !trayOpen && !shortcutsOpen &&
      !sortOpen && sortProfileView == null,
  });

  const enterAdminConsole = useCallback(() => {
    /* Thoat console session dang chay truoc, de tra lai window state — neu khong
       Admin Console se ve ben duoi mot fullscreen con treo. */
    if (controller.consoleSession) controller.toggleConsoleSession();
    setAdminSection(DEFAULT_ADMIN_SECTION);
    setDashboardOpen(false);
    setSettingsOpen(false);
    setAccountOpen(false);
    setTagsOpen(false);
    setTrayOpen(false);
    setDetailOpen(false);
    setFilterOpen(false);
    setSortOpen(false);
    setAddMethodOpen(false);
    setMetadataPickerOpen(false);
    setWorkspace("adminConsole");
  }, [controller]);

  const exitAdminConsole = useCallback(() => setWorkspace("library"), []);

  /* Mat quyen admin (dang xuat, doi tai khoan, bi khoa) thi roi ve library ngay.
     Mode khong duoc song sot qua mot phien khac. */
  useEffect(() => {
    if (!isAdmin && workspace !== "library") setWorkspace("library");
  }, [isAdmin, workspace]);

  return (
    <div
      className={cn(
        "app-shell relative min-h-screen bg-bg",
        /* Vien khung — tin hieu con thay duoc ke ca khi da cuon sau (spec §3). */
        adminConsoleActive && "ring-1 ring-inset ring-primary/60",
      )}
      data-workspace={adminConsoleActive ? "admin" : undefined}
      data-controller-connected={controller.connected ? "true" : "false"}
      data-input-modality={controller.inputModality}
      data-console-fullscreen={controller.fullscreenActive ? "true" : "false"}
    >
      {adminConsoleActive ? (
        <AdminConsoleView
          section={adminSection}
          onSectionChange={setAdminSection}
          username={account?.username ?? ""}
          onExit={exitAdminConsole}
        />
      ) : null}

      {showConsole ? (
        <Suspense fallback={<div aria-busy="true" className="fixed inset-0 z-30 bg-[#171a1f]" />}>
          <ConsoleLibraryView
            games={controllerGames}
            selectedGameId={controller.selectedGameId}
            tabLabel={t(`nav.${tab}`)}
            isLoading={controllerLoading}
            error={controllerError}
            emptyTitle={t(tab === "queue" ? "empty.queue" : tab === "console" ? "empty.console" : "empty.noGames")}
            emptyDescription={t(tab === "queue" ? "empty.queueDesc" : tab === "console" ? "empty.consoleDesc" : "empty.noGamesDesc")}
            retryLabel={t("error.retry")}
            previousGameCount={previousGameCount}
            remainingGameCount={remainingGameCount}
            previousGamesLabel={t("controller.previousGames").replace("{count}", String(previousGameCount))}
            browseBackLabel={t("controller.browseBack")}
            moreGamesLabel={t("controller.moreGames").replace("{count}", String(remainingGameCount))}
            continueLabel={t("controller.continueBrowsing")}
            playLabel={t("action.play")}
            exitHoldLabel={t("controller.holdToExit")}
            exitHoldSource={controller.exitHoldSource}
            exitHoldDurationMs={controller.exitHoldDurationMs}
            onSelectGame={controller.setSelectedGameId}
            onOpenGame={openDetail}
            onPlayGame={(gameId) => launchGame.mutate(gameId)}
            onRetry={() => void refreshCurrentView()}
          />
        </Suspense>
      ) : null}

      <header className={cn("glass-panel sticky top-0 z-40 border-b", hideLibraryChrome && "hidden")}>
        <div className="flex flex-wrap items-center gap-3 px-4 py-2.5">
          <nav data-guide={VIEW_GUIDE.tabs} className="stone-slab flex shrink-0 items-center gap-1 rounded-xl p-1">
            {TABS.map((key, i) => {
              const configurableView = key === "queue" ? null : key;
              const profile = configurableView
                ? viewSortProfiles.data?.profiles.find((item) => item.view === configurableView)
                : undefined;
              return (
                <Popover
                  key={key}
                  open={configurableView != null && sortProfileView === configurableView}
                  onOpenChange={(nextOpen) => {
                    if (!nextOpen && sortProfileView === configurableView) setSortProfileView(null);
                  }}
                >
                  <Tooltip label={t(`nav.${key}`)} shortcut={usingGamepad ? "LB/RB" : `Ctrl+${i + 1}`} side="bottom">
                    <PopoverAnchor asChild>
                      <button
                      data-guide={VIEW_GUIDE.tab(key)}
                      onClick={() => {
                        // Doi tab = mot con gio quet qua ca man hinh. Day la
                        // tin hieu chuyen canh manh nhat ma khong can animation
                        // chuyen trang.
                        setTab(key);
                        setPage(1);
                        setSortProfileView(null);
                        setDashboardOpen(false);
                      }}
                      onContextMenu={(event) => {
                        event.preventDefault();
                        if (configurableView) setSortProfileView(configurableView);
                      }}
                      className={cn(
                        "rounded-lg px-3 py-1.5 text-sm font-semibold transition-colors duration-200",
                        tab === key
                          ? "glass-surface bg-primary/45 text-fg"
                          : "text-muted-fg hover:bg-surface-2/60 hover:text-fg",
                      )}
                      >
                        {t(`nav.${key}`)}
                      </button>
                    </PopoverAnchor>
                  </Tooltip>
                  {configurableView ? (
                    <ViewSortProfileEditor
                      view={configurableView}
                      profile={profile}
                      isLoading={viewSortProfiles.isPending}
                      loadError={viewSortProfiles.error}
                      isSaving={viewSortProfiles.isSaving}
                      toolbarOverride={sort !== "default"}
                      onSave={viewSortProfiles.save}
                      onReset={viewSortProfiles.reset}
                    />
                  ) : null}
                </Popover>
              );
            })}
          </nav>

          {/* Chuyen Card <-> List. Play Queue von da la bang nen khong co nut.
              Nut nay KHONG dung he nut chung (`IconButton`) — no thuoc ve thanh
              cong cu, nen lay dung mat va do bo goc cua thanh search: mot o
              vuong `stone-slab h-9 w-9 rounded-xl`, khong mat kinh, khong noi 3D. */}
          {tab !== "queue" && !dashboardOpen ? (
            <Tooltip label={t(layout === "card" ? "layout.list" : "layout.card")} side="bottom">
              <button
                type="button"
                onClick={() => setLayout((current) => (current === "card" ? "list" : "card"))}
                aria-pressed={layout === "list"}
                /* Nhan/icon la che do SE CHUYEN SANG, khong phai che do dang bat. */
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

          <div className="flex flex-1 justify-center">
            {/* Search tat hoan toan o tab queue — DOMAIN_RULES §6.1. */}
            {tab !== "queue" && !dashboardOpen && (
              <div className="stone-slab flex h-9 w-full max-w-xl items-center rounded-xl">
                <div className="relative flex-1">
                  <Search size={15} className="absolute left-3 top-2.5 text-muted-fg" />
                  <input
                    ref={searchRef}
                    value={rawQuery}
                    onChange={(e) => setRawQuery(e.target.value)}
                    placeholder={t("search.placeholder")}
                    className="h-9 w-full bg-transparent pl-9 pr-8 text-sm outline-none placeholder:text-muted-fg"
                  />
                  {debouncing ? (
                    <Loader2 size={14} className="absolute right-2 top-2.5 animate-spin text-primary" />
                  ) : rawQuery ? (
                    <IconButton
                      onClick={() => setRawQuery("")}
                      label={t("action.clearSearch")}
                      icon={<X />}
                      size="compact"
                      className="absolute right-1.5 top-1"
                    />
                  ) : null}
                </div>
                <span className="h-5 w-px bg-border" />
                {/* Select cua Radix — <select> native cua trinh duyet trong khac han */}
                <Select
                  value={scope}
                  onValueChange={(v) => {
                    setScope(v as SearchScope);
                    setPage(1);
                  }}
                >
                  <SelectTrigger className="h-[34px] w-[128px] shrink-0 px-3">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent className="w-[168px]">
                    {SEARCH_SCOPES.map((s) => (
                      <SelectItem key={s} value={s}>
                        {t(`scope.${s}`)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
          </div>

          <div className="flex shrink-0 items-center gap-2">
            {/* Sort dang bat thi noi ro ra: khoa sort + huong. Chi la BANG BAO,
                thao tac van o nut sort ben canh. Dung dung mat va do bo goc cua
                thanh search de thuoc cung mot bo thanh cong cu. */}
            {/* Play Queue khong sort/loc duoc — an han hai nut thay vi de mo. */}
            {tab !== "queue" ? (
            <>
            <div className="flex shrink-0 items-center">
            {!dashboardOpen && (sort !== "default" || reverseSort) ? (
              <Tooltip
                label={`${t("action.sort")}: ${t(`sort.${sort}`)} · ${reverseSort ? t("sort.desc") : t("sort.asc")}`}
                side="bottom"
              >
                {/* Canh phai VUONG va thut -16px vao duoi nut sort (nut ban kinh
                    18px) — nut tron de len, khong con thay duong noi. */}
                <span className="stone-slab -mr-4 flex h-9 shrink-0 items-center gap-1.5 rounded-l-xl rounded-r-none !border-r-0 py-0 pl-3 pr-6 text-xs font-bold text-fg">
                  {reverseSort ? (
                    <ArrowDownWideNarrow size={14} className="shrink-0 text-primary" />
                  ) : (
                    <ArrowUpNarrowWide size={14} className="shrink-0 text-primary" />
                  )}
                  <span className="truncate">{t(`sort.${sort}`)}</span>
                </span>
              </Tooltip>
            ) : null}

            {/* Sort = Popover chua 11 khoa + nut dao chieu ngay ben trong,
                dung nhu Lovable. Truoc do toi dung <select> native nen trong khac han. */}
            <Popover open={sortOpen} onOpenChange={setSortOpen}>
              <PopoverTrigger asChild>
                <IconButton
                  disabled={dashboardOpen}
                  label={`${t("action.sort")}: ${t(`sort.${sort}`)}`}
                  tone="sort"
                  /* Icon KHONG dao theo huong nua — huong da nam o khung ben trai. */
                  icon={<ArrowUpDown />}
                  className="relative z-10"
                />
              </PopoverTrigger>
              <PopoverContent align="end" className="w-60">
                <div className="mb-2 rounded-lg border border-primary/20 bg-primary/10 px-2.5 py-2">
                  <p className="text-fs-11 font-semibold text-muted-fg">{t("sort.current")}</p>
                  <div className="mt-0.5 flex items-center justify-between gap-2">
                    <span className="min-w-0 truncate text-sm font-bold text-fg">{t(`sort.${sort}`)}</span>
                    <IconButton
                      onClick={() => setReverseSort((v) => !v)}
                      label={reverseSort ? t("sort.desc") : t("sort.asc")}
                      icon={reverseSort ? <ArrowDownWideNarrow /> : <ArrowUpNarrowWide />}
                      size="compact"
                    />
                  </div>
                </div>
                <div className="mb-1 flex items-center justify-between gap-2 px-1.5">
                  <span className="text-xs font-semibold text-muted-fg">{t("sort.choose")}</span>
                  <div className="flex items-center gap-1">
                    <IconButton
                      onClick={() => {
                        setSort("default");
                        setReverseSort(false);
                        setPage(1);
                        setSortOpen(false);
                      }}
                      label={t("action.clearSort")}
                      icon={<RotateCcw />}
                      size="compact"
                      disabled={sort === "default" && !reverseSort}
                    />
                  </div>
                </div>
                <div className="space-y-0.5">
                  {SORT_KEYS.map((k) => (
                    <button
                      key={k}
                      type="button"
                      aria-current={sort === k ? "true" : undefined}
                      onClick={() => {
                        setSort(k);
                        setPage(1);
                        setSortOpen(false);
                      }}
                      className={cn(
                        "flex w-full items-center justify-between rounded-lg px-2.5 py-2 text-sm font-semibold outline-none transition-colors focus-visible:ring-2 focus-visible:ring-primary",
                        sort === k
                          ? "bg-primary/18 text-fg"
                          : "text-muted-fg hover:bg-surface-2/70 hover:text-fg",
                      )}
                    >
                      <span>{t(`sort.${k}`)}</span>
                      {sort === k ? <Check size={15} className="text-primary" aria-hidden="true" /> : null}
                    </button>
                  ))}
                </div>
              </PopoverContent>
            </Popover>
            </div>

            <div className="relative shrink-0">
              <IconButton
                disabled={dashboardOpen}
                onClick={() => setFilterOpen(true)}
                label={activeFilterCount > 0 ? `${t("action.filter")} (${activeFilterCount})` : t("action.filter")}
                tone="filter"
                icon={<Filter />}
              />
              {activeFilterCount > 0 ? (
                <span className="pointer-events-none absolute -right-1 -top-1 z-10 grid min-h-4 min-w-4 place-items-center rounded-full bg-primary px-1 text-fs-9 font-black leading-none text-primary-fg shadow-sm ring-1 ring-bg">
                  {activeFilterCount}
                </span>
              ) : null}
            </div>
            </>
            ) : null}

            {/* Nut them dang pill co chu: hanh dong chinh cua man hinh. Nut dai
                khong co tooltip nen aria-label giu ten day du "Them game". */}
            <IconButton
              onClick={openAdd}
              label={t("action.add")}
              aria-label={t("action.addGame")}
              icon={<Plus />}
              tone="add"
              wide
            />

            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <IconButton label={t("menu.more")} icon={<Menu />} />
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-64">
                <DropdownMenuItem
                  onSelect={() => {
                    if (authStatus === "guest") {
                      void logout();
                    } else {
                      setAccountOpen(true);
                    }
                  }}
                  className="items-center gap-3 py-2.5"
                >
                  <span className="grid h-9 w-9 shrink-0 place-items-center rounded-lg border border-primary/25 bg-primary/12 text-primary">
                    <UserRound size={18} aria-hidden="true" />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm font-extrabold text-fg">
                      {authStatus === "guest"
                        ? t("auth.signIn")
                        : (account?.username ?? t("menu.account"))}
                    </span>
                    <span className="block truncate text-xs font-medium text-muted-fg">
                      {authStatus === "guest" ? t("auth.guestLocalDescription") : account?.email}
                    </span>
                  </span>
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem
                  onSelect={() => setSettingsOpen(true)}
                >
                  <SettingsIcon size={15} /> {t("menu.setting")}
                </DropdownMenuItem>
                <DropdownMenuItem onSelect={() => setDashboardOpen(true)}>
                  <BarChart3 size={15} /> {t("menu.dashboard")}
                </DropdownMenuItem>
                <DropdownMenuItem onSelect={() => setTagsOpen(true)}>
                  <Tags size={15} /> {t("menu.manageTags")}
                </DropdownMenuItem>
                {/* Chi admin. User va guest KHONG render muc nay (khong phai disable)
                    — spec 2026-09-14 §2.1. */}
                {isAdmin ? (
                  <>
                    <DropdownMenuSeparator />
                    <DropdownMenuItem onSelect={enterAdminConsole}>
                      <ShieldCheck size={15} /> {t("menu.adminConsole")}
                    </DropdownMenuItem>
                  </>
                ) : null}
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>
      </header>

      <main
        ref={gridRef}
        className={cn("relative z-10 px-4 py-5 pb-20", hideLibraryChrome && "hidden")}
      >
        {dashboardOpen ? (
          <DashboardView onClose={() => setDashboardOpen(false)} />
        ) : tab === "queue" ? (
          <GameTable
            variant="queue"
            title={t("nav.queue")}
            games={queue.data ?? []}
            isLoading={queue.isPending}
            isSaving={queue.isSaving}
            error={
              queue.isError
                ? queue.error instanceof Error
                  ? queue.error.message
                  : String(queue.error)
                : null
            }
            onRetry={() => void queue.refetch()}
            onReorder={queue.reorder}
            onOpen={(game) => openDetail(game.id)}
            maxRows={QUEUE_MAX_ROWS}
          />
        ) : isError ? (
          // Loi API PHAI hien ra, khong duoc nuot (AGENTS.md).
          <div className="flex flex-col items-center gap-3 py-24 text-center">
            <h3 className="text-lg font-bold">{t("error.title")}</h3>
            <p className="max-w-md text-sm text-muted-fg">
              {error instanceof Error ? error.message : String(error)}
            </p>
            <IconButton
              onClick={() => void refetch()}
              label={t("error.retry")}
              tone="edit"
              icon={<RotateCcw />}
            />
          </div>
        ) : isPending ? (
          layout === "list" ? (
            <GameTable
              variant="list"
              title={t(`nav.${tab}`)}
              games={[]}
              isLoading
              error={null}
              onRetry={() => void refetch()}
              onOpen={(game) => openDetail(game.id)}
            />
          ) : (
            <div className="grid gap-4 [grid-template-columns:repeat(auto-fill,minmax(270px,1fr))]">
              {Array.from({ length: 14 }).map((_, i) => (
                <GameCardSkeleton key={i} />
              ))}
            </div>
          )
        ) : items.length === 0 && libraryIsEmpty ? (
          <div className="flex min-h-[60vh] flex-col items-center justify-center gap-4 px-4 text-center">
            <div className="grid size-20 place-items-center rounded-3xl bg-primary/15 text-primary ring-1 ring-primary/30">
              <Gamepad2 size={40} strokeWidth={1.6} />
            </div>
            <div className="flex max-w-md flex-col gap-1.5">
              <h3 className="text-xl font-extrabold">{t("empty.newUser")}</h3>
              <p className="text-sm text-muted-fg">{t("empty.newUserDesc")}</p>
            </div>
            <button
              type="button"
              onClick={openAdd}
              className="glass-surface mt-2 flex min-h-11 items-center justify-center gap-2 rounded-lg bg-primary px-6 font-bold text-primary-fg outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
            >
              <Plus size={18} />
              {t("action.addGame")}
            </button>
          </div>
        ) : items.length === 0 ? (
          <div className="flex flex-col items-center gap-3 py-24 text-center">
            <Search className="text-muted-fg" size={32} />
            <h3 className="text-lg font-bold">
              {query || activeFilterCount > 0 ? t("empty.noResults") : t("empty.noGames")}
            </h3>
            <p className="text-sm text-muted-fg">
              {query || activeFilterCount > 0 ? t("empty.noResultsDesc") : t("empty.noGamesDesc")}
            </p>
            <IconButton
              onClick={resetAll}
              label={t("action.reset")}
              icon={<RotateCcw />}
            />
          </div>
        ) : layout === "list" ? (
          <div className="animate-in fade-in duration-300">
            <GameTable
              variant="list"
              title={t(`nav.${tab}`)}
              games={items}
              isLoading={false}
              error={null}
              onRetry={() => void refetch()}
              onOpen={(game) => openDetail(game.id)}
              startNumber={(page - 1) * PAGE_SIZE + 1}
            />
          </div>
        ) : (
          <div className="animate-in fade-in grid gap-4 duration-300 [grid-template-columns:repeat(auto-fill,minmax(270px,1fr))]">
            {items.map((game) => (
              <GameCard
                key={game.id}
                game={game}
                hints={data?.cardHints}
                onOpen={() => openDetail(game.id)}
                onPlay={() => launchGame.mutate(game.id)}
              />
            ))}
          </div>
        )}
      </main>

      {/* Footer dac hon glass-panel mac dinh (34%) mot chut cho chu/nut de doc. */}
      <footer
        className={cn(
          "glass-panel fixed inset-x-0 bottom-0 z-40 border-t bg-[color-mix(in_oklab,var(--color-surface)_50%,transparent)]!",
          hideLibraryChrome && "hidden",
        )}
      >
        <div className="grid grid-cols-[1fr_auto_1fr] items-center gap-3 px-4 py-2 text-xs text-muted-fg">
          {/* Trai: khoang item dang hien. Trang thai tray KHONG lap lai o day —
              nut Tray goc phai da co cham xanh/xam cho viec do. */}
          <span className="rune-etch truncate">
            {/* Dashboard khong co danh sach item nao de dem. */}
            {dashboardOpen ? null : (<>
            {t("lib.showing")}{" "}
            {totalItems === 0 ? (
              <span className="font-bold text-fg">0</span>
            ) : (
              <>
                <span className="font-bold text-fg">{firstItem}</span>
                {" - "}
                <span className="font-bold text-fg">{lastItem}</span>
                {" "}
                {t("lib.of")}{" "}
                <span className="font-bold text-fg">{totalItems}</span>
              </>
            )}{" "}
            {t("lib.items")}
            </>)}
          </span>

          {/* Play Queue tra tron bo, khong phan trang — DOMAIN_RULES §8.1 — nen
              khong hien cum nay. Van phai co mot o trong, khong thi cot phai
              nhay sang giua trong luoi 3 cot. */}
          {tab === "queue" || dashboardOpen ? (
            <span />
          ) : (
            <div className="flex items-center justify-center gap-2">
              <IconButton
                disabled={page <= 1}
                onClick={() => goToPage(page - 1)}
                label={t("lib.prev")}
                icon={<ChevronLeft />}
                size="compact"
                tooltipSide="top"
              />
              <span className="font-semibold text-fg">
                {t("lib.page")} {page}/{totalPages}
              </span>
              <IconButton
                disabled={page >= totalPages}
                onClick={() => goToPage(page + 1)}
                label={t("lib.next")}
                icon={<ChevronRight />}
                size="compact"
                tooltipSide="top"
              />
            </div>
          )}

          {/* Phai: phim tat + nut tray co cham trang thai — dung vi tri Lovable.
              Truoc do toi nhet tray vao menu More, lech han bo cuc. */}
          <div className="flex items-center justify-end gap-1">
            <span className="mr-2 truncate text-muted-fg/55">
              {t("lib.version")} {t("lib.by")} Vidagrr
            </span>
            <Popover open={shortcutsOpen} onOpenChange={setShortcutsOpen}>
              <PopoverTrigger asChild>
                <IconButton
                  label={t(usingGamepad ? "help.controllerButtons" : "help.shortcuts")}
                  icon={usingGamepad ? <Gamepad2 /> : <Keyboard />}
                  size="compact"
                  tooltipSide="top"
                />
              </PopoverTrigger>
              <PopoverContent align="end" side="top" className="w-72 p-3">
                <p className="mb-2 text-sm font-bold text-fg">
                  {t(usingGamepad ? "help.controllerButtons" : "help.shortcuts")}
                </p>
                <ul className="space-y-1">
                  {(usingGamepad ? GAMEPAD_SHORTCUTS : SHORTCUTS).map(([k, v]) => (
                    <li key={k} className="flex justify-between gap-3">
                      <kbd className="rounded bg-surface-2 px-1.5 py-0.5 font-mono text-fs-11 font-bold text-fg">
                        {k}
                      </kbd>
                      <span className="text-right text-fs-11 text-muted-fg">{t(v)}</span>
                    </li>
                  ))}
                </ul>
              </PopoverContent>
            </Popover>

            <ReleaseNotificationsBell onOpenGame={openDetail} />

            {/* Cham trang thai nam NGOAI nut: .icon-button co overflow-hidden va
                bo tron, dat ben trong thi goc cham bi vien tron cat mat. */}
            <span className="relative inline-grid">
              <IconButton
                onClick={() => setTrayOpen(true)}
                tone="play"
                label={t("action.tray")}
                size="compact"
                tooltipSide="top"
                icon={<Gamepad2 />}
              />
              <span
                aria-hidden
                className={cn(
                  "pointer-events-none absolute -right-px -top-px h-2 w-2 rounded-full ring-2 ring-bg",
                  trayRunning ? "bg-success" : "bg-muted-fg",
                )}
              />
            </span>
          </div>
        </div>
      </footer>

      <ControllerPrompts
        visible={controller.promptPopupAvailable && controller.exitHoldSource == null && (
          controller.inputModality === "keyboard" ||
          (controller.inputModality === "gamepad" && controller.connected)
        )}
        open={controller.promptsOpen}
        triggerButton={controller.promptTriggerButton}
        triggerLabel={t("controller.openControls")}
        prompts={controller.prompts}
        ariaLabel={t("controller.controls")}
      />

      <FilterDialog
        open={filterOpen}
        onOpenChange={setFilterOpen}
        value={filters}
        onApply={(f) => {
          setFilters(f);
          setPage(1);
        }}
      />
      <SettingsDialog
        open={settingsOpen}
        onOpenChange={setSettingsOpen}
      />
      <AccountDialog
        open={accountOpen && authStatus === "signedIn"}
        onOpenChange={setAccountOpen}
      />
      <AddGameMethodDialog
        open={addMethodOpen}
        onOpenChange={setAddMethodOpen}
        onManual={openManualAdd}
        onMetadata={() => setMetadataPickerOpen(true)}
      />
      <MetadataGamePickerDialog
        open={metadataPickerOpen}
        onOpenChange={setMetadataPickerOpen}
      />
      <ManageTagsDialog open={tagsOpen} onOpenChange={setTagsOpen} />
      <TrayPanel open={trayOpen} onOpenChange={setTrayOpen} />
      <GuideTour
        open={viewModesGuide.open}
        steps={viewModesGuide.steps}
        eyebrow={viewModesGuide.eyebrow}
        onClose={viewModesGuide.close}
        finishAction={{
          label: viewModesGuide.tryLabel,
          icon: <ListOrdered />,
          onClick: () => {
            setTab("all");
            setPage(1);
            setDashboardOpen(false);
            /* Mo sau khi lop tour go xong — cu bam con dang do co the bi Popover
               coi la bam ra ngoai roi tu dong lai. */
            window.setTimeout(() => setSortProfileView("all"), 0);
          },
        }}
      />
      <GameDetailDialog
        gameId={selectedGameId}
        open={detailOpen}
        mode={detailMode}
        onOpenChange={(nextOpen) => {
          setDetailOpen(nextOpen);
          if (!nextOpen) setDetailMode("view");
        }}
        onCancelEdit={() => setDetailMode("view")}
        onSaved={(game) => {
          setSelectedGameId(game.id);
          setDetailMode("view");
        }}
      />
    </div>
  );
}

export { PAGE_SIZE };
