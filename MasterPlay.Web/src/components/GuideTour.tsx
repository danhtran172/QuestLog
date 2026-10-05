import {
  useCallback,
  useEffect,
  useId,
  useLayoutEffect,
  useRef,
  useState,
  type CSSProperties,
  type ReactNode,
  type RefObject,
} from "react";
import { createPortal } from "react-dom";
import { ArrowLeft, ArrowRight, Check } from "lucide-react";
import { cn } from "@/lib/utils";
import { useI18n } from "@/lib/i18n";
import { IconButton } from "./ui";

export type GuideStep = {
  /** Gia tri `data-guide` cua phan tu duoc chieu sang. null = the nam giua, khong chieu gi. */
  target: string | null;
  title: string;
  body: ReactNode;
};

export type GuideCloseReason = "done" | "skip";

type Box = { top: number; left: number; width: number; height: number };

/* Le quanh vung chieu, khoang cach the <-> vung chieu, le voi mep khung. */
const SPOT_PAD = 6;
const CARD_GAP = 12;
const EDGE = 12;
const CARD_MAX_WIDTH = 360;
/* Duoi muc nay ca tren lan duoi deu chat: the de len mep duoi, chap nhan che mot phan. */
const MIN_CARD_SPACE = 170;
const DIM = "rgb(5 7 10 / 0.62)";

function guideElement(root: ParentNode, target: string | null) {
  return target ? root.querySelector<HTMLElement>(`[data-guide="${target}"]`) : null;
}

/** Vung cuon gan nhat ben trong container — cuon tay thay vi scrollIntoView, vi
 *  scrollIntoView con cuon ca cac khung overflow-hidden (DialogContent) lam lech the. */
function scrollParent(element: HTMLElement, stopAt: HTMLElement) {
  for (let node = element.parentElement; node && node !== stopAt; node = node.parentElement) {
    const overflowY = getComputedStyle(node).overflowY;
    if ((overflowY === "auto" || overflowY === "scroll") && node.scrollHeight > node.clientHeight) return node;
  }
  return null;
}

/**
 * Tour huong dan: lam toi man hinh, khoet sang phan tu dang noi toi va dat the
 * giai thich ben canh. Chi doc — lop phu chan moi thao tac ben duoi cho toi khi
 * xong/bo qua.
 *
 * - Co `containerRef`: ve TRONG container (absolute). Container co the nam duoi
 *   mot `transform: scale` (the game) — toa do duoc quy doi ve khung goc.
 * - Khong co: ve tren ca cua so (fixed, portal vao body); Esc = bo qua.
 */
export function GuideTour({
  open,
  steps,
  eyebrow,
  containerRef,
  finishAction,
  onClose,
}: {
  open: boolean;
  steps: GuideStep[];
  /** Dong chu nho phia tren tieu de, vd "Huong dan nhanh". */
  eyebrow: string;
  containerRef?: RefObject<HTMLElement | null>;
  /** Nut phu o buoc cuoi, vd "Thu ngay". Bam vao cung tinh la xong tour. */
  finishAction?: { label: string; icon: ReactNode; onClick: () => void };
  onClose: (reason: GuideCloseReason) => void;
}) {
  const { t } = useI18n();
  const titleId = useId();
  const bodyId = useId();
  const primaryRef = useRef<HTMLButtonElement>(null);
  const [index, setIndex] = useState(0);
  const [box, setBox] = useState<Box | null>(null);
  const [bounds, setBounds] = useState({ width: 0, height: 0 });
  const contained = Boolean(containerRef);
  const step = steps[Math.min(index, steps.length - 1)];
  const last = index >= steps.length - 1;

  useEffect(() => {
    if (open) setIndex(0);
  }, [open]);

  const measure = useCallback(() => {
    const container = containerRef?.current ?? null;
    let originX = 0;
    let originY = 0;
    let scale = 1;
    let width = window.innerWidth;
    let height = window.innerHeight;
    if (container) {
      const rect = container.getBoundingClientRect();
      scale = container.offsetWidth ? rect.width / container.offsetWidth : 1;
      originX = rect.left;
      originY = rect.top;
      width = container.offsetWidth;
      height = container.offsetHeight;
    }
    setBounds((current) => (current.width === width && current.height === height ? current : { width, height }));

    const element = guideElement(container ?? document, step?.target ?? null);
    if (!element) {
      setBox(null);
      return;
    }
    const rect = element.getBoundingClientRect();
    const top = Math.max(EDGE / 2, (rect.top - originY) / scale - SPOT_PAD);
    const left = Math.max(EDGE / 2, (rect.left - originX) / scale - SPOT_PAD);
    const bottom = Math.min(height - EDGE / 2, (rect.bottom - originY) / scale + SPOT_PAD);
    const right = Math.min(width - EDGE / 2, (rect.right - originX) / scale + SPOT_PAD);
    const next = bottom > top && right > left ? { top, left, width: right - left, height: bottom - top } : null;
    setBox((current) =>
      current && next &&
      current.top === next.top && current.left === next.left &&
      current.width === next.width && current.height === next.height
        ? current
        : next,
    );
  }, [containerRef, step?.target]);

  useLayoutEffect(() => {
    if (!open) return;
    const container = containerRef?.current ?? null;
    const element = guideElement(container ?? document, step?.target ?? null);

    /* Dua phan tu vao giua vung cuon cua the (chi khi ve trong container: thanh tab
       o man chinh la sticky, cuon trang chi lam nhay luoi game). */
    if (container && element) {
      const scroller = scrollParent(element, container);
      if (scroller) {
        const scrollerRect = scroller.getBoundingClientRect();
        const elementRect = element.getBoundingClientRect();
        const scale = scroller.offsetHeight ? scrollerRect.height / scroller.offsetHeight : 1;
        const offset = elementRect.top - scrollerRect.top - Math.max(0, (scrollerRect.height - elementRect.height) / 2);
        scroller.scrollBy({ top: offset / scale, behavior: "smooth" });
      }
    }

    measure();
    /* Cuon muot va animation mo the khong ban su kien resize — do lai vai nhip. */
    const timers = [120, 320, 650].map((delay) => window.setTimeout(measure, delay));
    const observer = new ResizeObserver(measure);
    if (container) observer.observe(container);
    if (element) observer.observe(element);
    window.addEventListener("resize", measure);
    document.addEventListener("scroll", measure, true);
    /* setTimeout thay vi requestAnimationFrame: rAF khong chay khi cua so bi an. */
    const focusTimer = window.setTimeout(() => primaryRef.current?.focus({ preventScroll: true }), 0);
    return () => {
      timers.forEach((timer) => window.clearTimeout(timer));
      window.clearTimeout(focusTimer);
      observer.disconnect();
      window.removeEventListener("resize", measure);
      document.removeEventListener("scroll", measure, true);
    };
  }, [containerRef, index, measure, open, step?.target]);

  /* Ve tren ca cua so thi tu xu ly Esc. Trong Dialog, Radix giu Esc — noi goi chan
     no bang close guard. */
  useEffect(() => {
    if (!open || contained) return;
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== "Escape") return;
      event.preventDefault();
      event.stopPropagation();
      onClose("skip");
    };
    window.addEventListener("keydown", onKey, true);
    return () => window.removeEventListener("keydown", onKey, true);
  }, [contained, onClose, open]);

  if (!open || !step) return null;

  const next = () => (last ? onClose("done") : setIndex((current) => current + 1));
  const back = () => setIndex((current) => Math.max(0, current - 1));

  const cardWidth = Math.max(0, Math.min(CARD_MAX_WIDTH, bounds.width - EDGE * 2));
  let placement: CSSProperties;
  if (!box) {
    placement = { left: (bounds.width - cardWidth) / 2, top: "50%", transform: "translateY(-50%)" };
  } else {
    const below = bounds.height - (box.top + box.height) - CARD_GAP - EDGE;
    const above = box.top - CARD_GAP - EDGE;
    const left = Math.min(
      Math.max(EDGE, box.left + box.width / 2 - cardWidth / 2),
      bounds.width - cardWidth - EDGE,
    );
    placement = Math.max(below, above) < MIN_CARD_SPACE
      ? { left, bottom: EDGE }
      : below >= above
        ? { left, top: box.top + box.height + CARD_GAP, maxHeight: below }
        : { left, bottom: bounds.height - box.top + CARD_GAP, maxHeight: above };
  }

  const layer = (
    <div
      className={cn(contained ? "absolute inset-0 z-40" : "fixed inset-0 z-[70]")}
      /* Lop chan: tour chi de doc, khong cho bam xuyen xuong ben duoi. */
      onPointerDown={(event) => event.stopPropagation()}
      onContextMenu={(event) => event.preventDefault()}
    >
      {box ? (
        <div
          aria-hidden
          className="pointer-events-none absolute rounded-xl ring-2 ring-primary/85 transition-[top,left,width,height] duration-300 ease-out motion-reduce:transition-none"
          style={{ ...box, boxShadow: `0 0 0 9999px ${DIM}, 0 0 24px 4px color-mix(in oklab, var(--color-primary) 35%, transparent)` }}
        />
      ) : (
        <div aria-hidden className="absolute inset-0" style={{ backgroundColor: DIM }} />
      )}

      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={bodyId}
        className="absolute flex flex-col overflow-hidden rounded-2xl border border-border bg-surface/95 text-fg shadow-2xl backdrop-blur-xl"
        style={{ width: cardWidth, ...placement }}
        onKeyDown={(event) => {
          if (event.key === "ArrowRight") {
            event.preventDefault();
            next();
          } else if (event.key === "ArrowLeft") {
            event.preventDefault();
            back();
          }
        }}
      >
        <div className="scrollbar-thin min-h-0 overflow-y-auto px-4 pb-3 pt-3.5">
          <div className="flex items-center gap-2 text-fs-11 font-black uppercase tracking-[0.14em]">
            <span className="text-primary">{eyebrow}</span>
            <span className="ml-auto tabular-nums text-muted-fg" aria-live="polite">
              {index + 1}/{steps.length}
            </span>
          </div>
          <h2 id={titleId} className="mt-1.5 text-base font-bold leading-snug">{step.title}</h2>
          <div id={bodyId} className="mt-1.5 space-y-1.5 text-sm leading-relaxed text-muted-fg">
            {step.body}
          </div>
        </div>

        <div className="flex shrink-0 items-center gap-2 border-t border-border/60 px-4 py-2.5">
          <div aria-hidden className="flex items-center gap-1">
            {steps.map((item, dot) => (
              <span
                key={`${item.target}-${dot}`}
                className={cn(
                  "h-1.5 rounded-full transition-all duration-200",
                  dot === index ? "w-4 bg-primary" : "w-1.5 bg-border",
                )}
              />
            ))}
          </div>
          {!last ? (
            <button
              type="button"
              onClick={() => onClose("skip")}
              className="ml-auto rounded-md px-2 py-1 text-xs font-semibold text-muted-fg transition-colors hover:text-fg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
            >
              {t("guide.skip")}
            </button>
          ) : (
            <span className="ml-auto" />
          )}
          {index > 0 ? (
            <IconButton size="compact" label={t("guide.back")} icon={<ArrowLeft />} onClick={back} />
          ) : null}
          {last && finishAction ? (
            <IconButton
              wide
              size="compact"
              tone="edit"
              label={finishAction.label}
              icon={finishAction.icon}
              onClick={() => {
                onClose("done");
                finishAction.onClick();
              }}
            />
          ) : null}
          <IconButton
            ref={primaryRef}
            wide
            size="compact"
            tone="add"
            label={t(last ? "guide.done" : "guide.next")}
            icon={last ? <Check /> : <ArrowRight />}
            onClick={next}
          />
        </div>
      </div>
    </div>
  );

  return contained ? layer : createPortal(layer, document.body);
}
