/**
 * Huy hieu trang thai xuat ban cua mot ban ghi metadata — cot "Trang thai" cua
 * danh sach Admin Console va khoi Xuat ban cua Preview.
 *
 * Ba trang thai, deu do server tinh (`isPublished`, `gateFailures`,
 * `manuallyUnpublished`) — client chi chon mau:
 *   - Da xuat ban.
 *   - Chua xuat ban vi truot tieu chi: noi TRUOT CAI GI, khong chi "chua xuat ban".
 *   - Admin go tay.
 */
import { FileCheck2, FileX } from "lucide-react";
import { adminHealthLabelKey, type AdminMetadataGame } from "@/lib/adminApi";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { Tooltip } from "./ui";

/** Ly do mot ban ghi chua xuat ban: truot tieu chi nao, hay do admin go tay. */
export function unpublishedReason(game: AdminMetadataGame, t: (key: string) => string): string {
  return game.gateFailures.length > 0
    ? t("admin.publishing.blockedBecause").replace(
        "{rules}",
        game.gateFailures.map((key) => t(adminHealthLabelKey(key))).join(", "),
      )
    : t("admin.publishing.unpublishedManually");
}

export function AdminPublishedStatus({ game }: { game: AdminMetadataGame }) {
  const { t } = useI18n();
  const badge = "inline-flex w-fit items-center gap-1 whitespace-nowrap rounded-full border px-2 py-0.5 text-fs-10 font-black uppercase tracking-wide";

  if (game.isPublished) {
    return (
      <span className={cn(badge, "border-success/45 bg-success/15 text-success")}>
        <FileCheck2 size={10} aria-hidden="true" />
        {t("admin.publishing.published")}
      </span>
    );
  }

  const blocked = game.gateFailures.length > 0;
  return (
    <Tooltip label={unpublishedReason(game, t)}>
      <span
        className={cn(
          badge,
          blocked
            ? "border-warning/45 bg-warning/15 text-warning"
            : "border-border bg-surface-2/50 text-muted-fg",
        )}
      >
        <FileX size={10} aria-hidden="true" />
        {blocked ? t("admin.publishing.notPublished") : t("admin.publishing.unpublishedManually")}
      </span>
    </Tooltip>
  );
}
