import { DpadGlyph, parseDpadButton } from "@/components/DpadGlyph";
import { cn } from "@/lib/utils";

export type ControllerPrompt = Readonly<{
  button: string;
  label: string;
}>;

function ButtonBadge({ button, size }: { button: string; size: "rail" | "panel" }) {
  const dpad = parseDpadButton(button);
  if (dpad) {
    return (
      <DpadGlyph
        direction={dpad}
        label={button}
        className={size === "rail" ? "h-5 w-5 opacity-70" : "h-4.5 w-4.5"}
      />
    );
  }

  return (
    <kbd
      className={cn(
        "controller-button-badge inline-grid place-items-center border font-sans text-fs-10 font-extrabold leading-none",
        size === "rail"
          ? "h-7 min-w-7 border-white/20 bg-[#15181d]/75 px-2 text-white/80 opacity-40 shadow-lg backdrop-blur-md"
          : "h-6 min-w-6 border-white/25 bg-white/12 px-1.5",
        button.length <= 1 ? "rounded-full" : "rounded-md",
      )}
    >
      {button}
    </kbd>
  );
}

export function ControllerPrompts({
  visible,
  open,
  triggerButton,
  triggerLabel,
  prompts,
  ariaLabel,
}: {
  visible: boolean;
  open: boolean;
  triggerButton: string;
  triggerLabel: string;
  prompts: readonly ControllerPrompt[];
  ariaLabel: string;
}) {
  if (!visible || prompts.length === 0) return null;

  if (!open) {
    return (
      <aside
        aria-label={ariaLabel}
        className="controller-prompt-rail pointer-events-none fixed bottom-0 right-0 z-[70] flex items-center gap-2 px-4 pb-4"
      >
        <ButtonBadge button={triggerButton} size="rail" />
        <span className="text-xs font-semibold text-white/65">{triggerLabel}</span>
      </aside>
    );
  }

  return (
    <aside
      aria-label={ariaLabel}
      aria-live="polite"
      className="controller-prompt-rail fixed bottom-0 right-0 z-[70] flex justify-end px-4 pb-4"
    >
      <div className="grid min-w-56 max-w-[min(22rem,calc(100vw-2rem))] grid-cols-[auto_1fr] gap-x-3 gap-y-2 rounded-2xl border border-white/12 bg-[#15181d]/92 px-4 py-3 text-[#f1f3f4] shadow-2xl backdrop-blur-xl">
        {prompts.map((prompt) => (
          <span
            key={`${prompt.button}-${prompt.label}`}
            className="col-span-2 grid grid-cols-subgrid items-center whitespace-nowrap text-xs font-semibold"
          >
            <span className="flex justify-center">
              <ButtonBadge button={prompt.button} size="panel" />
            </span>
            {prompt.label}
          </span>
        ))}
      </div>
    </aside>
  );
}
