import { useEffect, useMemo, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  DndContext,
  DragOverlay,
  KeyboardSensor,
  PointerSensor,
  closestCorners,
  useDroppable,
  useSensor,
  useSensors,
  type Announcements,
  type DragEndEvent,
  type DragOverEvent,
  type DragStartEvent,
} from "@dnd-kit/core";
import {
  SortableContext,
  rectSortingStrategy,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { GripVertical, Loader2, Lock, Pencil, Plus } from "lucide-react";
import type { TaxonomyItem, ThemeGroup, ThemeGroupBucket } from "@/lib/api/domain";
import { groupedThemesKey, taxonomyApi, type TaxonomyScope } from "@/lib/taxonomyApi";
import { bucketKey, findBucketKey, moveGroup, moveTheme, orderRequest } from "@/lib/themeBoard";
import { useI18n } from "@/lib/i18n";
import { cn, iconSrc } from "@/lib/utils";
import { IconButton } from "../ui";
import { ThemeGroupEditDialog } from "./ThemeGroupEditDialog";
import { ThemeItemEditDialog } from "./ThemeItemEditDialog";

const themeDragId = (id: number) => `t:${id}`;
const groupDragId = (id: number) => `grp:${id}`;
const parseDragId = (value: string | number) => {
  const [kind, raw] = String(value).split(":");
  return { kind, id: kind === "drop" ? raw : Number(raw) } as
    | { kind: "t" | "grp"; id: number }
    | { kind: "drop"; id: string };
};

/**
 * Bang nhom theme co keo tha — spec 2026-10-04-theme-groups §6.1.
 *
 * Dung cho ca ManageTagsDialog (scope "library") va Admin Console ("admin").
 * Cai gi keo duoc, sua duoc lay tu `editable` server tra; client khong suy ra.
 * Thu tu ban dau la thu tu server tra; client chi ap thao tac vua keo, gui mot
 * request, va lam moi tu server — loi thi quay lai trang thai cu + bao loi.
 */
export function ThemeGroupBoard({ scope }: { scope: TaxonomyScope }) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const grouped = useQuery({
    queryKey: groupedThemesKey(scope),
    queryFn: () => taxonomyApi.groupedThemes(scope),
  });

  const [board, setBoard] = useState<ThemeGroupBucket[]>([]);
  const snapshot = useRef<ThemeGroupBucket[]>([]);
  const [activeTheme, setActiveTheme] = useState<TaxonomyItem | null>(null);
  const [editingGroup, setEditingGroup] = useState<ThemeGroup | "new" | null>(null);
  const [editingTheme, setEditingTheme] = useState<TaxonomyItem | "new" | null>(null);

  /* Dong bo voi server khi KHONG dang keo. */
  useEffect(() => {
    if (grouped.data && !activeTheme) setBoard(grouped.data.groups);
  }, [grouped.data, activeTheme]);

  const refresh = () => queryClient.invalidateQueries({ queryKey: ["taxonomy"] });
  const fail = (error: unknown) => toast.error(error instanceof Error ? error.message : String(error));

  const saveThemeOrder = useMutation({
    mutationFn: (request: { groupId: number | null; ids: number[] }) => taxonomyApi.orderThemes(scope, request),
    onError: (error) => {
      setBoard(snapshot.current);
      fail(error);
    },
    onSettled: refresh,
  });

  const saveGroupOrder = useMutation({
    mutationFn: (ids: number[]) => taxonomyApi.orderGroups(scope, ids),
    onError: (error) => {
      setBoard(snapshot.current);
      fail(error);
    },
    onSettled: refresh,
  });

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  const groupLabel = (key: string | null) => {
    const bucket = board.find((candidate) => bucketKey(candidate) === key);
    return bucket?.group?.displayName ?? t("themes.ungrouped");
  };
  const themeName = (dragId: string | number) => {
    const parsed = parseDragId(dragId);
    if (parsed.kind === "t") return board.flatMap((b) => b.themes).find((x) => x.id === parsed.id)?.name ?? "";
    if (parsed.kind === "grp") return board.find((b) => b.group?.id === parsed.id)?.group?.displayName ?? "";
    return "";
  };
  const overGroupKey = (overId: string | number) => {
    const parsed = parseDragId(overId);
    if (parsed.kind === "drop") return parsed.id;
    if (parsed.kind === "t") return findBucketKey(board, parsed.id);
    // grp:0 la the "chua phan nhom" (khong co nhom that).
    if (parsed.kind === "grp") return parsed.id === 0 ? "none" : `g${parsed.id}`;
    return null;
  };

  const announcements: Announcements = {
    onDragStart: ({ active }) => t("themes.dnd.pickedUp").replace("{name}", themeName(active.id)),
    onDragOver: ({ active, over }) =>
      over ? t("themes.dnd.over").replace("{name}", themeName(active.id)).replace("{group}", groupLabel(overGroupKey(over.id))) : undefined,
    onDragEnd: ({ active, over }) =>
      over ? t("themes.dnd.dropped").replace("{name}", themeName(active.id)).replace("{group}", groupLabel(overGroupKey(over.id))) : undefined,
    onDragCancel: ({ active }) => t("themes.dnd.cancelled").replace("{name}", themeName(active.id)),
  };

  const onDragStart = ({ active }: DragStartEvent) => {
    snapshot.current = board;
    const parsed = parseDragId(active.id);
    if (parsed.kind === "t") setActiveTheme(board.flatMap((b) => b.themes).find((x) => x.id === parsed.id) ?? null);
  };

  /* Keo theme qua nhom khac: chuyen ngay trong state de nhom dich mo cho. */
  const onDragOver = ({ active, over }: DragOverEvent) => {
    const parsed = parseDragId(active.id);
    if (parsed.kind !== "t" || !over) return;
    const toKey = overGroupKey(over.id);
    const fromKey = findBucketKey(board, parsed.id);
    if (!toKey || toKey === fromKey) return;
    const overParsed = parseDragId(over.id);
    setBoard((current) => moveTheme(current, parsed.id, toKey, overParsed.kind === "t" ? overParsed.id : null));
  };

  const onDragEnd = ({ active, over }: DragEndEvent) => {
    const parsed = parseDragId(active.id);
    setActiveTheme(null);
    if (!over) {
      setBoard(snapshot.current);
      return;
    }

    if (parsed.kind === "grp") {
      const overParsed = parseDragId(over.id);
      const overGroupId = overParsed.kind === "grp" ? overParsed.id : null;
      if (overGroupId === null) return;
      const groups = board.filter((b) => b.group).map((b) => b.group!);
      const reordered = moveGroup(groups, parsed.id, overGroupId);
      if (reordered === groups) return;
      const order = new Map(reordered.map((group, index) => [group.id, index]));
      setBoard((current) =>
        [...current].sort((a, b) =>
          (a.group ? order.get(a.group.id)! : Number.MAX_SAFE_INTEGER) -
          (b.group ? order.get(b.group.id)! : Number.MAX_SAFE_INTEGER)));
      saveGroupOrder.mutate(reordered.filter((group) => group.editable).map((group) => group.id));
      return;
    }

    if (parsed.kind !== "t") return;
    const toKey = overGroupKey(over.id);
    if (!toKey) return;
    const overParsed = parseDragId(over.id);
    const next = moveTheme(board, parsed.id, toKey, overParsed.kind === "t" && overParsed.id !== parsed.id ? overParsed.id : null);
    /* Tha vao chinh vi tri cu (hoac len theme khong sua duoc) thi khong goi API. */
    const before = orderRequest(snapshot.current, toKey);
    const after = orderRequest(next, toKey);
    setBoard(next);
    if (before.ids.join(",") !== after.ids.join(",") || findBucketKey(snapshot.current, parsed.id) !== toKey) {
      saveThemeOrder.mutate(after);
    }
  };

  const groupIds = useMemo(
    () => board.filter((b) => b.group).map((b) => groupDragId(b.group!.id)),
    [board],
  );
  const sharedBuckets = board.filter((b) => b.group?.scope === "shared");
  const personalBuckets = board.filter((b) => b.group?.scope === "personal");
  const ungrouped = board.find((b) => !b.group);
  const busy = saveThemeOrder.isPending || saveGroupOrder.isPending;

  if (grouped.isPending) return <Loader2 size={18} className="mx-auto my-10 animate-spin text-muted-fg" />;
  if (grouped.error) {
    return (
      <p role="alert" className="rounded-lg bg-danger/10 p-3 text-sm text-danger">
        {grouped.error instanceof Error ? grouped.error.message : String(grouped.error)}
      </p>
    );
  }

  const renderBucket = (bucket: ThemeGroupBucket) => (
    <BucketCard
      key={bucketKey(bucket)}
      bucket={bucket}
      label={bucket.group?.displayName ?? t("themes.ungrouped")}
      emptyLabel={t("themes.emptyGroup")}
      editLabel={t("themes.editGroup")}
      onEditGroup={bucket.group?.editable ? () => setEditingGroup(bucket.group!) : undefined}
      onEditTheme={(item) => setEditingTheme(item)}
    />
  );

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        <p className="min-w-0 flex-1 text-xs text-muted-fg">{t("themes.dragHint")}</p>
        {busy ? <Loader2 size={14} className="animate-spin text-muted-fg" /> : null}
        <button
          type="button"
          className="flex items-center gap-1.5 rounded-lg border border-border px-2.5 py-1.5 text-xs font-semibold hover:bg-surface-2/60"
          onClick={() => setEditingGroup("new")}
        >
          <Plus size={13} /> {t("themes.addGroup")}
        </button>
        <button
          type="button"
          className="flex items-center gap-1.5 rounded-lg border border-border px-2.5 py-1.5 text-xs font-semibold hover:bg-surface-2/60"
          onClick={() => setEditingTheme("new")}
        >
          <Plus size={13} /> {t("themes.addTheme")}
        </button>
      </div>

      <DndContext
        sensors={sensors}
        collisionDetection={closestCorners}
        accessibility={{ announcements }}
        onDragStart={onDragStart}
        onDragOver={onDragOver}
        onDragEnd={onDragEnd}
        onDragCancel={() => {
          setActiveTheme(null);
          setBoard(snapshot.current);
        }}
      >
        <SortableContext items={groupIds} strategy={verticalListSortingStrategy}>
          {scope === "library" && sharedBuckets.length ? (
            <section className="space-y-2">
              <h3 className="flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-[0.12em] text-muted-fg">
                <Lock size={12} /> {t("themes.sharedGroups")}
                <span className="font-medium normal-case tracking-normal">— {t("themes.sharedLocked")}</span>
              </h3>
              <div className="grid gap-3 md:grid-cols-2">{sharedBuckets.map(renderBucket)}</div>
            </section>
          ) : null}

          {scope === "admin" ? (
            <div className="grid gap-3 md:grid-cols-2">{sharedBuckets.map(renderBucket)}</div>
          ) : (
            <section className="space-y-2">
              <h3 className="text-[11px] font-bold uppercase tracking-[0.12em] text-muted-fg">{t("themes.myGroups")}</h3>
              <div className="grid gap-3 md:grid-cols-2">
                {personalBuckets.map(renderBucket)}
                {ungrouped ? renderBucket(ungrouped) : null}
              </div>
            </section>
          )}

          {scope === "admin" && ungrouped ? (
            <div className="grid gap-3 md:grid-cols-2">{renderBucket(ungrouped)}</div>
          ) : null}
        </SortableContext>

        <DragOverlay>{activeTheme ? <ThemeChipView item={activeTheme} dragging /> : null}</DragOverlay>
      </DndContext>

      <ThemeGroupEditDialog
        scope={scope}
        group={editingGroup === "new" ? null : editingGroup}
        open={editingGroup !== null}
        onOpenChange={(next) => { if (!next) setEditingGroup(null); }}
      />
      <ThemeItemEditDialog
        scope={scope}
        item={editingTheme === "new" ? null : editingTheme}
        open={editingTheme !== null}
        onOpenChange={(next) => { if (!next) setEditingTheme(null); }}
      />
    </div>
  );
}

function BucketCard({
  bucket,
  label,
  emptyLabel,
  editLabel,
  onEditGroup,
  onEditTheme,
}: {
  bucket: ThemeGroupBucket;
  label: string;
  emptyLabel: string;
  editLabel: string;
  onEditGroup?: () => void;
  onEditTheme: (item: TaxonomyItem) => void;
}) {
  const key = bucketKey(bucket);
  const { setNodeRef: setDropRef, isOver } = useDroppable({ id: `drop:${key}` });
  const sortableGroup = useSortable({
    id: groupDragId(bucket.group?.id ?? 0),
    disabled: !bucket.group?.editable,
  });

  return (
    <div
      ref={sortableGroup.setNodeRef}
      style={{ transform: CSS.Translate.toString(sortableGroup.transform), transition: sortableGroup.transition }}
      className={cn("surface-card rounded-xl p-3", sortableGroup.isDragging && "opacity-60")}
    >
      <div className="mb-2 flex items-center gap-1.5 text-sm font-bold">
        {bucket.group?.editable ? (
          <button
            type="button"
            aria-label={label}
            className="cursor-grab touch-none text-muted-fg hover:text-fg"
            {...sortableGroup.attributes}
            {...sortableGroup.listeners}
          >
            <GripVertical size={14} />
          </button>
        ) : bucket.group ? (
          <Lock size={13} className="text-muted-fg" />
        ) : null}
        <span className="min-w-0 flex-1 truncate">{label}</span>
        <span className="text-[11px] font-semibold text-muted-fg">{bucket.themes.length}</span>
        {onEditGroup ? (
          <IconButton size="mini" tone="edit" label={editLabel} icon={<Pencil />} onClick={onEditGroup} />
        ) : null}
      </div>

      <SortableContext items={bucket.themes.map((item) => themeDragId(item.id))} strategy={rectSortingStrategy}>
        <div
          ref={setDropRef}
          className={cn(
            "flex min-h-[44px] flex-wrap content-start gap-1.5 rounded-lg p-1 transition",
            isOver && "bg-primary/10 ring-1 ring-primary/50",
          )}
        >
          {bucket.themes.map((item) => (
            <SortableThemeChip key={item.id} item={item} onEdit={() => onEditTheme(item)} />
          ))}
          {bucket.themes.length === 0 ? (
            <span className="m-auto text-xs text-muted-fg/80">{emptyLabel}</span>
          ) : null}
        </div>
      </SortableContext>
    </div>
  );
}

function SortableThemeChip({ item, onEdit }: { item: TaxonomyItem; onEdit: () => void }) {
  const editable = Boolean(item.editable);
  const sortable = useSortable({ id: themeDragId(item.id), disabled: !editable });

  return (
    <span
      ref={sortable.setNodeRef}
      style={{ transform: CSS.Translate.toString(sortable.transform), transition: sortable.transition }}
      className={cn(sortable.isDragging && "opacity-40")}
    >
      <ThemeChipView
        item={item}
        dragHandleProps={editable ? { ...sortable.attributes, ...sortable.listeners } : undefined}
        onEdit={editable ? onEdit : undefined}
      />
    </span>
  );
}

function ThemeChipView({
  item,
  dragging = false,
  dragHandleProps,
  onEdit,
}: {
  item: TaxonomyItem;
  dragging?: boolean;
  dragHandleProps?: Record<string, unknown>;
  onEdit?: () => void;
}) {
  const editable = Boolean(item.editable);
  const src = iconSrc(item.iconData);
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 rounded-lg border px-2 py-1 text-xs font-semibold",
        editable ? "border-primary/60 bg-primary/10 text-fg" : "border-border/70 bg-surface-2/60 text-fg/90",
        dragging && "shadow-lg ring-2 ring-primary/60",
      )}
    >
      {editable ? (
        <span {...dragHandleProps} className="cursor-grab touch-none text-muted-fg hover:text-fg" aria-label={item.name} role="button">
          <GripVertical size={12} />
        </span>
      ) : (
        <Lock size={10} className="text-muted-fg/80" />
      )}
      {src ? <img src={src} alt="" className="h-4 w-4 object-contain" /> : null}
      {onEdit ? (
        <button type="button" className="hover:underline" onClick={onEdit}>
          {item.name}
        </button>
      ) : (
        <span>{item.name}</span>
      )}
    </span>
  );
}
