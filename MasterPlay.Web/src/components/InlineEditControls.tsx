import { Pencil } from "lucide-react";
import {
  useRef,
  type ChangeEventHandler,
  type ComponentProps,
} from "react";
import { cn } from "@/lib/utils";

/** Tat spinner native cua input number, van giu ban phim so va validation HTML. */
export const NUMBER_INPUT_NO_SPINNER = cn(
  "[appearance:textfield]",
  "[&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none",
);

/**
 * Nut dung rieng cho inline edit: chi hien cay but, khong mang glass surface,
 * vien, nen hay shadow cua he IconButton chung.
 */
export function InlineEditButton({
  label,
  className,
  type = "button",
  ...props
}: Omit<ComponentProps<"button">, "children" | "aria-label"> & {
  label: string;
}) {
  return (
    <button
      type={type}
      aria-label={label}
      title={label}
      className={cn(
        "inline-grid h-4 w-4 shrink-0 appearance-none place-items-center border-0 bg-transparent p-0 text-muted-fg/75 shadow-none",
        "opacity-0 transition-[color,opacity] group-hover/inline-edit:opacity-100 hover:text-fg",
        "focus-visible:opacity-100 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-primary/70",
        "disabled:cursor-wait disabled:opacity-45 [&_svg]:size-3",
        className,
      )}
      {...props}
    >
      <Pencil aria-hidden />
    </button>
  );
}

type InlineNumberInputProps = Omit<
  ComponentProps<"input">,
  "type" | "value" | "onChange" | "onBlur" | "onKeyDown"
> & {
  value: string;
  onChange: ChangeEventHandler<HTMLInputElement>;
  onCommit: (value: string) => void;
  onCancel: () => void;
};

/**
 * Input so chuan cho inline edit:
 * - Enter hoac blur: commit mot lan.
 * - Escape: cancel va bo qua blur sinh ra khi input bi thao khoi DOM.
 * - Khong hien nut tang/giam native.
 */
export function InlineNumberInput({
  value,
  onChange,
  onCommit,
  onCancel,
  className,
  ...props
}: InlineNumberInputProps) {
  const cancelBlurRef = useRef(false);
  const committedByEnterRef = useRef<string | null>(null);

  const commit = (input: HTMLInputElement) => {
    if (!input.checkValidity()) {
      input.reportValidity();
      return;
    }
    onCommit(input.value);
  };

  return (
    <input
      {...props}
      type="number"
      value={value}
      onFocus={(event) => event.currentTarget.select()}
      onChange={(event) => {
        if (committedByEnterRef.current !== event.currentTarget.value) {
          committedByEnterRef.current = null;
        }
        onChange(event);
      }}
      onBlur={(event) => {
        if (cancelBlurRef.current) {
          cancelBlurRef.current = false;
          return;
        }
        if (committedByEnterRef.current === event.currentTarget.value) {
          committedByEnterRef.current = null;
          return;
        }
        commit(event.currentTarget);
      }}
      onKeyDown={(event) => {
        if (event.key === "Enter") {
          event.preventDefault();
          committedByEnterRef.current = event.currentTarget.value;
          commit(event.currentTarget);
        } else if (event.key === "Escape") {
          event.preventDefault();
          event.stopPropagation();
          cancelBlurRef.current = true;
          onCancel();
        }
      }}
      className={cn(NUMBER_INPUT_NO_SPINNER, className)}
    />
  );
}
