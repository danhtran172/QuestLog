import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { DndContext, KeyboardSensor, PointerSensor, closestCenter, useSensor, useSensors, type DragEndEvent } from "@dnd-kit/core";
import { SortableContext, arrayMove, sortableKeyboardCoordinates, useSortable, verticalListSortingStrategy } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { GripVertical, Loader2, Pencil, Plus } from "lucide-react";
import type { TaxonomyItem } from "@/lib/api/domain";
import { taxonomyApi } from "@/lib/taxonomyApi";
import { useI18n } from "@/lib/i18n";
import { cn, iconSrc } from "@/lib/utils";
import { IconButton } from "./ui";
import { ThemeGroupBoard } from "./themes/ThemeGroupBoard";
import { ThemeItemEditDialog } from "./themes/ThemeItemEditDialog";

type Tab = "themes" | "genres";

/**
 * Admin Console — Taxonomy chung (`system:shared`): nhom theme + theme + genre,
 * kem icon. Spec 2026-10-04-theme-groups §6.2. Moi endpoint deu RequireAdmin.
 */
export function AdminTaxonomySection() {
  const { t } = useI18n();
  const [tab, setTab] = useState<Tab>("themes");

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-1">
        {(["themes", "genres"] as const).map((key) => (
          <button
            key={key}
            type="button"
            onClick={() => setTab(key)}
            aria-pressed={tab === key}
            className={cn(
              "rounded-lg px-3 py-1.5 text-xs font-semibold transition-colors",
              tab === key ? "glass-surface bg-primary/40 text-fg" : "text-muted-fg hover:bg-surface-2/70",
            )}
          >
            {t(`admin.taxonomy.${key}`)}
          </button>
        ))}
      </div>
      {tab === "themes" ? <ThemeGroupBoard scope="admin" /> : <AdminGenreList />}
    </div>
  );
}

function AdminGenreList() {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const genres = useQuery({
    queryKey: ["taxonomy", "admin", "genres"],
    queryFn: () => taxonomyApi.list("admin", "genres"),
  });
  const [items, setItems] = useState<TaxonomyItem[]>([]);
  const [editing, setEditing] = useState<TaxonomyItem | "new" | null>(null);

  useEffect(() => {
    if (genres.data) setItems(genres.data);
  }, [genres.data]);

  const reorder = useMutation({
    mutationFn: (ids: number[]) => taxonomyApi.orderGenres(ids),
    onError: (error) => {
      if (genres.data) setItems(genres.data);
      toast.error(error instanceof Error ? error.message : String(error));
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["taxonomy"] }),
  });

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  const onDragEnd = ({ active, over }: DragEndEvent) => {
    if (!over || active.id === over.id) return;
    const from = items.findIndex((item) => item.id === active.id);
    const to = items.findIndex((item) => item.id === over.id);
    const next = arrayMove(items, from, to);
    setItems(next);
    reorder.mutate(next.map((item) => item.id));
  };

  if (genres.isPending) return <Loader2 size={18} className="mx-auto my-10 animate-spin text-muted-fg" />;
  if (genres.error) {
    return (
      <p role="alert" className="rounded-lg bg-danger/10 p-3 text-sm text-danger">
        {genres.error instanceof Error ? genres.error.message : String(genres.error)}
      </p>
    );
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2">
        <p className="min-w-0 flex-1 text-xs text-muted-fg">{t("admin.taxonomy.genresHint")}</p>
        {reorder.isPending ? <Loader2 size={14} className="animate-spin text-muted-fg" /> : null}
        <button
          type="button"
          className="flex items-center gap-1.5 rounded-lg border border-border px-2.5 py-1.5 text-xs font-semibold hover:bg-surface-2/60"
          onClick={() => setEditing("new")}
        >
          <Plus size={13} /> {t("action.add")}
        </button>
      </div>
      <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd}>
        <SortableContext items={items.map((item) => item.id)} strategy={verticalListSortingStrategy}>
          <div className="grid gap-1.5 md:grid-cols-2">
            {items.map((item) => (
              <GenreRow key={item.id} item={item} editLabel={t("action.edit")} onEdit={() => setEditing(item)} />
            ))}
          </div>
        </SortableContext>
      </DndContext>
      <ThemeItemEditDialog
        scope="admin"
        kind="genres"
        item={editing === "new" ? null : editing}
        open={editing !== null}
        onOpenChange={(next) => { if (!next) setEditing(null); }}
      />
    </div>
  );
}

function GenreRow({ item, editLabel, onEdit }: { item: TaxonomyItem; editLabel: string; onEdit: () => void }) {
  const sortable = useSortable({ id: item.id });
  const src = iconSrc(item.iconData);
  return (
    <div
      ref={sortable.setNodeRef}
      style={{ transform: CSS.Translate.toString(sortable.transform), transition: sortable.transition }}
      className={cn("surface-card flex items-center gap-2 px-3 py-2", sortable.isDragging && "opacity-60")}
    >
      <button type="button" aria-label={item.name} className="cursor-grab touch-none text-muted-fg hover:text-fg" {...sortable.attributes} {...sortable.listeners}>
        <GripVertical size={14} />
      </button>
      {src ? <img src={src} alt="" className="h-5 w-5 object-contain" /> : <span className="h-5 w-5" />}
      <span className="min-w-0 flex-1 truncate text-sm font-semibold">{item.name}</span>
      <IconButton size="mini" tone="edit" label={editLabel} icon={<Pencil />} onClick={onEdit} />
    </div>
  );
}
