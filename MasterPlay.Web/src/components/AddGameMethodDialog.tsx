import { FilePlus2, Sparkles, X } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogTitle,
  GlassFooter,
  IconButton,
} from "./ui";

type Props = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onManual: () => void;
  onMetadata: () => void;
};

/**
 * Method chooser for every library owner: start from the shared metadata
 * catalog, or create a private manual game (POST /api/games) that only exists
 * in the caller's own library.
 */
export function AddGameMethodDialog({ open, onOpenChange, onManual, onMetadata }: Props) {
  const { t } = useI18n();

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-xl p-0">
        <div className="space-y-5 p-5 pb-20 pr-14">
          <div>
            <DialogTitle>{t("addGame.methodTitle")}</DialogTitle>
            <DialogDescription>{t("addGame.methodDescription")}</DialogDescription>
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <button
              type="button"
              onClick={() => {
                onOpenChange(false);
                onMetadata();
              }}
              className="group rounded-xl border border-border bg-surface-2/25 p-4 text-left transition-colors hover:border-primary/35"
            >
              <Sparkles size={20} className="mb-3 text-primary" aria-hidden />
              <p className="text-sm font-extrabold text-fg">{t("addGame.metadataMethod")}</p>
              <p className="mt-1 text-xs leading-5 text-muted-fg">{t("addGame.metadataMethodDescription")}</p>
            </button>
            <button
              type="button"
              onClick={() => {
                onOpenChange(false);
                onManual();
              }}
              className="group rounded-xl border border-border bg-surface-2/25 p-4 text-left transition-colors hover:border-primary/35"
            >
              <FilePlus2 size={20} className="mb-3 text-warning" aria-hidden />
              <p className="text-sm font-extrabold text-fg">{t("addGame.manualMethod")}</p>
              <p className="mt-1 text-xs leading-5 text-muted-fg">{t("addGame.manualMethodDescription")}</p>
            </button>
          </div>
        </div>
        <GlassFooter className="justify-end px-5">
          <IconButton label={t("action.cancel")} tone="neutral" icon={<X />} onClick={() => onOpenChange(false)} />
        </GlassFooter>
      </DialogContent>
    </Dialog>
  );
}
