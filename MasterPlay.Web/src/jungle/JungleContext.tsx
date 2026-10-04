/**
 * Context cho jungle theme: muc do hieu ung nguoi dung chon.
 *
 * Doc/ghi qua /api/settings giong themeMode (DOMAIN_RULES §15 — config dung
 * chung voi Tray, khong luu localStorage).
 */
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { settingsApi, type Settings } from "@/lib/settingsApi";
import { useTheme } from "@/lib/theme";
/**
 * Muc do hieu ung nguoi dung chon trong Settings.
 *
 * "auto" la mac dinh: theo cai dat giam chuyen dong cua he dieu hanh — tat neu
 * he thong dang bat giam chuyen dong, con lai chay muc vua phai. Ba muc con lai
 * la lua chon RO RANG cua nguoi dung va duoc ton trong tuyet doi: nguoi tu tay
 * bat "Day du" thi khong con la ep buoc, khong duoc phu quyet.
 */
export type JungleFxLevel = "auto" | "off" | "medium" | "full";

type JungleValue = {
  /** True khi theme jungle dang bat VA fx khong bi tat. */
  active: boolean;
  /** Muc nguoi dung chon (co the la "auto"). */
  level: JungleFxLevel;
  /** Muc thuc te dang chay sau khi giai quyet "auto". */
  effectiveLevel: Exclude<JungleFxLevel, "auto">;
  /** True khi "auto" bi he dieu hanh keo ve off vi dang giam chuyen dong. */
  suppressedBySystem: boolean;
  reduced: boolean;
  setLevel: (level: JungleFxLevel) => Promise<void>;
};

const FALLBACK: JungleValue = {
  active: false,
  level: "auto",
  effectiveLevel: "medium",
  suppressedBySystem: false,
  reduced: false,
  setLevel: async () => {},
};

const JungleContext = createContext<JungleValue>(FALLBACK);

function normalizeLevel(value: unknown): JungleFxLevel {
  return value === "off" || value === "full" || value === "medium" || value === "auto"
    ? value
    : "auto";
}

function usePrefersReducedMotion(): boolean {
  const [reduced, setReduced] = useState(() =>
    typeof window !== "undefined"
      ? (window.matchMedia?.("(prefers-reduced-motion: reduce)").matches ?? false)
      : false,
  );
  useEffect(() => {
    const mq = window.matchMedia?.("(prefers-reduced-motion: reduce)");
    if (!mq) return;
    const onChange = (e: MediaQueryListEvent) => setReduced(e.matches);
    mq.addEventListener("change", onChange);
    return () => mq.removeEventListener("change", onChange);
  }, []);
  return reduced;
}

export function JungleProvider({ children }: { children: ReactNode }) {
  const { resolved } = useTheme();
  const queryClient = useQueryClient();
  const reduced = usePrefersReducedMotion();

  const settingsQuery = useQuery({
    queryKey: ["settings"],
    queryFn: settingsApi.get,
    staleTime: Infinity,
    retry: 1,
  });

  const level = normalizeLevel(settingsQuery.data?.jungleFx);
  const isJungle = resolved === "jungle";

  // "auto" theo he dieu hanh; ba muc kia la y dinh ro rang cua nguoi dung.
  const effectiveLevel: Exclude<JungleFxLevel, "auto"> =
    level === "auto" ? (reduced ? "off" : "medium") : level;
  const suppressedBySystem = level === "auto" && reduced;
  const active = isJungle && effectiveLevel !== "off";

  const saveMutation = useMutation({
    mutationFn: async (patch: Partial<Settings>) => {
      const current = settingsQuery.data ?? (await settingsApi.get());
      return settingsApi.update({ ...current, ...patch });
    },
    // Gop patch de len tren phan hoi cua server: backend cu (chua restart sau
    // khi them jungleFx) se KHONG tra ve field nay, va neu tin y nguyen phan
    // hoi thi lua chon cua nguoi dung bi nuot mat ngay lap tuc.
    onSuccess: (settings, patch) =>
      queryClient.setQueryData(["settings"], { ...settings, ...patch }),
  });

  const setLevel = useCallback(
    async (next: JungleFxLevel) => {
      await saveMutation.mutateAsync({ jungleFx: next });
    },
    [saveMutation],
  );

  // Cho CSS biet de bat/tat cac hieu ung thuan CSS (reu vien card, ven tan la).
  useEffect(() => {
    const root = document.documentElement;
    if (active) root.dataset.jungleFx = effectiveLevel;
    else delete root.dataset.jungleFx;
    // Co nay cho CSS biet: he thong dang giam chuyen dong NHUNG nguoi dung da
    // tu tay bat. Khoi @media (prefers-reduced-motion) trong jungle.css se
    // khong ap dung khi co nay co mat.
    if (active && reduced) root.dataset.jungleMotion = "forced";
    else delete root.dataset.jungleMotion;
  }, [active, effectiveLevel, reduced]);

  const value = useMemo<JungleValue>(
    () => ({
      active, level, effectiveLevel, suppressedBySystem,
      reduced, setLevel,
    }),
    [active, effectiveLevel, level, reduced, setLevel, suppressedBySystem],
  );

  return <JungleContext value={value}>{children}</JungleContext>;
}

export function useJungle(): JungleValue {
  return useContext(JungleContext);
}
