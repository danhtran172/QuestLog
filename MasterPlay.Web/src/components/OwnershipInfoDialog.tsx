/**
 * Popup THONG TIN SO HUU — dung chung cho trang xem va trang sua.
 *
 * Bo cuc doi theo kieu so huu:
 *   - Dung chung tai khoan  -> khung "Steam Account" (+ "Ubisoft Account" neu
 *                              nha phat hanh la Ubisoft) + khung Document/Note.
 *   - Ban tai ve            -> khung "Download": Link + Note.
 *   - Kieu khac             -> chi khung Document/Note.
 *
 * Khung nao CO DU LIEU thi van hien du bo cuc khong goi ten no, de khong bao
 * gio co du lieu bi khoa lai khong sua/khong xem duoc.
 */
import { Copy, SquareArrowOutUpRight } from "lucide-react";
import type { ReactNode } from "react";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { editorInputClass } from "./GameEditorControls";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogTitle,
  IconButton,
  Tooltip,
} from "./ui";

export type OwnershipInfoValues = {
  steamAccount: string;
  steamPassword: string;
  ubisoftAccount: string;
  ubisoftPassword: string;
  supportDocumentUrl: string;
  downloadUrl: string;
  note: string;
};

/**
 * `systemKey` cua Ownership.
 *
 * CHAP NHAN CA HAI CACH VIET vi backend va hop dong dang khong khop nhau:
 * `openapi.yaml` khai bao enum la `Shared` / `Picklocked` va ghi ro
 * `SteamOff` / `Pirated` la SAI, nhung `MasterPlay.Host/LibraryQuery.cs:31`
 * van tra ve `SteamOff` / `Pirated` cho id 6 va 2. Nhan ca hai thi man hinh
 * chay dung o ca truoc lan sau khi backend duoc chinh lai.
 */
const SHARED_ACCOUNT_KEYS = ["Shared", "SteamOff"];
const DOWNLOAD_KEYS = ["Picklocked", "Pirated"];

/**
 * Nut ⓘ CHI hien voi hai kieu so huu nay — cac kieu con lai khong co gi de
 * xem. Dung chung cho trang xem va trang sua de hai noi khong lech nhau.
 */
export function ownershipHasInfo(systemKey?: string | null): boolean {
  const key = systemKey ?? "";
  return SHARED_ACCOUNT_KEYS.includes(key) || DOWNLOAD_KEYS.includes(key);
}

function isUbisoftPublisher(publisher?: string | null): boolean {
  return /ubisoft/i.test(publisher ?? "");
}

function Group({
  title,
  children,
}: {
  title?: string;
  children: ReactNode;
}) {
  return (
    <section className="rounded-xl border border-border bg-surface-2/30 p-3 backdrop-blur-sm">
      {title ? (
        <h4 className="mb-2 text-[11px] font-black uppercase tracking-[0.14em] text-muted-fg">
          {title}
        </h4>
      ) : null}
      <div className="space-y-2">{children}</div>
    </section>
  );
}

function Field({
  label,
  children,
}: {
  label: string;
  children: ReactNode;
}) {
  return (
    <label className="grid gap-1 text-[11px] font-bold text-muted-fg">
      {label}
      {children}
    </label>
  );
}

function ReadValue({
  value,
  mono = false,
  action,
}: {
  value: string;
  mono?: boolean;
  action?: ReactNode;
}) {
  return (
    <span className="flex min-w-0 items-center gap-1.5">
      <Tooltip label={value} className="max-w-80 break-all font-mono font-medium">
        <span className={cn("min-w-0 flex-1 truncate text-sm font-semibold text-fg", mono && "font-mono text-xs")}>
          {value}
        </span>
      </Tooltip>
      {action}
    </span>
  );
}

export function OwnershipInfoDialog({
  open,
  onOpenChange,
  ownershipKey,
  publisher,
  values,
  onChange,
  onCopy,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  ownershipKey?: string | null;
  publisher?: string | null;
  values: OwnershipInfoValues;
  /** Co ham nay = SUA duoc; khong co = chi xem. */
  onChange?: (patch: Partial<OwnershipInfoValues>) => void;
  onCopy?: (value: string) => void;
}) {
  const { t } = useI18n();
  const editable = Boolean(onChange);

  const sharedLayout = SHARED_ACCOUNT_KEYS.includes(ownershipKey ?? "");
  const downloadLayout = DOWNLOAD_KEYS.includes(ownershipKey ?? "");

  const has = (value: string) => value.trim().length > 0;
  const showSteam = sharedLayout || has(values.steamAccount) || has(values.steamPassword);
  const showUbisoft =
    (sharedLayout && isUbisoftPublisher(publisher)) ||
    has(values.ubisoftAccount) ||
    has(values.ubisoftPassword);
  const showDownload = downloadLayout || has(values.downloadUrl);
  const showDocument = !downloadLayout || has(values.supportDocumentUrl);

  const openLink = (url: string) => window.open(url, "_blank", "noopener,noreferrer");

  const accountGroup = (
    title: string,
    accountKey: "steamAccount" | "ubisoftAccount",
    passwordKey: "steamPassword" | "ubisoftPassword",
  ) => {
    const account = values[accountKey];
    const password = values[passwordKey];
    if (!editable && !has(account) && !has(password)) return null;
    return (
      <Group title={title}>
        {editable || has(account) ? (
          <Field label={t("detail.account")}>
            {editable ? (
              <input
                className={cn(editorInputClass, "h-8")}
                value={account}
                onChange={(event) => onChange?.({ [accountKey]: event.target.value })}
              />
            ) : (
              <ReadValue
                value={account}
                action={onCopy ? (
                  <IconButton
                    size="compact"
                    tone="edit"
                    label={t("detail.copyValue")}
                    icon={<Copy />}
                    onClick={() => onCopy(account)}
                  />
                ) : undefined}
              />
            )}
          </Field>
        ) : null}
        {editable || has(password) ? (
          <Field label={t("detail.password")}>
            {editable ? (
              <input
                type="password"
                className={cn(editorInputClass, "h-8")}
                value={password}
                onChange={(event) => onChange?.({ [passwordKey]: event.target.value })}
              />
            ) : (
              <ReadValue
                value={password}
                mono
                action={onCopy ? (
                  <IconButton
                    size="compact"
                    tone="edit"
                    label={t("detail.copyValue")}
                    icon={<Copy />}
                    onClick={() => onCopy(password)}
                  />
                ) : undefined}
              />
            )}
          </Field>
        ) : null}
      </Group>
    );
  };

  const linkField = (
    label: string,
    key: "supportDocumentUrl" | "downloadUrl",
    openLabel: string,
  ) => {
    const value = values[key];
    if (!editable && !has(value)) return null;
    return (
      <Field label={label}>
        {editable ? (
          <span className="flex min-w-0 items-center gap-1.5">
            <input
              className={cn(editorInputClass, "h-8 text-xs")}
              value={value}
              onChange={(event) => onChange?.({ [key]: event.target.value })}
            />
            <IconButton
              size="compact"
              tone="open"
              label={openLabel}
              icon={<SquareArrowOutUpRight />}
              onClick={() => has(value) && openLink(value)}
              disabled={!has(value)}
            />
          </span>
        ) : (
          <ReadValue
            value={value}
            mono
            action={
              <IconButton
                size="compact"
                tone="open"
                label={openLabel}
                icon={<SquareArrowOutUpRight />}
                onClick={() => openLink(value)}
              />
            }
          />
        )}
      </Field>
    );
  };

  const noteField = (!editable && !has(values.note)) ? null : (
    <Field label={t("editor.note")}>
      {editable ? (
        <textarea
          className="min-h-20 rounded-xl border border-border/80 bg-surface/55 p-3 text-sm outline-none backdrop-blur-md transition focus:border-primary/80"
          value={values.note}
          onChange={(event) => onChange?.({ note: event.target.value })}
        />
      ) : (
        <p className="whitespace-pre-wrap text-sm font-medium leading-relaxed text-fg">
          {values.note}
        </p>
      )}
    </Field>
  );

  const steamGroup = showSteam ? accountGroup(t("detail.steamAccount"), "steamAccount", "steamPassword") : null;
  const ubisoftGroup = showUbisoft ? accountGroup(t("detail.ubisoftAccount"), "ubisoftAccount", "ubisoftPassword") : null;

  const downloadRow = showDownload
    ? linkField(t("ownership.link"), "downloadUrl", t("detail.openDownload"))
    : null;
  const documentRow = showDocument
    ? linkField(t("ownership.document"), "supportDocumentUrl", t("detail.openSupport"))
    : null;

  /* Note di kem khung cuoi cung: khung Download neu la ban tai ve, con lai thi
     nam cung Document — dung nhu template. */
  const extrasGroup = (downloadRow || documentRow || noteField) ? (
    <Group title={downloadLayout ? t("ownership.download") : undefined}>
      {downloadRow}
      {documentRow}
      {noteField}
    </Group>
  ) : null;

  const empty = !steamGroup && !ubisoftGroup && !extrasGroup;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <div className="max-h-[80vh] space-y-3 overflow-y-auto p-5 pr-14">
          <div>
            <DialogTitle>{t("detail.ownershipInfo")}</DialogTitle>
            <DialogDescription>{t("ownership.description")}</DialogDescription>
          </div>
          {empty ? (
            <p className="rounded-xl border border-border bg-surface-2/30 p-4 text-center text-xs font-semibold text-muted-fg">
              {t("ownership.empty")}
            </p>
          ) : (
            <>
              {steamGroup}
              {ubisoftGroup}
              {extrasGroup}
            </>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
