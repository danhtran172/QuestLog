import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { queueApi } from "./queueApi";

const PLAY_QUEUE_QUERY_KEY = ["play-queue"] as const;

export function usePlayQueue(enabled: boolean) {
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: PLAY_QUEUE_QUERY_KEY,
    queryFn: queueApi.get,
    enabled,
    staleTime: 30_000,
  });

  const reorder = useMutation({
    mutationFn: queueApi.reorder,
    onSuccess: (queue) => {
      queryClient.setQueryData(PLAY_QUEUE_QUERY_KEY, queue);
      void queryClient.invalidateQueries({ queryKey: ["games"] });
    },
  });

  return {
    ...query,
    reorder: reorder.mutateAsync,
    isSaving: reorder.isPending,
  };
}
