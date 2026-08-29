export type SpatialDirection = "up" | "down" | "left" | "right";

type RectLike = Readonly<{
  left: number;
  right: number;
  top: number;
  bottom: number;
  width: number;
  height: number;
}>;

const FOCUSABLE_SELECTOR = [
  "[data-gamepad-focusable]",
  "button",
  "a[href]",
  "input",
  "select",
  "textarea",
  "[tabindex]:not([tabindex='-1'])",
].join(",");

function center(rect: RectLike): { x: number; y: number } {
  return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
}

export function findSpatialCandidate(
  current: RectLike,
  candidates: readonly RectLike[],
  direction: SpatialDirection,
): number {
  const origin = center(current);
  let bestIndex = -1;
  let bestScore = Number.POSITIVE_INFINITY;

  candidates.forEach((candidate, index) => {
    const point = center(candidate);
    const dx = point.x - origin.x;
    const dy = point.y - origin.y;
    const primary = direction === "left" ? -dx
      : direction === "right" ? dx
        : direction === "up" ? -dy
          : dy;
    if (primary <= 1) return;

    const secondary = direction === "left" || direction === "right" ? Math.abs(dy) : Math.abs(dx);
    const score = primary + secondary * 1.8 + (secondary / primary) * 48;
    if (score < bestScore) {
      bestIndex = index;
      bestScore = score;
    }
  });

  return bestIndex;
}

function isFocusable(element: HTMLElement): boolean {
  if (element.dataset.gamepadFocusable === "false") return false;
  if (element.closest("[inert], [aria-hidden='true']")) return false;
  if ("disabled" in element && Boolean(element.disabled)) return false;
  const rect = element.getBoundingClientRect();
  if (rect.width <= 0 || rect.height <= 0 || element.getClientRects().length === 0) return false;
  const style = window.getComputedStyle(element);
  return style.display !== "none" && style.visibility !== "hidden" && style.pointerEvents !== "none";
}

function activeNavigationRoot(): ParentNode {
  const dialogs = Array.from(document.querySelectorAll<HTMLElement>("[role='dialog'][data-state='open']"))
    .filter(isFocusable);
  return dialogs.at(-1) ?? document;
}

export function moveSpatialFocus(direction: SpatialDirection): HTMLElement | null {
  const root = activeNavigationRoot();
  const candidates = Array.from(root.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR)).filter(isFocusable);
  if (candidates.length === 0) return null;

  const active = document.activeElement instanceof HTMLElement && root.contains(document.activeElement)
    ? document.activeElement
    : null;
  if (!active) {
    const first = candidates.find((candidate) => candidate.dataset.controllerSelected === "true") ?? candidates[0]!;
    first.focus({ preventScroll: true });
    first.scrollIntoView({ block: "nearest", inline: "nearest" });
    return first;
  }

  const others = candidates.filter((candidate) => candidate !== active);
  const index = findSpatialCandidate(
    active.getBoundingClientRect(),
    others.map((candidate) => candidate.getBoundingClientRect()),
    direction,
  );
  const next = index >= 0 ? others[index] : null;
  if (!next) return null;
  next.focus({ preventScroll: true });
  next.scrollIntoView({ block: "nearest", inline: "nearest" });
  return next;
}

export function activateFocusedControl(): boolean {
  const active = document.activeElement;
  if (!(active instanceof HTMLElement) || !isFocusable(active)) return false;
  active.click();
  return true;
}

export function scrollControllerSurface(direction: "up" | "down"): void {
  const active = document.activeElement instanceof HTMLElement ? document.activeElement : null;
  let target = active?.closest<HTMLElement>("[data-gamepad-scroll]") ?? null;
  let ancestor = active?.parentElement ?? null;
  while (!target && ancestor) {
    if (ancestor.scrollHeight > ancestor.clientHeight + 2) target = ancestor;
    ancestor = ancestor.parentElement;
  }
  target ??= document.scrollingElement as HTMLElement | null;
  target?.scrollBy({ top: direction === "down" ? 220 : -220, behavior: "smooth" });
}
