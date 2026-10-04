import { useEffect, useRef } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { api } from "./api/client";
import { useI18n } from "./i18n";

/**
 * Keo thay doi metadata tu kho dung chung ve thu vien, mot lan moi phien.
 *
 * Admin sua mot ban ghi ben Admin Console thi game tuong ung trong thu vien
 * nguoi dung phai doi theo. Field nguoi dung da sua tay thi GIU NGUYEN — luat
 * hop nhat nam o server (`LibraryMetadataMerge`), o day chi kich hoat va bao
 * ket qua.
 *
 * Chay o App chu khong o tung man hinh: mot lan cho ca phien. Server con mot
 * cua chan 10 phut nua, nen mo lai tab khong tao them round-trip nao.
 */
export function useLibraryMetadataRefresh(enabled: boolean) {
  const queryClient = useQueryClient();
  const { t } = useI18n();
  // StrictMode goi effect hai lan o dev. Khong chan thi lan dong bo dau tien
  // chay doi, va bao cao hien hai toast.
  const started = useRef(false);

  useEffect(() => {
    if (!enabled || started.current) return;
    started.current = true;

    let cancelled = false;
    void (async () => {
      try {
        const report = await api.refreshLibraryMetadata();
        if (cancelled || report.skipped || report.updated === 0) return;

        // Danh sach va man hinh chi tiet dang giu gia tri cu.
        await queryClient.invalidateQueries({ queryKey: ["games"] });
        await queryClient.invalidateQueries({ queryKey: ["gameDetail"] });
        toast.success(
          t("library.metadataRefresh.updated").replace("{count}", String(report.updated)),
        );
      } catch (error) {
        if (cancelled) return;
        // Khong nuot loi (AGENTS.md §2). Nhung day la viec chay nen, nen bao
        // bang toast chu khong chan man hinh thu vien.
        toast.error(
          t("library.metadataRefresh.failed").replace(
            "{message}",
            error instanceof Error ? error.message : String(error),
          ),
        );
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [enabled, queryClient, t]);
}
