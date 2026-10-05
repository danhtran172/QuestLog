/**
 * UI primitives — dung Radix (cung bo voi ban Lovable) de hanh vi
 * dialog/menu/focus-trap/ESC khop hoan toan.
 *
 * Chi giu component THUC SU dung. Ban Lovable keo ~60 file shadcn ma dung
 * chua toi mot nua (T18) — o day khong lap lai chuyen do.
 */
import * as DialogPrimitive from "@radix-ui/react-dialog";
import * as DropdownPrimitive from "@radix-ui/react-dropdown-menu";
import * as PopoverPrimitive from "@radix-ui/react-popover";
import * as SelectPrimitive from "@radix-ui/react-select";
import * as SwitchPrimitive from "@radix-ui/react-switch";
import * as TooltipPrimitive from "@radix-ui/react-tooltip";
import { Check, ChevronDown, X } from "lucide-react";
import {
  forwardRef,
  useId,
  type ComponentProps,
  type CSSProperties,
  type FocusEvent,
  type ReactElement,
  type ReactNode,
} from "react";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";

/* ── Dialog ── */

export const Dialog = DialogPrimitive.Root;
export const DialogTrigger = DialogPrimitive.Trigger;

export function DialogContent({
  className,
  children,
  showClose = false,
  overlayClassName,
  ...props
}: ComponentProps<typeof DialogPrimitive.Content> & {
  showClose?: boolean;
  overlayClassName?: string;
}) {
  const { t } = useI18n();

  return (
    <DialogPrimitive.Portal>
      <DialogPrimitive.Overlay
        className={cn(
          "fixed inset-0 z-50 bg-black/55 backdrop-blur-[2px] data-[state=closed]:animate-out data-[state=closed]:fade-out data-[state=open]:animate-in data-[state=open]:fade-in",
          overlayClassName,
        )}
      />
      <DialogPrimitive.Content
        className={cn(
          "surface-card fixed left-1/2 top-1/2 z-50 w-[92vw] max-w-lg -translate-x-1/2 -translate-y-1/2",
          "data-[state=closed]:animate-out data-[state=closed]:fade-out data-[state=closed]:zoom-out-95",
          "data-[state=open]:animate-in data-[state=open]:fade-in data-[state=open]:zoom-in-95 duration-200",
          className,
        )}
        {...props}
      >
        {/* Duong xi xoe o hai goc duoi moi khi dialog mo. Dat o day de moi
            dialog trong app deu co, khong phai nho tung cho mot. */}
        {children}
        {showClose && (
          <DialogPrimitive.Close asChild>
            <IconButton
              label={t("action.close")}
              icon={<X />}
              size="compact"
              className="absolute right-3 top-3"
            />
          </DialogPrimitive.Close>
        )}
      </DialogPrimitive.Content>
    </DialogPrimitive.Portal>
  );
}

export function DialogHeader({ className, ...props }: ComponentProps<"div">) {
  return <div className={cn("flex flex-col gap-1", className)} {...props} />;
}

export function DialogTitle({ className, ...props }: ComponentProps<typeof DialogPrimitive.Title>) {
  return (
    <DialogPrimitive.Title
      className={cn("rune-etch text-base font-bold", className)}
      {...props}
    />
  );
}

export function DialogDescription({
  className,
  ...props
}: ComponentProps<typeof DialogPrimitive.Description>) {
  return (
    <DialogPrimitive.Description
      className={cn("text-xs text-muted-fg", className)}
      {...props}
    />
  );
}

export function DialogFooter({ className, ...props }: ComponentProps<"div">) {
  return (
    <div
      className={cn("flex items-center justify-end gap-2 border-t border-border px-5 py-3", className)}
      {...props}
    />
  );
}

/**
 * Thanh nut o DAY the / hop thoai lon: kinh mo NOI len noi dung, noi dung cuon
 * xuong ben duoi va nhin xuyen qua no. Cha phai `relative`, va vung cuon phai
 * chua san le duoi (`pb-16` tro len) de hang cuoi khong nam ket sau thanh nay.
 */
export function GlassFooter({ className, ...props }: ComponentProps<"footer">) {
  return (
    <footer
      className={cn(
        "absolute inset-x-0 bottom-0 z-30 flex min-h-[52px] items-center gap-2 px-4 py-2",
        "border-t border-border/60 bg-surface/45 backdrop-blur-xl backdrop-saturate-150",
        "shadow-[0_-14px_32px_-22px_color-mix(in_oklab,black_75%,transparent)]",
        className,
      )}
      {...props}
    />
  );
}

/* ── Dropdown menu ── */

export const DropdownMenu = DropdownPrimitive.Root;
export const DropdownMenuTrigger = DropdownPrimitive.Trigger;

export function DropdownMenuContent({
  className,
  ...props
}: ComponentProps<typeof DropdownPrimitive.Content>) {
  return (
    <DropdownPrimitive.Portal>
      <DropdownPrimitive.Content
        sideOffset={6}
        className={cn(
          "surface-card z-50 min-w-52 overflow-hidden p-1.5",
          "data-[state=closed]:animate-out data-[state=closed]:fade-out data-[state=closed]:zoom-out-95",
          "data-[state=open]:animate-in data-[state=open]:fade-in data-[state=open]:zoom-in-95 duration-150",
          className,
        )}
        {...props}
      />
    </DropdownPrimitive.Portal>
  );
}

export function DropdownMenuItem({
  className,
  ...props
}: ComponentProps<typeof DropdownPrimitive.Item>) {
  return (
    <DropdownPrimitive.Item
      className={cn(
        "flex cursor-pointer select-none items-center gap-2 rounded-lg px-2.5 py-2 text-sm font-semibold outline-none transition-colors",
        "text-muted-fg data-[highlighted]:bg-surface-2 data-[highlighted]:text-fg",
        className,
      )}
      {...props}
    />
  );
}

export function DropdownMenuSeparator() {
  return <DropdownPrimitive.Separator className="my-1 h-px bg-border" />;
}

/* ── Popover ── */

export const Popover = PopoverPrimitive.Root;
export const PopoverAnchor = PopoverPrimitive.Anchor;
export const PopoverTrigger = PopoverPrimitive.Trigger;

export function PopoverContent({
  className,
  ...props
}: ComponentProps<typeof PopoverPrimitive.Content>) {
  return (
    <PopoverPrimitive.Portal>
      <PopoverPrimitive.Content
        sideOffset={6}
        className={cn(
          "surface-card z-50 p-1.5",
          "data-[state=closed]:animate-out data-[state=closed]:fade-out data-[state=closed]:zoom-out-95",
          "data-[state=open]:animate-in data-[state=open]:fade-in data-[state=open]:zoom-in-95 duration-150",
          className,
        )}
        {...props}
      />
    </PopoverPrimitive.Portal>
  );
}

/* ── Select (thay cho <select> native — native trong khac han) ── */

export const Select = SelectPrimitive.Root;
export const SelectValue = SelectPrimitive.Value;

export function SelectTrigger({
  className,
  children,
  ...props
}: ComponentProps<typeof SelectPrimitive.Trigger>) {
  return (
    <SelectPrimitive.Trigger
      className={cn(
        "inline-flex items-center justify-between gap-1.5 text-sm font-medium outline-none",
        "data-[placeholder]:text-muted-fg",
        className,
      )}
      {...props}
    >
      {children}
      <SelectPrimitive.Icon asChild>
        <ChevronDown size={14} className="shrink-0 opacity-60" />
      </SelectPrimitive.Icon>
    </SelectPrimitive.Trigger>
  );
}

export function SelectContent({
  className,
  children,
  ...props
}: ComponentProps<typeof SelectPrimitive.Content>) {
  return (
    <SelectPrimitive.Portal>
      <SelectPrimitive.Content
        position="popper"
        sideOffset={6}
        className={cn(
          "surface-card z-50 overflow-hidden p-1.5",
          "data-[state=closed]:animate-out data-[state=closed]:fade-out",
          "data-[state=open]:animate-in data-[state=open]:fade-in duration-150",
          className,
        )}
        {...props}
      >
        <SelectPrimitive.Viewport>{children}</SelectPrimitive.Viewport>
      </SelectPrimitive.Content>
    </SelectPrimitive.Portal>
  );
}

export function SelectItem({
  className,
  children,
  ...props
}: ComponentProps<typeof SelectPrimitive.Item>) {
  return (
    <SelectPrimitive.Item
      className={cn(
        "flex cursor-pointer select-none items-center justify-between gap-2 rounded-lg px-2.5 py-1.5 text-sm font-semibold outline-none",
        "text-muted-fg data-[highlighted]:bg-surface-2 data-[highlighted]:text-fg data-[state=checked]:text-fg",
        className,
      )}
      {...props}
    >
      <SelectPrimitive.ItemText>{children}</SelectPrimitive.ItemText>
      <SelectPrimitive.ItemIndicator>
        <Check size={13} className="text-primary" />
      </SelectPrimitive.ItemIndicator>
    </SelectPrimitive.Item>
  );
}

/* ── Switch ── */

export function Switch({ className, ...props }: ComponentProps<typeof SwitchPrimitive.Root>) {
  return (
    <SwitchPrimitive.Root
      className={cn(
        "relative inline-flex h-6 w-11 shrink-0 items-center rounded-full border transition-all",
        "border-muted-fg/80 bg-muted-fg/30 shadow-inner",
        "data-[state=checked]:border-primary data-[state=checked]:bg-primary data-[state=checked]:shadow-[0_0_0_1px_color-mix(in_oklab,var(--color-primary)_45%,transparent)]",
        "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2 focus-visible:ring-offset-bg",
        "disabled:cursor-not-allowed disabled:opacity-45",
        className,
      )}
      {...props}
    >
      <SwitchPrimitive.Thumb className="block h-4 w-4 translate-x-1 rounded-full bg-fg shadow-md ring-1 ring-black/15 transition-all data-[state=checked]:translate-x-6 data-[state=checked]:bg-primary-fg" />
    </SwitchPrimitive.Root>
  );
}

/* ── Tooltip + action button icon-only ── */

export function TooltipProvider({ children }: { children: ReactNode }) {
  return (
    <TooltipPrimitive.Provider delayDuration={250} skipDelayDuration={80}>
      {children}
    </TooltipPrimitive.Provider>
  );
}

/**
 * Radix mo tooltip cho MOI lan focus, ke ca focus do code goi. Popup vua mo la
 * Dialog tu day focus vao nut dau tien; popup vua dong la focus quay ve nut da
 * mo no — ca hai lan tooltip deu bat len va nam do cho toi khi mat focus.
 *
 * Chi cho phep focus mo tooltip khi la focus tu ban phim (`:focus-visible`) —
 * nguoi dung Tab toi thi van thay nhan. `preventDefault()` chan handler cua
 * Radix vi Radix ghep handler bang `composeEventHandlers(props.onFocus, …)`,
 * ma ham cua no bo qua khi event da bi preventDefault.
 */
function keyboardOnlyFocus(event: FocusEvent<HTMLElement>) {
  if (!event.currentTarget.matches(":focus-visible")) event.preventDefault();
}

type TooltipProps = {
  label: ReactNode;
  children: ReactElement;
  shortcut?: string;
  side?: ComponentProps<typeof TooltipPrimitive.Content>["side"];
  sideOffset?: number;
  className?: string;
};

export function Tooltip({
  label,
  children,
  shortcut,
  side = "top",
  sideOffset = 7,
  className,
}: TooltipProps) {
  return (
    <TooltipPrimitive.Root>
      <TooltipPrimitive.Trigger asChild onFocus={keyboardOnlyFocus}>
        {children}
      </TooltipPrimitive.Trigger>
      <TooltipPrimitive.Portal>
        <TooltipPrimitive.Content
          side={side}
          sideOffset={sideOffset}
          collisionPadding={8}
          className={cn(
            "z-[100] max-w-72 origin-[var(--radix-tooltip-content-transform-origin)] rounded-[10px] border",
            "border-[color-mix(in_oklab,white_14%,var(--color-border))] bg-[color-mix(in_oklab,var(--color-surface)_88%,transparent)]",
            "px-2.5 py-1.5 text-fs-11 font-semibold leading-[1.35] text-fg backdrop-blur-md backdrop-saturate-150",
            "shadow-[0_14px_38px_-18px_color-mix(in_oklab,black_72%,transparent),0_4px_12px_-8px_color-mix(in_oklab,black_58%,transparent),inset_0_1px_0_color-mix(in_oklab,white_18%,transparent)]",
            "data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:zoom-out-95 data-[state=closed]:duration-100",
            "data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-95 data-[state=open]:duration-150 motion-reduce:animate-none",
            className,
          )}
        >
          <span>{label}</span>
          {shortcut ? (
            <kbd className="ml-2 rounded border border-border/70 bg-bg/35 px-1 py-0.5 font-mono text-fs-9 font-medium text-muted-fg">
              {shortcut}
            </kbd>
          ) : null}
          <TooltipPrimitive.Arrow
            width={8}
            height={4}
            className="fill-[color-mix(in_oklab,var(--color-surface)_88%,transparent)]"
          />
        </TooltipPrimitive.Content>
      </TooltipPrimitive.Portal>
    </TooltipPrimitive.Root>
  );
}

type IconButtonTone =
  | "neutral" | "play" | "edit" | "add" | "open" | "finish" | "danger"
  /* Tone rieng cho nut thanh cong cu: moi nut mot mau, khong mang nghia hanh dong. */
  | "sort" | "filter" | "notify";
type IconButtonSize = "mini" | "compact" | "default";

type LiquidGlassStyle = CSSProperties & {
  "--liquid-shadow-x": string;
  "--liquid-shadow-y": string;
};

/**
 * Tao huong bong toi ben trong gia-ngau-nhien, giu nguyen trong suot vong doi
 * cua nut.
 */
function liquidGlassStyle(seed: string): LiquidGlassStyle {
  let hash = 2166136261;
  for (let index = 0; index < seed.length; index += 1) {
    hash ^= seed.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }

  const angle = (hash >>> 0) % 360;
  const radians = (angle * Math.PI) / 180;

  return {
    "--liquid-shadow-x": `${(-Math.cos(radians) * 2.4).toFixed(2)}px`,
    "--liquid-shadow-y": `${(-Math.sin(radians) * 2.4).toFixed(2)}px`,
  };
}

type IconButtonProps = Omit<ComponentProps<"button">, "children"> & {
  label: string;
  icon: ReactNode;
  shortcut?: string;
  tone?: IconButtonTone;
  size?: IconButtonSize;
  /** Nut dai (pill): hien ca nhan ben canh icon, khong can tooltip. */
  wide?: boolean;
  /** "lens": thau kinh day (mac dinh). "info": nut nhe, it hieu ung — dung qua InfoButton. */
  variant?: "lens" | "info";
  tooltipSide?: ComponentProps<typeof TooltipPrimitive.Content>["side"];
};

export const IconButton = forwardRef<HTMLButtonElement, IconButtonProps>(function IconButton({
  label,
  icon,
  shortcut,
  tone = "neutral",
  size = "default",
  wide = false,
  variant = "lens",
  tooltipSide = "top",
  className,
  style,
  disabled,
  type = "button",
  "aria-label": ariaLabel,
  ...props
}, ref) {
  const lightSeed = useId();
  const liquidStyle = liquidGlassStyle(`${lightSeed}:${label}:${tone}`);
  const button = (
    <button
      type={type}
      ref={ref}
      disabled={disabled}
      data-tone={tone}
      data-size={size}
      data-wide={wide || undefined}
      aria-label={ariaLabel ?? label}
      style={{ ...liquidStyle, ...style }}
      className={cn(
        /* Nut tron thau kinh; huong bong rieng nam trong CSS variables. */
        variant === "info" ? "info-button" : "icon-button",
        "relative shrink-0 rounded-full",
        wide
          ? "inline-flex items-center justify-center gap-1.5 px-4 text-sm font-semibold"
          : "inline-grid place-items-center",
        size === "mini" ? cn("h-5 [&_svg]:size-3", !wide && "w-5")
          : size === "compact" ? cn("h-7 [&_svg]:size-3.5", !wide && "w-7")
            : cn("h-9 [&_svg]:size-[18px]", !wide && "w-9"),
        className,
      )}
      {...props}
    >
      {icon}
      {wide ? <span>{label}</span> : null}
    </button>
  );

  if (wide) {
    return disabled ? <span className="inline-flex cursor-not-allowed">{button}</span> : button;
  }

  return (
    <Tooltip label={label} shortcut={shortcut} side={tooltipSide}>
      {disabled ? <span className="inline-grid cursor-not-allowed">{button}</span> : button}
    </Tooltip>
  );
});

/** Nut xem thong tin: chu "i" tran (khong vien tron), it hieu ung hon IconButton. */
export const InfoButton = forwardRef<HTMLButtonElement, Omit<IconButtonProps, "icon" | "wide" | "variant">>(
  function InfoButton(props, ref) {
    return (
      <IconButton
        {...props}
        ref={ref}
        variant="info"
        icon={<span aria-hidden="true" className="info-glyph">i</span>}
      />
    );
  },
);

export function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block space-y-1.5">
      <span className="text-xs font-bold uppercase tracking-wide text-muted-fg">{label}</span>
      {children}
    </label>
  );
}

export function TextInput({ className, ...props }: ComponentProps<"input">) {
  return (
    <input
      className={cn(
        "stone-slab h-9 w-full rounded-lg px-3 text-sm outline-none",
        "placeholder:text-muted-fg focus:ring-2 focus:ring-primary/40",
        className,
      )}
      {...props}
    />
  );
}
