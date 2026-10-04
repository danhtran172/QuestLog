import { Check, Gamepad2, Loader2, Lock, Plus, Search, Trash2, X } from "lucide-react";
import { useEffect, useMemo, useRef, useState, type FocusEvent as ReactFocusEvent, type FormEvent } from "react";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { cn, thumbUrl } from "@/lib/utils";
import { useI18n } from "@/lib/i18n";
import { api } from "@/lib/api/client";
import { SEARCH_DEBOUNCE_MS } from "@/lib/api/domain";
import { PORTAL_SCROLLER_PROPS, usePortalWheelScroll } from "@/lib/usePortalWheelScroll";
import type { CatalogItem, MetadataGameCandidate } from "@/lib/api/domain";
import {
  RATING_MAX,
  formatPlaytimeSeconds,
  formatPublishDate,
  formatScore,
  isRatingInputDraft,
} from "@/lib/format";
import { GameCoverFlags } from "./GameCoverFlags";
import { CatalogPill } from "./Pills";
import { NUMBER_INPUT_NO_SPINNER } from "./InlineEditControls";
import { RatingScaleHint } from "./RatingScaleHint";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogTitle,
  IconButton,
  Popover,
  PopoverAnchor,
  PopoverContent,
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "./ui";

type Props = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
};

type UserDataDraft = {
  score: string;
  excited: string;
  playedHours: string;
  playedMinutes: string;
  progressId: string;
  ownershipId: string;
  exeName: string;
  launchTargetPath: string;
  saveFolderPath: string;
  backupFolderPath: string;
  note: string;
};

const emptyDraft = (): UserDataDraft => ({
  score: "",
  excited: "",
  playedHours: "",
  playedMinutes: "",
  progressId: "",
  ownershipId: "",
  exeName: "",
  launchTargetPath: "",
  saveFolderPath: "",
  backupFolderPath: "",
  note: "",
});

function seedDraft(_game: MetadataGameCandidate): UserDataDraft { return emptyDraft(); }

function CompactGameCard({
  game,
  selected,
  onClick,
}: {
  game: MetadataGameCandidate;
  selected: boolean;
  onClick: () => void;
}) {
  const { t } = useI18n();
  // O anh chi 80x56 — keo ban goc ve day la tai 24 tam 1920x1080 de ve thanh tem.
  const image = thumbUrl(game.bannerUrl ?? game.backdropUrl, 160);
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={selected}
      className={cn(
        "group relative flex min-w-0 gap-3 rounded-xl border p-2 text-left transition-colors",
        selected ? "border-primary/70 bg-primary/10" : "border-border bg-surface-2/25 hover:border-primary/40",
      )}
    >
      <div className="h-14 w-20 shrink-0 overflow-hidden rounded-lg bg-muted">
        {image ? <img src={image} alt="" width={80} height={56} loading="lazy" decoding="async" className="h-full w-full object-cover" /> : <div className="grid h-full place-items-center text-muted-fg"><Gamepad2 size={18} aria-hidden /></div>}
      </div>
      <span className="min-w-0 flex-1 py-0.5">
        <span className="block truncate text-sm font-extrabold text-fg">{game.name}</span>
        <span className="mt-1 block truncate text-xs text-muted-fg">
          {game.developer ?? game.publisher ?? formatPublishDate(game.publishDate) ?? t("addGame.noMetadata")}
        </span>
        {game.libraryStatus !== "notInLibrary" ? <span className={cn("mt-1 inline-flex rounded-full px-1.5 py-0.5 text-[10px] font-black", game.libraryStatus === "disabled" ? "bg-warning/15 text-warning" : "bg-primary/15 text-primary")}>{game.libraryStatus === "disabled" ? t("addGame.restoreRequired") : t("addGame.alreadyInLibrary")}</span> : null}
      </span>
      {selected ? <Check size={16} className="mt-1 shrink-0 text-primary" aria-hidden /> : null}
    </button>
  );
}

function SelectedGalleryCard({
  game,
  draft,
  progress,
  ownership,
  active,
  onOpen,
  onRemove,
}: {
  game: MetadataGameCandidate;
  draft: UserDataDraft;
  progress?: CatalogItem;
  ownership?: CatalogItem;
  active: boolean;
  onOpen: () => void;
  onRemove: () => void;
}) {
  const { t } = useI18n();
  const image = thumbUrl(game.bannerUrl ?? game.backdropUrl, 480);
  const playtimeSeconds = (Number(draft.playedHours) || 0) * 3600 + (Number(draft.playedMinutes) || 0) * 60;
  const playtime = playtimeSeconds > 0 ? formatPlaytimeSeconds(playtimeSeconds) : null;
  const score = draft.score ? formatScore(Number(draft.score)) : null;

  return (
    <article className={cn(
      "game-card surface-card group relative overflow-hidden transition-[border-color,box-shadow,transform] duration-150",
      active
        ? "border-primary/75 shadow-[0_16px_34px_-18px_color-mix(in_oklab,var(--color-primary)_70%,transparent)]"
        : "border-border hover:-translate-y-0.5 hover:border-primary/40",
    )}>
      <button
        type="button"
        onClick={onOpen}
        aria-pressed={active}
        aria-label={`${t("action.edit")}: ${game.name}`}
        className="block w-full text-left outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary/70"
      >
        <div className="relative aspect-[270/126.2] overflow-hidden bg-muted">
          {image ? <img src={image} alt="" loading="lazy" decoding="async" className="h-full w-full object-cover transition-transform duration-300 group-hover:scale-105" /> : <div className="grid h-full place-items-center text-muted-fg"><Gamepad2 size={28} aria-hidden /></div>}
          <GameCoverFlags isDlc={game.isDlc} isUpcoming={game.isUpcoming} publishDate={game.publishDate} />
          <span className="pointer-events-none absolute inset-0 bg-gradient-to-t from-black/55 via-transparent to-transparent" aria-hidden />
        </div>
        <div className="space-y-2 p-3">
          <div className="min-w-0 pr-7">
            <p className="line-clamp-1 text-sm font-black leading-5 text-fg">{game.name}</p>
            <p className="mt-0.5 line-clamp-1 text-xs font-medium text-muted-fg">{game.developer ?? game.publisher ?? t("addGame.noMetadata")}</p>
          </div>
          <div className="flex min-h-5 flex-wrap items-center gap-1.5 text-[11px] font-bold text-muted-fg">
            {progress ? <CatalogPill item={progress} /> : null}
            {ownership ? <CatalogPill item={ownership} /> : null}
            {score ? <span className="rounded-md border border-border bg-surface-2/60 px-1.5 py-0.5 text-fg">{t("sort.score")} {score}</span> : null}
            {playtime ? <span className="rounded-md border border-border bg-surface-2/60 px-1.5 py-0.5">{playtime}</span> : null}
            {formatPublishDate(game.publishDate) ? <span className="rounded-md border border-border bg-surface-2/60 px-1.5 py-0.5">{formatPublishDate(game.publishDate)}</span> : null}
          </div>
        </div>
      </button>
      <button
        type="button"
        onClick={onRemove}
        aria-label={`${t("action.remove")}: ${game.name}`}
        className="absolute right-2 top-2 z-10 grid h-7 w-7 place-items-center rounded-lg border border-white/15 bg-black/55 text-white/80 backdrop-blur-sm transition-colors hover:bg-danger/85 hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
      >
        <Trash2 size={14} aria-hidden />
      </button>
    </article>
  );
}

function CatalogSelect({
  value,
  items,
  placeholder,
  onChange,
}: {
  value: string;
  items: CatalogItem[] | undefined;
  placeholder: string;
  onChange: (value: string) => void;
}) {
  return (
    <Select value={value || "none"} onValueChange={(next) => onChange(next === "none" ? "" : next)}>
      <SelectTrigger className="h-9 w-full rounded-lg border border-border bg-surface px-3 text-xs">
        <SelectValue placeholder={placeholder} />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="none">{placeholder}</SelectItem>
        {(items ?? []).map((item) => <SelectItem key={item.id} value={item.id.toString()}>{item.displayName}</SelectItem>)}
      </SelectContent>
    </Select>
  );
}

export function MetadataGamePickerDialog({ open, onOpenChange }: Props) {
  const { t } = useI18n();
  const [searchInput, setSearchInput] = useState("");
  const [searchTerm, setSearchTerm] = useState("");
  const [searchOpen, setSearchOpen] = useState(false);
  const [debouncing, setDebouncing] = useState(false);
  const searchBoxRef = useRef<HTMLFormElement>(null);
  const resultsRef = useRef<HTMLDivElement>(null);
  const [selected, setSelected] = useState<Record<string, MetadataGameCandidate>>({});
  const [activeId, setActiveId] = useState<number | null>(null);
  const [drafts, setDrafts] = useState<Record<string, UserDataDraft>>({});

  const queryClient = useQueryClient();
  const results = useQuery({
    queryKey: ["metadata-games", searchTerm],
    queryFn: () => api.metadataGames({ q: searchTerm, pageSize: 24 }),
    enabled: open && searchTerm.trim().length >= 2,
    /*
     * Giu ket qua CU tren man hinh trong luc goi ban moi. Khong co no, moi ky tu
     * go them lam danh sach trong roi hien vong quay — voi o tim kiem go-toi-dau-
     * hien-toi-do thi man hinh nhap nhay lien tuc va CAM GIAC cham hon han thuc te,
     * du so lan goi mang khong doi.
     */
    placeholderData: keepPreviousData,
    /*
     * Kho metadata doi rat it. 15 giay khien viec xoa mot ky tu roi go lai — chuyen
     * xay ra lien tuc khi go — phai goi mang lai tu dau. 5 phut thi lan go lui lay
     * ngay tu cache.
     */
    staleTime: 5 * 60_000,
  });
  const progress = useQuery({ queryKey: ["catalog", "progress"], queryFn: () => api.catalog("progress"), enabled: open });
  const ownership = useQuery({ queryKey: ["catalog", "ownership"], queryFn: () => api.catalog("ownership"), enabled: open });
  const capabilities = useQuery({ queryKey: ["capabilities"], queryFn: () => api.capabilities(), enabled: open, staleTime: Infinity });
  // Backup dang tam tat o backend -> an o nhap thu muc backup.
  const canBackup = capabilities.data?.features.backup === true;
  const importMutation = useMutation({
    mutationFn: () => api.importGames(selectedGames.map((game) => {
      const draft = drafts[game.id] ?? emptyDraft();
      const launchPath = draft.launchTargetPath.trim();
      const extension = launchPath.toLowerCase().endsWith(".lnk") ? "shortcut" : "exe";
      return {
        metadataGameId: game.id,
        userData: {
          score: draft.score ? Number(draft.score) : null,
          excited: draft.excited ? Number(draft.excited) : null,
          totalPlaytimeSeconds: ((Number(draft.playedHours) || 0) * 3600) + ((Number(draft.playedMinutes) || 0) * 60),
          progressId: draft.progressId ? Number(draft.progressId) : null,
          ownershipId: draft.ownershipId ? Number(draft.ownershipId) : null,
          exeName: draft.exeName.trim() || null,
          launchTarget: launchPath ? { kind: extension, path: launchPath } : null,
          saveFolderPath: draft.saveFolderPath.trim() || null,
          backupFolderPath: draft.backupFolderPath.trim() || null,
          ownershipInfo: draft.note.trim() ? {
            steamAccount: null, steamPassword: null, ubisoftAccount: null, ubisoftPassword: null,
            supportDocumentUrl: null, downloadUrl: null, note: draft.note.trim(),
          } : null,
        },
      };
    })),
    onSuccess: (result) => {
      void queryClient.invalidateQueries({ queryKey: ["games"] });
      toast.success(`${result.importedCount} game(s) imported`);
      if (result.restoreRequiredCount === 0) onOpenChange(false);
    },
    onError: (error) => toast.error(error instanceof Error ? error.message : String(error)),
  });

  const selectedGames = useMemo(() => Object.values(selected), [selected]);
  const activeGame = activeId ? selected[activeId] : selectedGames[0];
  const activeDraft = activeGame ? (drafts[activeGame.id] ?? seedDraft(activeGame)) : emptyDraft();

  useEffect(() => {
    if (!open) {
      setSearchInput("");
      setSearchTerm("");
      setSearchOpen(false);
      setDebouncing(false);
      setSelected({});
      setActiveId(null);
      setDrafts({});
    }
  }, [open]);

  /*
   * Go toi dau tim toi do, hoan lai SEARCH_DEBOUNCE_MS (§6.1) — cung nhip voi o
   * tim kiem ngoai thu vien. Khong tu tim khi duoi 2 ky tu: server tu choi truy
   * van ngan (422 METADATA_QUERY_INVALID), goi len chi de an loi la lang phi.
   */
  useEffect(() => {
    const nextTerm = searchInput.trim();
    if (nextTerm === searchTerm) {
      setDebouncing(false);
      return;
    }

    setDebouncing(true);
    const timer = setTimeout(() => {
      setSearchTerm(nextTerm);
      setDebouncing(false);
      // Chi MO khi co ket qua de mo; go xoa het thi dong lai.
      if (nextTerm.length >= 2) setSearchOpen(true);
      else setSearchOpen(false);
    }, SEARCH_DEBOUNCE_MS);

    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchInput]);

  usePortalWheelScroll(searchOpen);

  /*
   * Dong popover khi focus roi khoi CA HAI vung: o search va chinh danh sach.
   *
   * Phai gan cho ca hai. Bam mot the ket qua lam focus nhay VAO popover; tu do
   * di tiep xuong o userdata thi `onBlur` cua o search khong con duoc goi nua —
   * popover se ke o lai che mat dung khu vua can dung.
   *
   * `relatedTarget` null (bam vao cho khong nhan focus) cung tinh la roi di.
   */
  const closeIfFocusLeft = (event: ReactFocusEvent<HTMLElement>) => {
    const next = event.relatedTarget as Node | null;
    if (next && (resultsRef.current?.contains(next) || searchBoxRef.current?.contains(next))) return;
    setSearchOpen(false);
  };

  /* Enter = tim NGAY, khong doi het delay. */
  const submitSearch = (event: FormEvent) => {
    event.preventDefault();
    const nextTerm = searchInput.trim();
    setSearchTerm(nextTerm);
    setDebouncing(false);
    setSearchOpen(nextTerm.length >= 2);
  };

  const toggleSelected = (game: MetadataGameCandidate) => {
    setSelected((current) => {
      if (current[game.id]) {
        const next = { ...current };
        delete next[game.id];
        return next;
      }
      return { ...current, [game.id]: game };
    });
    setDrafts((current) => current[game.id] ? current : { ...current, [game.id]: seedDraft(game) });
    setActiveId(game.id);
  };

  const removeSelected = (id: number) => {
    setSelected((current) => {
      const next = { ...current };
      delete next[id];
      return next;
    });
    setDrafts((current) => {
      const next = { ...current };
      delete next[id];
      return next;
    });
    setActiveId((current) => current === id ? (selectedGames.find((game) => game.id !== id)?.id ?? null) : current);
  };

  const patchDraft = <K extends keyof UserDataDraft>(key: K, value: UserDataDraft[K]) => {
    if (!activeGame) return;
    setDrafts((current) => ({ ...current, [activeGame.id]: { ...activeDraft, [key]: value } }));
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-6xl overflow-hidden p-0">
        <div className="flex max-h-[88vh] min-h-[650px] flex-col">
          <div className="border-b border-border px-5 py-4 pr-14">
            <DialogTitle>{t("addGame.metadataTitle")}</DialogTitle>
            <DialogDescription>{t("addGame.metadataDescription")}</DialogDescription>
            <Popover open={searchOpen} onOpenChange={setSearchOpen}>
              <PopoverAnchor asChild>
                <form
                  ref={searchBoxRef}
                  onSubmit={submitSearch}
                  /* Con tro vao o la hien lai danh sach, khong bat go lai tu dau. */
                  onFocus={() => { if (searchInput.trim().length >= 2) setSearchOpen(true); }}
                  onBlur={closeIfFocusLeft}
                  className="mt-4 flex gap-2"
                >
                  <div className="relative min-w-0 flex-1">
                    <Search size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted-fg" aria-hidden />
                    <input
                      value={searchInput}
                      onChange={(event) => setSearchInput(event.target.value)}
                      placeholder={t("addGame.searchPlaceholder")}
                      className="h-10 w-full rounded-lg border border-border bg-surface pl-9 pr-3 text-sm font-medium text-fg outline-none focus:border-primary"
                    />
                  </div>
                  <IconButton
                    type="submit"
                    label={t("action.search")}
                    tone="add"
                    icon={debouncing || results.isFetching ? <Loader2 className="animate-spin" /> : <Search />}
                  />
                </form>
              </PopoverAnchor>
              <PopoverContent
                {...PORTAL_SCROLLER_PROPS}
                align="start"
                sideOffset={8}
                className="max-h-[58vh] w-[min(760px,calc(100vw-32px))] overflow-y-auto p-3"
                /*
                 * Mac dinh Radix keo focus vao noi dung popover khi mo. Voi tim
                 * kiem go-toi-dau-hien-toi-do thi ky tu thu hai se mo popover va
                 * CUOP con tro — nguoi dung khong go tiep duoc. Phai chan.
                 */
                onOpenAutoFocus={(event) => event.preventDefault()}
                /*
                 * O search nam NGOAI noi dung popover (no la anchor), nen Radix coi
                 * moi cu bam vao do la "bam ra ngoai" va dong popover ngay. Bo qua
                 * nhung su kien phat ra tu chinh o search.
                 */
                onInteractOutside={(event) => {
                  if (searchBoxRef.current?.contains(event.target as Node)) event.preventDefault();
                }}
              >
                <div ref={resultsRef} onBlur={closeIfFocusLeft}>
                <div className="mb-3 flex items-center justify-between gap-2">
                  <div>
                    <p className="text-xs font-black uppercase tracking-[0.14em] text-muted-fg">{t("addGame.searchResults")}</p>
                    <p className="mt-1 text-xs text-muted-fg">{searchTerm}</p>
                  </div>
                  {searchTerm ? <span className="rounded-full bg-primary/12 px-2 py-0.5 text-xs font-black text-primary">{results.data?.totalItems ?? 0}</span> : null}
                </div>
                {results.isError ? <p className="py-6 text-sm text-danger">{results.error instanceof Error ? results.error.message : String(results.error)}</p> : null}
                {results.isPending ? <div className="grid min-h-28 place-items-center text-muted-fg"><Loader2 className="animate-spin" /></div> : null}
                {!results.isPending && !results.isError ? (
                  <div className="grid gap-2 sm:grid-cols-2">
                    {(results.data?.items ?? []).map((game) => <CompactGameCard key={game.id} game={game} selected={Boolean(selected[game.id])} onClick={() => toggleSelected(game)} />)}
                  </div>
                ) : null}
                {!results.isPending && !results.isError && (results.data?.items.length ?? 0) === 0 ? <p className="py-6 text-center text-sm text-muted-fg">{t("addGame.noResults")}</p> : null}
                </div>
              </PopoverContent>
            </Popover>
          </div>

          <div className="grid min-h-0 flex-1 grid-cols-[minmax(0,1fr)_minmax(300px,0.86fr)]">
            <section className="min-h-0 overflow-y-auto border-r border-border p-4">
              <div className="mb-3 flex items-center justify-between gap-2">
                <p className="text-xs font-black uppercase tracking-[0.14em] text-muted-fg">{t("addGame.selectedGames")}</p>
                <span className="rounded-full bg-primary/15 px-2 py-0.5 text-xs font-black text-primary">{selectedGames.length}</span>
              </div>
              {selectedGames.length > 0 ? (
                <div className="grid gap-4 sm:grid-cols-2">
                  {selectedGames.map((game) => <SelectedGalleryCard
                    key={game.id}
                    game={game}
                    draft={drafts[game.id] ?? emptyDraft()}
                    progress={(progress.data ?? []).find((item) => item.id.toString() === (drafts[game.id]?.progressId ?? ""))}
                    ownership={(ownership.data ?? []).find((item) => item.id.toString() === (drafts[game.id]?.ownershipId ?? ""))}
                    active={activeGame?.id === game.id}
                    onOpen={() => setActiveId(game.id)}
                    onRemove={() => removeSelected(game.id)}
                  />)}
                </div>
              ) : <div className="grid min-h-[420px] place-items-center text-center text-sm text-muted-fg"><div><Gamepad2 size={32} className="mx-auto mb-3 opacity-50" aria-hidden /><p>{t("addGame.searchHint")}</p></div></div>}
            </section>

            <section className="min-h-0 overflow-y-auto bg-surface-2/15 p-4">
              {activeGame ? (
                <>
                  <div className="mb-4 flex items-start gap-3">
                    <div className="h-16 w-24 shrink-0 overflow-hidden rounded-lg bg-muted">
                      {(activeGame.bannerUrl ?? activeGame.backdropUrl) ? <img src={activeGame.bannerUrl ?? activeGame.backdropUrl ?? ""} alt="" className="h-full w-full object-cover" /> : <div className="grid h-full place-items-center text-muted-fg"><Gamepad2 size={22} aria-hidden /></div>}
                    </div>
                    <div className="min-w-0">
                      <p className="truncate text-base font-black text-fg">{activeGame.name}</p>
                      <p className="mt-1 text-xs text-muted-fg">{t("addGame.userDataOnly")}</p>
                    </div>
                  </div>

                  {/* Metadata VAN hien de doi chieu, nhung khoa san — chi la
                      tham khao. Sua that su thi lam sau khi da them vao thu
                      vien, o the sua game (khong khoa nua). */}
                  <div className="mb-4 rounded-lg border border-border/70 bg-surface-2/25 p-3">
                    <div className="mb-2 flex items-center gap-1.5 text-[11px] font-black uppercase tracking-[0.14em] text-muted-fg">
                      <Lock size={12} aria-hidden />
                      {t("addGame.metadataPreview")}
                    </div>
                    <dl className="grid grid-cols-2 gap-x-3 gap-y-1 text-xs">
                      <dt className="text-muted-fg">{t("editor.developer")}</dt>
                      <dd className="truncate text-right text-fg">{activeGame.developer || "—"}</dd>
                      <dt className="text-muted-fg">{t("detail.publisher")}</dt>
                      <dd className="truncate text-right text-fg">{activeGame.publisher || "—"}</dd>
                      <dt className="text-muted-fg">{t("sort.publish")}</dt>
                      <dd className="truncate text-right text-fg">{formatPublishDate(activeGame.publishDate) || "—"}</dd>
                    </dl>
                    <p className="mt-2 text-[11px] leading-4 text-muted-fg">{t("addGame.editMetadataLaterHint")}</p>
                  </div>

                  <div className="space-y-4">
                    <div className="grid grid-cols-2 gap-3">
                      <label className="grid gap-1.5 text-xs font-bold text-muted-fg">{t("sort.score")}<input type="number" min="0" max={RATING_MAX} step="0.5" value={activeDraft.score} onChange={(event) => { if (isRatingInputDraft(event.target.value)) patchDraft("score", event.target.value); }} className={cn(NUMBER_INPUT_NO_SPINNER, "h-9 rounded-lg border border-border bg-surface px-3 text-sm text-fg outline-none focus:border-primary")} /></label>
                      <label className="grid gap-1.5 text-xs font-bold text-muted-fg">{t("sort.excited")}<input type="number" min="0" max={RATING_MAX} step="0.5" value={activeDraft.excited} onChange={(event) => { if (isRatingInputDraft(event.target.value)) patchDraft("excited", event.target.value); }} className={cn(NUMBER_INPUT_NO_SPINNER, "h-9 rounded-lg border border-border bg-surface px-3 text-sm text-fg outline-none focus:border-primary")} /></label>
                    </div>
                    <RatingScaleHint />
                    <div className="grid grid-cols-2 gap-3">
                      <label className="grid gap-1.5 text-xs font-bold text-muted-fg">{t("sort.progress")}<CatalogSelect value={activeDraft.progressId} items={progress.data} placeholder={t("editor.none")} onChange={(value) => patchDraft("progressId", value)} /></label>
                      <label className="grid gap-1.5 text-xs font-bold text-muted-fg">{t("sort.ownership")}<CatalogSelect value={activeDraft.ownershipId} items={ownership.data} placeholder={t("editor.none")} onChange={(value) => patchDraft("ownershipId", value)} /></label>
                    </div>
                    <div>
                      <p className="mb-1.5 text-xs font-bold text-muted-fg">{t("editor.playedTime")}</p>
                      <div className="grid grid-cols-2 gap-3"><input aria-label={t("editor.playedHours")} type="number" min="0" value={activeDraft.playedHours} onChange={(event) => patchDraft("playedHours", event.target.value)} placeholder={t("editor.playedHours")} className="h-9 rounded-lg border border-border bg-surface px-3 text-sm text-fg outline-none focus:border-primary" /><input aria-label={t("editor.playedMinutes")} type="number" min="0" max="59" value={activeDraft.playedMinutes} onChange={(event) => patchDraft("playedMinutes", event.target.value)} placeholder={t("editor.playedMinutes")} className="h-9 rounded-lg border border-border bg-surface px-3 text-sm text-fg outline-none focus:border-primary" /></div>
                    </div>
                    <div className="border-t border-border pt-4">
                      <p className="mb-3 text-xs font-black uppercase tracking-[0.14em] text-muted-fg">{t("addGame.deviceData")}</p>
                      <div className="space-y-3">
                        <label className="grid gap-1.5 text-xs font-bold text-muted-fg">{t("editor.process")}<input value={activeDraft.exeName} onChange={(event) => patchDraft("exeName", event.target.value)} className="h-9 rounded-lg border border-border bg-surface px-3 text-sm text-fg outline-none focus:border-primary" /></label>
                        <label className="grid gap-1.5 text-xs font-bold text-muted-fg">{t("editor.launchTarget")}<input value={activeDraft.launchTargetPath} onChange={(event) => patchDraft("launchTargetPath", event.target.value)} placeholder={t("editor.pickLaunchTarget")} className="h-9 rounded-lg border border-border bg-surface px-3 text-sm text-fg outline-none focus:border-primary" /></label>
                        <label className="grid gap-1.5 text-xs font-bold text-muted-fg">{t("editor.saveFolder")}<input value={activeDraft.saveFolderPath} onChange={(event) => patchDraft("saveFolderPath", event.target.value)} className="h-9 rounded-lg border border-border bg-surface px-3 text-sm text-fg outline-none focus:border-primary" /></label>
                        {canBackup && <label className="grid gap-1.5 text-xs font-bold text-muted-fg">{t("editor.backupFolder")}<input value={activeDraft.backupFolderPath} onChange={(event) => patchDraft("backupFolderPath", event.target.value)} className="h-9 rounded-lg border border-border bg-surface px-3 text-sm text-fg outline-none focus:border-primary" /></label>}
                        <label className="grid gap-1.5 text-xs font-bold text-muted-fg">{t("editor.note")}<textarea value={activeDraft.note} onChange={(event) => patchDraft("note", event.target.value)} className="min-h-20 rounded-lg border border-border bg-surface px-3 py-2 text-sm text-fg outline-none focus:border-primary" /></label>
                      </div>
                    </div>
                  </div>
                </>
              ) : <div className="grid h-full place-items-center text-center text-sm text-muted-fg"><div><Gamepad2 size={30} className="mx-auto mb-3 opacity-50" aria-hidden /><p>{t("addGame.selectGameHint")}</p></div></div>}
            </section>
          </div>
          <DialogFooter className="justify-between">
            <p className="text-xs text-muted-fg">{t("addGame.saveDisabled")}</p>
            <div className="flex gap-2"><IconButton label={t("action.cancel")} tone="neutral" icon={<X />} onClick={() => onOpenChange(false)} /><IconButton label={t("addGame.addSelected")} tone="add" icon={importMutation.isPending ? <Loader2 className="animate-spin" /> : <Plus />} disabled={selectedGames.length === 0 || importMutation.isPending} onClick={() => importMutation.mutate()} /></div>
          </DialogFooter>
        </div>
      </DialogContent>
    </Dialog>
  );
}
