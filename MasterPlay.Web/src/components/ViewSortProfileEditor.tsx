import { useEffect, useMemo, useState, type DragEvent, type KeyboardEvent } from "react";
import {
  AlertTriangle,
  ArrowDown,
  ArrowUp,
  GripVertical,
  Loader2,
  Plus,
  RotateCcw,
  Trash2,
} from "lucide-react";
import {
  PROFILE_SORT_KEYS,
  type SortRule,
  type ViewSortProfile,
} from "@/lib/api/domain";
import type { ConfigurableView } from "@/lib/useViewSortProfiles";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { IconButton, PopoverContent, Tooltip } from "./ui";

type Props = {
  view: ConfigurableView;
  profile: ViewSortProfile | undefined;
  isLoading: boolean;
  loadError: unknown;
  isSaving: boolean;
  toolbarOverride: boolean;
  onSave: (view: ConfigurableView, rules: SortRule[]) => Promise<ViewSortProfile>;
  onReset: (view: ConfigurableView) => Promise<ViewSortProfile>;
};

function messageOf(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

export function ViewSortProfileEditor({
  view,
  profile,
  isLoading,
  loadError,
  isSaving,
  toolbarOverride,
  onSave,
  onReset,
}: Props) {
  const { t } = useI18n();
  const [rules, setRules] = useState<SortRule[]>(profile?.rules ?? []);
  const [adding, setAdding] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dragIndex, setDragIndex] = useState<number | null>(null);

  useEffect(() => {
    setRules(profile?.rules ?? []);
    setError(null);
    setAdding(false);
    setDragIndex(null);
  }, [profile, view]);

  const availableKeys = useMemo(
    () => PROFILE_SORT_KEYS.filter((key) => !rules.some((rule) => rule.key === key)),
    [rules],
  );

  const commit = async (next: SortRule[]) => {
    const previous = rules;
    setRules(next);
    setAdding(false);
    setError(null);
    try {
      const saved = await onSave(view, next);
      setRules(saved.rules);
    } catch (saveError) {
      setRules(previous);
      setError(messageOf(saveError));
    }
  };

  const move = (from: number, to: number) => {
    if (from === to || to < 0 || to >= rules.length || isSaving) return;
    const next = [...rules];
    const [moved] = next.splice(from, 1);
    if (!moved) return;
    next.splice(to, 0, moved);
    void commit(next);
  };

  const handleDrop = (event: DragEvent<HTMLDivElement>, to: number) => {
    event.preventDefault();
    if (dragIndex != null) move(dragIndex, to);
    setDragIndex(null);
  };

  const handleGripKey = (event: KeyboardEvent<HTMLButtonElement>, index: number) => {
    if (event.key === "ArrowUp") {
      event.preventDefault();
      move(index, index - 1);
    } else if (event.key === "ArrowDown") {
      event.preventDefault();
      move(index, index + 1);
    }
  };

  const reset = async () => {
    const previous = rules;
    setError(null);
    try {
      const restored = await onReset(view);
      setRules(restored.rules);
      setAdding(false);
    } catch (resetError) {
      setRules(previous);
      setError(messageOf(resetError));
    }
  };

  return (
    <PopoverContent align="start" className="w-80 p-2">
      <div className="flex items-start justify-between gap-3 px-1.5 pb-2 pt-1">
        <div className="min-w-0">
          <p className="text-xs font-black uppercase tracking-[0.12em] text-fg">
            {t("sortProfile.title")}
          </p>
          <p className="truncate text-fs-11 text-muted-fg">{t(`nav.${view}`)}</p>
        </div>
        {isSaving ? <Loader2 size={15} className="mt-1 animate-spin text-primary" /> : null}
      </div>

      {toolbarOverride ? (
        <div className="mb-2 rounded-lg border border-warning/25 bg-warning/10 px-2.5 py-2 text-fs-11 font-semibold text-warning">
          {t("sortProfile.overrideActive")}
        </div>
      ) : null}

      {loadError || error ? (
        <div className="mb-2 flex gap-2 rounded-lg border border-danger/35 bg-danger/10 p-2 text-fs-11 text-danger">
          <AlertTriangle size={14} className="mt-0.5 shrink-0" />
          <span>{error ?? messageOf(loadError)}</span>
        </div>
      ) : null}

      {isLoading ? (
        <div className="grid min-h-28 place-items-center text-muted-fg">
          <Loader2 className="animate-spin" size={18} />
        </div>
      ) : (
        <>
          <div className="max-h-64 space-y-1 overflow-y-auto pr-0.5">
            {rules.map((rule, index) => (
              <div
                key={rule.key}
                onDragOver={(event) => event.preventDefault()}
                onDrop={(event) => handleDrop(event, index)}
                className={cn(
                  "flex items-center gap-1.5 rounded-lg border border-border bg-surface-2/45 p-1.5 transition",
                  dragIndex === index && "opacity-50",
                )}
              >
                <Tooltip label={t("sortProfile.drag")}>
                  <button
                    type="button"
                    draggable={!isSaving}
                    disabled={isSaving}
                    aria-label={t("sortProfile.drag")}
                    onDragStart={(event) => {
                      event.dataTransfer.effectAllowed = "move";
                      setDragIndex(index);
                    }}
                    onDragEnd={() => setDragIndex(null)}
                    onKeyDown={(event) => handleGripKey(event, index)}
                    className="grid h-7 w-6 shrink-0 cursor-grab place-items-center rounded-md text-muted-fg outline-none hover:bg-surface focus-visible:ring-2 focus-visible:ring-primary active:cursor-grabbing disabled:cursor-not-allowed"
                  >
                    <GripVertical size={14} />
                  </button>
                </Tooltip>
                <span className="min-w-0 flex-1 truncate text-xs font-bold">
                  {t(`sort.${rule.key}`)}
                </span>
                <IconButton
                  size="compact"
                  disabled={isSaving}
                  label={rule.direction === "asc" ? t("sort.asc") : t("sort.desc")}
                  tone="edit"
                  icon={rule.direction === "asc" ? <ArrowUp /> : <ArrowDown />}
                  onClick={() =>
                    void commit(
                      rules.map((item, itemIndex) =>
                        itemIndex === index
                          ? { ...item, direction: item.direction === "asc" ? "desc" : "asc" }
                          : item,
                      ),
                    )
                  }
                />
                <IconButton
                  size="compact"
                  tone="danger"
                  disabled={isSaving}
                  label={t("action.delete")}
                  icon={<Trash2 />}
                  onClick={() => void commit(rules.filter((_, itemIndex) => itemIndex !== index))}
                />
              </div>
            ))}
          </div>

          {rules.length === 0 ? (
            <p className="rounded-lg border border-dashed border-border px-3 py-4 text-center text-xs text-muted-fg">
              {t("sortProfile.empty")}
            </p>
          ) : null}

          {adding ? (
            <div className="mt-2 max-h-44 space-y-0.5 overflow-y-auto rounded-lg border border-border bg-surface p-1">
              {availableKeys.map((key) => (
                <button
                  key={key}
                  type="button"
                  disabled={isSaving}
                  onClick={() => void commit([...rules, { key, direction: "asc" }])}
                  className="flex w-full items-center rounded-md px-2.5 py-1.5 text-left text-xs font-semibold text-muted-fg outline-none hover:bg-surface-2 hover:text-fg focus-visible:ring-2 focus-visible:ring-primary"
                >
                  {t(`sort.${key}`)}
                </button>
              ))}
              {availableKeys.length === 0 ? (
                <p className="px-2.5 py-2 text-xs text-muted-fg">{t("sortProfile.noMore")}</p>
              ) : null}
            </div>
          ) : null}

          <div className="mt-2 flex items-center justify-between border-t border-border px-1 pt-2">
            <IconButton
              size="compact"
              tone="add"
              disabled={isSaving || availableKeys.length === 0}
              label={t("sortProfile.add")}
              icon={<Plus />}
              onClick={() => setAdding((current) => !current)}
            />
            <IconButton
              size="compact"
              disabled={isSaving || profile?.customized === false}
              label={t("sortProfile.reset")}
              icon={<RotateCcw />}
              onClick={() => void reset()}
            />
          </div>
        </>
      )}
    </PopoverContent>
  );
}
