/**
 * Chuong thong bao game trong thu vien sap ra mat. DOMAIN_RULES §22.
 *
 * Server quyet dinh moc nao hien (con 1..3 ngay / dung ngay) theo mui gio trinh
 * duyet gui len; o day chi render, format `releaseAt` theo `timeZone` cua feed,
 * va bao "da xem" khi nguoi dung mo danh sach.
 */
import { useEffect, useRef, useState } from "react";
import { Bell, CalendarClock, PartyPopper, RotateCcw } from "lucide-react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { cn } from "@/lib/utils";
import { useI18n, type Lang } from "@/lib/i18n";
import {
  browserTimeZone,
  releaseNotificationsApi,
  type ReleaseNotification,
} from "@/lib/releaseNotificationsApi";
import { IconButton, Popover, PopoverContent, PopoverTrigger } from "@/components/ui";

const QUERY_KEY = "release-notifications";
// Du de qua nua dem thi moc "hom nay" hien ma khong can tai lai trang.
const REFETCH_MS = 15 * 60 * 1000;

function formatReleaseAt(releaseAt: string, timeZone: string, lang: Lang): string {
  const options: Intl.DateTimeFormatOptions = {
    weekday: "short",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  };
  try {
    return new Intl.DateTimeFormat(lang, { ...options, timeZone }).format(new Date(releaseAt));
  } catch {
    // `timeZone` la ten Windows (server tu chon khi trinh duyet khong gui) — Intl khong
    // hieu, dung gio cua trinh duyet. Ngay va gio van dung moc UTC server tra ve.
    return new Intl.DateTimeFormat(lang, options).format(new Date(releaseAt));
  }
}

function stageText(item: ReleaseNotification, t: (key: string) => string): string {
  if (item.stage === "today") return t("releaseNotify.today");
  return t(item.daysLeft === 1 ? "releaseNotify.inOneDay" : "releaseNotify.inDays").replace(
    "{days}",
    String(item.daysLeft),
  );
}

export function ReleaseNotificationsBell({ onOpenGame }: { onOpenGame: (gameId: string) => void }) {
  const { t, lang } = useI18n();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [timeZone] = useState(browserTimeZone);
  // Thong bao chua doc luc MO danh sach — giu dau "moi" trong lan mo nay du server da ghi da xem.
  const [freshIds, setFreshIds] = useState<ReadonlySet<string>>(new Set());
  const toastedIds = useRef(new Set<string>());

  const feed = useQuery({
    queryKey: [QUERY_KEY, timeZone],
    queryFn: () => releaseNotificationsApi.feed(timeZone),
    refetchInterval: REFETCH_MS,
  });

  const markRead = useMutation({
    mutationFn: releaseNotificationsApi.markRead,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [QUERY_KEY] }),
    onError: (error) => toast.error(error instanceof Error ? error.message : String(error)),
  });

  const items = feed.data?.items ?? [];
  const unreadCount = feed.data?.unreadCount ?? 0;

  // Toast mot lan cho moi thong bao chua doc trong phien nay; doc that su khi mo chuong.
  useEffect(() => {
    if (!feed.data) return;
    const unseen = feed.data.items.filter((item) => !item.read && !toastedIds.current.has(item.id));
    if (unseen.length === 0) return;
    unseen.forEach((item) => toastedIds.current.add(item.id));

    const first = unseen[0];
    const title = unseen.length === 1
      ? first.gameName
      : t("releaseNotify.toastMany").replace("{count}", String(unseen.length));
    const description = unseen.length === 1
      ? `${stageText(first, t)} · ${formatReleaseAt(first.releaseAt, feed.data.timeZone, lang)}`
      : unseen.map((item) => item.gameName).join(", ");
    toast(title, {
      icon: first.stage === "today" ? <PartyPopper size={16} /> : <CalendarClock size={16} />,
      description,
      duration: 10_000,
      action: { label: t("releaseNotify.view"), onClick: () => handleOpenChange(true) },
    });
  }, [feed.data, lang, t]);

  const handleOpenChange = (nextOpen: boolean) => {
    if (nextOpen) {
      setFreshIds(new Set());
      markRead.reset();
    }
    setOpen(nextOpen);
  };

  // Dang mo (ke ca mo tu nut "Xem" cua toast) thi moi thong bao chua doc deu tinh la da xem.
  // markRead cho refetch xong moi het pending, nen feed cu khong gui lai lan hai. Loi thi
  // dung (toast da bao), lan mo sau thu lai — khong lap goi lien tuc.
  const { isPending: markingRead, isError: markReadFailed, mutate: sendRead } = markRead;
  useEffect(() => {
    if (!open || !feed.data || markingRead || markReadFailed) return;
    const unread = feed.data.items.filter((item) => !item.read).map((item) => item.id);
    if (unread.length === 0) return;
    setFreshIds((current) => new Set([...current, ...unread]));
    sendRead(unread);
  }, [open, feed.data, markingRead, markReadFailed, sendRead]);

  const label = unreadCount > 0
    ? `${t("releaseNotify.title")} (${unreadCount})`
    : t("releaseNotify.title");

  return (
    <Popover open={open} onOpenChange={handleOpenChange}>
      <div className="relative shrink-0">
        <PopoverTrigger asChild>
          <IconButton label={label} tone="notify" size="compact" tooltipSide="top" icon={<Bell />} />
        </PopoverTrigger>
        {unreadCount > 0 ? (
          <span className="pointer-events-none absolute -right-1 -top-1 z-10 grid min-h-4 min-w-4 place-items-center rounded-full bg-primary px-1 text-fs-9 font-black leading-none text-primary-fg shadow-sm ring-1 ring-bg">
            {unreadCount}
          </span>
        ) : null}
      </div>
      <PopoverContent align="end" side="top" className="w-80 p-2">
        <div className="mb-1.5 flex items-center justify-between gap-2 px-1.5 pt-0.5">
          <p className="text-sm font-extrabold text-fg">{t("releaseNotify.title")}</p>
          {/* Mui gio chi co nghia khi co moc gio de doc; panel trong thi khong hien. */}
          {feed.data && items.length > 0 ? (
            <span className="truncate text-fs-11 font-medium text-muted-fg">{feed.data.timeZone}</span>
          ) : null}
        </div>

        {feed.isPending ? (
          <p className="px-1.5 py-3 text-sm text-muted-fg">{t("releaseNotify.loading")}</p>
        ) : feed.isError ? (
          <div className="flex items-start justify-between gap-2 rounded-lg border border-danger/30 bg-danger/10 px-2.5 py-2">
            <p className="min-w-0 text-xs font-semibold text-fg">
              {feed.error instanceof Error ? feed.error.message : String(feed.error)}
            </p>
            <IconButton
              label={t("releaseNotify.retry")}
              icon={<RotateCcw />}
              size="compact"
              onClick={() => void feed.refetch()}
            />
          </div>
        ) : items.length === 0 ? (
          <p className="px-1.5 py-3 text-sm text-muted-fg">{t("releaseNotify.empty")}</p>
        ) : (
          <ul className="max-h-96 space-y-1 overflow-y-auto">
            {items.map((item) => {
              const image = item.bannerPath ?? item.posterPath;
              const fresh = freshIds.has(item.id);
              return (
                <li key={item.id}>
                  <button
                    type="button"
                    onClick={() => {
                      setOpen(false);
                      onOpenGame(item.gameId);
                    }}
                    className={cn(
                      "flex w-full items-center gap-2.5 rounded-lg px-2 py-2 text-left outline-none transition-colors",
                      "hover:bg-surface-2/70 focus-visible:ring-2 focus-visible:ring-primary",
                      fresh && "bg-primary/10",
                    )}
                  >
                    <span className="relative h-10 w-16 shrink-0 overflow-hidden rounded-md bg-surface-2">
                      {image ? (
                        <img src={image} alt="" loading="lazy" className="h-full w-full object-cover" />
                      ) : (
                        <CalendarClock size={16} className="absolute inset-0 m-auto text-muted-fg" aria-hidden="true" />
                      )}
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="flex items-center gap-1.5">
                        {fresh ? (
                          <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-primary" aria-label={t("releaseNotify.new")} />
                        ) : null}
                        <span className="truncate text-sm font-bold text-fg">{item.gameName}</span>
                      </span>
                      <span
                        className={cn(
                          "block truncate text-xs font-semibold",
                          item.stage === "today" ? "text-primary" : "text-muted-fg",
                        )}
                      >
                        {stageText(item, t)}
                      </span>
                      <span className="block truncate text-fs-11 text-muted-fg">
                        {formatReleaseAt(item.releaseAt, feed.data.timeZone, lang)}
                      </span>
                    </span>
                  </button>
                </li>
              );
            })}
          </ul>
        )}
      </PopoverContent>
    </Popover>
  );
}
