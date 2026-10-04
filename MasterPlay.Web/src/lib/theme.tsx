/**
 * Theme: light | dark | system | jungle.
 * Persist qua GET/PUT /api/settings, khong luu vao browser storage
 * vi theme thuoc config dung chung voi Tray (DOMAIN_RULES §15).
 */
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { settingsApi, type Settings } from "@/lib/settingsApi";

export type ThemeMode = NonNullable<Settings["themeMode"]>;

/**
 * TAM THOI tat chon theme: app luon chay dark, khong doc/ghi themeMode tu
 * config (gia tri dang luu giu nguyen de bat lai la dung). Doi thanh true
 * de mo lai bo chon trong Settings.
 */
export const THEME_SELECTION_ENABLED = false;
const FORCED_THEME: ThemeMode = "dark";
export type ResolvedTheme = "light" | "dark" | "jungle";

type ThemeValue = {
  mode: ThemeMode;
  setMode: (m: ThemeMode) => void;
  persistMode: (m: ThemeMode) => Promise<void>;
  /** Che do dang ap dung thuc te sau khi giai quyet "system". */
  resolved: ResolvedTheme;
  isLoading: boolean;
  isSaving: boolean;
  error: Error | null;
};

const ThemeContext = createContext<ThemeValue | null>(null);

function systemPrefersDark() {
  return window.matchMedia?.("(prefers-color-scheme: dark)").matches ?? true;
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [mode, setModeState] = useState<ThemeMode>("dark");
  const [systemDark, setSystemDark] = useState(() => systemPrefersDark());
  const hasHydrated = useRef(false);

  const settingsQuery = useQuery({
    queryKey: ["settings"],
    queryFn: settingsApi.get,
    staleTime: Infinity,
    retry: 1,
  });

  const saveMutation = useMutation({
    mutationFn: async (nextMode: ThemeMode) => {
      const current = settingsQuery.data ?? await settingsApi.get();
      return settingsApi.update({ ...current, themeMode: nextMode });
    },
    onSuccess: (settings) => {
      queryClient.setQueryData(["settings"], settings);
      setModeState(settings.themeMode ?? "system");
    },
  });

  useEffect(() => {
    if (!THEME_SELECTION_ENABLED || hasHydrated.current || !settingsQuery.data) return;
    setModeState(settingsQuery.data.themeMode ?? "system");
    hasHydrated.current = true;
  }, [settingsQuery.data]);

  useEffect(() => {
    const mq = window.matchMedia?.("(prefers-color-scheme: dark)");
    if (!mq) return;
    const onChange = (e: MediaQueryListEvent) => setSystemDark(e.matches);
    mq.addEventListener("change", onChange);
    return () => mq.removeEventListener("change", onChange);
  }, []);

  const effectiveMode = THEME_SELECTION_ENABLED ? mode : FORCED_THEME;
  const resolved: ResolvedTheme =
    effectiveMode === "system" ? (systemDark ? "dark" : "light") : effectiveMode;

  useEffect(() => {
    document.documentElement.dataset.theme = resolved;
  }, [resolved]);

  const setMode = useCallback((m: ThemeMode) => setModeState(m), []);
  const persistMode = useCallback(async (m: ThemeMode) => {
    if (!THEME_SELECTION_ENABLED) return;
    await saveMutation.mutateAsync(m);
  }, [saveMutation]);

  const error = settingsQuery.error instanceof Error
    ? settingsQuery.error
    : saveMutation.error instanceof Error
      ? saveMutation.error
      : null;
  const value = useMemo(
    () => ({
      mode: effectiveMode,
      setMode,
      persistMode,
      resolved,
      isLoading: settingsQuery.isPending,
      isSaving: saveMutation.isPending,
      error,
    }),
    [error, effectiveMode, persistMode, resolved, saveMutation.isPending, settingsQuery.isPending, setMode],
  );
  return <ThemeContext value={value}>{children}</ThemeContext>;
}

export function useTheme(): ThemeValue {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error("useTheme must be used inside <ThemeProvider>");
  return ctx;
}
