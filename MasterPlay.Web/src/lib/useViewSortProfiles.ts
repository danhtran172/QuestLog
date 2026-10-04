import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { SortRule, ViewSortProfile, ViewSortProfiles } from "./api/domain";
import { api } from "./api/client";

export type ConfigurableView = "all" | "planning" | "finished";

const QUERY_KEY = ["view-sort-profiles"] as const;

function replaceProfile(
  current: ViewSortProfiles | undefined,
  profile: ViewSortProfile,
): ViewSortProfiles {
  const profiles = current?.profiles ?? [];
  return {
    profiles: profiles.some((item) => item.view === profile.view)
      ? profiles.map((item) => (item.view === profile.view ? profile : item))
      : [...profiles, profile],
  };
}

export function useViewSortProfiles() {
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: QUERY_KEY,
    queryFn: api.viewSortProfiles,
    staleTime: Infinity,
  });

  const saveMutation = useMutation({
    mutationFn: ({ view, rules }: { view: ConfigurableView; rules: SortRule[] }) =>
      api.updateViewSortProfile(view, rules),
    onSuccess: (profile) => {
      queryClient.setQueryData<ViewSortProfiles>(QUERY_KEY, (current) =>
        replaceProfile(current, profile),
      );
      void queryClient.invalidateQueries({ queryKey: ["games"] });
    },
  });

  const resetMutation = useMutation({
    mutationFn: (view: ConfigurableView) => api.resetViewSortProfile(view),
    onSuccess: (profile) => {
      queryClient.setQueryData<ViewSortProfiles>(QUERY_KEY, (current) =>
        replaceProfile(current, profile),
      );
      void queryClient.invalidateQueries({ queryKey: ["games"] });
    },
  });

  return {
    ...query,
    save: (view: ConfigurableView, rules: SortRule[]) =>
      saveMutation.mutateAsync({ view, rules }),
    reset: (view: ConfigurableView) => resetMutation.mutateAsync(view),
    isSaving: saveMutation.isPending || resetMutation.isPending,
  };
}
