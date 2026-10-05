import { Flame, Minus, Star } from "lucide-react";
import type { CSSProperties, ReactNode } from "react";
import { cn, iconSrc } from "@/lib/utils";
import { useI18n } from "@/lib/i18n";
import type { CatalogItem, MetaBand, ScoreMaterialBand } from "@/lib/api/domain";
import {
  META_BAND_NONE_TONE,
  META_BAND_TONE,
  excitedBandOf,
  formatAvgPlaytimeHours,
  formatExcited,
  formatScore,
  isPeakScore,
  type ExcitedBand,
} from "@/lib/format";
import { Tooltip } from "./ui";

/** Khoa dich nhan dai — dung o tooltip de nguoi dung hoc dan cac bac. */
const EXCITED_BAND_LABEL: Record<ExcitedBand, string> = {
  "extreme-cold": "excited.band.extremeCold",
  cold: "excited.band.cold",
  neutral: "excited.band.neutral",
  fire: "excited.band.fire",
  "extreme-fire": "excited.band.extremeFire",
  lava: "excited.band.lava",
};

export function Pill({
  className,
  style,
  children,
}: {
  className?: string;
  style?: CSSProperties;
  children: ReactNode;
}) {
  return (
    <span
      style={style}
      className={cn(
        "inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-fs-11 font-semibold leading-none",
        className,
      )}
    >
      {children}
    </span>
  );
}

/**
 * Progress / Ownership pill.
 *
 * Mau va nhan den TU DU LIEU (colorCode, displayName, iconData do backend tra ve),
 * KHONG hardcode theo systemKey — nguoi dung doi ten/mau/icon trong Catalog Settings
 * thi pill phai doi theo (DOMAIN_RULES §3, §14).
 *
 * Ban Lovable hardcode 2 bang style theo 6 literal cung -> lech ngay khi user sua.
 */
export function CatalogPill({ item }: { item: CatalogItem | null | undefined }) {
  if (!item) return null;
  return (
    <Pill
      className="border-border bg-surface-2"
      // colorCode tu DB — dung inline style vi gia tri la du lieu, khong phai token.
      style={{ color: item.colorCode, borderColor: `${item.colorCode}55` }}
    >
      {iconSrc(item.iconData) ? (
        <img src={iconSrc(item.iconData)} alt="" className="h-3 w-3 shrink-0 object-contain" />
      ) : null}
      {item.displayName}
    </Pill>
  );
}

/** Gia tri catalog chua gan van giu cung footprint voi chip co du lieu. */
export function EmptyCatalogPill() {
  return (
    <Pill className="min-w-7 justify-center border-border/70 bg-surface-2/45 text-muted-fg">
      <Minus aria-hidden size={12} strokeWidth={2} />
    </Pill>
  );
}

/**
 * Excited — thang 0-11 BUOC 0.5; 11 hien thi "10+" (DOMAIN_RULES §2.2).
 *
 * Hien so thay vi ve 10 icon lua: du lieu that co gia tri le (1.5, 7.5, 9.5)
 * ma day icon roi rac thi khong the bieu dien chinh xac, lai con roi mat.
 *
 * Ban Lovable ep ve 0-5 nguyen nen sai gap doi — vua cat thang 10 xuong 5,
 * vua lam mat phan .5.
 */
export function ExcitedFire({
  value,
  size = 14,
  className,
  iconClassName,
  showIcon = true,
}: {
  value: number | null | undefined;
  size?: number;
  className?: string;
  /** Can chinh rieng ngon lua (vd nang len 1px tren card) ma khong dung den so. */
  iconClassName?: string;
  /** Bang co cot rieng cho Excited thi tieu de cot da noi ro roi — tat icon di. */
  showIcon?: boolean;
}) {
  const { t } = useI18n();
  const text = formatExcited(value);
  if (text == null) return null;
  const displayedScore = isPeakScore(value) ? "10+" : text;

  // Dai nhiet do chi quyet dinh chat lieu cua SO. Icon luon la ngon lua tinh.
  // `text != null` da loai gia tri rong nen band chac chan khac null.
  const band = excitedBandOf(value)!;
  const bandLabel = t(EXCITED_BAND_LABEL[band]);

  return (
    <Tooltip label={`${t("excited.label")} ${text} · ${bandLabel}`}>
    <span
      role="img"
      aria-label={`${t("excited.label")} ${displayedScore} — ${bandLabel}`}
      className={cn(
        // `excited-thermal` + `excited-band--*` chi cap bien CSS cho phan so.
        // Kich thuoc theo font-size (chu) + `size` (icon), nen dung o card /
        // detail / list voi co khac nhau ma khong sua CSS.
        "excited-thermal inline-flex shrink-0 items-center gap-1 align-middle text-fs-11 font-black tabular-nums",
        `excited-band--${band}`,
        className,
      )}
    >
      {showIcon ? (
        <Flame aria-hidden size={size} strokeWidth={2} className={cn("excited-thermal__icon shrink-0", iconClassName)} />
      ) : null}
      <span aria-hidden className="excited-thermal__text">
        {displayedScore}
      </span>
    </span>
    </Tooltip>
  );
}

export type CommunityAveragePlaytimeProps = {
  hours: number | null | undefined;
  className?: string;
  showValue?: boolean;
  /** Tat icon khi cot da co icon o tieu de — vi du bang Play Queue. */
  showIcon?: boolean;
};

type PlaytimeIconProps = { size?: number; className?: string };

/**
 * Khung SVG chung theo luoi 24 / net 2 cua lucide. Hai icon thoi gian ve thanh
 * MOT glyph lien thay vi chong hai icon lucide 10px: o co 13-15px, hai icon
 * chong nhau nhoe thanh mot cuc va ca hai deu dung Clock3 nen nhin nhu nhau.
 */
function PlaytimeGlyph({ size = 14, className, children }: PlaytimeIconProps & { children: ReactNode }) {
  return (
    <svg
      aria-hidden
      viewBox="0 0 24 24"
      width={size}
      height={size}
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      className={cn("shrink-0", className)}
    >
      {children}
    </svg>
  );
}

/**
 * Dong ho cat = do dai cua game: mot luot choi het bao lau (so lieu cong dong).
 * Cat da roi xuong day de khong bi doc nham thanh "dang cho".
 */
export function CommunityPlaytimeIcon({ size, className }: PlaytimeIconProps) {
  return (
    <PlaytimeGlyph size={size} className={cn("text-field-community-playtime", className)}>
      <path d="M5 2h14" />
      <path d="M5 22h14" />
      <path d="M7 2v4.172a2 2 0 0 0 .586 1.414L12 12l4.414-4.414A2 2 0 0 0 17 6.172V2" />
      <path d="M17 22v-4.172a2 2 0 0 0-.586-1.414L12 12l-4.414 4.414A2 2 0 0 0 7 17.828V22" />
      <path d="M8 21h8v-2.5L12 16l-4 2.5z" fill="currentColor" stroke="none" />
    </PlaytimeGlyph>
  );
}

export function CommunityAveragePlaytime({
  hours,
  className,
  showValue = true,
  showIcon = true,
}: CommunityAveragePlaytimeProps) {
  const { t } = useI18n();
  const value = formatAvgPlaytimeHours(hours);
  const label = t("avg.communityTooltip");

  return (
    <Tooltip label={label}>
    <span
      role="img"
      aria-label={showValue ? `${label}: ${value}` : label}
      className={cn(
        "inline-flex items-center gap-1 text-fs-11 font-semibold tabular-nums text-muted-fg",
        className,
      )}
    >
      {showIcon ? <CommunityPlaytimeIcon /> : null}
      {showValue ? <span aria-hidden>{value}</span> : null}
    </span>
    </Tooltip>
  );
}

/**
 * Tay cam + huy hieu dong ho = thoi gian CHINH nguoi dung da choi.
 * Than tay cam bi cat chua khe 2px quanh dong ho, giong icon ghep cua lucide.
 * Mau do cho goi truyen vao (card va detail dung sky).
 */
export function PlayerPlaytimeIcon({ size, className }: PlaytimeIconProps) {
  return (
    <PlaytimeGlyph size={size} className={className}>
      <path d="M11 16H9.83a2 2 0 0 0-1.42.59L7 18c-.5.5-1 1-2 1a3 3 0 0 1-3-3c0-1.54.6-6.58.69-7.26A4 4 0 0 1 6.68 5h10.64a4 4 0 0 1 3.98 3.59c.03.27.13 1.1.24 2.08" />
      <path d="M6 11h4" />
      <path d="M8 9v4" />
      <path d="M15 9h.01" />
      <circle cx="18" cy="17" r="5" />
      <path d="M18 14.8V17l1.4 1" />
    </PlaytimeGlyph>
  );
}

/** Score thang 0-11 buoc 0.5; 11 -> "10+" (DOMAIN_RULES §2.1). */
export function ScoreBadge({ score }: { score: number | null | undefined }) {
  const text = formatScore(score);
  if (text == null) return <span className="text-xs text-muted-fg">—</span>;
  return (
    <span className="inline-flex items-center gap-1 text-xs font-black tabular-nums">
      <Star aria-hidden size={12} className="fill-warning text-warning" />
      {text}
    </span>
  );
}

const SCORE_MATERIAL_CLASS: Record<ScoreMaterialBand, string> = {
  Rust: "score-material--rust",
  Copper: "score-material--copper",
  Silver: "score-material--silver",
  Gold: "score-material--gold",
  Platinum: "score-material--platinum",
  Diamond: "score-material--diamond",
  Opal: "score-material--opal",
};

/** User Score rieng cho Gallery: band do backend tinh, UI chi map sang chat lieu. */
export function GalleryScoreChip({
  score,
  band,
  size = 24,
  className,
}: {
  score: number | null | undefined;
  band: ScoreMaterialBand | null | undefined;
  size?: number;
  className?: string;
}) {
  const { t } = useI18n();
  const formatted = formatScore(score);
  if (formatted == null) return null;

  const text = formatted;
  const label = `${t("sort.score")} ${text}`;

  return (
    <Tooltip label={label}>
    <span
      aria-label={label}
      style={{ minWidth: size + 10, height: size * 0.8 }}
      className={cn(
        "score-material-glass relative inline-grid shrink-0 place-items-center rounded-[4px] px-2 text-xs font-black tabular-nums tracking-tight",
        band ? SCORE_MATERIAL_CLASS[band] : "score-material--fallback",
        className,
      )}
    >
      <span aria-hidden className="score-material-chip__text">
        {text}
      </span>
    </span>
    </Tooltip>
  );
}

/**
 * MetaScore chip.
 *
 * Mau lay theo `band` do BACKEND tra ve (nguong 90/80/70/50 nam o server).
 * Client KHONG tu so sanh so — ban Lovable dung >=75 / >=50 nen lech han
 * so voi he thong that (mismatch M16 / M39).
 */
export function MetaChip({
  value,
  band,
  size = 24,
  className,
}: {
  value: number | null | undefined;
  band: MetaBand | null | undefined;
  size?: number;
  className?: string;
}) {
  const tone = band ? META_BAND_TONE[band] : META_BAND_NONE_TONE;

  // band == null nghia la CHUA CO metascore (null hoac <= 0) — DOMAIN_RULES §5.5.
  // Phai hien "—", khong duoc hien so 0 nhu mot diem that.
  const text = band == null ? "—" : String(value);

  return (
    <Tooltip label={`Metascore ${text}`}>
    <span
      style={{ minWidth: size + 10, height: size * 0.8 }}
      className={cn(
        "score-meta-glass grid shrink-0 place-items-center rounded-[4px] px-2 text-xs font-black tabular-nums",
        tone,
        className,
      )}
    >
      {text}
    </span>
    </Tooltip>
  );
}
