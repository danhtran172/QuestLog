import { CalendarDays } from "lucide-react";
import {
  FINISH_MONTHS,
  FINISH_YEAR_MIN,
  finishYearMax,
  formatFinishTimeDraft,
  isFinishYearDraft,
  type FinishTimeDraft,
} from "@/lib/finishTime";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { NUMBER_INPUT_NO_SPINNER } from "./InlineEditControls";

const fieldClass =
  "h-9 rounded-lg border border-border bg-surface px-3 text-sm font-medium text-fg outline-none transition focus:border-primary";

/**
 * O nhap thoi diem hoan thanh: NAM bat buoc, THANG tuy chon.
 *
 * Kem mot dong xem truoc dung chuoi se hien tren pill. Khong co dong do thi
 * "bo trong thang" trong nhu mot o chua dien xong; co no thi nguoi dung thay
 * ngay rang `2019` la mot ket qua hop le, khong phai loi.
 */
export function FinishTimeFields({
  draft,
  onChange,
  disabled,
  autoFocus,
}: {
  draft: FinishTimeDraft;
  onChange: (draft: FinishTimeDraft) => void;
  disabled?: boolean;
  autoFocus?: boolean;
}) {
  const { t } = useI18n();
  const preview = formatFinishTimeDraft(draft);

  return (
    <div className="grid gap-2">
      <div className="grid grid-cols-[minmax(0,1fr)_minmax(0,1.3fr)] gap-2">
        <label className="grid gap-1.5 text-xs font-bold text-muted-fg">
          {t("finish.year")}
          <input
            autoFocus={autoFocus}
            required
            inputMode="numeric"
            type="text"
            maxLength={4}
            min={FINISH_YEAR_MIN}
            max={finishYearMax()}
            placeholder={String(finishYearMax() - 1)}
            value={draft.year}
            disabled={disabled}
            onChange={(event) => {
              if (isFinishYearDraft(event.target.value)) {
                onChange({ ...draft, year: event.target.value });
              }
            }}
            onFocus={(event) => event.currentTarget.select()}
            className={cn(NUMBER_INPUT_NO_SPINNER, fieldClass, "tabular-nums")}
          />
        </label>
        <label className="grid gap-1.5 text-xs font-bold text-muted-fg">
          {t("finish.month")}
          {/* `<select>` native chu khong phai Select cua Radix: o nay nam trong
              mot Dialog da khoa cuon, va menu native khong bi anh huong. */}
          <select
            value={draft.month}
            disabled={disabled}
            onChange={(event) => onChange({ ...draft, month: event.target.value })}
            className={cn(fieldClass, "tabular-nums")}
          >
            <option value="">{t("finish.monthUnknown")}</option>
            {FINISH_MONTHS.map((month) => (
              <option key={month} value={month}>{month}</option>
            ))}
          </select>
        </label>
      </div>

      <p className="flex items-center gap-1.5 text-xs font-bold text-muted-fg">
        <CalendarDays size={13} className="shrink-0 text-fuchsia-400" />
        {t("finish.preview")}
        <span className="rounded-md border border-border/70 bg-surface-2/70 px-1.5 py-0.5 tabular-nums text-fg">
          {preview ?? "—"}
        </span>
      </p>
    </div>
  );
}
