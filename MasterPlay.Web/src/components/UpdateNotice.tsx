import { useEffect } from "react";
import { toast } from "sonner";
import { useI18n } from "@/lib/i18n";
import { applyUpdateAndRestart, subscribeUpdateReady } from "@/lib/desktopBridge";

/**
 * Ban cai: Shell tai ngam ban moi roi bao o day. Nam ngoai AuthGate de man dang
 * nhap cung thay. Toast dung yen toi khi nguoi dung bam; bo qua thi lan mo app
 * sau Shell tu ap dung.
 */
export function UpdateNotice() {
  const { t } = useI18n();

  useEffect(() => subscribeUpdateReady((version) => {
    toast.info(t("update.ready").replace("{version}", version), {
      id: "questlog-update",
      duration: Infinity,
      action: { label: t("update.restart"), onClick: applyUpdateAndRestart },
    });
  }), [t]);

  return null;
}
