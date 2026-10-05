import { cn } from "@/lib/utils";

export type DpadDirection = "up" | "down" | "left" | "right";

/** Cac nhanh cua khung D-pad (viewBox 512x512), moi nhanh nhon vao tam. */
const ARMS: Record<DpadDirection, string> = {
  up: "160,0 352,0 352,115 256,211 160,115",
  right: "512,160 512,352 397,352 301,256 397,160",
  down: "160,512 352,512 352,397 256,301 160,397",
  left: "0,160 115,160 211,256 115,352 0,352",
};

const DIRECTION_BY_ARROW: Record<string, DpadDirection> = {
  "↑": "up",
  "↓": "down",
  "←": "left",
  "→": "right",
};

/** Nhan dien nhan nut dang "D↑" / "D↓" / "D←" / "D→". */
export function parseDpadButton(button: string): DpadDirection | null {
  if (button.length !== 2 || button[0] !== "D") return null;
  return DIRECTION_BY_ARROW[button[1]] ?? null;
}

export function DpadGlyph({
  direction,
  label,
  className,
}: {
  direction: DpadDirection;
  label: string;
  className?: string;
}) {
  return (
    <span className={cn("controller-dpad-glyph inline-grid shrink-0 place-items-center", className)}>
      <svg viewBox="0 0 512 512" aria-hidden className="h-full w-full overflow-visible">
        {(Object.keys(ARMS) as DpadDirection[]).map((arm) => (
          <polygon
            key={arm}
            points={ARMS[arm]}
            data-active={arm === direction || undefined}
            className={arm === direction ? "fill-white" : "fill-white/30"}
          />
        ))}
      </svg>
      <span className="sr-only">{label}</span>
    </span>
  );
}
