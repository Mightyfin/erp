import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { AlertTriangle, Lock } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

/**
 * A decision that cannot be taken without saying why.
 *
 * Distinct from ConfirmDialog: that one asks you to acknowledge a consequence,
 * this one makes you account for a choice. Anything that overrides a control —
 * waiving a blocking exception, excluding someone from a pay run, rejecting a
 * request — belongs here, because the reason is the only thing that makes the
 * override reviewable afterwards.
 *
 * `blockedBecause` renders the dialog as a refusal instead: the action is shown
 * with the rule that prevents it, rather than the button silently doing nothing.
 */
export function ReasonDialog({
  open,
  onOpenChange,
  title,
  consequence,
  detail,
  reasonLabel = "Reason",
  reasonHint,
  placeholder,
  minLength = 15,
  confirmLabel,
  destructive,
  blockedBecause,
  onConfirm,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
  title: string;
  consequence: string;
  detail?: ReactNode;
  reasonLabel?: string;
  reasonHint?: string;
  placeholder?: string;
  /** Long enough to be an actual explanation rather than a keystroke. */
  minLength?: number;
  confirmLabel: string;
  destructive?: boolean;
  blockedBecause?: string;
  onConfirm: (reason: string) => void;
}) {
  const [reason, setReason] = useState("");
  const [attempted, setAttempted] = useState(false);

  useEffect(() => {
    if (open) {
      setReason("");
      setAttempted(false);
    }
  }, [open]);

  const tooShort = reason.trim().length < minLength;
  const showError = attempted && tooShort;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle className={destructive ? "flex items-center gap-2 text-danger" : ""}>
            {destructive && !blockedBecause ? (
              <AlertTriangle className="size-4 shrink-0" aria-hidden />
            ) : null}
            {title}
          </DialogTitle>
          <DialogDescription>{consequence}</DialogDescription>
        </DialogHeader>

        {blockedBecause ? (
          <p className="flex gap-2 rounded-md border border-warning/40 bg-warning-soft p-3 text-sm text-warning">
            <Lock className="mt-0.5 size-4 shrink-0" aria-hidden />
            {blockedBecause}
          </p>
        ) : (
          <>
            {detail ? (
              <div className="rounded-md border bg-surface-muted p-3 text-sm">{detail}</div>
            ) : null}

            <div>
              <Label htmlFor="reason-dialog">{reasonLabel}</Label>
              <Textarea
                id="reason-dialog"
                rows={3}
                className="mt-1"
                value={reason}
                placeholder={placeholder}
                aria-invalid={showError || undefined}
                aria-describedby={showError ? "reason-dialog-err" : "reason-dialog-hint"}
                onChange={(e) => setReason(e.target.value)}
              />
              {showError ? (
                <p id="reason-dialog-err" role="alert" className="mt-1 text-xs text-danger">
                  Give a reason someone reviewing this later would find sufficient — at least{" "}
                  {minLength} characters.
                </p>
              ) : (
                <p id="reason-dialog-hint" className="mt-1 text-xs text-muted-foreground">
                  {reasonHint ?? "Recorded against this record with your name and the time."}
                </p>
              )}
            </div>
          </>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            {blockedBecause ? "Close" : "Cancel"}
          </Button>
          {blockedBecause ? null : (
            <Button
              className={destructive ? "bg-danger text-danger-foreground hover:bg-danger/90" : ""}
              onClick={() => {
                setAttempted(true);
                if (tooShort) return;
                onConfirm(reason.trim());
                onOpenChange(false);
              }}
            >
              {confirmLabel}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
