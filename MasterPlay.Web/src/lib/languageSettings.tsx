import { useCallback, useEffect, useState, type ReactNode } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useI18n, type Lang } from "./i18n";
import { settingsApi, type Settings } from "./settingsApi";

/**
 * Ngon ngu thuoc config.json, dung chung voi Tray (DOMAIN_RULES §15) — man cai dat
 * lan dau cua desktop ghi lua chon vao do. Nap mot lan TRUOC khi ve app: khong
 * nhay tu tieng Anh sang ngon ngu da chon, va request dau tien da mang dung
 * Accept-Language. Dung chung query ["settings"] voi ThemeProvider nen chi goi mot lan.
 *
 * Loi tai settings khong chan app: ThemeProvider giu loi do va Settings hien no.
 */
export function SettingsLanguageGate({ children, fallback }: { children: ReactNode; fallback: ReactNode }) {
  const { setLang } = useI18n();
  const settings = useQuery({
    queryKey: ["settings"],
    queryFn: settingsApi.get,
    staleTime: Infinity,
    retry: 1,
  });
  const [ready, setReady] = useState(false);

  useEffect(() => {
    if (ready) return;
    if (settings.data) {
      setLang(settings.data.language);
      setReady(true);
    } else if (settings.isError) {
      setReady(true);
    }
  }, [ready, setLang, settings.data, settings.isError]);

  return ready ? children : fallback;
}

/** Luu ngon ngu vao config.json qua PUT /api/settings (khong bao gio localStorage). */
export function usePersistLanguage(): (language: Lang) => Promise<void> {
  const queryClient = useQueryClient();
  return useCallback(
    async (language: Lang) => {
      const current = queryClient.getQueryData<Settings>(["settings"]) ?? (await settingsApi.get());
      const saved = await settingsApi.update({ ...current, language });
      queryClient.setQueryData(["settings"], saved);
    },
    [queryClient],
  );
}
