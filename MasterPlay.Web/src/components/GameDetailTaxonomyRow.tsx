import { Check, Loader2, Plus, Search, Trash2, X } from "lucide-react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState, type ReactNode } from "react";
import { toast } from "sonner";
import type { GameDetail, TaxonomyItem } from "@/lib/api/domain";
import { api } from "@/lib/api/client";
import { gameDetailApi } from "@/lib/gameDetailApi";
import {
  addTaxonomyValue,
  addTaxonomyValues,
  canCreateTaxonomyName,
  removeTaxonomyValue,
  toggleTaxonomyValue,
} from "@/lib/gameTaxonomyEdit";
import { useI18n } from "@/lib/i18n";
import { useExitOnOutsidePointer } from "@/lib/useExitOnOutsidePointer";
import {
  PORTAL_SCROLLER_PROPS,
  PORTAL_SCROLL_ROOT_PROPS,
  usePortalWheelScroll,
} from "@/lib/usePortalWheelScroll";
import { cn, iconSrc } from "@/lib/utils";
import { InlineEditButton } from "./InlineEditControls";
import { ThemePickerDialog } from "./themes/ThemePickerDialog";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogTitle,
  IconButton,
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "./ui";

export type GameTaxonomyKind = "genres" | "themes";

/**
 * Hang Genre/Theme cua the chi tiet, sua duoc tai cho.
 *
 * Sua o DAY chu khong bat mo trinh sua game: hai hang nay la thu hay phai chinh
 * nhat sau khi dong bo metadata ve lech. Nut but nam canh nhan, giong cac hang
 * inline edit khac cua the (`GameDetailMetadataQuickRows`).
 *
 * Quy uoc: dau `+` THEM, dau `x` tren chip BO — bo thi hoi lai. Gia tri chi roi
 * khoi GAME NAY, bang taxonomy dung chung khong bi dong toi.
 */
export function GameDetailTaxonomyRow({
  game,
  kind,
  label,
  icon,
  onSaved,
}: {
  game: GameDetail;
  kind: GameTaxonomyKind;
  label: string;
  icon: ReactNode;
  onSaved: (game: GameDetail) => void;
}) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [pickerOpen, setPickerOpen] = useState(false);
  /* Theme dung bang chon LON theo nhom (spec 2026-10-04 §6.4); genre giu popover. */
  const [themeDialogOpen, setThemeDialogOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [query, setQuery] = useState("");
  const [pendingRemoval, setPendingRemoval] = useState<TaxonomyItem | null>(null);
  /**
   * Nhung gia tri vua tick trong lan mo popover nay, CHUA luu. Giu lai de tick
   * bao nhieu cai cung duoc roi day len bang MOT lan PUT — moi cai mot lan PUT
   * thi hai lenh luu ke nhau se doc trung mot ban cache va an mat nhau.
   */
  const [draft, setDraft] = useState<TaxonomyItem[]>([]);
  /* Ban sao dong bo cua `draft`: luc chot phai xoa duoc ngay trong cung mot
     nhip, khong cho toi luot render sau — neu khong, popover dong bang hai
     duong (bam ra ngoai + nut Xong) se chot hai lan cung mot lo. */
  const draftRef = useRef<TaxonomyItem[]>(draft);
  draftRef.current = draft;
  const rowRef = useRef<HTMLDivElement>(null);

  const values = game[kind] ?? [];

  useEffect(() => {
    const timer = window.setTimeout(() => setQuery(search.trim()), 240);
    return () => window.clearTimeout(timer);
  }, [search]);

  const results = useQuery({
    queryKey: ["taxonomy", kind, query],
    queryFn: () => api.taxonomy(kind, query || undefined),
    enabled: pickerOpen && kind === "genres",
  });

  /* Popover nay mo BEN TRONG the chi tiet — mot Dialog dang khoa cuon. Khong co
     doan nay thi con lan chuot trong danh sach khong an gi. */
  usePortalWheelScroll(pickerOpen);

  const save = useMutation({
    /**
     * Doc ban MOI NHAT tu cache roi moi bien doi: the chi tiet con nhieu cho sua
     * khac cung goi PUT nay, lay `game` tu props se ghi de mat thay doi cua ho.
     */
    mutationFn: (change: (current: TaxonomyItem[]) => TaxonomyItem[]) => {
      const latest = queryClient.getQueryData<GameDetail>(["game-detail", game.id]) ?? game;
      const next = change(latest[kind] ?? []);
      return gameDetailApi.update(game.id, {
        ...latest,
        ...(kind === "genres" ? { genres: next } : { themes: next }),
      });
    },
    onSuccess: async (saved) => {
      queryClient.setQueryData(["game-detail", saved.id], saved);
      setThemeDialogOpen(false);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["games"] }),
        queryClient.invalidateQueries({ queryKey: ["play-queue"] }),
        queryClient.invalidateQueries({ queryKey: ["dashboard-stats"] }),
      ]);
      onSaved(saved);
    },
    onError: (error) => toast.error(error instanceof Error ? error.message : String(error)),
  });

  const create = useMutation({
    mutationFn: async (name: string) => {
      const created = await api.createTaxonomy(kind, { id: 0, name, iconData: null, sortOrder: 0 });
      await queryClient.invalidateQueries({ queryKey: ["taxonomy", kind] });
      return created;
    },
    /* Tao xong thi CHI tick vao ban nhap va giu popover mo — dang trong luong
       chon nhieu, dong lai la cat ngang. */
    onSuccess: (created) => {
      setSearch("");
      setQuery("");
      setDraft((current) => addTaxonomyValue(current, created));
    },
    onError: (error) => toast.error(error instanceof Error ? error.message : String(error)),
  });

  const busy = save.isPending || create.isPending;

  /** Chot ca lo vua tick bang mot lan PUT. Goi lai lan nua la vo hai. */
  const commitDraft = () => {
    const picks = draftRef.current;
    draftRef.current = [];
    setDraft([]);
    setSearch("");
    setQuery("");
    if (picks.length) save.mutate((current) => addTaxonomyValues(current, picks));
  };

  const closePicker = () => {
    setPickerOpen(false);
    commitDraft();
  };

  const stopEditing = () => {
    closePicker();
    setEditing(false);
  };

  /* Bam ra ngoai hang la ket thuc viec sua — va vi moi thay doi deu luu ngay
     luc thuc hien, ket thuc cung dong nghia da luu xong. */
  useExitOnOutsidePointer(editing && !pendingRemoval && !themeDialogOpen, rowRef, stopEditing);

  /**
   * Ap THAY DOI (them/bo so voi luc mo dialog) len ban moi nhat trong cache, khong
   * ghi de nguyen danh sach — giu duoc thay doi khac vua luu trong luc dialog mo.
   */
  const saveThemePicks = (next: TaxonomyItem[]) => {
    const initialIds = new Set(values.map((item) => item.id));
    const nextIds = new Set(next.map((item) => item.id));
    const added = next.filter((item) => !initialIds.has(item.id));
    const removedIds = new Set(values.filter((item) => !nextIds.has(item.id)).map((item) => item.id));
    save.mutate((current) => addTaxonomyValues(current.filter((item) => !removedIds.has(item.id)), added));
  };

  const confirmRemoval = () => {
    const item = pendingRemoval;
    if (!item) return;
    setPendingRemoval(null);
    save.mutate((current) => removeTaxonomyValue(current, item.id));
  };

  const canCreate = canCreateTaxonomyName(search, query, results.data);
  const pickerError = results.error ?? create.error;

  /* Nhay dup o BAT CU dau trong hang cung vao che do sua — nhan, gia tri, hay
     khoang trong giua chung. Bat moi hang mot muc tieu nho rieng la bat nguoi
     dung ngam trung. */
  const startEditingOnDoubleClick = editing || busy ? undefined : () => setEditing(true);

  return (
    <div ref={rowRef} className="group/inline-edit contents">
      <dt
        className="flex items-center gap-1.5 font-semibold text-muted-fg"
        onDoubleClick={startEditingOnDoubleClick}
      >
        <span className="grid shrink-0 place-items-center">{icon}</span>
        <span>{label}:</span>
        {editing ? null : (
          <InlineEditButton
            label={`${t("action.edit")} ${label}`}
            disabled={busy}
            onClick={() => setEditing(true)}
          />
        )}
      </dt>

      <dd
        className={cn("min-w-0 break-words font-semibold text-fg", !editing && "cursor-text select-none")}
        onDoubleClick={startEditingOnDoubleClick}
      >
        {editing ? (
          <span className="flex flex-wrap items-center gap-1.5">
            {values.map((item) => (
              <span
                key={item.id}
                className="inline-flex items-center gap-1 rounded-md border border-border/70 bg-surface-2/70 px-1.5 py-0.5 text-xs font-semibold text-fg"
              >
                {iconSrc(item.iconData) ? (
                  <img src={iconSrc(item.iconData)} alt="" className="h-3 w-3 shrink-0 object-contain" />
                ) : null}
                {item.name}
                <button
                  type="button"
                  aria-label={`${t("action.remove")} ${item.name}`}
                  title={`${t("action.remove")} ${item.name}`}
                  disabled={busy}
                  className={cn(
                    "opacity-55 transition hover:opacity-100",
                    "focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-primary/70",
                    "disabled:cursor-wait disabled:opacity-30",
                  )}
                  onClick={() => setPendingRemoval(item)}
                >
                  <X size={11} />
                </button>
              </span>
            ))}

            {kind === "themes" ? (
              <>
                <button
                  type="button"
                  aria-label={`${t("action.add")} ${label}`}
                  title={`${t("action.add")} ${label}`}
                  disabled={busy}
                  onClick={() => setThemeDialogOpen(true)}
                  className={cn(
                    "inline-grid h-[22px] w-[22px] place-items-center rounded-md border border-dashed border-border text-muted-fg transition",
                    "hover:border-primary/70 hover:text-primary",
                    "focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-primary/70",
                    "disabled:cursor-wait disabled:opacity-45",
                  )}
                >
                  {busy ? <Loader2 size={12} className="animate-spin" /> : <Plus size={12} />}
                </button>
                <ThemePickerDialog
                  open={themeDialogOpen}
                  onOpenChange={setThemeDialogOpen}
                  title={label}
                  selected={values}
                  saving={save.isPending}
                  onSave={saveThemePicks}
                />
              </>
            ) : (
            <Popover
              open={pickerOpen}
              onOpenChange={(next) => {
                if (busy) return;
                if (next) setPickerOpen(true);
                else closePicker();
              }}
            >
              <PopoverTrigger asChild>
                <button
                  type="button"
                  aria-label={`${t("action.add")} ${label}`}
                  title={`${t("action.add")} ${label}`}
                  disabled={busy}
                  className={cn(
                    "inline-grid h-[22px] w-[22px] place-items-center rounded-md border border-dashed border-border text-muted-fg transition",
                    "hover:border-primary/70 hover:text-primary",
                    "focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-primary/70",
                    "disabled:cursor-wait disabled:opacity-45",
                  )}
                >
                  {busy ? <Loader2 size={12} className="animate-spin" /> : <Plus size={12} />}
                </button>
              </PopoverTrigger>
              <PopoverContent
                {...PORTAL_SCROLL_ROOT_PROPS}
                align="start"
                className="w-[min(320px,calc(100vw-32px))] space-y-2 p-2"
                /*
                 * Chan su kien lan chuot tai day.
                 *
                 * Popover duoc cam ra `<body>`, nhung su kien TONG HOP cua React
                 * van di theo CAY COMPONENT — no chay thang len `onWheel` cua the
                 * chi tiet va bi hieu thanh "lat trang", nen cuon trong popover
                 * lai lam trang phia sau truot di. Khoi `usePortalWheelScroll` da
                 * lo phan cuon that o tren roi.
                 */
                onWheel={(event) => event.stopPropagation()}
              >
                <div className="relative">
                  <Search size={14} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted-fg" />
                  <input
                    autoFocus
                    aria-label={t("editor.taxonomySearch")}
                    placeholder={t("editor.taxonomySearch")}
                    value={search}
                    onChange={(event) => setSearch(event.target.value)}
                    className={cn(
                      "h-9 w-full rounded-xl border border-border/80 bg-surface/55 pl-9 pr-3 text-sm font-medium text-fg",
                      "outline-none backdrop-blur-md transition focus:border-primary/80 focus:ring-2 focus:ring-primary/20",
                    )}
                  />
                </div>

                {pickerError ? (
                  <p role="alert" className="rounded-lg bg-danger/10 p-2 text-xs text-danger">
                    {pickerError instanceof Error ? pickerError.message : String(pickerError)}
                  </p>
                ) : null}

                <div
                  {...PORTAL_SCROLLER_PROPS}
                  role="listbox"
                  aria-multiselectable
                  aria-label={label}
                  className="scrollbar-thin max-h-56 space-y-1 overflow-y-auto overscroll-contain"
                >
                  {results.isPending ? (
                    <Loader2 size={16} className="mx-auto my-4 animate-spin text-muted-fg" />
                  ) : results.data?.length ? (
                    results.data.map((item) => {
                      const already = values.some((value) => value.id === item.id);
                      const picked = draft.some((value) => value.id === item.id);

                      return (
                        <button
                          key={item.id}
                          type="button"
                          /* Gia tri DA LUU thi bam khong lam gi — `+` chi THEM,
                             con bo la viec cua dau `x` tren chip (co buoc hoi
                             lai). Thu vua tick thi bam lai de bo tick. */
                          disabled={already || busy}
                          role="option"
                          aria-selected={already || picked}
                          aria-label={item.name}
                          className={cn(
                            "flex w-full min-w-0 items-center gap-2 rounded-lg px-2.5 py-2 text-left text-sm font-semibold transition",
                            already ? "text-muted-fg" : "hover:bg-surface-2/55",
                            "disabled:cursor-default",
                          )}
                          onClick={() => setDraft((current) => toggleTaxonomyValue(current, item))}
                        >
                          <span
                            className={cn(
                              "grid h-4 w-4 shrink-0 place-items-center rounded border",
                              already || picked
                                ? "border-primary bg-primary text-primary-fg"
                                : "border-border",
                            )}
                          >
                            {already || picked ? <Check size={11} /> : null}
                          </span>
                          {iconSrc(item.iconData) ? (
                            <img src={iconSrc(item.iconData)} alt="" className="h-4 w-4 shrink-0 object-contain" />
                          ) : null}
                          <span className="truncate">{item.name}</span>
                        </button>
                      );
                    })
                  ) : (
                    <p className="py-4 text-center text-xs text-muted-fg">{t("editor.taxonomyNoMatch")}</p>
                  )}
                </div>

                {canCreate ? (
                  <button
                    type="button"
                    disabled={create.isPending}
                    className="flex w-full items-center gap-2 rounded-lg border border-primary/25 bg-primary/10 px-3 py-2 text-sm font-bold text-primary disabled:opacity-60"
                    onClick={() => create.mutate(search.trim())}
                  >
                    {create.isPending ? <Loader2 size={14} className="animate-spin" /> : <Plus size={14} />}
                    {t("editor.taxonomyCreate")} “{search.trim()}”
                  </button>
                ) : null}

                {/* Chot ca lo va LUU LUON. Dong popover bang cach nao cung chot
                    y het vay — nut nay de thay ro da tick may cai. */}
                <button
                  type="button"
                  className={cn(
                    "flex w-full items-center justify-center gap-2 rounded-lg px-3 py-2 text-sm font-bold transition",
                    draft.length
                      ? "border border-primary/35 bg-primary/15 text-primary hover:bg-primary/25"
                      : "border border-border/60 text-muted-fg hover:bg-surface-2/55",
                  )}
                  onClick={closePicker}
                >
                  {draft.length ? <Plus size={14} /> : <Check size={14} />}
                  {draft.length ? `${t("action.add")} (${draft.length})` : t("action.done")}
                </button>
              </PopoverContent>
            </Popover>
            )}
          </span>
        ) : values.length ? (
          <span className="flex flex-wrap items-center gap-x-3 gap-y-1">
            {values.map((item) => (
              <span key={item.id} className="inline-flex items-center gap-1 font-semibold">
                {iconSrc(item.iconData) ? (
                  <img src={iconSrc(item.iconData)} alt="" className="h-3.5 w-3.5 shrink-0 object-contain" />
                ) : null}
                {item.name}
              </span>
            ))}
          </span>
        ) : (
          <span className="text-muted-fg">—</span>
        )}
      </dd>

      <div className="-my-1 justify-self-end">
        {/* Chi THOAT che do sua — moi thay doi da luu ngay luc thuc hien, o day
            khong con gi de xac nhan. Vi vay la dau `x`, khong phai dau tick:
            mot dau tick o day doc ra thanh "con mot lan luu nua". */}
        {editing ? (
          <IconButton
            size="mini"
            label={t("action.done")}
            icon={save.isPending ? <Loader2 className="animate-spin" /> : <X />}
            disabled={busy}
            onClick={stopEditing}
          />
        ) : null}
      </div>

      <Dialog open={pendingRemoval !== null} onOpenChange={(next) => { if (!next) setPendingRemoval(null); }}>
        <DialogContent className="max-w-sm">
          <div className="space-y-2 p-5 pr-14">
            <DialogTitle>{t("detail.taxonomyRemoveTitle")}</DialogTitle>
            <DialogDescription>
              <strong className="font-bold text-fg">{pendingRemoval?.name}</strong>
              {" — "}
              {t("detail.taxonomyRemoveHint")}
            </DialogDescription>
          </div>
          <DialogFooter>
            <IconButton label={t("action.cancel")} icon={<X />} onClick={() => setPendingRemoval(null)} />
            {/* Khoa trong luc dang co lenh luu khac bay: hai lenh PUT chong nhau
                deu doc cung mot ban cache, lenh ve sau se an mat lenh truoc. */}
            <IconButton
              tone="danger"
              label={t("action.remove")}
              icon={busy ? <Loader2 className="animate-spin" /> : <Trash2 />}
              disabled={busy}
              onClick={confirmRemoval}
            />
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
