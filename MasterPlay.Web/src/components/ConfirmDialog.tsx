import { Trash2, X } from "lucide-react";
import type { ReactNode } from "react";
import { useI18n } from "@/lib/i18n";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogTitle,
  IconButton,
} from "./ui";

/**
 * Hop xac nhan cua chinh app, thay `window.confirm` (hop cua trinh duyet/WebView
 * lech han giao dien). Cung khuon voi hop "thay doi chua luu" cua GameEditorForm.
 * Bam xac nhan thi dong hop roi goi `onConfirm`; mutation phia goi tu bao trang
 * thai cho va loi nhu truoc. Focus mac dinh roi vao nut Huy (phan tu dau tien)
 * de Enter khong vo tinh xoa.
 */
export function ConfirmDialog({
  open,
  onOpenChange,
  title,
  description,
  confirmLabel,
  confirmIcon = <Trash2 />,
  onConfirm,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description: ReactNode;
  confirmLabel: string;
  confirmIcon?: ReactNode;
  onConfirm: () => void;
}) {
  const { t } = useI18n();

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-sm">
        <div className="space-y-2 p-5 pr-14">
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </div>
        <DialogFooter>
          <IconButton label={t("action.cancel")} icon={<X />} onClick={() => onOpenChange(false)} />
          <IconButton
            tone="danger"
            label={confirmLabel}
            icon={confirmIcon}
            onClick={() => {
              onOpenChange(false);
              onConfirm();
            }}
          />
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
