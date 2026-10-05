import { useEffect } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Activity, FolderSearch, Loader2, Power, Save, ScrollText } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { api, ApiRequestError } from "@/lib/api/client";
import { trayApi, type TrayServiceName } from "@/lib/trayApi";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  IconButton,
  Switch,
} from "./ui";

/**
 * Tray panel.
 *
 * Day KHONG phai tray that. Tray that van la tien trinh WinForms rieng vi no
 * phai song khi UI dong (quyet dinh B8). Panel nay chi la BANG DIEU KHIEN
 * doc/ghi qua API.
 *
 * Host chuyen lenh sang tray qua named pipe (/api/tray/*). `capabilities.features.tray`
 * chi noi Host CO THE dieu khien tray; tray co dang chay hay khong thi hoi
 * /api/tray/status — tray chua chay thi moi cong tac bi khoa va co dong giai thich.
 * Khong con nut dong bo anh: Host tu dua anh ve noi luu sau moi lan luu (DOMAIN_RULES §12).
 */
const SERVICES = [
  { key: "playtime", icon: Activity },
  { key: "scanner", icon: FolderSearch },
  { key: "backup", icon: Save },
] as const;

function trayErrorMessage(error: unknown, t: (key: string) => string): string {
  if (error instanceof ApiRequestError) {
    if (error.code === "TRAY_NOT_RUNNING") return t("tray.notRunningHint");
    if (error.code === "SCANNER_STOPPED") return t("tray.scannerStopped");
    if (error.code === "TRAY_BUSY") return t("tray.busy");
  }
  return error instanceof Error ? error.message : String(error);
}

export function TrayPanel({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
}) {
  const { t } = useI18n();
  const queryClient = useQueryClient();

  const { data: caps } = useQuery({
    queryKey: ["capabilities"],
    queryFn: () => api.capabilities(),
    enabled: open,
    staleTime: Infinity,
  });

  const trayAvailable = caps?.features?.tray === true;
  // Backup dang tam tat o backend -> an dich vu backup va nut "Backup ngay".
  const backupAvailable = caps?.features?.backup === true;
  const services = SERVICES.filter((s) => s.key !== "backup" || backupAvailable);

  const trayStatus = useQuery({
    queryKey: ["tray-status"],
    queryFn: trayApi.status,
    enabled: open && trayAvailable,
    // Tray co the vua bi tat/bat tu menu cua chinh no trong luc panel dong.
    staleTime: 0,
  });
  const trayRunning = trayStatus.data?.running === true;
  const serviceEnabled = (name: TrayServiceName) =>
    trayStatus.data?.services.some((service) => service.name === name && service.enabled) === true;

  const setService = useMutation({
    mutationFn: ({ name, enabled }: { name: TrayServiceName; enabled: boolean }) =>
      trayApi.setService(name, enabled),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["tray-status"] }),
  });
  const scanNow = useMutation({ mutationFn: trayApi.scanNow });
  const openLogs = useMutation({ mutationFn: trayApi.openLogs });

  // Panel van mounted khi dong: xoa ket qua lenh cu de lan mo sau khong hien
  // "Da bat dau quet" cua mot lan bam tu truoc.
  const { reset: resetSetService } = setService;
  const { reset: resetScanNow } = scanNow;
  const { reset: resetOpenLogs } = openLogs;
  useEffect(() => {
    if (open) return;
    resetSetService();
    resetScanNow();
    resetOpenLogs();
  }, [open, resetOpenLogs, resetScanNow, resetSetService]);
  const trayError = trayStatus.error ?? setService.error ?? scanNow.error ?? openLogs.error;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md p-5">
        <DialogHeader className="mb-4">
          <DialogTitle className="flex items-center gap-2">
            <Power size={18} /> {t("tray.title")}
          </DialogTitle>
          <DialogDescription>
            {!trayAvailable
              ? t("tray.unavailable")
              : trayStatus.isPending
                ? t("tray.checking")
                : trayRunning
                  ? t("tray.running")
                  : t("tray.notRunning")}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-2">
          {services.map((s) => (
            <div key={s.key} className="surface-card flex items-center gap-3 px-3 py-2.5">
              <s.icon size={15} className="text-primary" />
              <span className="flex-1 text-sm font-semibold">{t(`tray.${s.key}`)}</span>
              <Switch
                checked={serviceEnabled(s.key)}
                disabled={!trayRunning || setService.isPending}
                onCheckedChange={(enabled) => setService.mutate({ name: s.key, enabled })}
                aria-label={t(`tray.${s.key}`)}
              />
            </div>
          ))}
        </div>

        <div className="mt-4 flex flex-wrap gap-2">
          <IconButton
            tone="open"
            label={t("tray.scanNow")}
            icon={scanNow.isPending ? <Loader2 className="animate-spin" /> : <FolderSearch />}
            disabled={!trayRunning || scanNow.isPending}
            onClick={() => scanNow.mutate()}
          />
          {backupAvailable && (
            <IconButton
              tone="add"
              label={t("tray.backupNow")}
              icon={<Save />}
              disabled={!trayRunning}
            />
          )}
          <IconButton
            label={t("tray.logs")}
            icon={<ScrollText />}
            disabled={!trayRunning || openLogs.isPending}
            onClick={() => openLogs.mutate()}
          />
        </div>

        {scanNow.isSuccess && (
          <p className="mt-4 text-xs text-muted-fg" role="status">{t("tray.scanStarted")}</p>
        )}
        {trayError && (
          <p className="mt-4 text-xs text-danger" role="alert">{trayErrorMessage(trayError, t)}</p>
        )}
        {trayStatus.data && !trayRunning && !trayError && (
          <p className="mt-4 text-xs text-warning">{t("tray.notRunningHint")}</p>
        )}

        {!trayAvailable && (
          <p className="mt-4 text-xs text-warning">{t("tray.unavailableHint")}</p>
        )}
      </DialogContent>
    </Dialog>
  );
}
