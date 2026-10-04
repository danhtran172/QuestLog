import { useQuery } from "@tanstack/react-query";
import { gameDetailApi } from "./gameDetailApi";

export function useGameDetail(id: string | null, enabled: boolean) {
  return useQuery({
    queryKey: ["game-detail", id],
    queryFn: () => gameDetailApi.get(id!),
    enabled: enabled && id != null,
    staleTime: 30_000,
  });
}
