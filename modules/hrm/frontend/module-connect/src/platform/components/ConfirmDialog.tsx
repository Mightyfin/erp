import { AlertTriangle } from "lucide-react";
import type { ReactNode } from "react";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";

/**
 * Confirmation for anything hard to undo.
 *
 * The rule it encodes: a confirm must state the CONSEQUENCE, not repeat the
 * button label. "Are you sure?" tells the user nothing they did not already
 * know.
 */
export function ConfirmDialog({
  open,
  onOpenChange,
  title,
  consequence,
  detail,
  confirmLabel,
  cancelLabel = "Cancel",
  destructive,
  onConfirm,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
  title: string;
  /** What will actually happen. Required — this is the point of the dialog. */
  consequence: string;
  detail?: ReactNode;
  confirmLabel: string;
  cancelLabel?: string;
  destructive?: boolean;
  onConfirm: () => void;
}) {
  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle className={destructive ? "flex items-center gap-2 text-danger" : ""}>
            {destructive ? <AlertTriangle className="size-4 shrink-0" aria-hidden /> : null}
            {title}
          </AlertDialogTitle>
          <AlertDialogDescription>{consequence}</AlertDialogDescription>
        </AlertDialogHeader>

        {detail ? <div className="rounded-md border bg-surface-muted p-3 text-sm">{detail}</div> : null}

        <AlertDialogFooter>
          <AlertDialogCancel>{cancelLabel}</AlertDialogCancel>
          <AlertDialogAction
            onClick={onConfirm}
            className={destructive ? "bg-danger text-danger-foreground hover:bg-danger/90" : ""}
          >
            {confirmLabel}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
