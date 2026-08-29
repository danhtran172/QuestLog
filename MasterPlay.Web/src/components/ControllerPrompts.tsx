import { cn } from "@/lib/utils";

export type ControllerPrompt = Readonly<{
  button: string;
  label: string;
}>;

export function ControllerPrompts({
  visible,
  prompts,
  ariaLabel,
}: {
  visible: boolean;
  prompts: readonly ControllerPrompt[];
  ariaLabel: string;
}) {
  if (!visible || prompts.length === 0) return null;

  return (
    <aside
      aria-label={ariaLabel}
      aria-live="polite"
      className="controller-prompt-rail fixed inset-x-0 bottom-0 z-[70] flex justify-center px-4 pb-4"
    >
      <div className="flex max-w-full flex-wrap items-center justify-center gap-x-5 gap-y-2 rounded-2xl border border-white/12 bg-[#15181d]/92 px-5 py-3 text-[#f1f3f4] shadow-2xl backdrop-blur-xl">
        {prompts.map((prompt) => (
          <span key={`${prompt.button}-${prompt.label}`} className="inline-flex items-center gap-2 whitespace-nowrap text-xs font-semibold">
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
