import {
  AlertTriangle,
  Archive,
  ChevronDown,
  ChevronRight,
  ChevronUp,
  CalendarDays,
  FolderSymlink,
  Gamepad2,
  History,
  Image as ImageIcon,
  KeyRound,
  Library,
  ListChecks,
  Loader2,
  Package,
  Palette,
  Pencil,
  RotateCcw,
  Tags,
  Trash2,
  Trophy,
  Users,
} from "lucide-react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type KeyboardEvent as ReactKeyboardEvent,
  type ReactNode,
  type TouchEvent as ReactTouchEvent,
  type WheelEvent as ReactWheelEvent,
} from "react";
import { useGameDetail } from "@/lib/useGameDetail";
import { formatPublishDate } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import type { GameDetail } from "@/lib/api/domain";
import { cn, iconSrc } from "@/lib/utils";
import { GameCoverFlags } from "./GameCoverFlags";
import { ConfirmDialog } from "./ConfirmDialog";
import { FinishGameDialog } from "./FinishGameDialog";
import { GameDetailCatalogPicker } from "./GameDetailCatalogPicker";
import { GameDetailFinishHistoryRow } from "./GameDetailFinishHistoryRow";
import { GameDetailLaunchRows } from "./GameDetailLaunchRows";
import { GameDetailMetadataQuickRows } from "./GameDetailMetadataQuickRows";
import { GameDetailQuickStats } from "./GameDetailQuickStats";
import { GameDetailTaxonomyRow } from "./GameDetailTaxonomyRow";
import { GameEditorForm } from "./GameEditorForm";
import { HERO_IMAGE_FADE } from "./GameEditorControls";
import { GameInformationEditor } from "./GameInformationEditor";
import { GameDetailOwnershipDialog } from "./GameDetailOwnershipDialog";
import { ownershipHasInfo } from "./OwnershipInfoDialog";
import { gameDetailApi } from "@/lib/gameDetailApi";
import { api } from "@/lib/api/client";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogTitle,
  IconButton,
  InfoButton,
  Tooltip,
} from "./ui";

type GameDetailDialogProps = {
  gameId: string | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  mode: "view" | "edit" | "add";
  onCancelEdit: () => void;
  onSaved: (game: GameDetail) => void;
};

type DetailPage = 0 | 1;
type WheelGesture = "page-transition" | "content-scroll";

/**
 * Kich thuoc GOC cua the chi tiet. Bo cuc ben trong duoc thiet ke o dung kich
 * thuoc nay va KHONG doi theo cua so — cua so to nho chi lam thay doi he so
 * phong (`scale`) cua ca the.
 *
 * Lam vay vi bo cuc nay co rang buoc "moi tab khong duoc cuon": neu de bo cuc
 * tu co gian theo cua so thi cua so thap se bop cac khung den muc chi con
 * tieu de, hoac te hon la lam chung tran de len nhau.
 */
const CARD_WIDTH = 870;
const CARD_HEIGHT = 900;
/** Chua lai mot vien quanh the de khong dinh sat canh man hinh. */
const VIEWPORT_MARGIN = 0.94;
const MAX_SCALE = 1.6;
const MIN_SCALE = 0.4;

function useCardScale(active: boolean) {
  const [scale, setScale] = useState(1);

  useEffect(() => {
    const compute = () => {
      const fit = Math.min(
        (window.innerWidth * VIEWPORT_MARGIN) / CARD_WIDTH,
        (window.innerHeight * VIEWPORT_MARGIN) / CARD_HEIGHT,
        MAX_SCALE,
      );
      setScale(Math.max(MIN_SCALE, fit));
    };

    compute();
    window.addEventListener("resize", compute);
    /* Su kien `resize` khong phai luc nao cung den (cua so bi doi kich thuoc
       trong luc tab an, WebView2 doi khung...), nen bam thang vao kich thuoc
       tai lieu cho chac. Va tinh lai moi lan mo the. */
    const observer = new ResizeObserver(compute);
    observer.observe(document.documentElement);
    return () => {
      window.removeEventListener("resize", compute);
      observer.disconnect();
    };
  }, [active]);

  return scale;
}

function DetailPanel({
  title,
  children,
  extra,
  headerAction,
  listClassName,
  className,
}: {
  title: string;
  children: ReactNode;
  /** Khoi con dat SAU danh sach hang — vi du cum Average playtime / MetaScore. */
  extra?: ReactNode;
  headerAction?: ReactNode;
  /** Chinh rieng phan danh sach hang — vi du noi khoang cach dong ra. */
  listClassName?: string;
  className?: string;
}) {
  return (
    <section
      className={cn(
        /* `min-h-0` + cuon ben trong: trang KHONG cuon, nhung khung nao qua day
           thi tu cuon phan ruot cua no — du lieu khong bi cat mat. */
        "group/panel scrollbar-thin flex min-h-0 flex-col overflow-y-auto rounded-xl border border-border bg-surface-2/30 p-3 backdrop-blur-sm",
        className,
      )}
    >
      <div className="relative mb-2">
        <h3 className="text-xs font-black uppercase tracking-[0.14em] text-muted-fg">
          {title}
        </h3>
        {headerAction ? <div className="absolute right-0 top-1/2 -translate-y-1/2">{headerAction}</div> : null}
      </div>
      <dl
        className={cn(
          "grid grid-cols-[max-content_minmax(0,1fr)_auto] items-start gap-x-3 gap-y-3 text-sm",
          /* Co khoi con thi hang cuoi phai cach canh tren cua no mot khoang. */
          extra && "mb-3",
          listClassName,
        )}
      >
        {children}
      </dl>
      {extra}
    </section>
  );
}

/** Khung long trong mot khung — cum so den tu nguon ngoai (HLTB, Metacritic). */
function DetailSubPanel({ children }: { children: ReactNode }) {
  return (
    /* Chay HET be rong khung cha (huy dem `p-3` bang le am) va dinh xuong DAY
       khung cha (`mt-auto`), nen canh duoi cua no chinh la canh duoi khung cha.
       Chi giu vien TREN lam duong ngan, ba canh con lai da co vien khung cha. */
    <div className="-mx-3 -mb-3 mt-auto rounded-b-xl rounded-t-xl border-t border-border/70 bg-surface/40 px-3 py-2.5">
      <dl className="grid grid-cols-[max-content_minmax(0,1fr)_auto] items-start gap-x-3 gap-y-3 text-sm">
        {children}
      </dl>
    </div>
  );
}

function DetailRow({
  label,
  children,
  action,
  icon,
  onValueDoubleClick,
}: {
  label: string;
  children: ReactNode;
  action?: ReactNode;
  /** Icon dat TRUOC nhan — moi hang mot bieu tuong de quet mat cho nhanh. */
  icon?: ReactNode;
  onValueDoubleClick?: () => void;
}) {
  return (
    <>
      <dt className="flex items-center gap-1.5 font-semibold text-muted-fg">
        {icon ? <span className="grid shrink-0 place-items-center">{icon}</span> : null}
        {/* Dau hai cham dat o DAY, khong nhet vao chuoi dich — de ban dich chi
            chua chu, khong chua dau cau cua bo cuc. */}
        {label}:
      </dt>
      <dd
        className="min-w-0 break-words font-semibold text-fg"
        onDoubleClick={onValueDoubleClick}
      >
        {children}
      </dd>
      {/* Cot thu ba luon ton tai du khong co nut, khong thi luoi lech hang. */}
      <div className="-my-1 justify-self-end">{action}</div>
    </>
  );
}

/** Gia tri rong — moi hang deu hien du chua co du lieu, nen can mot cho dung chung. */
function EmptyValue() {
  return <span className="text-muted-fg">—</span>;
}

/**
 * Hang co gia tri dai — cat con HAI DONG kem dau "…", ro chuot vao moi hien nut
 * mui ten de mo rong; luc da mo thi nut thu gon hien san.
 */
function DetailRowClamped({
  label,
  icon,
  text,
  onValueDoubleClick,
}: {
  label: string;
  icon?: ReactNode;
  text: string;
  onValueDoubleClick?: () => void;
}) {
  const { t } = useI18n();
  const [expanded, setExpanded] = useState(false);
  const [overflowing, setOverflowing] = useState(false);
  const valueRef = useRef<HTMLSpanElement>(null);

  useEffect(() => {
    /* Chi do khi DANG CAT — luc mo rong thi scrollHeight = clientHeight, do
       tiep se ra "khong tran" va nut thu gon bien mat. */
    if (expanded) return;
    const element = valueRef.current;
    if (!element) return;
    const check = () => setOverflowing(element.scrollHeight > element.clientHeight + 1);
    check();
    const observer = new ResizeObserver(check);
    observer.observe(element);
    return () => observer.disconnect();
  }, [text, expanded]);

  return (
    <>
      <dt className="flex items-center gap-1.5 font-semibold text-muted-fg">
        {icon ? <span className="grid shrink-0 place-items-center">{icon}</span> : null}
        {label}:
      </dt>
      <dd className="relative min-w-0 font-semibold text-fg" onDoubleClick={onValueDoubleClick}>
        {/* Chua san le phai cho con mui ten dau o CUOI DONG, chu khong day chu
            chay xuong duoi no. */}
        <span ref={valueRef} className={cn("block break-words pr-5", !expanded && "line-clamp-2")}>
          {text}
        </span>
        {/* Nut TRAN, dat o cuoi dong cuoi cua phan chu — khong dung `IconButton`
            de khoi mang theo mat kinh / do noi 3D cua he nut. */}
        {overflowing ? (
          <button
            type="button"
            aria-label={t(expanded ? "action.collapse" : "action.expand")}
            title={t(expanded ? "action.collapse" : "action.expand")}
            onClick={() => setExpanded((current) => !current)}
            className={cn(
              "absolute bottom-0 right-0 grid h-4 w-4 place-items-center rounded text-muted-fg",
              "hover:text-fg focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-primary",
              "opacity-0 group-hover/panel:opacity-100 focus-visible:opacity-100",
              expanded && "opacity-100",
            )}
          >
            {expanded ? <ChevronUp size={12} /> : <ChevronDown size={12} />}
          </button>
        ) : null}
      </dd>
      <div className="-my-1 justify-self-end" />
    </>
  );
}

function TaxonomyValues({
  items,
}: {
  items: Array<{ id: number; name: string; iconData?: string | null }>;
}) {
  return (
    <span className="flex flex-wrap items-center gap-x-3 gap-y-1">
      {items.map((item) => (
        <span key={item.id} className="inline-flex items-center gap-1 font-semibold">
          {iconSrc(item.iconData) ? (
            <img src={iconSrc(item.iconData)} alt="" className="h-3.5 w-3.5 shrink-0 object-contain" />
          ) : null}
          {item.name}
        </span>
      ))}
    </span>
  );
}

function DetailSkeleton() {
  return (
    <div className="animate-pulse">
      <div className="h-64 bg-muted" />
      <div className="space-y-4 p-5">
        <div className="h-7 w-2/3 rounded bg-muted" />
        <div className="h-4 w-1/3 rounded bg-muted" />
        <div className="grid grid-cols-3 gap-3">
          {Array.from({ length: 6 }).map((_, index) => (
            <div key={index} className="h-16 rounded-lg bg-muted/70" />
          ))}
        </div>
      </div>
    </div>
  );
}

function GameHero({
  game,
  onInformationDoubleClick,
}: {
  game: GameDetail;
  onInformationDoubleClick?: () => void;
}) {
  return (
    /* Ti le 41:20 giong header WPF cu (cua so 820px x header 400px). The co
       kich thuoc goc co dinh nen ti le nay luon dung, khong can chan theo vh. */
    <div className="relative aspect-[41/20] w-full shrink-0 overflow-hidden bg-[var(--color-bg)]">
      {game.backdropPath || game.bannerPath ? (
        <img
          src={game.backdropPath ?? game.bannerPath ?? undefined}
          alt=""
          className="h-full w-full object-cover"
          style={HERO_IMAGE_FADE}
        />
      ) : (
        <div className="grid h-full place-items-center text-muted-fg"><Gamepad2 size={44} /></div>
      )}
      <div
        className="pointer-events-none absolute inset-0"
        style={{ background: "linear-gradient(to bottom, rgb(0 0 0 / 0.12) 0%, transparent 52%)" }}
      />
      <GameCoverFlags
        isDlc={game.isDlc}
        isUpcoming={game.isUpcoming}
        publishDate={game.publishDate}
        size="hero"
        fadeUpcomingBottom={game.isUpcoming}
      />
      <div className="absolute inset-x-0 bottom-0 z-20 pb-3 pl-5 pr-40 pt-20 sm:pl-6 sm:pr-44">
        <h2
          className="truncate text-2xl font-black text-white drop-shadow-lg sm:text-4xl"
          onDoubleClick={onInformationDoubleClick}
        >
          {game.name}
        </h2>
        {/* Theo template: duoi tieu de la nha PHAT HANH. */}
        {(game.publisher ?? game.developer)?.trim() ? (
          <p
            className="mt-1 truncate text-xs text-white/70 sm:text-sm"
            onDoubleClick={onInformationDoubleClick}
          >
            {game.publisher?.trim() || game.developer}
          </p>
        ) : null}
      </div>
    </div>
  );
}

export function GameDetailDialog({
  gameId,
  open,
  onOpenChange,
  mode,
  onCancelEdit,
  onSaved,
}: GameDetailDialogProps) {
  const { t } = useI18n();
  const detail = useGameDetail(gameId, open);
  const game = detail.data;
  /* Nut ⓘ chi co nghia voi kieu so huu dung chung tai khoan / ban tai ve. */
  const ownershipInfoAvailable = ownershipHasInfo(game?.ownership);
  const cardScale = useCardScale(open);
  /* Form sua tu dang ky mot chot chan: con thay doi chua luu thi no giu lai
     cu bam dong (nut X, Esc, bam ra ngoai) va tu hoi nguoi dung. */
  const closeGuardRef = useRef<(() => boolean) | null>(null);
  const registerCloseGuard = useCallback((guard: (() => boolean) | null) => {
    closeGuardRef.current = guard;
  }, []);
  const handleOpenChange = (next: boolean) => {
    if (!next && closeGuardRef.current?.()) return;
    onOpenChange(next);
  };

  const [activePage, setActivePage] = useState<DetailPage>(0);
  const [informationEditing, setInformationEditing] = useState(false);
  const [imageEditing, setImageEditing] = useState(false);
  const [informationEditHint, setInformationEditHint] = useState({ active: false, version: 0 });
  const [ownershipInfoOpen, setOwnershipInfoOpen] = useState(false);
  const [finishOpen, setFinishOpen] = useState(false);
  const [disableConfirmOpen, setDisableConfirmOpen] = useState(false);
  const overviewScrollerRef = useRef<HTMLElement>(null);
  const filesScrollerRef = useRef<HTMLElement>(null);
  const touchStartYRef = useRef<number | null>(null);
  const wheelGestureRef = useRef<WheelGesture | null>(null);
  const wheelGestureTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const informationEditHintTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const flashInformationEdit = useCallback(() => {
    if (informationEditHintTimerRef.current) clearTimeout(informationEditHintTimerRef.current);
    setInformationEditHint((current) => ({ active: true, version: current.version + 1 }));
    informationEditHintTimerRef.current = setTimeout(() => {
      setInformationEditHint((current) => ({ ...current, active: false }));
      informationEditHintTimerRef.current = null;
    }, 1_000);
  }, []);

  const externalLinks = useQuery({
    queryKey: ["game-external-links", gameId],
    queryFn: () => gameDetailApi.externalLinks(gameId!),
    enabled: open && mode === "view" && Boolean(gameId && game),
    staleTime: 5 * 60_000,
  });
  const capabilities = useQuery({
    queryKey: ["capabilities"],
    queryFn: () => api.capabilities(),
    enabled: open && mode === "view",
    staleTime: Infinity,
  });

  const revealFolder = useMutation({
    mutationFn: (path: string) => gameDetailApi.revealFolder(path),
    onError: (error) => toast.error(error instanceof Error ? error.message : String(error)),
  });

  const queryClient = useQueryClient();
  /* "Xoa" game o day la vo hieu hoa — backend chi co /disable, giong nut thung
     rac trong form sua. */
  const disableGame = useMutation({
    mutationFn: (id: string) => gameDetailApi.disable(id),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["games"] }),
        queryClient.invalidateQueries({ queryKey: ["play-queue"] }),
        queryClient.invalidateQueries({ queryKey: ["dashboard-stats"] }),
      ]);
      onOpenChange(false);
    },
    onError: (error) => toast.error(error instanceof Error ? error.message : String(error)),
  });

  useEffect(() => {
    if (!open) return;
    if (wheelGestureTimerRef.current) clearTimeout(wheelGestureTimerRef.current);
    wheelGestureTimerRef.current = null;
    wheelGestureRef.current = null;
    touchStartYRef.current = null;
    setActivePage(0);
    setInformationEditing(false);
    setImageEditing(false);
    if (informationEditHintTimerRef.current) clearTimeout(informationEditHintTimerRef.current);
    informationEditHintTimerRef.current = null;
    setInformationEditHint({ active: false, version: 0 });
    setOwnershipInfoOpen(false);
    setFinishOpen(false);
    overviewScrollerRef.current?.scrollTo({ top: 0 });
    filesScrollerRef.current?.scrollTo({ top: 0 });
  }, [gameId, open]);

  useEffect(() => {
    return () => {
      if (wheelGestureTimerRef.current) clearTimeout(wheelGestureTimerRef.current);
      if (informationEditHintTimerRef.current) clearTimeout(informationEditHintTimerRef.current);
    };
  }, []);

  const selectPage = (page: DetailPage) => {
    const activeElement = document.activeElement;
    const hiddenPage = page === 0 ? filesScrollerRef.current : overviewScrollerRef.current;
    const focusLeavesHiddenPage =
      activeElement instanceof HTMLElement &&
      hiddenPage?.contains(activeElement);

    setActivePage(page);
    if (page === 0) overviewScrollerRef.current?.scrollTo({ top: 0 });
    if (page === 1) filesScrollerRef.current?.scrollTo({ top: 0 });
    if (focusLeavesHiddenPage) {
      focusPageTab(page);
    }
  };

  const focusPageTab = (page: DetailPage) => {
    const tabId = page === 0 ? "detail-overview-tab" : "detail-files-tab";
    requestAnimationFrame(() => document.getElementById(tabId)?.focus());
  };

  const holdWheelGesture = (gesture?: WheelGesture) => {
    if (gesture) wheelGestureRef.current = gesture;
    if (wheelGestureTimerRef.current) clearTimeout(wheelGestureTimerRef.current);
    wheelGestureTimerRef.current = setTimeout(() => {
      wheelGestureRef.current = null;
      wheelGestureTimerRef.current = null;
    }, 180);
  };

  const handleWheel = (event: ReactWheelEvent<HTMLDivElement>) => {
    if (Math.abs(event.deltaY) <= Math.abs(event.deltaX) || Math.abs(event.deltaY) < 8) return;

    const currentGesture = wheelGestureRef.current;
    if (currentGesture) {
      holdWheelGesture();
      if (currentGesture === "page-transition") event.preventDefault();
      return;
    }

    const activeScroller = activePage === 0 ? overviewScrollerRef.current : filesScrollerRef.current;
    const canScrollUp = (activeScroller?.scrollTop ?? 0) > 1;
    const canScrollDown = activeScroller
      ? activeScroller.scrollTop + activeScroller.clientHeight < activeScroller.scrollHeight - 1
      : false;

    if ((event.deltaY < 0 && canScrollUp) || (event.deltaY > 0 && canScrollDown)) {
      holdWheelGesture("content-scroll");
      return;
    }

    const nextPage: DetailPage = event.deltaY > 0 ? 1 : 0;
    holdWheelGesture("page-transition");
    event.preventDefault();
    if (nextPage === activePage) return;

    selectPage(nextPage);
  };

  const handleKeyDown = (event: ReactKeyboardEvent<HTMLDivElement>) => {
    if (event.altKey || event.ctrlKey || event.metaKey) return;

    const target = event.target as HTMLElement;
    if (target.isContentEditable || ["INPUT", "SELECT", "TEXTAREA"].includes(target.tagName)) return;

    if (event.key === "ArrowDown" || event.key === "PageDown") {
      event.preventDefault();
      selectPage(1);
      focusPageTab(1);
    } else if (event.key === "ArrowUp" || event.key === "PageUp") {
      event.preventDefault();
      selectPage(0);
      focusPageTab(0);
    }
  };

  const handleTouchStart = (event: ReactTouchEvent<HTMLDivElement>) => {
    touchStartYRef.current = event.touches[0]?.clientY ?? null;
  };

  const handleTouchEnd = (event: ReactTouchEvent<HTMLDivElement>) => {
    const startY = touchStartYRef.current;
    touchStartYRef.current = null;
    if (startY === null) return;

    const distance = startY - (event.changedTouches[0]?.clientY ?? startY);
    if (Math.abs(distance) < 48) return;

    if (distance > 0 && activePage === 0) {
      const overview = overviewScrollerRef.current;
      const overviewAtEnd = overview
        ? overview.scrollTop + overview.clientHeight >= overview.scrollHeight - 1
        : true;
      if (overviewAtEnd) selectPage(1);
    } else if (
      distance < 0 &&
      activePage === 1 &&
      (filesScrollerRef.current?.scrollTop ?? 0) <= 1
    ) {
      selectPage(0);
    }
  };

  return (
    <>
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent
        className="gap-0 overflow-hidden p-0 [&>button]:bg-[color-mix(in_oklab,var(--tone-file)_36%,transparent)]"
        overlayClassName="bg-black/50 backdrop-blur-[1px]"
        onKeyDown={mode === "view" && game && !informationEditing ? handleKeyDown : undefined}
        /* Hop ngoai lay dung kich thuoc SAU KHI phong, de Radix van can giua
           duoc va bong/bo goc khong bi lech. */
        style={{
          width: CARD_WIDTH * cardScale,
          height: CARD_HEIGHT * cardScale,
          maxWidth: "none",
        }}
      >
        {/* Khung kich thuoc GOC: moi thu ben trong do bang px co dinh, ca the
            duoc phong bang mot phep `scale` duy nhat. */}
        <div
          className="origin-top-left"
          style={{ width: CARD_WIDTH, height: CARD_HEIGHT, transform: `scale(${cardScale})` }}
        >
        {mode === "add" ? (
          <div className="flex h-full flex-col overflow-hidden" style={{ backgroundColor: "var(--color-bg)" }}>
            <GameEditorForm
              mode="add"
              onCancel={() => onOpenChange(false)}
              onClose={() => onOpenChange(false)}
              registerCloseGuard={registerCloseGuard}
              onSaved={onSaved}
              onDisabled={() => onOpenChange(false)}
            />
          </div>
        ) : mode === "edit" && game ? (
          <div className="flex h-full flex-col overflow-hidden" style={{ backgroundColor: "var(--color-bg)" }}>
            {/* Hero nam TRONG form: tieu de va anh doi theo ban nhap dang go. */}
            <GameEditorForm
              game={game}
              mode="edit"
              onCancel={onCancelEdit}
              onClose={() => onOpenChange(false)}
              registerCloseGuard={registerCloseGuard}
              onSaved={onSaved}
              onDisabled={() => onOpenChange(false)}
            />
          </div>
        ) : informationEditing && game ? (
          <div className="flex h-full flex-col overflow-hidden" style={{ backgroundColor: "var(--color-bg)" }}>
            <GameInformationEditor
              key={game.id}
              game={game}
              onBack={() => setInformationEditing(false)}
              onClose={() => onOpenChange(false)}
              registerCloseGuard={registerCloseGuard}
              onSaved={(saved) => {
                setInformationEditing(false);
                onSaved(saved);
              }}
            />
          </div>
        ) : imageEditing && game ? (
          <div className="flex h-full flex-col overflow-hidden" style={{ backgroundColor: "var(--color-bg)" }}>
            <GameEditorForm
              game={game}
              mode="edit"
              scope="images"
              onCancel={() => setImageEditing(false)}
              onClose={() => onOpenChange(false)}
              registerCloseGuard={registerCloseGuard}
              onSaved={(saved) => {
                setImageEditing(false);
                onSaved(saved);
              }}
              onDisabled={() => onOpenChange(false)}
            />
          </div>
        ) : detail.isPending ? (
          <>
            <DialogTitle className="sr-only">{t("detail.loading")}</DialogTitle>
            <DialogDescription className="sr-only">{t("detail.loadingDesc")}</DialogDescription>
            <DetailSkeleton />
          </>
        ) : detail.isError ? (
          <div className="flex min-h-72 flex-col items-center justify-center gap-3 p-8 text-center">
            <DialogTitle>{t("detail.error")}</DialogTitle>
            <DialogDescription className="max-w-md">
              {detail.error instanceof Error ? detail.error.message : String(detail.error)}
            </DialogDescription>
            <IconButton
              onClick={() => void detail.refetch()}
              label={t("error.retry")}
              tone="edit"
              icon={<RotateCcw />}
            />
          </div>
        ) : game ? (
          <div
            className="flex h-full flex-col overflow-hidden overscroll-contain"
            style={{ backgroundColor: "var(--color-bg)" }}
          >
            <DialogTitle className="sr-only">{game.name}</DialogTitle>
            <DialogDescription className="sr-only">{t("detail.navigationHint")}</DialogDescription>

            <GameHero game={game} onInformationDoubleClick={flashInformationEdit} />

            <div
              className="relative min-h-0 flex-1 overflow-hidden"
              onWheel={handleWheel}
              onTouchStart={handleTouchStart}
              onTouchEnd={handleTouchEnd}
            >
            <div
              className="flex h-full flex-col transition-transform duration-500 ease-out motion-reduce:transition-none"
              style={{ transform: `translateY(-${activePage * 100}%)` }}
            >
              <section
                id="detail-overview-panel"
                ref={overviewScrollerRef}
                role="tabpanel"
                aria-labelledby="detail-overview-tab"
                aria-hidden={activePage !== 0}
                inert={activePage !== 0}
                className="h-full shrink-0 overflow-hidden"
                style={{ backgroundColor: "var(--color-bg)" }}
              >
                <div className="flex h-full flex-col gap-3 pb-5 pl-5 pr-7 pt-4 sm:pl-6 sm:pr-8">

                  {externalLinks.isError ? (
                    <div className="flex items-center gap-2 rounded-lg border border-danger/35 bg-danger/10 p-2.5 text-xs text-danger">
                      <AlertTriangle size={14} className="shrink-0" />
                      {t("detail.externalLinksError")}
                    </div>
                  ) : null}

                  {/*
                    Hai khung LUON ton tai (moi hang deu hien du chua co du lieu),
                    nen luoi hai cot la co dinh.

                    `auto-rows-[minmax(0,1fr)]` la thu CHONG CHONG KHUNG: mac dinh
                    hang cua luoi co gian theo NOI DUNG, khong quan tam hop cha
                    cao bao nhieu — cua so thap la hai khung tran ra khoi hop, con
                    khung Theme & Genre thi van xep ngay sau HOP (chu khong phai
                    sau phan tran) nen de len tren. Cho hang toi thieu 0 thi hang
                    co lai duoc, khung tu cuon ben trong, khong con tran.
                  */}
                  <div className="grid min-h-0 auto-rows-[minmax(0,1fr)] gap-3 md:grid-cols-[1.15fr_1fr]">
                    <DetailPanel
                      title={t("detail.gameInfo")}
                      headerAction={(
                        <button
                          key={informationEditHint.version}
                          type="button"
                          aria-label={t("action.edit")}
                          title={t("action.edit")}
                          onClick={() => setInformationEditing(true)}
                          className={cn(
                            "grid h-6 w-6 place-items-center rounded-md text-muted-fg opacity-0 transition",
                            "hover:bg-primary/12 hover:text-primary focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/45",
                            "group-hover/panel:opacity-100",
                            informationEditHint.active && [
                              "opacity-100 bg-success/20 text-success ring-1 ring-success/55",
                              "animate-pulse [animation-duration:450ms] [animation-iteration-count:2] motion-reduce:animate-none",
                            ],
                          )}
                        >
                          <Pencil size={13} />
                        </button>
                      )}
                      extra={(
                        <DetailSubPanel>
                          <GameDetailMetadataQuickRows
                            key={`${game.id}:${open ? "open" : "closed"}`}
                            game={game}
                            hltbUrl={externalLinks.data?.hltbUrl}
                            metacriticUrl={externalLinks.data?.metacriticUrl}
                            onSaved={onSaved}
                          />
                        </DetailSubPanel>
                      )}
                    >
                      {game.isDlc ? (
                        <DetailRow label={t("detail.baseGame")} icon={<Package size={13} className="text-field-base-game" />} onValueDoubleClick={flashInformationEdit}>
                          {game.baseGameName?.trim() || <EmptyValue />}
                        </DetailRow>
                      ) : (
                        <DetailRow label={t("sort.series")} icon={<Library size={13} className="text-field-series" />} onValueDoubleClick={flashInformationEdit}>
                          {game.series ? <TaxonomyValues items={[game.series]} /> : <EmptyValue />}
                        </DetailRow>
                      )}
                      <DetailRow label={t("sort.publish")} icon={<CalendarDays size={13} className="text-field-publish" />} onValueDoubleClick={flashInformationEdit}>
                        {game.publishDate ? (formatPublishDate(game.publishDate) ?? game.publishDate) : <EmptyValue />}
                      </DetailRow>
                      {game.developer?.trim() ? (
                        <DetailRowClamped
                          label={t("editor.developer")}
                          icon={<Users size={13} className="text-field-developer" />}
                          text={game.developer}
                          onValueDoubleClick={flashInformationEdit}
                        />
                      ) : (
                        <DetailRow label={t("editor.developer")} icon={<Users size={13} className="text-field-developer" />} onValueDoubleClick={flashInformationEdit}>
                          <EmptyValue />
                        </DetailRow>
                      )}
                    </DetailPanel>

                    {/*
                      Cot phai theo template: hai o thong ke (Played Time,
                      Rated) nam TREN CUNG, roi den khoi trang thai.
                    */}
                    {/* Cot phai keo cao bang cot trai: khoi trang thai an het
                        phan chieu cao con lai, nen hai ben luon bang nhau. */}
                    <div className="flex flex-col gap-3">
                      {/* Ba o nay LUON hien du co du lieu hay khong. */}
                      {/* Excited va Score hep hon deu 15% so voi chia ba; phan do don sang
                          Playtime — chuoi "12h 34m" la thu dai nhat trong ba o. */}
                      <GameDetailQuickStats
                        key={`${game.id}:${open ? "open" : "closed"}`}
                        game={game}
                        onSaved={onSaved}
                      />

                    <DetailPanel title={t("detail.ratingsStatus")} className="flex-1">
                      <DetailRow label={t("sort.progress")} icon={<ListChecks size={13} className="text-field-progress" />}>
                        <GameDetailCatalogPicker game={game} kind="progress" onSaved={onSaved} />
                      </DetailRow>
                      <DetailRow
                        label={t("sort.ownership")}
                        icon={<KeyRound size={13} className="text-field-ownership" />}
                        action={ownershipInfoAvailable ? (
                          <InfoButton
                            size="mini"
                            label={t(ownershipInfoOpen ? "detail.hideOwnershipInfo" : "detail.showOwnershipInfo")}
                            tone={ownershipInfoOpen ? "edit" : "neutral"}
                            aria-expanded={ownershipInfoOpen}
                            onClick={() => setOwnershipInfoOpen((current) => !current)}
                          />
                        ) : undefined}
                      >
                        <GameDetailCatalogPicker game={game} kind="ownership" onSaved={onSaved} />
                      </DetailRow>
                      <GameDetailFinishHistoryRow
                        key={`${game.id}:finishes:${open ? "open" : "closed"}`}
                        game={game}
                        label={t("detail.finishHistory")}
                        icon={<History size={13} className="text-field-finish-history" />}
                        onSaved={onSaved}
                        action={!game.isDlc ? (
                          <IconButton
                            size="mini"
                            label={t("action.finish")}
                            tone="finish"
                            icon={<Trophy />}
                            onClick={() => setFinishOpen(true)}
                          />
                        ) : undefined}
                      />
                    </DetailPanel>
                    </div>
                  </div>

                  {/* Theme va Genre: mot khoi rieng chay HET be rong, duoi hai cot.
                      LUON hien du hai hang — trong thi de gach ngang, de bo cuc
                      cua the khong nhay theo tung game. */}
                  <DetailPanel title={t("detail.taxonomy")} className="shrink-0">
                    <GameDetailTaxonomyRow
                      key={`${game.id}:genres:${open ? "open" : "closed"}`}
                      game={game}
                      kind="genres"
                      label={t("editor.genres")}
                      icon={<Tags size={13} className="text-field-genres" />}
                      onSaved={onSaved}
                    />
                    <GameDetailTaxonomyRow
                      key={`${game.id}:themes:${open ? "open" : "closed"}`}
                      game={game}
                      kind="themes"
                      label={t("editor.themes")}
                      icon={<Palette size={13} className="text-field-themes" />}
                      onSaved={onSaved}
                    />
                  </DetailPanel>

                </div>
              </section>

              <section
                id="detail-files-panel"
                ref={filesScrollerRef}
                role="tabpanel"
                aria-labelledby="detail-files-tab"
                aria-hidden={activePage !== 1}
                inert={activePage !== 1}
                className="h-full shrink-0 overflow-hidden"
                style={{ backgroundColor: "var(--color-bg)" }}
              >
                <div className="flex h-full flex-col gap-3 pb-5 pl-5 pr-7 pt-4 sm:pl-6 sm:pr-8">
                  {game.folderWarning && (
                    <div className="flex items-center gap-2 rounded-lg border border-warning/40 bg-warning/10 p-3 text-sm text-warning">
                      <AlertTriangle size={16} className="shrink-0" /> {game.folderWarning}
                    </div>
                  )}

                  <DetailPanel title={t("detail.files")} listClassName="gap-y-5">
                    <GameDetailLaunchRows
                      key={`${game.id}:launch:${open ? "open" : "closed"}`}
                      game={game}
                      canPickFile={capabilities.data?.features?.filePicker === true}
                      onSaved={onSaved}
                    />
                    {/* Backup dang tam tat o backend (capabilities.features.backup). */}
                    {capabilities.data?.features?.backup === true && (
                      <DetailRow
                        label={t("editor.backupFolder")}
                        icon={<Archive size={13} className="text-field-backup" />}
                        action={game.backupFolderPath?.trim() && capabilities.data?.features?.revealFolder ? (
                          <IconButton
                            size="mini"
                            label={t("detail.openFolder")}
                            tone="open"
                            icon={revealFolder.isPending && revealFolder.variables === game.backupFolderPath
                              ? <Loader2 className="animate-spin" />
                              : <FolderSymlink />}
                            disabled={revealFolder.isPending}
                            onClick={() => revealFolder.mutate(game.backupFolderPath!)}
                          />
                        ) : undefined}
                      >
                        {game.backupFolderPath?.trim() ? (
                          <Tooltip label={game.backupFolderPath} className="max-w-80 break-all font-mono font-medium">
                            <span className="break-all font-mono">{game.backupFolderPath}</span>
                          </Tooltip>
                        ) : <EmptyValue />}
                      </DetailRow>
                    )}
                  </DetailPanel>

                  <button
                    type="button"
                    aria-label={`${t("action.edit")} ${t("editor.images")}`}
                    onClick={() => setImageEditing(true)}
                    className={cn(
                      "group flex min-h-20 w-full items-center justify-between rounded-xl border border-border bg-surface-2/30 p-4 text-left backdrop-blur-sm",
                      "transition hover:border-primary/45 hover:bg-primary/8 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/45",
                    )}
                  >
                    <span className="flex items-center gap-3">
                      <span className="grid h-9 w-9 place-items-center rounded-lg border border-border/70 bg-surface/55 text-primary transition group-hover:border-primary/35 group-hover:bg-primary/10">
                        <ImageIcon size={17} aria-hidden />
                      </span>
                      <span className="text-sm font-black text-fg">{t("editor.images")}</span>
                    </span>
                    <span className="inline-flex items-center gap-1 text-xs font-black uppercase tracking-[0.12em] text-muted-fg transition group-hover:text-primary">
                      {t("action.edit")}
                      <ChevronRight size={15} aria-hidden className="transition-transform group-hover:translate-x-0.5" />
                    </span>
                  </button>

                  {/* `mt-auto` day nut xuong goc duoi phai cua trang. */}
                  <div className="mt-auto flex justify-end">
                    <IconButton
                      tone="danger"
                      label={t("editor.disableGame")}
                      icon={disableGame.isPending ? <Loader2 className="animate-spin" /> : <Trash2 />}
                      onClick={() => setDisableConfirmOpen(true)}
                      disabled={disableGame.isPending}
                    />
                  </div>
                </div>
              </section>
            </div>

            <div
              role="tablist"
              aria-label={t("detail.sections")}
              aria-orientation="vertical"
              className="absolute inset-y-4 right-0 z-20 flex w-4 flex-col"
            >
              <span
                aria-hidden
                className="pointer-events-none absolute inset-y-0 left-1/2 w-[11px] -translate-x-1/2 rounded-full border border-border/52 bg-surface/44 shadow-[0_8px_24px_-12px_color-mix(in_oklab,black_65%,transparent)] backdrop-blur-md"
              />
              <span
                aria-hidden
                className={cn(
                  "pointer-events-none absolute left-1/2 w-[7px] -translate-x-1/2 rounded-full bg-primary/48",
                  "shadow-[inset_0_1px_0_color-mix(in_oklab,white_28%,transparent),0_3px_12px_-4px_color-mix(in_oklab,var(--color-primary)_80%,transparent)]",
                  "transition-[top] duration-500 ease-out motion-reduce:transition-none",
                )}
                /* Ray cao bang ca vung noi dung nen con truot chiem dung mot
                   nua — nhin ra ngay dang o trang nao trong hai trang. */
                style={{ top: activePage === 0 ? 3 : "calc(50% + 1px)", height: "calc(50% - 4px)" }}
              />
              {([
                [0, "detail-overview-tab", "detail-overview-panel", t("detail.overview")],
                [1, "detail-files-tab", "detail-files-panel", t("detail.files")],
              ] as const).map(([page, id, panelId, label]) => (
                <Tooltip key={id} label={label} side="left">
                  <button
                    id={id}
                    type="button"
                    role="tab"
                    aria-label={label}
                    aria-selected={activePage === page}
                    aria-controls={panelId}
                    tabIndex={activePage === page ? 0 : -1}
                    onClick={() => selectPage(page)}
                    className={cn(
                      "group/tab relative z-20 h-full flex-1 w-full rounded-full",
                      "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary",
                    )}
                  >
                    <span
                      aria-hidden
                      className={cn(
                        "absolute inset-y-1 left-1/2 w-[11px] -translate-x-1/2 rounded-full transition-colors",
                        activePage !== page && "group-hover/tab:bg-primary/10",
                      )}
                    />
                  </button>
                </Tooltip>
              ))}
            </div>
            </div>
          </div>
        ) : null}
        </div>
      </DialogContent>
    </Dialog>
    <GameDetailOwnershipDialog
      game={game}
      open={ownershipInfoOpen && ownershipInfoAvailable}
      onOpenChange={setOwnershipInfoOpen}
      onSaved={onSaved}
    />
    {/* Nut Trophy o day va dau `+` trong hang Finish history ghi CUNG mot thu,
        nen dung chung mot hop thoai — hai ban sao se tach doi hanh vi. */}
    <FinishGameDialog
      gameId={game?.id ?? gameId ?? ""}
      open={finishOpen}
      onOpenChange={setFinishOpen}
      defaultScore={game?.score ?? null}
      firstFinish={(game?.finishes?.length ?? 0) === 0}
      onSaved={onSaved}
    />
    <ConfirmDialog
      open={disableConfirmOpen}
      onOpenChange={setDisableConfirmOpen}
      title={t("editor.disableGame")}
      description={t("editor.disableConfirm")}
      confirmLabel={t("editor.disableGame")}
      onConfirm={() => {
        if (game) disableGame.mutate(game.id);
      }}
    />
    </>
  );
}
