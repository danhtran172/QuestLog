import { useCallback, useEffect, useMemo, useState } from "react";
import { useI18n } from "@/lib/i18n";
import { useGuides } from "@/lib/guides";
import type { RegularTab } from "@/lib/api/domain";
import type { GuideStep } from "./GuideTour";

/* Moc `data-guide` tren thanh tab cua App. */
export const VIEW_GUIDE = {
  tabs: "view-tabs",
  tab: (key: RegularTab) => `view-tab-${key}`,
} as const;

const STEPS: [target: string, key: string, title: string | null, paragraphs: number][] = [
  [VIEW_GUIDE.tabs, "intro", null, 2],
  [VIEW_GUIDE.tab("all"), "all", "nav.all", 2],
  [VIEW_GUIDE.tab("planning"), "planning", "nav.planning", 2],
  [VIEW_GUIDE.tab("finished"), "finished", "nav.finished", 2],
  [VIEW_GUIDE.tab("queue"), "queue", "nav.queue", 2],
  [VIEW_GUIDE.tabs, "custom", null, 2],
];

/**
 * Huong dan cac tab (view mode) — tac dung tung tab va thu tu rieng cua tung tab.
 * Bat mot lan khi thu vien DA CO game (vua them game dau tien, hoac nguoi dung cu
 * lan dau thay tinh nang) va khong co popup/man hinh nao dang mo.
 */
export function useViewModesGuide({ hasGames, idle }: { hasGames: boolean; idle: boolean }) {
  const { t } = useI18n();
  const { isPending, markSeen } = useGuides();
  const pending = isPending("viewModes");
  const [open, setOpen] = useState(false);

  useEffect(() => {
    if (open || !pending || !hasGames || !idle) return;
    /* Doi popup vua dong (vd the game sau khi Luu) bien mat han roi moi lam toi. */
    const timer = window.setTimeout(() => setOpen(true), 700);
    return () => window.clearTimeout(timer);
  }, [hasGames, idle, open, pending]);

  const close = useCallback(() => {
    setOpen(false);
    markSeen("viewModes");
  }, [markSeen]);

  const steps = useMemo<GuideStep[]>(() => STEPS.map(([target, key, title, paragraphs]) => ({
    target,
    title: title ? t(title) : t(`guide.views.${key}.title`),
    body: Array.from({ length: paragraphs }, (_, index) => (
      <p key={index}>{t(`guide.views.${key}.body${index + 1}`)}</p>
    )),
  })), [t]);

  return { open, close, steps, eyebrow: t("guide.views.eyebrow"), tryLabel: t("guide.views.tryIt") };
}
