import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useI18n } from "@/lib/i18n";
import { useGuides } from "@/lib/guides";
import type { GuideStep } from "./GuideTour";

/* Moc `data-guide` trong GameEditorForm. Doi bo cuc form thi doi theo. */
export const FORM_GUIDE = {
  name: "form-name",
  lock: "form-lock",
  playtime: "form-playtime",
  status: "form-status",
  tags: "form-tags",
  launch: "form-launch",
  images: "form-images",
  save: "form-save",
} as const;

const STEPS: [target: string, key: string, paragraphs: number][] = [
  [FORM_GUIDE.name, "name", 2],
  [FORM_GUIDE.lock, "lock", 2],
  [FORM_GUIDE.playtime, "playtime", 2],
  [FORM_GUIDE.status, "status", 3],
  [FORM_GUIDE.tags, "tags", 1],
  [FORM_GUIDE.launch, "launch", 2],
  [FORM_GUIDE.images, "images", 2],
  [FORM_GUIDE.save, "save", 2],
];

/**
 * Huong dan lan dau mo form THEM game. Chi bat khi `/api/settings.seenGuides`
 * chua co "gameForm"; xong hoac bo qua deu ghi lai, mo lai duoc trong Settings.
 *
 * `canStart`: cac danh muc (Progress, Ownership...) da tai xong — lam toi man
 * hinh luc form con nhay bo cuc thi vung chieu sang se lech.
 */
export function useGameFormGuide(canStart: boolean) {
  const { t } = useI18n();
  const { isPending, markSeen } = useGuides();
  const [open, setOpen] = useState(false);
  const startedRef = useRef(false);
  const pending = isPending("gameForm");

  useEffect(() => {
    if (!canStart || !pending || startedRef.current) return;
    /* Doi Dialog mo xong (animation) roi moi lam toi. */
    const timer = window.setTimeout(() => {
      startedRef.current = true;
      setOpen(true);
    }, 450);
    return () => window.clearTimeout(timer);
  }, [canStart, pending]);

  const close = useCallback(() => {
    setOpen(false);
    markSeen("gameForm");
  }, [markSeen]);

  /* Cho close guard cua Dialog: Esc / nut X khi tour dang mo chi dong tour. */
  const openRef = useRef(open);
  openRef.current = open;
  const stop = useCallback(() => {
    if (!openRef.current) return false;
    close();
    return true;
  }, [close]);

  const steps = useMemo<GuideStep[]>(() => STEPS.map(([target, key, paragraphs]) => ({
    target,
    title: t(`guide.form.${key}.title`),
    body: Array.from({ length: paragraphs }, (_, index) => (
      <p key={index}>{t(`guide.form.${key}.body${index + 1}`)}</p>
    )),
  })), [t]);

  return { open, close, stop, steps, eyebrow: t("guide.form.eyebrow") };
}
