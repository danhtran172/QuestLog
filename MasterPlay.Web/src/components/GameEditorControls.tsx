/**
 * Cac manh dung chung cua THE SUA GAME.
 *
 * Khung (`EditorPanel`), khung long (`EditorSubPanel`), hang co nhan
 * (`EditorRow`), o thong ke (`EditorStat`) va hero — dung mot bo voi trang xem
 * o `GameDetailDialog` de hai che do nhin nhu cung mot the.
 */
import { ChevronRight, Gamepad2, ImageOff } from "lucide-react";
import {
  useEffect,
  useRef,
  useState,
  type PointerEvent as ReactPointerEvent,
  type ReactNode,
  type RefObject,
} from "react";
import type { CatalogItem } from "@/lib/api/domain";
import { cn, iconSrc } from "@/lib/utils";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "./ui";
import { GameCoverFlags } from "./GameCoverFlags";

/** Anh hero mo dan vao nen o phia duoi — dung chung voi hero trang xem. */
export const HERO_IMAGE_FADE = {
  maskImage: "linear-gradient(to bottom, black 0%, black 52%, rgb(0 0 0 / 0.86) 62%, transparent 100%)",
  WebkitMaskImage: "linear-gradient(to bottom, black 0%, black 52%, rgb(0 0 0 / 0.86) 62%, transparent 100%)",
} as const;

export const editorInputClass =
  "h-9 w-full rounded-xl border border-border/80 bg-surface/55 px-3 text-sm font-medium text-fg outline-none backdrop-blur-md transition focus:border-primary/80 focus:ring-2 focus:ring-primary/20 disabled:opacity-45";

export function EditorField({
  label,
  children,
  wide = false,
  row = false,
}: {
  label: string;
  children: ReactNode;
  wide?: boolean;
  /** Nhan nam BEN TRAI o nhap thay vi phia tren — dung trong cac khung cua the. */
  row?: boolean;
}) {
  if (row) {
    /*
     * CO Y KHONG dung `<label>` o day.
     *
     * Bam vao bat ky dau trong mot `<label>` deu duoc chuyen sang phan tu
     * labelable DAU TIEN ben trong, ma `<button>` cung la labelable. Hang nay
     * chua chip co nut × nen bam vao nhan hoac vao khoang trong se gõ mat chip
     * dau tien. Nhan o day chi la chu; dieu khien tu dat `aria-label`.
     */
    return (
      <div className="grid grid-cols-[minmax(64px,0.32fr)_minmax(0,1fr)] items-center gap-2 text-xs font-bold text-muted-fg">
        <span className="truncate">{label}</span>
        <div className="grid min-w-0 gap-1.5">{children}</div>
      </div>
    );
  }

  return (
    <label className={cn("grid gap-1.5 text-xs font-bold text-muted-fg", wide && "sm:col-span-2")}>
      {label}
      {children}
    </label>
  );
}

/** Mot khung cua the — tuong duong `DetailPanel` ben trang xem. */
export function EditorPanel({
  children,
  className,
  guide,
}: {
  children: ReactNode;
  className?: string;
  /** Moc cho GuideTour (`data-guide`). */
  guide?: string;
}) {
  return (
    <section
      data-guide={guide}
      className={cn(
        "rounded-xl border border-border bg-surface-2/30 p-3 backdrop-blur-sm",
        className,
      )}
    >
      {children}
    </section>
  );
}

/**
 * Thanh truot VE TAY, noi DE LEN noi dung.
 *
 * Thanh truot native luon chiem mot dai o mep phai, an mat mep anh hero. O day
 * thanh native bi an (`scrollbar-none`) va thay bang mot con truot tuyet doi
 * nam tren cung, nen anh chay het be rong the.
 *
 * Van keo duoc: pointer capture tren con truot, doi quang duong chuot thanh
 * quang duong cuon theo dung ty le noi dung / khung nhin.
 */
export function OverlayScrollbar({
  targetRef,
  className,
}: {
  targetRef: RefObject<HTMLElement | null>;
  className?: string;
}) {
  const [thumb, setThumb] = useState({ top: 0, height: 0, visible: false });
  const dragRef = useRef<{ startY: number; startScroll: number } | null>(null);

  useEffect(() => {
    const element = targetRef.current;
    if (!element) return;

    const update = () => {
      const { scrollTop, scrollHeight, clientHeight } = element;
      const scrollable = scrollHeight - clientHeight;
      if (scrollable <= 1) {
        setThumb((current) => (current.visible ? { ...current, visible: false } : current));
        return;
      }
      const height = Math.max(28, (clientHeight / scrollHeight) * clientHeight);
      const top = (scrollTop / scrollable) * (clientHeight - height);
      setThumb({ top, height, visible: true });
    };

    update();
    element.addEventListener("scroll", update, { passive: true });
    /* Noi dung co the cao len khi mo khoi thong tin so huu, nen phai theo doi
       ca khung lan phan tu con. */
    const observer = new ResizeObserver(update);
    observer.observe(element);
    if (element.firstElementChild) observer.observe(element.firstElementChild);
    return () => {
      element.removeEventListener("scroll", update);
      observer.disconnect();
    };
  }, [targetRef]);

  const onPointerDown = (event: ReactPointerEvent<HTMLDivElement>) => {
    const element = targetRef.current;
    if (!element) return;
    dragRef.current = { startY: event.clientY, startScroll: element.scrollTop };
    event.currentTarget.setPointerCapture(event.pointerId);
    event.preventDefault();
  };

  const onPointerMove = (event: ReactPointerEvent<HTMLDivElement>) => {
    const drag = dragRef.current;
    const element = targetRef.current;
    if (!drag || !element) return;
    const travel = element.clientHeight - thumb.height;
    if (travel <= 0) return;
    const ratio = (element.scrollHeight - element.clientHeight) / travel;
    element.scrollTop = drag.startScroll + (event.clientY - drag.startY) * ratio;
  };

  const endDrag = (event: ReactPointerEvent<HTMLDivElement>) => {
    dragRef.current = null;
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }
  };

  if (!thumb.visible) return null;

  return (
    <div
      className={cn("pointer-events-none absolute inset-y-2 right-1.5 z-20 w-2.5", className)}
      aria-hidden
    >
      <div
        className="pointer-events-auto absolute right-0 w-full cursor-grab rounded-full bg-fg/25 shadow-[0_1px_4px_-1px_rgb(0_0_0/0.5)] backdrop-blur-md transition-colors hover:bg-fg/40 active:cursor-grabbing active:bg-fg/50"
        style={{ height: thumb.height, transform: `translateY(${thumb.top}px)` }}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={endDrag}
        onPointerCancel={endDrag}
      />
    </div>
  );
}

/**
 * Mot MANG cua the sua: tieu de nho + gach ngang keo het be rong, roi den cac
 * khung ben trong. The sua chi con MOT trang cuon, nen ranh gioi giua cac mang
 * phai nhin ra ngay.
 */
export function EditorSection({
  title,
  children,
  className,
  actions,
  collapsible = false,
  defaultOpen = true,
  guide,
}: {
  title: string;
  children: ReactNode;
  className?: string;
  /** Dieu khien phu dat o cuoi hang tieu de — vi du nut khoa/mo metadata. */
  actions?: ReactNode;
  /** Cho phep dong/mo ca mang bang cach bam vao tieu de. */
  collapsible?: boolean;
  defaultOpen?: boolean;
  /** Moc cho GuideTour (`data-guide`). */
  guide?: string;
}) {
  const [open, setOpen] = useState(defaultOpen);
  const showContent = !collapsible || open;
  return (
    <section data-guide={guide} className={cn("space-y-3", className)}>
      <div className="flex items-center gap-3">
        {collapsible ? (
          <button
            type="button"
            onClick={() => setOpen((current) => !current)}
            aria-expanded={open}
            className="flex shrink-0 items-center gap-1 text-fs-11 font-black uppercase tracking-[0.14em] text-muted-fg transition-colors hover:text-fg"
          >
            <ChevronRight size={13} className={cn("transition-transform", open && "rotate-90")} aria-hidden />
            {title}
          </button>
        ) : (
          <h3 className="shrink-0 text-fs-11 font-black uppercase tracking-[0.14em] text-muted-fg">
            {title}
          </h3>
        )}
        <span aria-hidden className="h-px flex-1 bg-border/70" />
        {actions}
      </div>
      {showContent ? children : null}
    </section>
  );
}

/** Khung long ben trong mot khung — theo template, vi du cum Avg/MetaScore. */
export function EditorSubPanel({
  children,
  className,
}: {
  children: ReactNode;
  className?: string;
}) {
  return (
    <div className={cn("rounded-lg border border-border/70 bg-surface/40 p-2.5", className)}>
      {children}
    </div>
  );
}

/** Mot hang `nhan: dieu khien` ben trong khung. */
export function EditorRow({
  label,
  children,
  className,
}: {
  label: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <label
      className={cn(
        "grid grid-cols-[minmax(64px,0.32fr)_minmax(0,1fr)] items-center gap-2 text-xs font-bold text-muted-fg",
        className,
      )}
    >
      <span className="truncate">{label}</span>
      <span className="flex min-w-0 items-center gap-1.5">{children}</span>
    </label>
  );
}

/** O thong ke noi bat — o `Rated` ben phai hang dau cua trang tong quan. */
export function EditorStat({
  label,
  icon,
  children,
  className,
}: {
  label: string;
  icon?: ReactNode;
  children: ReactNode;
  className?: string;
}) {
  return (
    <label
      className={cn(
        "flex flex-col gap-1 rounded-xl border border-border bg-surface-2/30 p-2.5 backdrop-blur-sm",
        className,
      )}
    >
      <span className="flex items-center gap-1 text-fs-11 font-semibold text-muted-fg">
        {icon}
        {label}
      </span>
      {/* O nhap nam giua phan con lai theo chieu doc. */}
      <div className="grid flex-1 place-items-center">{children}</div>
    </label>
  );
}

/**
 * Select cho Progress / Ownership — nut duoc nhuom theo `colorCode` cua muc
 * dang chon (mau den tu backend, xem DOMAIN_RULES §14).
 */
export function CatalogSelectControl({
  value,
  items,
  noneLabel,
  ariaLabel,
  onChange,
  className,
}: {
  value: string;
  items: CatalogItem[];
  noneLabel: string;
  /** `<label>` khong dat ten cho `<button>` cua Radix nen phai truyen tay. */
  ariaLabel: string;
  onChange: (value: string) => void;
  className?: string;
}) {
  const selected = items.find((item) => String(item.id) === value);
  return (
    <Select value={value} onValueChange={onChange}>
      <SelectTrigger
        aria-label={ariaLabel}
        className={cn(editorInputClass, "h-8", className)}
        style={selected ? {
          borderColor: `color-mix(in srgb, ${selected.colorCode} 52%, transparent)`,
          background: `color-mix(in srgb, ${selected.colorCode} 13%, var(--color-surface))`,
        } : undefined}
      >
        <SelectValue />
      </SelectTrigger>
      <SelectContent className="max-h-72 min-w-60">
        <SelectItem value="__none__">{noneLabel}</SelectItem>
        {items.map((item) => (
          <SelectItem key={item.id} value={String(item.id)}>
            <span className="flex items-center gap-2" style={{ color: item.colorCode }}>
              {iconSrc(item.iconData) ? <img src={iconSrc(item.iconData)} alt="" className="h-4 w-4 object-contain" /> : null}
              <span>{item.displayName}</span>
            </span>
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

export function ImagePreview({
  src,
  alt,
  portrait = false,
  className,
}: {
  src: string;
  alt: string;
  portrait?: boolean;
  className?: string;
}) {
  const [failed, setFailed] = useState(false);
  useEffect(() => setFailed(false), [src]);
  return (
    <div
      className={cn(
        "grid overflow-hidden rounded-xl border border-border/70 bg-black/15",
        portrait ? "aspect-[2/3]" : "aspect-[16/7]",
        className,
      )}
    >
      {src && !failed ? (
        <img src={src} alt={alt} className="h-full w-full object-cover" onError={() => setFailed(true)} />
      ) : (
        <span className="grid place-items-center text-muted-fg"><ImageOff size={22} aria-hidden /></span>
      )}
    </div>
  );
}

/**
 * Hero cua the sua — cung bo cuc voi hero trang xem, nhung an theo BAN NHAP:
 * go ten den dau, tieu de doi den do.
 */
export function EditorHero({
  name,
  publisher,
  imageSrc,
  fallbackName,
  isDlc = false,
  isUpcoming = false,
  publishDate = null,
}: {
  name: string;
  publisher: string;
  imageSrc: string;
  fallbackName: string;
  isDlc?: boolean;
  isUpcoming?: boolean;
  publishDate?: string | null;
}) {
  const [failed, setFailed] = useState(false);
  useEffect(() => setFailed(false), [imageSrc]);

  /* Cung ti le 41:20, cung lop mo va co DLC/sap ra voi hero trang xem
     (`GameHero` trong GameDetailDialog) — them/sua xong nhin y nhu luc xem. */
  return (
    <div className="relative aspect-[41/20] w-full shrink-0 overflow-hidden bg-[var(--color-bg)]">
      {imageSrc && !failed ? (
        <img
          src={imageSrc}
          alt=""
          className="h-full w-full object-cover"
          style={HERO_IMAGE_FADE}
          onError={() => setFailed(true)}
        />
      ) : (
        <div className="grid h-full place-items-center text-muted-fg"><Gamepad2 size={44} /></div>
      )}
      <div
        className="pointer-events-none absolute inset-0"
        style={{ background: "linear-gradient(to bottom, rgb(0 0 0 / 0.12) 0%, transparent 52%)" }}
      />
      <GameCoverFlags
        isDlc={isDlc}
        isUpcoming={isUpcoming}
        publishDate={publishDate}
        size="hero"
        fadeUpcomingBottom={isUpcoming}
      />
      <div className="absolute inset-x-0 bottom-0 z-20 px-5 pb-3 pt-20 sm:px-6">
        <h2 className="truncate text-2xl font-black text-white drop-shadow-lg sm:text-4xl">
          {name.trim() || fallbackName}
        </h2>
        {publisher.trim() ? (
          <p className="mt-1 truncate text-xs text-white/70 sm:text-sm">{publisher}</p>
        ) : null}
      </div>
    </div>
  );
}
