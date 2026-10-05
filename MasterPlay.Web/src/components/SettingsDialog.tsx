import { useEffect, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { FolderOpen, GraduationCap, HardDrive, Languages, Monitor, Moon, Palette, Plus, RotateCcw, Save, Sun, Trash2, TreePine, X } from "lucide-react";
import { toast } from "sonner";
import { cn } from "@/lib/utils";
import { api } from "@/lib/api/client";
import { useI18n, type Lang } from "@/lib/i18n";
import { usePersistLanguage } from "@/lib/languageSettings";
import { THEME_SELECTION_ENABLED, useTheme, type ThemeMode } from "@/lib/theme";
import { useJungle, type JungleFxLevel } from "@/jungle/JungleContext";
import { useGuides } from "@/lib/guides";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  Field,
  IconButton,
  TextInput,
} from "./ui";

/** Tieu de cho tung khoi trong trang Settings (gop chung mot trang cuon, khong tab). */
function SectionHeading({ icon: Icon, children }: { icon: typeof Sun; children: ReactNode }) {
  return (
    <h3 className="flex items-center gap-2 text-xs font-bold uppercase tracking-wide text-muted-fg">
      <Icon size={14} /> {children}
    </h3>
  );
}

/**
 * Settings — mot trang cuon duy nhat: giao dien, ngon ngu, luu tru, thu muc game.
 *
 * TODO(backend): GET/PUT /api/settings chua co. Cac o nhap hien luu trong state
 * de dung duoc luong UI; chua ghi xuong config.
 */
export function SettingsDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
}) {
  const { t, lang, setLang } = useI18n();
  const persistLanguage = usePersistLanguage();
  const { mode, setMode, persistMode, isLoading: isThemeLoading, isSaving: isThemeSaving, error: themeError } = useTheme();
  const jungle = useJungle();
  const guides = useGuides();
  const capabilities = useQuery({
    queryKey: ["capabilities"],
    queryFn: () => api.capabilities(),
    enabled: open,
    staleTime: Infinity,
  });
  // Backup dang tam tat o backend -> an o chon thu muc backup goc.
  const canBackup = capabilities.data?.features.backup === true;

  const [originalLang, setOriginalLang] = useState<Lang>(lang);
  const [originalMode, setOriginalMode] = useState<ThemeMode>(mode);
  const [libraries, setLibraries] = useState<string[]>([]);
  const [newLib, setNewLib] = useState("");
  const [saveError, setSaveError] = useState<string | null>(null);

  useEffect(() => {
    if (!open || isThemeLoading) return;
    setOriginalLang(lang);
    setOriginalMode(mode);
    setSaveError(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isThemeLoading, open]);

  // A24: Cancel phai khoi phuc CA ngon ngu LAN theme (WPF co, Lovable chi khoi phuc ngon ngu).
  const cancel = () => {
    setLang(originalLang);
    setMode(originalMode);
    setSaveError(null);
    onOpenChange(false);
  };

  const save = async () => {
    setSaveError(null);
    try {
      await persistMode(mode);
      // Tuan tu sau theme: ca hai PUT ca bo settings, chay song song thi cai sau ghi de cai truoc.
      if (lang !== originalLang) await persistLanguage(lang);
      onOpenChange(false);
    } catch (error) {
      setSaveError(error instanceof Error ? error.message : String(error));
    }
  };

  return (
    <Dialog open={open} onOpenChange={(o) => (o ? onOpenChange(true) : cancel())}>
      <DialogContent className="max-w-2xl gap-0 overflow-hidden p-0">
        <DialogHeader className="border-b border-border px-5 py-4">
          <DialogTitle>{t("settings.title")}</DialogTitle>
          <DialogDescription>QuestLog · {t("app.tagline")}</DialogDescription>
        </DialogHeader>

        <div className="scrollbar-thin max-h-[70vh] space-y-7 overflow-y-auto p-5">
          {THEME_SELECTION_ENABLED && (
            <section className="space-y-3">
              <SectionHeading icon={Palette}>{t("settings.theme")}</SectionHeading>
              <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
                {(
                  [
                    ["light", Sun],
                    ["dark", Moon],
                    ["system", Monitor],
                    ["jungle", TreePine],
                  ] as [ThemeMode, typeof Sun][]
                ).map(([m, Icon]) => (
                  <button
                    key={m}
                    onClick={() => setMode(m)}
                    disabled={isThemeLoading || isThemeSaving}
                    className={cn(
                      "surface-card flex flex-col items-center gap-2 p-4 transition-transform hover:-translate-y-0.5 disabled:cursor-wait disabled:opacity-60",
                      m === "jungle" && "theme-choice-jungle",
                      mode === m && "border-primary/60 ring-2 ring-primary/35",
                    )}
                  >
                    <Icon size={20} className={mode === m ? "text-primary" : "text-muted-fg"} />
                    <span className="text-sm font-semibold">{t(`settings.${m}`)}</span>
                  </button>
                ))}
              </div>

              {/* Chi hien khi dang o theme jungle — cac lua chon nay khong
                  co tac dung o theme khac, bay ra thi chi lam roi Settings. */}
              {mode === "jungle" ? (
                <div className="space-y-3 rounded-xl border border-border/70 bg-surface-2/40 p-3.5">
                  <div>
                    <p className="text-sm font-semibold">{t("settings.jungleFx")}</p>
                    <p className="mt-0.5 text-xs text-muted-fg">{t("settings.jungleFxHint")}</p>
                  </div>

                  <div className="grid grid-cols-4 gap-2">
                    {(["auto", "off", "medium", "full"] as JungleFxLevel[]).map((level) => (
                      <button
                        key={level}
                        onClick={() => void jungle.setLevel(level)}
                        className={cn(
                          "rounded-lg border border-border px-3 py-2 text-xs font-semibold transition-colors",
                          jungle.level === level
                            ? "border-primary/60 bg-primary/20 text-fg"
                            : "text-muted-fg hover:bg-surface-2/70 hover:text-fg",
                        )}
                      >
                        {t(`settings.jungleFx.${level}`)}
                      </button>
                    ))}
                  </div>

                  {/* Noi ro vi sao khong thay gi, va chi duong bat len.
                      Im lang o day chinh la thu khien nguoi dung tuong app hong. */}
                  {jungle.suppressedBySystem ? (
                    <div className="space-y-2 rounded-lg border border-warning/35 bg-warning/10 px-3 py-2">
                      <p className="text-xs text-warning">{t("settings.jungleReduced")}</p>
                      <button
                        onClick={() => void jungle.setLevel("medium")}
                        className="rounded-md border border-warning/45 px-2.5 py-1 text-xs font-semibold text-warning hover:bg-warning/15"
                      >
                        {t("settings.jungleReducedOverride")}
                      </button>
                    </div>
                  ) : null}
                </div>
              ) : null}
            </section>
          )}

          <section className="space-y-3">
            <SectionHeading icon={Languages}>{t("settings.language")}</SectionHeading>
            <div className="grid grid-cols-2 gap-3">
              {(
                [
                  ["en-US", "English"],
                  ["vi-VN", "Tiếng Việt"],
                ] as [Lang, string][]
              ).map(([code, native]) => (
                <button
                  key={code}
                  onClick={() => setLang(code)}
                  className={cn(
                    "surface-card p-4 text-left transition-transform hover:-translate-y-0.5",
                    lang === code && "border-primary/60 ring-2 ring-primary/35",
                  )}
                >
                  {/* Ten ban dia — giong WPF, hien ngay khi chon (preview) */}
                  <p className="text-sm font-bold">{native}</p>
                  <p className="text-xs text-muted-fg">{code}</p>
                </button>
              ))}
            </div>
          </section>

          <section className="space-y-3">
            <SectionHeading icon={HardDrive}>{t("settings.storage")}</SectionHeading>
            {canBackup && (
              <Field label={t("settings.backupRoot")}>
                <TextInput placeholder="D:\Backups\MasterPlay" />
              </Field>
            )}
            <Field label={t("settings.databasePath")}>
              <TextInput placeholder="%APPDATA%\MasterPlay\MasterPlay.db" />
            </Field>
            <p className="text-xs text-muted-fg">
              {t("settings.pickerHint")}
            </p>
          </section>

          <section className="space-y-3">
            <SectionHeading icon={FolderOpen}>{t("settings.libraries")}</SectionHeading>
            <div className="flex gap-2">
              <TextInput
                value={newLib}
                onChange={(e) => setNewLib(e.target.value)}
                placeholder="D:\Games"
              />
              <IconButton
                tone="add"
                wide
                label={t("action.add")}
                icon={<Plus />}
                onClick={() => {
                  if (!newLib.trim()) return;
                  setLibraries((l) => [...l, newLib.trim()]);
                  setNewLib("");
                }}
              />
            </div>

            {libraries.length === 0 ? (
              <p className="text-sm text-muted-fg">{t("settings.noLibraries")}</p>
            ) : (
              <ul className="space-y-2">
                {libraries.map((p, i) => (
                  <li key={`${p}-${i}`} className="surface-card flex items-center gap-3 p-3">
                    <FolderOpen size={15} className="text-muted-fg" />
                    <span className="flex-1 truncate text-sm">{p}</span>
                    <IconButton
                      tone="danger"
                      size="compact"
                      label={t("action.delete")}
                      icon={<Trash2 />}
                      onClick={() => setLibraries((l) => l.filter((_, j) => j !== i))}
                    />
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section className="space-y-3">
            <SectionHeading icon={GraduationCap}>{t("settings.guides")}</SectionHeading>
            <div className="flex items-center gap-3">
              <p className="flex-1 text-xs text-muted-fg">{t("settings.guidesHint")}</p>
              <IconButton
                wide
                size="compact"
                tone="edit"
                label={t("settings.guidesReplay")}
                icon={<RotateCcw />}
                /* Host cu chua co seenGuides thi khong luu duoc — tat nut thay vi bao thanh cong gia. */
                disabled={!guides.ready}
                onClick={() => {
                  guides.resetAll();
                  toast.success(t("settings.guidesReplayed"));
                }}
              />
            </div>
          </section>
        </div>

        {saveError || themeError ? (
          <p role="alert" className="mx-5 mb-1 rounded-lg border border-danger/35 bg-danger/10 px-3 py-2 text-xs text-danger">
            {saveError ?? themeError?.message}
          </p>
        ) : null}

        <DialogFooter>
          <IconButton label={t("action.cancel")} icon={<X />} onClick={cancel} />
          <IconButton
            tone="add"
            label={t("action.save")}
            icon={<Save />}
            disabled={isThemeLoading || isThemeSaving}
            onClick={() => void save()}
          />
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
