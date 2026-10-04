import { Info } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";

/** Huong dan dung chung cho moi noi nhap Score/Excited. */
export function RatingScaleHint({ className }: { className?: string }) {
  const { t } = useI18n();

  return (
    <div
      role="note"
      className={cn(
        "flex w-fit max-w-full items-center gap-2 rounded-full border border-border/70 bg-surface/65 px-3 py-2 text-xs font-medium text-muted-fg shadow-sm backdrop-blur-xl",
        className,
      )}
    >
      <Info aria-hidden size={14} className="shrink-0 text-primary" />
      <span>{t("rating.inputHint")}</span>
    </div>
  );
}
