import { useEffect } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Activity, CircleAlert, FolderSearch, Loader2, Power, Save, ScrollText } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { api, ApiRequestError } from "@/lib/api/client";
import { imageSyncApi, type ImageSyncReport } from "@/lib/imageSyncApi";
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
 * Ngoai le: dong bo anh chay ngay trong Host nen co co rieng
 * `capabilities.features.imageSync`.
 */
const SERVICES = [
  { key: "playtime", icon: Activity },
  { key: "scanner", icon: FolderSearch },
  { key: "backup", icon: Save },
] as const;

const IMAGE_ROLES = ["banner", "backdrop", "poster"] as const;

function syncErrorMessage(error: unknown, t: (key: string) => string): string {
  if (error instanceof ApiRequestError) {
    if (error.code === "IMAGE_SYNC_RUNNING") return t("tray.imageSync.running");
    if (error.code === "INTEGRATION_NOT_CONFIGURED") return t("tray.imageSync.notConfigured");
  }
  return error instanceof Error ? error.message : String(error);
}

function trayErrorMessage(error: unknown, t: (key: string) => string): string {
  if (error instanceof ApiRequestError) {
    if (error.code === "TRAY_NOT_RUNNING") return t("tray.notRunningHint");
    if (error.code === "SCANNER_STOPPED") return t("tray.scannerStopped");
    if (error.code === "TRAY_BUSY") return t("tray.busy");
  }
  return error instanceof Error ? error.message : String(error);
}

function ImageSyncReportView({ report }: { report: ImageSyncReport }) {
  const { t } = useI18n();

  return (
    <div className="surface-card mt-4 space-y-2 px-3 py-2.5 text-xs">
      {IMAGE_ROLES.map((role) => (
        <div key={role} className="flex items-baseline justify-between gap-3">
          <span className="font-semibold text-fg">{t(`editor.${role}`)}</span>
          <span className="tabular-nums text-muted-fg">
            {t("tray.imageSync.counts")
              .replace("{uploaded}", String(report[role].uploaded))
              .replace("{synced}", String(report[role].alreadySynced))
              .replace("{missing}", String(report[role].missing))}
          </span>
        </div>
      ))}

      {report.failures.length > 0 && (
        <div className="border-t border-border pt-2">
          <p className="mb-1.5 flex items-center gap-1.5 font-semibold text-danger">
            <CircleAlert size={13} />
            {t("tray.imageSync.failed").replace("{count}", String(report.failed))}
          </p>
          <ul className="max-h-40 space-y-1.5 overflow-y-auto pr-1">
            {report.failures.map((failure) => (
              <li key={`${failure.gameId}:${failure.role}`}>
                <span className="font-semibold text-fg">{failure.gameName}</span>
                <span className="text-muted-fg"> · {t(`editor.${failure.role}`)}</span>
                <span className="block break-words text-muted-fg">{failure.reason}</span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
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
  const imageSyncAvailable = caps?.features?.imageSync === true;
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

  const imageSync = useMutation({
    mutationFn: imageSyncApi.run,
    onSuccess: async (report) => {
      // Chi tai lai khi co anh doi duong dan that.
      if (IMAGE_ROLES.some((role) => report[role].uploaded > 0)) {
        await Promise.all([
          queryClient.invalidateQueries({ queryKey: ["games"] }),
          queryClient.invalidateQueries({ queryKey: ["game-detail"] }),
          queryClient.invalidateQueries({ queryKey: ["play-queue"] }),
        ]);
      }
    },
  });

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
          <IconButton
            label={imageSync.isPending ? t("tray.imageSync.syncing") : t("tray.sync")}
            icon={imageSync.isPending ? <Loader2 className="animate-spin" /> : <Activity />}
            disabled={!imageSyncAvailable || imageSync.isPending}
            aria-busy={imageSync.isPending}
            onClick={() => imageSync.mutate()}
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

        {imageSync.isPending && (
          <p className="mt-4 text-xs text-muted-fg" role="status">{t("tray.imageSync.syncingHint")}</p>
        )}
        {imageSync.isError && (
          <p className="mt-4 text-xs text-danger" role="alert">{syncErrorMessage(imageSync.error, t)}</p>
        )}
        {imageSync.data && <ImageSyncReportView report={imageSync.data} />}

        {caps && !imageSyncAvailable && (
          <p className="mt-4 text-xs text-warning">{t("tray.imageSync.notConfigured")}</p>
        )}
        {!trayAvailable && (
          <p className="mt-4 text-xs text-warning">{t("tray.unavailableHint")}</p>
        )}
      </DialogContent>
    </Dialog>
  );
}
