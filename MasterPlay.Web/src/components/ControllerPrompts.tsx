import { cn } from "@/lib/utils";

export type ControllerPrompt = Readonly<{
  button: string;
  label: string;
}>;

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
        <kbd
          className={cn(
            "controller-button-badge inline-grid h-7 min-w-7 place-items-center border border-white/20 bg-[#15181d]/75 px-2 font-sans text-[10px] font-extrabold leading-none text-white/80 opacity-40 shadow-lg backdrop-blur-md",
            triggerButton.length <= 1 ? "rounded-full" : "rounded-md",
          )}
        >
          {triggerButton}
        </kbd>
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
      <div className="flex min-w-56 max-w-[min(22rem,calc(100vw-2rem))] flex-col items-stretch gap-2 rounded-2xl border border-white/12 bg-[#15181d]/92 px-4 py-3 text-[#f1f3f4] shadow-2xl backdrop-blur-xl">
        {prompts.map((prompt) => (
          <span key={`${prompt.button}-${prompt.label}`} className="flex items-center gap-3 whitespace-nowrap text-xs font-semibold">
            <kbd
              className={cn(
                "controller-button-badge inline-grid h-6 min-w-6 place-items-center border border-white/25 bg-white/12 px-1.5 font-sans text-[10px] font-extrabold leading-none",
                prompt.button.length <= 1 ? "rounded-full" : "rounded-md",
              )}
            >
              {prompt.button}
            </kbd>
            {prompt.label}
          </span>
        ))}
      </div>
    </aside>
  );
}
