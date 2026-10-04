import { Check, CircleDot, Loader2 } from "lucide-react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import type { CatalogItem, GameDetail } from "@/lib/api/domain";
import { api } from "@/lib/api/client";
import { gameDetailApi } from "@/lib/gameDetailApi";
import { gameUserDataPatch } from "@/lib/gameUserData";
import { useI18n } from "@/lib/i18n";
import { cn, iconSrc } from "@/lib/utils";
import { CatalogPill, EmptyCatalogPill, Pill } from "./Pills";
import { Popover, PopoverContent, PopoverTrigger } from "./ui";

type CatalogKind = "progress" | "ownership";

export function GameDetailCatalogPicker({
  game,
  kind,
  onSaved,
}: {
  game: GameDetail;
  kind: CatalogKind;
  onSaved: (game: GameDetail) => void;
}) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const label = t(kind === "progress" ? "sort.progress" : "sort.ownership");
  const current = game[kind];
  const catalog = useQuery({
    queryKey: ["catalog", kind],
    queryFn: () => api.catalog(kind),
    enabled: open,
  });
  const save = useMutation({
    mutationFn: (item: CatalogItem | null) => gameDetailApi.updateUserData(
      game.id,
      gameUserDataPatch(game, kind === "progress"
        ? { progressId: item?.id ?? null }
        : { ownershipId: item?.id ?? null }),
    ),
    onSuccess: async (saved) => {
      queryClient.setQueryData(["game-detail", saved.id], saved);
      setOpen(false);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["games"] }),
        queryClient.invalidateQueries({ queryKey: ["play-queue"] }),
        queryClient.invalidateQueries({ queryKey: ["dashboard-stats"] }),
      ]);
      onSaved(saved);
    },
    onError: (error) => toast.error(error instanceof Error ? error.message : String(error)),
  });

  const option = (item: CatalogItem) => {
    const selected = current?.id === item.id;

    return (
      <button
        key={item.id}
        type="button"
        role="option"
        aria-selected={selected}
        aria-label={item.displayName}
        disabled={save.isPending}
        className={cn(
          "group inline-flex rounded-full p-[2px] outline-none",
          "focus-visible:ring-2 focus-visible:ring-primary/60",
          "disabled:cursor-wait disabled:opacity-60",
        )}
        onClick={() => {
          if (selected) {
            setOpen(false);
            return;
          }
          save.mutate(item);
        }}
      >
        <Pill
          className="whitespace-nowrap border-border bg-surface-2 transition-[background-color,filter] duration-150 group-hover:bg-primary/10 group-hover:brightness-110"
          style={{ color: item.colorCode, borderColor: `${item.colorCode}55` }}
        >
          {iconSrc(item.iconData) ? (
            <img src={iconSrc(item.iconData)} alt="" className="h-3 w-3 shrink-0 object-contain" />
          ) : (
            <CircleDot aria-hidden size={12} className="shrink-0" />
          )}
          {item.displayName}
          {selected ? <Check aria-hidden size={11} className="shrink-0" /> : null}
        </Pill>
      </button>
    );
  };

  return (
    <Popover open={open} onOpenChange={(next) => !save.isPending && setOpen(next)}>
      <PopoverTrigger asChild>
        <button
          type="button"
          aria-label={`${t("action.edit")} ${label}`}
          disabled={save.isPending}
          className={cn(
            "relative inline-flex rounded-full outline-none transition-opacity",
            "hover:opacity-80 focus-visible:ring-2 focus-visible:ring-primary/60 focus-visible:ring-offset-2 focus-visible:ring-offset-surface",
            "disabled:cursor-wait",
          )}
        >
          {current ? <CatalogPill item={current} /> : <EmptyCatalogPill />}
          {save.isPending ? (
            <span className="absolute inset-0 grid place-items-center rounded-full bg-surface/75">
              <Loader2 aria-hidden size={12} className="animate-spin text-primary" />
            </span>
          ) : null}
        </button>
      </PopoverTrigger>
      <PopoverContent
        align="start"
        className="w-fit max-w-[calc(100vw-16px)] rounded-xl border border-border bg-surface p-1.5 shadow-lg"
      >
        {catalog.isPending ? (
          <div className="flex min-h-12 items-center justify-center text-muted-fg">
            <Loader2 aria-hidden size={15} className="animate-spin" />
          </div>
        ) : catalog.isError ? (
          <p role="alert" className="px-1 py-2 text-xs text-danger">
            {catalog.error instanceof Error ? catalog.error.message : String(catalog.error)}
          </p>
        ) : (
          <div role="listbox" aria-label={label} className="flex flex-col items-start gap-1">
            {catalog.data?.map((item) => option(item))}
          </div>
        )}
      </PopoverContent>
    </Popover>
  );
}
