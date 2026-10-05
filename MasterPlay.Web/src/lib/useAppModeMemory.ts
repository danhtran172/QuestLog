import { useEffect, useRef } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { settingsApi, type Settings } from "./settingsApi";

type AppMode = NonNullable<Settings["appMode"]>;

type AppModeMemoryOptions = Readonly<{
  consoleSession: boolean;
  /** Admin Console dang mo thi khong duoc keo console session len (spec 2026-09-14 §2.1). */
  canRestore: boolean;
  onRestoreConsole: () => void;
}>;

/**
 * Nho mode cuoi cung cua app (desktop | console) qua `/api/settings.appMode` —
 * config dung chung voi Tray, KHONG localStorage (DOMAIN_RULES §15).
 *
 * Mo app: settings la "console" thi vao lai console session mot lan. Sau do moi
 * lan vao/thoat console (F10, LB+RB+A, tray, giu de thoat) thi ghi lai.
 */
export function useAppModeMemory({ consoleSession, canRestore, onRestoreConsole }: AppModeMemoryOptions) {
  const queryClient = useQueryClient();
  const hydratedRef = useRef(false);
  const savedModeRef = useRef<AppMode>("desktop");

  const settingsQuery = useQuery({
    queryKey: ["settings"],
    queryFn: settingsApi.get,
    staleTime: Infinity,
    retry: 1,
  });

  const { mutate: saveMode } = useMutation({
    mutationFn: async (appMode: AppMode) => {
      const current = queryClient.getQueryData<Settings>(["settings"]) ?? await settingsApi.get();
      return settingsApi.update({ ...current, appMode });
    },
    // Gop lai appMode: Host cu (chua restart sau khi them field) khong tra ve no.
    onSuccess: (settings, appMode) => queryClient.setQueryData(["settings"], { ...settings, appMode }),
    onError: (saveError) => toast.error(saveError instanceof Error ? saveError.message : String(saveError)),
  });

  useEffect(() => {
    if (!hydratedRef.current) {
      if (!settingsQuery.data || !canRestore) return;
      hydratedRef.current = true;
      savedModeRef.current = settingsQuery.data.appMode === "console" ? "console" : "desktop";
      if (savedModeRef.current === "console" && !consoleSession) {
        // Render sau consoleSession = true, trung voi mode da luu nen khong ghi.
        onRestoreConsole();
        return;
      }
    }

    const mode: AppMode = consoleSession ? "console" : "desktop";
    if (mode === savedModeRef.current) return;
    savedModeRef.current = mode;
    saveMode(mode);
  }, [canRestore, consoleSession, onRestoreConsole, saveMode, settingsQuery.data]);
}
