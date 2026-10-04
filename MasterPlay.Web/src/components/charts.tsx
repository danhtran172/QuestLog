/**
 * Manh ghep ve bieu do dung chung giua Dashboard thu vien va Admin dashboard.
 *
 * Tach ra tu `DashboardView.tsx` khi lam Admin dashboard (spec 2026-09-14 §12.8).
 * Hai dashboard tra loi hai cau hoi khac han nhau nen KHONG dung chung so lieu —
 * chi dung chung cach ve.
 *
 * Ve bang SVG/CSS thuan, khong keo them thu vien chart: cac dang can dung deu don
 * gian, va lam vay thi mau sac di dung he token cua app. Nho do Admin Console tu
 * doi sang thang xanh duong ma khong phai sua gi o day.
 */
import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

export function Panel({
  title,
  icon,
  children,
  className,
  hint,
}: {
  title: string;
  icon?: ReactNode;
  children: ReactNode;
  className?: string;
  hint?: string;
}) {
  return (
    <section className={cn("surface-card flex min-w-0 flex-col p-4", className)}>
      <header className="mb-3 flex items-baseline justify-between gap-3">
        <h3 className="flex items-center gap-1.5 text-xs font-black uppercase tracking-[0.14em] text-muted-fg">
          {icon}
          {title}
        </h3>
        {hint ? <span className="shrink-0 text-[11px] text-muted-fg/70">{hint}</span> : null}
      </header>
      <div className="min-w-0 flex-1">{children}</div>
    </section>
  );
}

export function Empty({ label }: { label: string }) {
  return <p className="py-6 text-center text-sm text-muted-fg">{label}</p>;
}

/** Bieu do cot dung. Nhan truc X la nam / diem, so nam tren dinh cot. */
export function ColumnChart({
  data,
  emptyLabel,
}: {
  data: { label: string; value: number }[];
  emptyLabel: string;
}) {
  if (data.length === 0) return <Empty label={emptyLabel} />;
  const max = Math.max(...data.map((d) => d.value), 1);

  return (
    <div className="flex h-40 items-end gap-1.5 overflow-x-auto pb-1">
      {data.map((d) => (
        <div key={d.label} className="flex min-w-8 flex-1 flex-col items-center gap-1">
          <span className="text-[11px] font-black tabular-nums text-fg">{d.value}</span>
          {/* Chieu cao theo ti le voi coc lon nhat; toi thieu 3px de coc gia tri
              nho van nhin thay duoc. */}
          <span
            className="w-full rounded-t-[3px] bg-gradient-to-t from-primary/45 to-primary"
            style={{ height: `${Math.max(3, (d.value / max) * 100)}%` }}
          />
          <span className="w-full truncate text-center text-[10px] font-semibold text-muted-fg">
            {d.label}
          </span>
        </div>
      ))}
    </div>
  );
}

/** Bang xep hang dang cot ngang — dung cho the loai / series / hang. */
export function RankedBars({
  data,
  emptyLabel,
}: {
  data: { name: string; count: number }[];
  emptyLabel: string;
}) {
  if (data.length === 0) return <Empty label={emptyLabel} />;
  const max = Math.max(...data.map((d) => d.count), 1);

  return (
    <ul className="space-y-1.5">
      {data.map((d) => (
        <li key={d.name} className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-2">
          <span className="relative h-5 min-w-0 overflow-hidden rounded-md bg-surface-2/60">
            <span
              className="absolute inset-y-0 left-0 rounded-md bg-primary/30"
              style={{ width: `${(d.count / max) * 100}%` }}
            />
            <span className="relative flex h-full items-center truncate px-2 text-[11px] font-bold text-fg">
              {d.name}
            </span>
          </span>
          <span className="w-7 text-right text-[11px] font-black tabular-nums text-muted-fg">
            {d.count}
          </span>
        </li>
      ))}
    </ul>
  );
}
