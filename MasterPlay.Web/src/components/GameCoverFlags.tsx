import type { ReactNode } from "react";

import { Tooltip } from "@/components/ui";
import { daysUntilPublishUtc } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";

type GameCoverFlagsProps = {
  isDlc: boolean;
  isUpcoming: boolean;
  /** Ngay phat hanh (ISO) — dung tinh so ngay con lai khi hover tag Upcoming. */
  publishDate?: string | null;
  size?: "card" | "thumbnail" | "hero";
  fadeUpcomingBottom?: boolean;
};

const UPCOMING_BOTTOM_FADE = {
  maskImage: "linear-gradient(to bottom, black 0%, black 62%, transparent 100%)",
  WebkitMaskImage: "linear-gradient(to bottom, black 0%, black 62%, transparent 100%)",
} as const;

const flagStyles = {
  card: {
    dlcFrame: "h-14 w-14",
    dlcRibbon: "-left-7 top-2.5 w-24 py-1 text-fs-9 tracking-[0.14em]",
    upcomingFrame: "h-16 w-20",
    upcomingRibbon: "-right-8 bottom-3 w-28 py-1 text-fs-8 tracking-[0.1em]",
  },
  thumbnail: {
    dlcFrame: "h-8 w-8",
    dlcRibbon: "-left-4 top-1.5 w-14 py-0.5 text-fs-6 tracking-[0.1em]",
    upcomingFrame: "h-9 w-11",
    upcomingRibbon: "-right-5 bottom-1.5 w-16 py-0.5 text-fs-5 tracking-[0.06em]",
  },
  hero: {
    dlcFrame: "h-24 w-24",
    dlcRibbon: "-left-11 top-5 w-40 py-1.5 text-xs tracking-[0.16em]",
    upcomingFrame: "h-28 w-36",
    upcomingRibbon: "-right-12 bottom-6 w-44 py-1.5 text-fs-11 tracking-[0.12em]",
  },
} as const;

/** Corner ribbons dung chung cho moi cover; chi scale theo kich thuoc anh. */
export function GameCoverFlags({
  isDlc,
  isUpcoming,
  publishDate,
  size = "card",
  fadeUpcomingBottom = false,
}: GameCoverFlagsProps) {
  const { t } = useI18n();
  const styles = flagStyles[size];

  const daysLeft = isUpcoming ? daysUntilPublishUtc(publishDate) : null;
  const countdownTemplate =
    daysLeft == null ? null : daysLeft === 1 ? t("card.upcomingDay") : t("card.upcomingDays");
  const countdownText =
    countdownTemplate == null ? null : countdownTemplate.replace("{days}", String(daysLeft));
  const countdownNode =
    countdownTemplate == null ? null : renderCountdown(countdownTemplate, daysLeft as number);

  return (
    <>
      {isDlc ? (
        <span
          className={cn(
            "pointer-events-none absolute left-0 top-0 z-10 overflow-hidden",
            styles.dlcFrame,
          )}
          aria-label={t("card.dlc")}
        >
          <span
            aria-hidden
            className={cn(
              "absolute -rotate-45 border-y border-white/20 bg-gradient-to-r from-violet-700/85 via-fuchsia-500/85 to-violet-600/85 text-center font-black leading-none text-white shadow-lg shadow-fuchsia-950/25",
              styles.dlcRibbon,
            )}
          >
            {t("card.dlc")}
          </span>
        </span>
      ) : null}

      {isUpcoming ? (
        <UpcomingRibbon
          label={t("card.upcoming")}
          countdown={countdownNode}
          countdownText={countdownText}
          frameClass={styles.upcomingFrame}
          ribbonClass={styles.upcomingRibbon}
          fadeBottom={fadeUpcomingBottom}
        />
      ) : null}
    </>
  );
}

/**
 * Chen so ngay (mau xanh, dam) vao giua template i18n. Vi du "{days} days left"
 * -> "…" + <b class=xanh>{days}</b> + "…".
 */
function renderCountdown(template: string, days: number) {
  const [before, after = ""] = template.split("{days}");
  return (
    <>
      {before}
      <span className="font-black text-sky-500">{days}</span>
      {after}
    </>
  );
}

/**
 * Ribbon "Upcoming" o goc phai duoi. Chu tren ribbon KHONG doi.
 * Khi co ngay phat hanh, hover se bat mot bong bong kinh cho biet con bao nhieu ngay.
 */
function UpcomingRibbon({
  label,
  countdown,
  countdownText,
  frameClass,
  ribbonClass,
  fadeBottom,
}: {
  label: string;
  countdown: ReactNode | null;
  countdownText: string | null;
  frameClass: string;
  ribbonClass: string;
  fadeBottom: boolean;
}) {
  const frame = (
    <span
      className={cn(
        "absolute bottom-0 right-0 z-10 overflow-hidden",
        countdown ? "pointer-events-auto cursor-default" : "pointer-events-none",
        frameClass,
      )}
      aria-label={countdownText ?? label}
      style={fadeBottom ? UPCOMING_BOTTOM_FADE : undefined}
    >
      <span
        aria-hidden
        className={cn(
          "absolute -rotate-45 border-y border-white/20 bg-gradient-to-r from-amber-600/85 via-orange-400/85 to-amber-500/85 text-center font-black uppercase leading-none text-white shadow-lg shadow-orange-950/25",
          ribbonClass,
        )}
      >
        {label}
      </span>
    </span>
  );

  if (!countdown) return frame;
  return (
    <Tooltip
      label={countdown}
      side="left"
      className="px-3.5 py-2 text-sm font-bold tracking-wide"
    >
      {frame}
    </Tooltip>
  );
}
