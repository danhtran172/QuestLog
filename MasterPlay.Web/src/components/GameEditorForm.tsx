/**
 * THE SUA GAME — MOT trang cuon, chia thanh ba mang co tieu de:
 *
 *   Tong quan: ten + gio choi + diem | thong tin game | trang thai |
 *              avg & metascore | the loai & chu de.
 *   Khoi chay: tien trinh + file khoi chay tuy chon | backup (khi bat).
 *   Anh:       anh doc | anh card | anh chi tiet.
 *
 * So khung va cach xep khung bam theo template; kich thuoc/mau chi la he
 * token san co cua app.
 */
import { useEffect, useId, useMemo, useRef, useState, type ClipboardEvent, type ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  AlertTriangle,
  ArrowLeft,
  CalendarPlus,
  Check,
  CloudDownload,
  Eye,
  FileSearch,
  Folder,
  FolderPlus,
  FolderSymlink,
  Loader2,
  Lock,
  LockOpen,
  Minus,
  Plus,
  RefreshCw,
  RotateCcw,
  Save,
  Trash2,
  Trophy,
  X,
} from "lucide-react";
import type { CatalogItem, GameDetail, GameFinishEntry, GameMetadataPreview, TaxonomyItem } from "@/lib/api/domain";
import { api } from "@/lib/api/client";
import { gameDetailApi } from "@/lib/gameDetailApi";
import {
  RATING_MAX,
  finishMonthInputValue,
  isRatingInputDraft,
} from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { clipboardImageFile, imageFileToDataUrl, isInlineImage } from "@/lib/pastedImage";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogTitle,
  GlassFooter,
  IconButton,
  InfoButton,
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
  Switch,
  Tooltip,
} from "./ui";
import { CardPreview, ConsolePreview } from "./EditorImagePreviews";
import {
  CatalogSelectControl,
  EditorHero,
  EditorPanel,
  EditorRow,
  EditorSection,
  EditorStat,
  ImagePreview,
  OverlayScrollbar,
  editorInputClass,
} from "./GameEditorControls";
import { TaxonomyPicker } from "./TaxonomyPicker";
import { OwnershipInfoDialog, ownershipHasInfo } from "./OwnershipInfoDialog";
import { NUMBER_INPUT_NO_SPINNER } from "./InlineEditControls";
import { RatingScaleHint } from "./RatingScaleHint";
import { ConfirmDialog } from "./ConfirmDialog";
import { GuideTour } from "./GuideTour";
import { FORM_GUIDE, useGameFormGuide } from "./gameFormGuide";

type Draft = {
  name: string;
  developer: string;
  publisher: string;
  slugName: string;
  publishDate: string;
  bannerPath: string;
  backdropPath: string;
  posterPath: string;
  exeName: string;
  launchTargetPath: string;
  saveFolderPath: string;
  backupFolderPath: string;
  score: string;
  metaScore: string;
  excited: string;
  avgPlaytimeHours: string;
  playedHours: string;
  playedMinutes: string;
  progressId: string;
  ownershipId: string;
  seriesId: string;
  genreIds: number[];
  themeIds: number[];
  isDlc: boolean;
  baseGameId: string;
  steamAccount: string;
  steamPassword: string;
  ubisoftAccount: string;
  ubisoftPassword: string;
  supportDocumentUrl: string;
  downloadUrl: string;
  note: string;
};

type ImageField = "bannerPath" | "backdropPath" | "posterPath";

type Props = {
  game?: GameDetail;
  mode: "edit" | "add";
  /** Gioi han form vao rieng man sua anh, tai su dung dung UI cua edit view. */
  scope?: "all" | "images";
  onCancel: () => void;
  onSaved: (game: GameDetail) => void;
  onDisabled: () => void;
  /** Dong HAN the — khac `onCancel`, cai do chi thoat che do sua. */
  onClose?: () => void;
  /**
   * Dang ky chot chan dong the. Ham tra `true` nghia la form da giu lai cu bam
   * dong (con thay doi chua luu) va se tu hoi nguoi dung.
   */
  registerCloseGuard?: (guard: (() => boolean) | null) => void;
};

/**
 * Gia tri cho `<input type="month">` cua mot moc hoan thanh.
 *
 * Moc "chi nho nam" cho ra CHUOI RONG chu khong phai `YYYY-01`: backend luu
 * ngay 1 thang 1 cho nhung moc do, doc thang tu do roi hien ra la bia mot thang
 * 1 khong co that (DOMAIN_RULES 9.1). O rong doc dung y nghia "khong co thang".
 *
 * He qua: o day khong doi duoc NAM cua mot moc chi-nho-nam — `<input
 * type="month">` khong dien duoc nam ma khong dien thang. Doi nam thi dung o
 * sua trong the chi tiet (`FinishTimeFields`), noi nam va thang tach roi.
 */
function finishEditorMonth(finish: GameFinishEntry): string {
  return finish.hasMonth ? finishMonthInputValue(finish.finishedAt) : "";
}


const NONE = "__none__";

function toDraft(game?: GameDetail): Draft {
  const seconds = game?.totalPlaytimeSeconds ?? 0;
  return {
    name: game?.name ?? "",
    developer: game?.developer ?? "",
    publisher: game?.publisher ?? "",
    slugName: game?.slugName ?? "",
    publishDate: game?.publishDate ?? "",
    bannerPath: game?.bannerPath ?? "",
    backdropPath: game?.backdropPath ?? "",
    posterPath: game?.posterPath ?? "",
    exeName: game?.exeName ?? "",
    launchTargetPath: game?.launchTarget?.path ?? "",
    saveFolderPath: game?.saveFolderPath ?? "",
    backupFolderPath: game?.backupFolderPath ?? "",
    score: game?.score?.toString() ?? "",
    metaScore: game?.metaScore?.toString() ?? "",
    excited: game?.excited?.toString() ?? "",
    avgPlaytimeHours: game?.avgPlaytimeHours?.toString() ?? "",
    /* Chua tung nhap thi de TRONG. "0" trong o nhap trong nhu mot gia tri da
       nhap, khien khong phan biet duoc "chua co" voi "dung bang khong". */
    playedHours: seconds > 0 ? Math.floor(seconds / 3600).toString() : "",
    playedMinutes: seconds > 0 ? Math.floor((seconds % 3600) / 60).toString() : "",
    progressId: game?.progress?.id.toString() ?? NONE,
    ownershipId: game?.ownership?.id.toString() ?? NONE,
    seriesId: game?.series?.id.toString() ?? NONE,
    genreIds: game?.genres?.map((item) => item.id) ?? [],
    themeIds: game?.themes?.map((item) => item.id) ?? [],
    isDlc: game?.isDlc ?? false,
    baseGameId: game?.baseGameId ?? NONE,
    steamAccount: game?.ownershipInfo?.steamAccount ?? "",
    steamPassword: game?.ownershipInfo?.steamPassword ?? "",
    ubisoftAccount: game?.ownershipInfo?.ubisoftAccount ?? "",
    ubisoftPassword: game?.ownershipInfo?.ubisoftPassword ?? "",
    supportDocumentUrl: game?.ownershipInfo?.supportDocumentUrl ?? "",
    downloadUrl: game?.ownershipInfo?.downloadUrl ?? "",
    note: game?.ownershipInfo?.note ?? "",
  };
}

function asNumber(value: string): number | null {
  if (!value.trim()) return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function sameIds(a: number[], b: number[]): boolean {
  if (a.length !== b.length) return false;
  const left = [...a].sort((x, y) => x - y);
  const right = [...b].sort((x, y) => x - y);
  return left.every((id, index) => id === right[index]);
}

/** Ban nhap co khac ban goc khong — dung de canh bao khi dong the. */
function isDirtyDraft(draft: Draft, base: Draft): boolean {
  return (Object.keys(base) as Array<keyof Draft>).some((key) => {
    const current = draft[key];
    const original = base[key];
    if (Array.isArray(current) && Array.isArray(original)) return !sameIds(current, original);
    return current !== original;
  });
}

function imageInputsFromDraft(draft: Draft): Record<ImageField, string> {
  return {
    bannerPath: draft.bannerPath,
    backdropPath: draft.backdropPath,
    posterPath: draft.posterPath,
  };
}

/**
 * Mot o anh co hai trang thai: URL dang dan va URL da xac nhan trong draft.
 * Nhu vay dan nham khong lam doi preview hay bi luu cung ca form.
 */
function ImageSlot({
  label,
  previewAlt,
  value,
  confirmedValue,
  portrait = false,
  previewClassName,
  disabled = false,
  syncing = false,
  syncDisabled = false,
  onValueChange,
  onConfirm,
  onRemove,
  onSync,
  maxWidth,
  onPasteImage,
  preview,
}: {
  label: string;
  previewAlt: string;
  value: string;
  confirmedValue: string;
  portrait?: boolean;
  previewClassName?: string;
  disabled?: boolean;
  syncing?: boolean;
  syncDisabled?: boolean;
  onValueChange: (value: string) => void;
  onConfirm: () => void;
  onRemove: () => void;
  onSync: () => void;
  /** Rong toi da cua anh dan vao, theo cho anh do thuc su hien thi. */
  maxWidth: number;
  onPasteImage: (dataUrl: string) => void;
  /** Re chuot vao nhan nho duoi anh thi hien anh do o cho no THUC SU hien thi. */
  preview?: { label: string; content: ReactNode };
}) {
  const { t } = useI18n();
  const inputRef = useRef<HTMLInputElement>(null);
  const labelId = useId();
  const [pasting, setPasting] = useState(false);
  const [pasteError, setPasteError] = useState(false);
  const changed = value.trim() !== confirmedValue;
  const inline = isInlineImage(value);

  /* Chon khung (click preview) roi Ctrl+V: anh trong clipboard thay vao khung.
     Dan chu thi de input xu ly nhu cu (URL). */
  const handlePaste = (event: ClipboardEvent<HTMLElement>) => {
    const file = clipboardImageFile(event.clipboardData);
    if (!file) return;
    event.preventDefault();
    setPasting(true);
    setPasteError(false);
    imageFileToDataUrl(file, maxWidth)
      .then(onPasteImage)
      .catch(() => setPasteError(true))
      .finally(() => setPasting(false));
  };

  return (
    <EditorPanel className="flex flex-col gap-2 focus-within:ring-2 focus-within:ring-primary/50">
      <button
        type="button"
        className="relative rounded-xl text-left outline-none focus-visible:ring-2 focus-visible:ring-primary/70"
        aria-label={t("editor.selectImage").replace("{label}", label)}
        onClick={() => inputRef.current?.focus()}
        onPaste={handlePaste}
        disabled={disabled || pasting}
      >
        <ImagePreview src={confirmedValue} alt={previewAlt} portrait={portrait} className={previewClassName} />
        {pasting ? (
          <span className="absolute inset-0 grid place-items-center rounded-xl bg-bg/50">
            <Loader2 size={20} className="animate-spin text-primary" />
          </span>
        ) : null}
      </button>
      <div className="grid gap-1.5 text-xs font-bold text-muted-fg">
        <div className="flex items-center justify-between gap-2">
          <span id={labelId}>{label}</span>
          {preview ? (
            <Tooltip label={preview.content} side="top" sideOffset={10} className="max-w-none p-2">
              <button
                type="button"
                aria-label={`${preview.label} — ${t("editor.previewHint")}`}
                className="inline-flex items-center gap-1 rounded-full border border-border/70 bg-surface/50 px-2 py-0.5 text-fs-10 font-bold text-muted-fg transition hover:border-primary/50 hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
              >
                <Eye size={11} aria-hidden />
                {preview.label}
              </button>
            </Tooltip>
          ) : null}
        </div>
        <div className="flex min-w-0 items-center gap-1.5">
          <input
            ref={inputRef}
            className={cn(editorInputClass, "h-8 min-w-0 text-xs")}
            value={inline ? "" : value}
            onChange={(event) => onValueChange(event.target.value)}
            onPaste={handlePaste}
            placeholder={t(inline ? "editor.pastedImage" : "editor.pasteImageUrl")}
            aria-labelledby={labelId}
            disabled={disabled || pasting}
          />
          <IconButton
            type="button"
            size="compact"
            tone="add"
            label={t("editor.confirmImage")}
            icon={<Check />}
            onClick={onConfirm}
            disabled={disabled || !changed}
          />
          <IconButton
            type="button"
            size="compact"
            tone="danger"
            label={t("editor.removeImage")}
            icon={<Trash2 />}
            onClick={onRemove}
            disabled={disabled || (!value && !confirmedValue)}
          />
          <IconButton
            type="button"
            size="compact"
            tone="edit"
            label={t("editor.syncImage")}
            icon={syncing ? <Loader2 className="animate-spin" /> : <CloudDownload />}
            onClick={onSync}
            disabled={disabled || syncDisabled}
          />
        </div>
        {pasteError ? <span role="alert" className="text-danger">{t("editor.pasteImageFailed")}</span> : null}
      </div>
    </EditorPanel>
  );
}

export function GameEditorForm({
  game,
  mode,
  scope = "all",
  onCancel,
  onSaved,
  onDisabled,
  onClose,
  registerCloseGuard,
}: Props) {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const imagesOnly = scope === "images";
  const [draft, setDraft] = useState(() => toDraft(game));
  const [imageInputs, setImageInputs] = useState(() => imageInputsFromDraft(toDraft(game)));
  const [finishMonths, setFinishMonths] = useState<Record<number, string>>(() =>
    Object.fromEntries((game?.finishes ?? []).map((finish) => [finish.id, finishEditorMonth(finish)])),
  );
  /* Trang thai CHI CUA GIAO DIEN: co dung duong launch rieng hay khong.
     Du lieu that van la `launchTarget` (null = khong co). */
  const [hasCustomLaunch, setHasCustomLaunch] = useState(() => Boolean(game?.launchTarget?.path?.trim()));
  const [adjustMinutes, setAdjustMinutes] = useState("");
  const [ownershipOpen, setOwnershipOpen] = useState(false);
  const [disableConfirmOpen, setDisableConfirmOpen] = useState(false);
  /* Dang cho nguoi dung tra loi hop canh bao: dong han the, hay chi thoat sua. */
  const [pendingExit, setPendingExit] = useState<"close" | "cancel" | null>(null);
  /* The THEM game: metadata (ten hang, anh, diem ngoai...) mac dinh khoa de
     nguoi dung khong lo tay sua nham — chi userdata (diem so, thoi gian choi,
     tinh trang...) duoc mo san. Bam o khoa moi sua duoc metadata. */
  const [metadataLocked, setMetadataLocked] = useState(mode === "add");
  /* Ten series IGDB goi y ma thu vien CHUA co (preview khong duoc tao). Nguoi
     dung bam thi moi tao that. */
  const [seriesSuggestion, setSeriesSuggestion] = useState<string | null>(null);
  /* Ket qua lan sync gan nhat: game kho metadata da khop va nguon nao hong (vd thieu
     key RAWG) — phai hien ra, khong de nguoi dung tuong "RAWG khong chay" ma khong ro vi sao. */
  const [syncReport, setSyncReport] = useState<{
    matchedName: string | null;
    warnings: NonNullable<GameMetadataPreview["warnings"]>;
  } | null>(null);

  const baseDraft = useMemo(() => toDraft(game), [game]);
  const dirty = useMemo(() => isDirtyDraft(draft, baseDraft), [draft, baseDraft]);
  /* Chot chan duoc dang ky mot lan, nen no phai doc trang thai qua ref. */
  const dirtyRef = useRef(dirty);
  dirtyRef.current = dirty;
  const pendingExitRef = useRef(pendingExit);
  pendingExitRef.current = pendingExit;
  const scrollerRef = useRef<HTMLDivElement>(null);
  const guideContainerRef = useRef<HTMLDivElement>(null);
  /* Tour huong dan dang mo thi Esc / nut X chi dong tour, khong dong the. */
  const stopGuideRef = useRef<(() => boolean) | null>(null);

  useEffect(() => {
    registerCloseGuard?.(() => {
      if (stopGuideRef.current?.()) return true;
      if (!dirtyRef.current) return false;
      setPendingExit("close");
      return true;
    });
    return () => registerCloseGuard?.(null);
  }, [registerCloseGuard]);

  useEffect(() => {
    setDraft(toDraft(game));
    setImageInputs(imageInputsFromDraft(toDraft(game)));
    setFinishMonths(
      Object.fromEntries((game?.finishes ?? []).map((finish) => [finish.id, finishEditorMonth(finish)])),
    );
    setHasCustomLaunch(Boolean(game?.launchTarget?.path?.trim()));
    setAdjustMinutes("");
    setOwnershipOpen(false);
    setPendingExit(null);
    setMetadataLocked(mode === "add");
    setSeriesSuggestion(null);
  }, [game?.id, mode]);

  const progress = useQuery({ queryKey: ["catalog", "progress"], queryFn: () => api.catalog("progress"), enabled: !imagesOnly });
  const ownership = useQuery({ queryKey: ["catalog", "ownership"], queryFn: () => api.catalog("ownership"), enabled: !imagesOnly });
  const series = useQuery({ queryKey: ["taxonomy", "series"], queryFn: () => api.taxonomy("series"), enabled: !imagesOnly });
  const genres = useQuery({ queryKey: ["taxonomy", "genres"], queryFn: () => api.taxonomy("genres"), enabled: !imagesOnly });
  const themes = useQuery({ queryKey: ["taxonomy", "themes"], queryFn: () => api.taxonomy("themes"), enabled: !imagesOnly });
  const baseGames = useQuery({
    queryKey: ["base-game-candidates", game?.id],
    queryFn: () => api.baseGameCandidates(game?.id),
    enabled: !imagesOnly && draft.isDlc,
  });
  const capabilities = useQuery({ queryKey: ["capabilities"], queryFn: () => api.capabilities(), enabled: !imagesOnly, staleTime: Infinity });
  const externalLinks = useQuery({
    queryKey: ["game-external-links", game?.id],
    queryFn: () => gameDetailApi.externalLinks(game!.id),
    enabled: !imagesOnly && mode === "edit" && Boolean(game?.id),
  });
  const referencesPending = !imagesOnly && ([progress, ownership, series, genres, themes].some((query) => query.isPending) ||
    (draft.isDlc && baseGames.isPending));
  const referenceError = imagesOnly ? undefined : (
    [progress, ownership, series, genres, themes].find((query) => query.isError)?.error ??
    (draft.isDlc && baseGames.isError ? baseGames.error : undefined)
  );
  const formGuide = useGameFormGuide(mode === "add" && !imagesOnly && !referencesPending && !referenceError);
  stopGuideRef.current = formGuide.stop;

  const pickCatalog =(items: CatalogItem[] | undefined, value: string) =>
    items?.find((item) => item.id === Number(value)) ?? null;
  const pickTaxonomy = (items: TaxonomyItem[] | undefined, value: string) =>
    items?.find((item) => item.id === Number(value)) ?? null;

  /* Doi kieu so huu sang loai khong co thong tin thi nut ⓘ bien mat — popup
     dang mo phai dong theo, khong de no treo lai giua man hinh. */
  const ownershipInfoAvailable = ownershipHasInfo(pickCatalog(ownership.data, draft.ownershipId));

  const payload = useMemo<GameDetail>(() => {
    if (imagesOnly && game) {
      return {
        ...game,
        bannerPath: draft.bannerPath || null,
        backdropPath: draft.backdropPath || null,
        posterPath: draft.posterPath || null,
      };
    }

    const hoursInput = asNumber(draft.playedHours);
    const minutesInput = asNumber(draft.playedMinutes);
    const hasPlaytime = hoursInput !== null || minutesInput !== null;
    const playedHours = Math.max(0, hoursInput ?? 0);
    const playedMinutes = Math.max(0, minutesInput ?? 0);
    return {
      id: game?.id ?? "",
      name: draft.name.trim(),
      isDlc: draft.isDlc,
      isUpcoming: game?.isUpcoming ?? false,
      ready: game?.ready ?? false,
      isRunning: game?.isRunning ?? false,
      canLaunch: game?.canLaunch ?? false,
      developer: draft.developer || null,
      publisher: draft.publisher || null,
      slugName: draft.slugName || null,
      publishDate: draft.publishDate || null,
      bannerPath: draft.bannerPath || null,
      backdropPath: draft.backdropPath || null,
      posterPath: draft.posterPath || null,
      exeName: draft.exeName || null,
      launchTarget: hasCustomLaunch && draft.launchTargetPath ? {
        kind: draft.launchTargetPath.toLowerCase().endsWith(".lnk") ? "shortcut" : "exe",
        path: draft.launchTargetPath,
      } : null,
      saveFolderPath: draft.saveFolderPath || null,
      backupFolderPath: draft.backupFolderPath || null,
      score: asNumber(draft.score),
      metaScore: asNumber(draft.metaScore),
      excited: asNumber(draft.excited),
      avgPlaytimeHours: asNumber(draft.avgPlaytimeHours),
      // Form chi sua gio/phut; giu nguyen phan giay hien co de Save khong lam mat du lieu ngam.
      // Ca hai o deu trong = chua nhap -> gui null chu khong phai 0.
      totalPlaytimeSeconds: hasPlaytime
        ? Math.round(playedHours * 3600 + playedMinutes * 60 + ((game?.totalPlaytimeSeconds ?? 0) % 60))
        : null,
      progress: pickCatalog(progress.data, draft.progressId),
      ownership: pickCatalog(ownership.data, draft.ownershipId),
      series: pickTaxonomy(series.data, draft.seriesId),
      genres: (genres.data ?? []).filter((item) => draft.genreIds.includes(item.id)),
      themes: (themes.data ?? []).filter((item) => draft.themeIds.includes(item.id)),
      baseGameId: draft.baseGameId === NONE ? null : draft.baseGameId,
      ownershipInfo: {
        steamAccount: draft.steamAccount || null,
        steamPassword: draft.steamPassword || null,
        ubisoftAccount: draft.ubisoftAccount || null,
        ubisoftPassword: draft.ubisoftPassword || null,
        supportDocumentUrl: draft.supportDocumentUrl || null,
        downloadUrl: draft.downloadUrl || null,
        note: draft.note || null,
      },
    };
  }, [draft, game, genres.data, hasCustomLaunch, imagesOnly, ownership.data, progress.data, series.data, themes.data]);

  const save = useMutation({
    mutationFn: async () => {
      let body = payload;
      /* Sync tim ra mot Series thu vien CHUA co (preview khong duoc ghi DB) va
         nguoi dung khong chon Series nao khac: tao that luc Luu, khong thi bam
         Luu ma quen bam chip goi y la series cua sync roi mat. */
      if (!imagesOnly && !draft.isDlc && draft.seriesId === NONE && seriesSuggestion) {
        const created = await api.createTaxonomy("series", { id: 0, name: seriesSuggestion, iconData: null, sortOrder: 0 });
        await queryClient.invalidateQueries({ queryKey: ["taxonomy", "series"] });
        body = { ...payload, series: created };
      }
      return mode === "add" ? gameDetailApi.create(body) : gameDetailApi.update(game!.id, body);
    },
    onSuccess: async (saved) => {
      queryClient.setQueryData(["game-detail", saved.id], saved);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["games"] }),
        queryClient.invalidateQueries({ queryKey: ["play-queue"] }),
        queryClient.invalidateQueries({ queryKey: ["dashboard-stats"] }),
      ]);
      /* Neu dang luu vi nguoi dung chon "Luu" trong hop canh bao dong the thi
         luu xong phai di not viec dong. */
      const exit = pendingExitRef.current;
      setPendingExit(null);
      onSaved(saved);
      if (exit === "close") (onClose ?? onCancel)();
    },
  });

  const finishTimeSave = useMutation({
    /* O `<input type="month">` luon cho ra "YYYY-MM", nen o day thang LUON co.
       Moc "chi nho nam" phai sua o the chi tiet — xem `FinishTimeFields`. */
    mutationFn: ({ finishId, finishedMonth }: { finishId: number; finishedMonth: string }) =>
      gameDetailApi.updateFinishTime(game!.id, finishId, {
        finishedYear: Number(finishedMonth.slice(0, 4)),
        finishedMonth: Number(finishedMonth.slice(5, 7)),
      }),
    onSuccess: async (saved) => {
      queryClient.setQueryData(["game-detail", saved.id], saved);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["games"] }),
        queryClient.invalidateQueries({ queryKey: ["play-queue"] }),
      ]);
    },
  });

  /* Them mot moc hoan thanh. Backend chay ca transaction: ghi moc voi thoi
     gian hien tai, set progress=Done (va set score neu la LAN DAU) — nen phai
     keo hai field do ve ban nhap, khong thi luc Save minh ghi de nguoc lai.
     Diem chi gui o lan dau; cac lan sau khong con diem theo lan (§9.2). */
  const addFinish = useMutation({
    mutationFn: (score: number | null) =>
      gameDetailApi.finish(game!.id, score === null ? {} : { score }),
    onSuccess: async (saved) => {
      setDraft((current) => ({
        ...current,
        progressId: saved.progress?.id.toString() ?? NONE,
        score: saved.score?.toString() ?? current.score,
      }));
      queryClient.setQueryData(["game-detail", saved.id], saved);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["games"] }),
        queryClient.invalidateQueries({ queryKey: ["play-queue"] }),
        queryClient.invalidateQueries({ queryKey: ["dashboard-stats"] }),
      ]);
    },
  });

  const cacheServerGame = async (saved: GameDetail) => {
    queryClient.setQueryData(["game-detail", saved.id], saved);
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["games"] }),
      queryClient.invalidateQueries({ queryKey: ["play-queue"] }),
    ]);
  };
  /* Preview metadata: goi provider theo TEN, chi DIEN vao ban nhap — KHONG ghi
     DB. Nguoi dung phai bam Save moi luu. Chay duoc ca khi them game moi (chua
     co id). */
  const sync = useMutation({
    mutationFn: (source: "metadata" | "card-cover" | "cover" | "portrait-cover") =>
      gameDetailApi.previewMetadata(draft.name.trim(), source),
    onMutate: () => setSyncReport(null),
    onSuccess: (preview, source) => {
      /* Field null = khong nguon nao co -> GIU gia tri dang co thay vi xoa trang
         (truoc day sync hut mot nguon la mat luon ten exe/developer da nhap). */
      const str = (value: string | null | undefined, current: string) => value ?? current;
      const num = (value: number | null | undefined, current: string) => value?.toString() ?? current;
      setDraft((current) => source === "metadata" ? {
        ...current,
        name: str(preview.name, current.name),
        developer: str(preview.developer, current.developer),
        publisher: str(preview.publisher, current.publisher),
        slugName: str(preview.slugName, current.slugName),
        publishDate: str(preview.publishDate, current.publishDate),
        metaScore: num(preview.metaScore, current.metaScore),
        avgPlaytimeHours: num(preview.avgPlaytimeHours, current.avgPlaytimeHours),
        /* Sync metadata KHONG dung toi anh: moi o anh co nut sync rieng, de anh
           da chon tay khong bi thay mat khi chi muon cap nhat thong tin. */
        exeName: str(preview.exeName, current.exeName),
        isDlc: preview.isDlc ?? current.isDlc,
        /* Server da khop san voi Series dang co; khong khop thi giu lua chon cu. */
        seriesId: preview.series ? preview.series.id.toString() : current.seriesId,
      } : source === "cover" ? { ...current, backdropPath: str(preview.backdropPath, current.backdropPath) }
        : source === "card-cover" ? { ...current, bannerPath: str(preview.bannerPath, current.bannerPath) }
          : { ...current, posterPath: str(preview.posterPath, current.posterPath) });
      if (source === "metadata") {
        setSeriesSuggestion(preview.series ? null : preview.seriesName ?? null);
      } else if (source === "cover") {
        setImageInputs((current) => ({ ...current, backdropPath: str(preview.backdropPath, current.backdropPath) }));
      } else if (source === "card-cover") {
        setImageInputs((current) => ({ ...current, bannerPath: str(preview.bannerPath, current.bannerPath) }));
      } else {
        setImageInputs((current) => ({ ...current, posterPath: str(preview.posterPath, current.posterPath) }));
      }
      setSyncReport({ matchedName: preview.metadataGameName ?? null, warnings: preview.warnings ?? [] });
    },
  });
  const createSeries = useMutation({
    mutationFn: (name: string) => api.createTaxonomy("series", { id: 0, name, iconData: null, sortOrder: 0 }),
    onSuccess: async (created) => {
      await queryClient.invalidateQueries({ queryKey: ["taxonomy", "series"] });
      setDraft((current) => ({ ...current, seriesId: created.id.toString() }));
      setSeriesSuggestion(null);
    },
  });
  /* Kho metadata la nguon su that chung. Endpoint da tu phan biet metadata
     nguoi dung sua tay (giu lai) va metadata chua sua (lay gia tri moi). */
  const resetMetadata = useMutation({
    mutationFn: async () => {
      await api.refreshLibraryMetadata({ force: true });
      return gameDetailApi.get(game!.id);
    },
    onSuccess: async (saved) => {
      const refreshedDraft = toDraft(saved);
      setDraft(refreshedDraft);
      setImageInputs(imageInputsFromDraft(refreshedDraft));
      queryClient.setQueryData(["game-detail", saved.id], saved);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["games"] }),
        queryClient.invalidateQueries({ queryKey: ["play-queue"] }),
        queryClient.invalidateQueries({ queryKey: ["dashboard-stats"] }),
      ]);
      onSaved(saved);
    },
  });
  const adjustPlaytime = useMutation({
    mutationFn: (deltaMinutes: number) => gameDetailApi.adjustPlaytime(game!.id, deltaMinutes),
    onSuccess: async (saved) => {
      const synced = toDraft(saved);
      setDraft((current) => ({ ...current, playedHours: synced.playedHours, playedMinutes: synced.playedMinutes }));
      await cacheServerGame(saved);
    },
  });
  const pickFolder = useMutation({
    mutationFn: (field: "saveFolderPath" | "backupFolderPath") => gameDetailApi.pickFolder().then((result) => ({ field, path: result.path })),
    onSuccess: ({ field, path }) => { if (path) setDraft((current) => ({ ...current, [field]: path })); },
  });
  const pickLaunchTarget = useMutation({
    mutationFn: () => gameDetailApi.pickFile("exe-or-shortcut"),
    onSuccess: (result) => { if (result.path) setDraft((current) => ({ ...current, launchTargetPath: result.path! })); },
  });
  const revealFolder = useMutation({ mutationFn: (path: string) => gameDetailApi.revealFolder(path) });
  const generateBackup = useMutation({
    mutationFn: () => gameDetailApi.generateBackupFolder(game!.id),
    onSuccess: (result) => {
      setDraft((current) => ({ ...current, backupFolderPath: result.backupFolderPath }));
      queryClient.setQueryData<GameDetail>(["game-detail", game!.id], (current) =>
        current ? { ...current, backupFolderPath: result.backupFolderPath } : current);
    },
  });
  const disableGame = useMutation({
    mutationFn: () => gameDetailApi.disable(game!.id),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["games"] }),
        queryClient.invalidateQueries({ queryKey: ["play-queue"] }),
        queryClient.invalidateQueries({ queryKey: ["dashboard-stats"] }),
      ]);
      onDisabled();
    },
  });
  const commandError = sync.error ?? createSeries.error ?? resetMetadata.error ??adjustPlaytime.error ?? pickFolder.error ?? pickLaunchTarget.error ??
    revealFolder.error ?? generateBackup.error ?? disableGame.error ?? externalLinks.error ?? capabilities.error;
  const failure = save.error ?? finishTimeSave.error ?? addFinish.error ?? commandError ?? referenceError;
  const errorMessage = failure ? (failure instanceof Error ? failure.message : String(failure)) : null;

  const set = <K extends keyof Draft>(key: K, value: Draft[K]) =>
    setDraft((current) => ({ ...current, [key]: value }));

  const scoreForFinish = asNumber(draft.score);
  const firstFinish = (game?.finishes?.length ?? 0) === 0;
  const finishNeedsScore = firstFinish && scoreForFinish === null;
  const adjustAmount = Math.max(0, Math.floor(asNumber(adjustMinutes) ?? 0));
  const canAdjust = mode === "edit" && adjustAmount > 0 && !adjustPlaytime.isPending;
  const canPickFile = Boolean(capabilities.data?.features.filePicker);
  const canReveal = Boolean(capabilities.data?.features.revealFolder);
  // Backup dang tam tat o backend -> an ca o thu muc backup. Draft van giu gia
  // tri cu nen luu game khong xoa BackupFolderPath da co.
  const canBackup = capabilities.data?.features.backup === true;
  const syncing = (source: string) => sync.isPending && sync.variables === source;
  const syncStatusKey = resetMetadata.isPending ? "editor.resettingMetadata" : sync.variables === "metadata"
    ? "editor.syncingMetadata"
    : sync.variables === "cover"
      ? "editor.syncingBackdrop"
      : sync.variables === "card-cover"
        ? "editor.syncingBanner"
        : sync.variables === "portrait-cover"
          ? "editor.syncingPoster"
          : null;
  const syncStatusMessage = sync.isPending && syncStatusKey ? t(syncStatusKey) : null;
  /* Chi khoa o the THEM game — the SUA (game da co san) luon mo het nhu truoc. */
  const metaDisabled = mode === "add" && !imagesOnly && metadataLocked;
  /* Banner chay tren card; poster va backdrop chay o Console. */
  const cardPreview = { label: t("editor.previewCard"), content: <CardPreview game={payload} /> };
  const consolePreview = { label: t("editor.previewConsole"), content: <ConsolePreview game={payload} /> };

  return (
    <form
      noValidate
      className="flex min-h-0 flex-1 flex-col overflow-hidden"
      onSubmit={(event) => { event.preventDefault(); if (draft.name.trim()) save.mutate(); }}
    >
      <DialogTitle className="sr-only">{imagesOnly ? t("editor.images") : mode === "add" ? t("editor.addTitle") : t("editor.editTitle")}</DialogTitle>
      <DialogDescription className="sr-only">{t("editor.description")}</DialogDescription>

      <div ref={guideContainerRef} className="relative min-h-0 flex-1 overflow-hidden">
        {/* `scrollbar-none`: thanh truot native se an mat mot dai o mep phai cua
            anh hero. Thay bang thanh truot noi de len tren, ben duoi. */}
        <div ref={scrollerRef} className="scrollbar-none h-full overflow-y-auto">
          {/* Bang loi DINH o dinh vung cuon: cuon di dau cung phai thay duoc loi,
              nhat la loi Save khi nguoi dung dang o cuoi trang. */}
          {errorMessage ? (
            <div className="sticky top-0 z-20 mx-5 mt-3 flex gap-2 rounded-lg border border-danger/45 bg-danger/20 p-2.5 text-xs text-danger shadow-lg backdrop-blur-xl">
              <AlertTriangle size={14} className="mt-0.5 shrink-0" />
              <span className="min-w-0 break-words">{errorMessage}</span>
            </div>
          ) : null}
          {syncReport && (syncReport.matchedName || syncReport.warnings.length > 0) ? (
            <div role="status" className="mx-5 mt-3 space-y-1 rounded-lg border border-border bg-muted/40 p-2.5 text-xs text-muted-fg">
              <p className="flex items-center gap-2">
                <RefreshCw size={13} className="shrink-0 text-primary" />
                <span className="min-w-0 break-words">
                  {syncReport.matchedName
                    ? t("editor.syncMatchedMetadata").replace("{name}", syncReport.matchedName)
                    : t("editor.syncNoMetadataMatch")}
                </span>
              </p>
              {syncReport.warnings.map((warning) => (
                <p key={`${warning.provider}:${warning.code}`} className="flex gap-2 text-warning">
                  <AlertTriangle size={13} className="mt-0.5 shrink-0" />
                  <span className="min-w-0 break-words">
                    <span className="font-bold uppercase">{warning.provider}</span> · {warning.message}
                  </span>
                </p>
              ))}
            </div>
          ) : null}

          {/* Hero NAM TRONG vung cuon: keo la anh troi di theo, nhuong het cho
              cho noi dung. The THEM game chi hien hero khi DA CO backdrop (dong
              bo hoac dan vao) — giong trang xem; chua co thi o trong voi icon
              placeholder chi chiem cho vo ich, ten game da o o Ten ben duoi. */}
          {mode !== "add" || draft.backdropPath ? (
            <EditorHero
              name={draft.name}
              publisher={draft.publisher || draft.developer}
              imageSrc={draft.backdropPath || draft.bannerPath}
              fallbackName={t("editor.addTitle")}
              isDlc={draft.isDlc}
              isUpcoming={game?.isUpcoming ?? false}
              publishDate={draft.publishDate || null}
            />
          ) : null}

          {/* Le duoi chua cho footer kinh mo noi o day the. */}
          <div className="space-y-6 pb-20 pl-5 pr-4 pt-4 sm:pl-6 sm:pr-5">
            {!imagesOnly ? (
              <>
            <EditorSection
              title={t("detail.overview")}
              actions={mode === "add" ? (
                <IconButton
                  data-guide={FORM_GUIDE.lock}
                  size="compact"
                  tone={metadataLocked ? "neutral" : "edit"}
                  label={t(metadataLocked ? "editor.unlockMetadata" : "editor.lockMetadata")}
                  icon={metadataLocked ? <Lock /> : <LockOpen />}
                  onClick={() => setMetadataLocked((current) => !current)}
                />
              ) : undefined}
            >
              {mode === "add" ? (
                <p className="-mt-1 text-fs-11 font-medium text-muted-fg">
                  {t(metadataLocked ? "editor.metadataLockedHint" : "editor.metadataUnlockedHint")}
                </p>
              ) : null}
              {/* Hang ten: ten game va cong tac DLC nam cung mot hang. */}
              <div className="space-y-3">
                <EditorPanel guide={FORM_GUIDE.name} className="flex items-center gap-3">
                  <EditorRow label={t("editor.name")} className="min-w-0 flex-1">
                    <input
                      autoFocus
                      className={cn(editorInputClass, "h-8")}
                      value={draft.name}
                      onChange={(event) => set("name", event.target.value)}
                    />
                    <IconButton
                      size="compact"
                      label={t("editor.syncMetadata")}
                      tone="edit"
                      icon={syncing("metadata") ? <Loader2 className="animate-spin" /> : <RefreshCw />}
                      onClick={() => sync.mutate("metadata")}
                      disabled={sync.isPending || resetMetadata.isPending || !draft.name.trim()}
                    />
                    {mode === "edit" && game?.metadataGameId ? (
                      <IconButton
                        type="button"
                        size="compact"
                        tone="neutral"
                        label={t("editor.resetMetadata")}
                        icon={resetMetadata.isPending ? <Loader2 className="animate-spin" /> : <RotateCcw />}
                        onClick={() => resetMetadata.mutate()}
                        disabled={sync.isPending || resetMetadata.isPending}
                      />
                    ) : null}
                  </EditorRow>
                  <label className="flex items-center gap-2 whitespace-nowrap text-xs font-bold text-muted-fg">
                    {t("editor.dlc")}
                    <Switch checked={draft.isDlc} onCheckedChange={(value) => set("isDlc", value)} />
                  </label>
                </EditorPanel>

                {/* Gio choi va o diem cung hang. */}
                <div data-guide={FORM_GUIDE.playtime} className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_minmax(88px,auto)]">
                  <EditorPanel>
                    <EditorRow label={t("editor.playedTime")}>
                      <input
                        type="number"
                        min="0"
                        aria-label={t("editor.playedHours")}
                        className={cn(editorInputClass, "h-8 w-16 px-2")}
                        value={draft.playedHours}
                        onChange={(event) => set("playedHours", event.target.value)}
                      />
                      <span className="text-fs-11 font-black text-muted-fg">h</span>
                      <input
                        type="number"
                        min="0"
                        max="59"
                        aria-label={t("editor.playedMinutes")}
                        className={cn(editorInputClass, "h-8 w-16 px-2")}
                        value={draft.playedMinutes}
                        onChange={(event) => set("playedMinutes", event.target.value)}
                      />
                      <span className="text-fs-11 font-black text-muted-fg">m</span>
                      {/* Cum cong/tru: goi API playtime, khong tu tinh o client. */}
                      <span className="ml-auto flex items-center gap-1.5">
                        <IconButton
                          size="compact"
                          label={t(adjustAmount > 0 ? "editor.subtractMinute" : "editor.adjustMinutesNeeded")}
                          tone="edit"
                          icon={<Minus />}
                          onClick={() => adjustPlaytime.mutate(-adjustAmount)}
                          disabled={!canAdjust}
                        />
                        <input
                          type="number"
                          min="1"
                          aria-label={t("editor.adjustMinutes")}
                          className={cn(editorInputClass, "h-8 w-16 px-2")}
                          value={adjustMinutes}
                          onChange={(event) => setAdjustMinutes(event.target.value)}
                        />
                        <IconButton
                          size="compact"
                          label={t(adjustAmount > 0 ? "editor.addMinute" : "editor.adjustMinutesNeeded")}
                          tone="edit"
                          icon={adjustPlaytime.isPending ? <Loader2 className="animate-spin" /> : <Plus />}
                          onClick={() => adjustPlaytime.mutate(adjustAmount)}
                          disabled={!canAdjust}
                        />
                      </span>
                    </EditorRow>
                  </EditorPanel>

                  <EditorStat
                    label={t("sort.score")}
                    icon={<Trophy size={12} className="text-warning" aria-hidden />}
                    className="p-2"
                  >
                    <input
                      type="number"
                      min="0"
                      max={RATING_MAX}
                      step="0.5"
                      className={cn(NUMBER_INPUT_NO_SPINNER, editorInputClass, "h-8 px-2 text-center text-lg font-black")}
                      value={draft.score}
                      onChange={(event) => {
                        if (isRatingInputDraft(event.target.value)) set("score", event.target.value);
                      }}
                    />
                  </EditorStat>
                </div>
              </div>

              <div className="grid gap-3 md:grid-cols-[1.15fr_1fr]">
                {/* Khung thong tin game. */}
                <EditorPanel className="space-y-2">
                  {/* DLC thi khong co Series ma co game goc — giong trang xem. */}
                  {draft.isDlc ? (
                    <EditorRow label={t("editor.baseGame")}>
                      <Select value={draft.baseGameId} onValueChange={(value) => set("baseGameId", value)}>
                        <SelectTrigger className={cn(editorInputClass, "h-8")} aria-label={t("editor.baseGame")}>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent className="max-h-72 min-w-64">
                          <SelectItem value={NONE}>{t("editor.none")}</SelectItem>
                          {baseGames.data?.map((candidate) => (
                            <SelectItem key={candidate.id} value={candidate.id}>{candidate.name}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </EditorRow>
                  ) : (
                    <>
                      <TaxonomyPicker
                        kind="series"
                        label={t("sort.series")}
                        items={series.data ?? []}
                        value={draft.seriesId === NONE ? [] : [Number(draft.seriesId)]}
                        multiple={false}
                        row
                        onChange={(value) => set("seriesId", value[0] ? String(value[0]) : NONE)}
                      />
                      {seriesSuggestion && draft.seriesId === NONE ? (
                        <div className="grid grid-cols-[minmax(64px,0.32fr)_minmax(0,1fr)] items-center gap-2">
                          <span aria-hidden />
                          <button
                            type="button"
                            onClick={() => createSeries.mutate(seriesSuggestion)}
                            disabled={createSeries.isPending}
                            title={t("editor.createSeries")}
                            className="inline-flex min-w-0 items-center gap-1.5 justify-self-start rounded-full border border-primary/40 bg-primary/10 px-2.5 py-1 text-fs-11 font-bold text-primary transition hover:bg-primary/20 disabled:opacity-50"
                          >
                            {createSeries.isPending ? <Loader2 size={12} className="animate-spin" /> : <Plus size={12} />}
                            <span className="truncate">{t("editor.seriesSuggestion").replace("{name}", seriesSuggestion)}</span>
                          </button>
                        </div>
                      ) : null}
                    </>
                  )}
                  <EditorRow label={t("sort.publish")}>
                    <input
                      type="date"
                      className={cn(editorInputClass, "h-8")}
                      value={draft.publishDate}
                      onChange={(event) => set("publishDate", event.target.value)}
                      disabled={metaDisabled}
                    />
                  </EditorRow>
                  <EditorRow label={t("editor.developer")}>
                    <input
                      className={cn(editorInputClass, "h-8")}
                      value={draft.developer}
                      onChange={(event) => set("developer", event.target.value)}
                      disabled={metaDisabled}
                    />
                  </EditorRow>
                  <EditorRow label={t("detail.publisher")}>
                    <input
                      className={cn(editorInputClass, "h-8")}
                      value={draft.publisher}
                      onChange={(event) => set("publisher", event.target.value)}
                      disabled={metaDisabled}
                    />
                  </EditorRow>
                  {/* Slug KHONG sua tay: do sync metadata dat, chi di theo payload. */}
                </EditorPanel>

                {/* Khung trang thai. */}
                <EditorPanel guide={FORM_GUIDE.status} className="flex flex-col gap-2">
                  <div className="space-y-2">
                    <EditorRow label={t("sort.progress")}>
                      <CatalogSelectControl
                        ariaLabel={t("sort.progress")}
                        value={draft.progressId}
                        items={progress.data ?? []}
                        noneLabel={t("editor.none")}
                        onChange={(value) => set("progressId", value)}
                      />
                      {/* Cho trong bang dung nut ⓘ o hang Ownership — hai o
                          chon phai dai bang nhau. */}
                      <span aria-hidden className="w-7 shrink-0" />
                    </EditorRow>
                    <EditorRow label={t("sort.ownership")}>
                      <CatalogSelectControl
                        ariaLabel={t("sort.ownership")}
                        value={draft.ownershipId}
                        items={ownership.data ?? []}
                        noneLabel={t("editor.none")}
                        onChange={(value) => set("ownershipId", value)}
                      />
                      {/* Kieu so huu khong co gi de xem thi bo nut, nhung van
                          chua cho trong dung bang no — hai o chon phai dai
                          bang nhau du co nut hay khong. */}
                      {ownershipInfoAvailable ? (
                        <InfoButton
                          size="compact"
                          label={t(ownershipOpen ? "detail.hideOwnershipInfo" : "detail.showOwnershipInfo")}
                          tone={ownershipOpen ? "edit" : "neutral"}
                          aria-expanded={ownershipOpen}
                          onClick={() => setOwnershipOpen((current) => !current)}
                        />
                      ) : (
                        <span aria-hidden className="w-7 shrink-0" />
                      )}
                    </EditorRow>
                    <EditorRow label={t("sort.excited")}>
                      <input
                        type="number"
                        min="0"
                        max={RATING_MAX}
                        step="0.5"
                        className={cn(NUMBER_INPUT_NO_SPINNER, editorInputClass, "h-8")}
                        value={draft.excited}
                        onChange={(event) => {
                          if (isRatingInputDraft(event.target.value)) set("excited", event.target.value);
                        }}
                      />
                    </EditorRow>
                    {mode === "edit" ? (
                      <div className="grid grid-cols-[minmax(64px,0.32fr)_minmax(0,1fr)] items-start gap-2 text-xs font-bold text-muted-fg">
                        <span className="truncate pt-1">{t("detail.finishHistory")}</span>
                        <div className="flex min-w-0 flex-wrap items-center gap-1.5">
                          {game?.finishes?.map((finish) => {
                            const originalMonth = finishEditorMonth(finish);
                            const selectedMonth = finishMonths[finish.id] ?? originalMonth;
                            const changed = Boolean(selectedMonth) && selectedMonth !== originalMonth;
                            const savingThis = finishTimeSave.isPending && finishTimeSave.variables?.finishId === finish.id;
                            return (
                              <span
                                key={finish.id}
                                className="inline-flex items-center gap-1 rounded-full border border-border bg-surface/50 py-0.5 pl-2 pr-1"
                              >
                                <input
                                  type="month"
                                  aria-label={t("editor.finishTime")}
                                  className="w-[104px] bg-transparent text-fs-11 font-bold text-fg outline-none"
                                  value={selectedMonth}
                                  onChange={(event) =>
                                    setFinishMonths((current) => ({ ...current, [finish.id]: event.target.value }))
                                  }
                                />
                                {changed ? (
                                  <IconButton
                                    size="compact"
                                    className="h-5 w-5 [&_svg]:size-3"
                                    label={t("editor.saveFinishTime")}
                                    tone="add"
                                    icon={savingThis ? <Loader2 className="animate-spin" /> : <Save />}
                                    onClick={() => finishTimeSave.mutate({ finishId: finish.id, finishedMonth: selectedMonth })}
                                    disabled={finishTimeSave.isPending}
                                  />
                                ) : null}
                              </span>
                            );
                          })}
                          {/* Them moc: dung dung diem dang co o o Score. */}
                          <IconButton
                            size="compact"
                            label={t(finishNeedsScore ? "editor.addFinishNeedsScore" : "editor.addFinish")}
                            tone="finish"
                            icon={addFinish.isPending ? <Loader2 className="animate-spin" /> : <CalendarPlus />}
                            onClick={() => !finishNeedsScore && addFinish.mutate(firstFinish ? scoreForFinish : null)}
                            disabled={addFinish.isPending || finishNeedsScore}
                          />
                        </div>
                      </div>
                    ) : null}
                  </div>
                </EditorPanel>
              </div>

              <RatingScaleHint />

              {/* Hai so den tu nguon ngoai (HLTB, Metacritic) — khung rieng,
                  xep ngang. */}
              <EditorPanel className="grid gap-3 sm:grid-cols-2">
                <EditorRow label={t("sort.avgPlaytime")}>
                  <input
                    type="number"
                    min="0"
                    className={cn(editorInputClass, "h-8")}
                    value={draft.avgPlaytimeHours}
                    onChange={(event) => set("avgPlaytimeHours", event.target.value)}
                    disabled={metaDisabled}
                  />
                  <InfoButton
                    size="compact"
                    label={t("editor.openHltb")}
                    tone="open"
                    onClick={() => externalLinks.data?.hltbUrl && window.open(externalLinks.data.hltbUrl, "_blank", "noopener,noreferrer")}
                    disabled={!externalLinks.data?.hltbUrl}
                  />
                </EditorRow>
                <EditorRow label={t("sort.metaScore")}>
                  <input
                    type="number"
                    min="0"
                    max="100"
                    className={cn(editorInputClass, "h-8")}
                    value={draft.metaScore}
                    onChange={(event) => set("metaScore", event.target.value)}
                    disabled={metaDisabled}
                  />
                  <InfoButton
                    size="compact"
                    label={t(externalLinks.data?.metacriticUrl === "" ? "detail.metacriticNoSlug" : "editor.openMetacritic")}
                    tone="open"
                    onClick={() => externalLinks.data?.metacriticUrl && window.open(externalLinks.data.metacriticUrl, "_blank", "noopener,noreferrer")}
                    disabled={!externalLinks.data?.metacriticUrl}
                  />
                </EditorRow>
              </EditorPanel>

              {/* Chu de va the loai — mot khung chay het be rong. */}
              <EditorPanel guide={FORM_GUIDE.tags} className="space-y-2">
                <TaxonomyPicker
                  kind="genres"
                  label={t("editor.genres")}
                  items={genres.data ?? []}
                  value={draft.genreIds}
                  multiple
                  row
                  onChange={(value) => set("genreIds", value)}
                />
                <TaxonomyPicker
                  kind="themes"
                  label={t("editor.themes")}
                  items={themes.data ?? []}
                  value={draft.themeIds}
                  multiple
                  row
                  onChange={(value) => set("themeIds", value)}
                />
              </EditorPanel>

            </EditorSection>

            {/* The THEM game: gap it dung den ngay, nen thu gon san — bam tieu
                de la mo ra khi can khai bao process/thu muc. */}
            <EditorSection guide={FORM_GUIDE.launch} title={t("detail.files")} collapsible={mode === "add"} defaultOpen={mode !== "add"}>
              {game?.folderWarning ? (
                <div className="flex items-center gap-2 rounded-lg border border-warning/40 bg-warning/10 p-2.5 text-xs text-warning">
                  <AlertTriangle size={14} className="shrink-0" /> {game.folderWarning}
                </div>
              ) : null}

              <EditorPanel className="space-y-2">
                <EditorRow label={t("editor.process")}>
                  <input
                    className={cn(editorInputClass, "h-8 font-mono text-xs")}
                    value={draft.exeName}
                    onChange={(event) => set("exeName", event.target.value)}
                  />
                </EditorRow>
                <EditorRow label={t("editor.hasCustomLaunch")}>
                  <Switch
                    checked={hasCustomLaunch}
                    aria-label={t("editor.hasCustomLaunch")}
                    onCheckedChange={(value) => {
                      setHasCustomLaunch(value);
                      if (!value) set("launchTargetPath", "");
                    }}
                  />
                </EditorRow>
                <EditorRow label={t("editor.launchTarget")}>
                  <input
                    className={cn(editorInputClass, "h-8 font-mono text-xs")}
                    disabled={!hasCustomLaunch}
                    value={draft.launchTargetPath}
                    onChange={(event) => set("launchTargetPath", event.target.value)}
                  />
                  <IconButton
                    size="compact"
                    label={t("editor.pickLaunchTarget")}
                    tone="open"
                    icon={pickLaunchTarget.isPending ? <Loader2 className="animate-spin" /> : <FileSearch />}
                    onClick={() => pickLaunchTarget.mutate()}
                    disabled={!hasCustomLaunch || !canPickFile || pickLaunchTarget.isPending}
                  />
                </EditorRow>
              </EditorPanel>

              {/* Thu muc save khong con sua o day: ban nhap van giu gia tri cu nen
                  Save khong xoa SaveFolderPath da co. */}
              {canBackup && (
                <EditorPanel>
                  <EditorRow label={t("editor.backupFolder")}>
                    <input
                      className={cn(editorInputClass, "h-8 font-mono text-xs")}
                      value={draft.backupFolderPath}
                      onChange={(event) => set("backupFolderPath", event.target.value)}
                    />
                    <IconButton
                      size="compact"
                      label={t("editor.generateBackup")}
                      tone="add"
                      icon={generateBackup.isPending ? <Loader2 className="animate-spin" /> : <FolderPlus />}
                      onClick={() => generateBackup.mutate()}
                      disabled={mode === "add" || draft.isDlc || generateBackup.isPending}
                    />
                    <IconButton
                      size="compact"
                      label={t("editor.pickFolder")}
                      tone="open"
                      icon={<Folder />}
                      onClick={() => pickFolder.mutate("backupFolderPath")}
                      disabled={!canPickFile || pickFolder.isPending}
                    />
                    <IconButton
                      size="compact"
                      label={t("detail.openFolder")}
                      tone="open"
                      icon={<FolderSymlink />}
                      onClick={() => revealFolder.mutate(draft.backupFolderPath)}
                      disabled={!canReveal || !draft.backupFolderPath || revealFolder.isPending}
                    />
                  </EditorRow>
                </EditorPanel>
              )}
            </EditorSection>

              </>
            ) : null}

            <EditorSection
              guide={FORM_GUIDE.images}
              title={t("editor.images")}
              actions={mode === "add" && !imagesOnly ? (
                <IconButton
                  size="compact"
                  tone={metadataLocked ? "neutral" : "edit"}
                  label={t(metadataLocked ? "editor.unlockMetadata" : "editor.lockMetadata")}
                  icon={metadataLocked ? <Lock /> : <LockOpen />}
                  onClick={() => setMetadataLocked((current) => !current)}
                />
              ) : undefined}
            >
              <div className="grid gap-3 sm:grid-cols-[minmax(0,0.8fr)_minmax(0,1.2fr)]">
                {/* Anh doc chi lay tu IGDB; click vao preview de chon o URL va dan anh. */}
                <ImageSlot
                  label={t("editor.poster")}
                  previewAlt={t("editor.poster")}
                  portrait
                  value={imageInputs.posterPath}
                  confirmedValue={draft.posterPath}
                  disabled={metaDisabled}
                  syncing={syncing("portrait-cover")}
                  syncDisabled={sync.isPending || !draft.name.trim()}
                  onValueChange={(value) => setImageInputs((current) => ({ ...current, posterPath: value }))}
                  onConfirm={() => set("posterPath", imageInputs.posterPath.trim())}
                  onRemove={() => {
                    setImageInputs((current) => ({ ...current, posterPath: "" }));
                    set("posterPath", "");
                  }}
                  onSync={() => sync.mutate("portrait-cover")}
                  maxWidth={1056}
                  onPasteImage={(dataUrl) => {
                    setImageInputs((current) => ({ ...current, posterPath: dataUrl }));
                    set("posterPath", dataUrl);
                  }}
                  preview={consolePreview}
                />

              <div className="grid content-start gap-3">
                {([
                  /* Ti le khung xem truoc = ti le cho anh do THUC SU chay:
                     card trong luoi, va hero 41:20 (giong WPF cu). */
                  ["banner", "bannerPath", "card-cover", "aspect-[16/7]", 920],
                  ["backdrop", "backdropPath", "cover", "aspect-[41/20]", 1920],
                ] as const).map(([labelKey, field, source, ratioClass, maxWidth]) => (
                  <ImageSlot
                    key={field}
                    label={t(`editor.${labelKey}`)}
                    previewAlt={t(`editor.${labelKey}`)}
                    value={imageInputs[field]}
                    confirmedValue={draft[field]}
                    previewClassName={ratioClass}
                    disabled={metaDisabled}
                    syncing={syncing(source)}
                    syncDisabled={sync.isPending || !draft.name.trim()}
                    onValueChange={(value) => setImageInputs((current) => ({ ...current, [field]: value }))}
                    onConfirm={() => set(field, imageInputs[field].trim())}
                    onRemove={() => {
                      setImageInputs((current) => ({ ...current, [field]: "" }));
                      set(field, "");
                    }}
                    onSync={() => sync.mutate(source)}
                    maxWidth={maxWidth}
                    onPasteImage={(dataUrl) => {
                      setImageInputs((current) => ({ ...current, [field]: dataUrl }));
                      set(field, dataUrl);
                    }}
                    preview={field === "bannerPath" ? cardPreview : consolePreview}
                  />
                ))}
              </div>
              </div>
            </EditorSection>
          </div>
        </div>

        <OverlayScrollbar targetRef={scrollerRef} />

      {/* Footer kinh mo NOI o day the: noi dung cuon xuong duoi va nhin xuyen
          qua; trang thai sync nam giua hai cum nut thay vi day chung len. */}
        <GlassFooter className="gap-3">
        <div className="flex min-w-9 items-center">
          {mode === "edit" && !imagesOnly ? (
            <IconButton
              tone="danger"
              label={t("editor.disableGame")}
              icon={disableGame.isPending ? <Loader2 className="animate-spin" /> : <Trash2 />}
              onClick={() => setDisableConfirmOpen(true)}
              disabled={disableGame.isPending}
            />
          ) : null}
        </div>
        <div className="flex min-w-0 flex-1 justify-center">
          {syncStatusMessage ? (
            <div role="status" aria-live="polite" className="flex min-w-0 items-center gap-2 text-xs font-medium text-muted-fg">
              <Loader2 size={14} className="shrink-0 animate-spin text-primary" />
              <span className="truncate">{syncStatusMessage}</span>
            </div>
          ) : null}
        </div>
        <div data-guide={FORM_GUIDE.save} className="flex items-center gap-2">
          <IconButton
            label={t(mode === "edit" ? "action.back" : "action.cancel")}
            icon={<ArrowLeft />}
            onClick={() => (dirty ? setPendingExit("cancel") : onCancel())}
          />
          <IconButton
            type="submit"
            tone="add"
            label={t("action.save")}
            icon={save.isPending ? <Loader2 className="animate-spin" /> : <Check />}
            disabled={referencesPending || save.isPending || !draft.name.trim()}
          />
        </div>
        </GlassFooter>

        <GuideTour
          open={formGuide.open}
          steps={formGuide.steps}
          eyebrow={formGuide.eyebrow}
          containerRef={guideContainerRef}
          onClose={formGuide.close}
        />
      </div>

      <OwnershipInfoDialog
        open={ownershipOpen && ownershipInfoAvailable}
        onOpenChange={setOwnershipOpen}
        layout={pickCatalog(ownership.data, draft.ownershipId)?.infoLayout}
        publisher={draft.publisher}
        values={{
          steamAccount: draft.steamAccount,
          steamPassword: draft.steamPassword,
          ubisoftAccount: draft.ubisoftAccount,
          ubisoftPassword: draft.ubisoftPassword,
          supportDocumentUrl: draft.supportDocumentUrl,
          downloadUrl: draft.downloadUrl,
          note: draft.note,
        }}
        onChange={(patch) => setDraft((current) => ({ ...current, ...patch }))}
      />

      <ConfirmDialog
        open={disableConfirmOpen}
        onOpenChange={setDisableConfirmOpen}
        title={t("editor.disableGame")}
        description={t("editor.disableConfirm")}
        confirmLabel={t("editor.disableGame")}
        onConfirm={() => disableGame.mutate()}
      />

      {/* Con thay doi chua luu ma dong the: hoi truoc, khong am tham vut di. */}
      <Dialog
        open={pendingExit !== null}
        onOpenChange={(open) => { if (!open) setPendingExit(null); }}
      >
        <DialogContent className="max-w-sm">
          <div className="space-y-2 p-5 pr-14">
            <DialogTitle>{t("editor.unsavedTitle")}</DialogTitle>
            <DialogDescription>{t("editor.unsavedDescription")}</DialogDescription>
          </div>
          <DialogFooter>
            <IconButton
              tone="danger"
              label={t("editor.discardChanges")}
              icon={<X />}
              disabled={save.isPending}
              onClick={() => {
                const exit = pendingExit;
                setPendingExit(null);
                if (exit === "close") (onClose ?? onCancel)();
                else onCancel();
              }}
            />
            <IconButton
              tone="add"
              label={t("action.save")}
              icon={save.isPending ? <Loader2 className="animate-spin" /> : <Save />}
              disabled={referencesPending || save.isPending || !draft.name.trim()}
              onClick={() => save.mutate()}
            />
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </form>
  );
}
