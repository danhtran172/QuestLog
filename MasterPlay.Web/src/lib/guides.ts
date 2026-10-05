import { useCallback } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { settingsApi, type Settings } from "./settingsApi";

export type GuideId = NonNullable<Settings["seenGuides"]>[number];

/**
 * Huong dan UI da xem — luu o `/api/settings.seenGuides` (config dung chung, KHONG
 * localStorage, DOMAIN_RULES §15).
 *
 * `ready` chi true khi Host tra ve field nay. Host cu (chua restart sau khi them
 * field) khong tra `seenGuides`: coi nhu chua biet va KHONG hien huong dan, vi luu
 * xuong cung khong giu duoc -> huong dan se bat lai moi lan mo.
 */
export function useGuides() {
  const queryClient = useQueryClient();
  const settings = useQuery({
    queryKey: ["settings"],
    queryFn: settingsApi.get,
    staleTime: Infinity,
    retry: 1,
  });
  const seen = settings.data?.seenGuides;

  const { mutate: save } = useMutation({
    mutationFn: async (seenGuides: GuideId[]) => {
      const current = queryClient.getQueryData<Settings>(["settings"]) ?? await settingsApi.get();
      return settingsApi.update({ ...current, seenGuides });
    },
    /* Cap nhat cache truoc: dong huong dan la phai bien mat ngay, khong cho mang. */
    onMutate: (seenGuides) => {
      queryClient.setQueryData<Settings>(["settings"], (current) => current ? { ...current, seenGuides } : current);
    },
    onSuccess: (saved) => queryClient.setQueryData(["settings"], saved),
    onError: (error) => toast.error(error instanceof Error ? error.message : String(error)),
  });

  const isPending = useCallback((id: GuideId) => seen !== undefined && !seen.includes(id), [seen]);

  const markSeen = useCallback((id: GuideId) => {
    const current = queryClient.getQueryData<Settings>(["settings"])?.seenGuides;
    if (!current || current.includes(id)) return;
    save([...current, id]);
  }, [queryClient, save]);

  const resetAll = useCallback(() => save([]), [save]);

  return { ready: seen !== undefined, isPending, markSeen, resetAll };
}
