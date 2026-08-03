import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { AlertTriangle, Undo2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { ConfirmDialog } from "./ConfirmDialog";

export interface FieldDef {
  name: string;
  label: string;
  type?: "text" | "number" | "date" | "select" | "textarea";
  options?: string[];
  hint?: string;
  required?: boolean;
  /** Return an error message, or null when the value is acceptable. */
  validate?: (value: string, all: Record<string, string>) => string | null;
}

/**
 * Edit an existing record in a side sheet.
 *
 * Rules this encodes:
 *  - Nothing saves until the user says so, and Save stays disabled until
 *    something actually changed — a no-op save is a lie in an audit trail.
 *  - Closing with unsaved edits asks first.
 *  - Validation runs on submit and on blur after the first attempt, not on
 *    every keystroke while someone is still typing.
 *  - Every change is described as "what it was → what it becomes".
 */
export function EditDrawer({
  open,
  onOpenChange,
  title,
  description,
  fields,
  initial,
  saveLabel = "Save changes",
  onSave,
  footerNote,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
  title: string;
  description?: string;
  fields: FieldDef[];
  initial: Record<string, string>;
  saveLabel?: string;
  onSave: (values: Record<string, string>, changed: string[]) => void;
  footerNote?: ReactNode;
}) {
  const [values, setValues] = useState(initial);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [attempted, setAttempted] = useState(false);
  const [confirmDiscard, setConfirmDiscard] = useState(false);

  useEffect(() => {
    if (open) {
      setValues(initial);
      setErrors({});
      setAttempted(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const changed = fields.map((f) => f.name).filter((n) => (values[n] ?? "") !== (initial[n] ?? ""));
  const dirty = changed.length > 0;

  function check(all: Record<string, string>) {
    const next: Record<string, string> = {};
    for (const f of fields) {
      const v = (all[f.name] ?? "").trim();
      if (f.required && !v) next[f.name] = `${f.label} is required.`;
      else if (f.validate) {
        const msg = f.validate(v, all);
        if (msg) next[f.name] = msg;
      }
    }
    return next;
  }

  function submit() {
    setAttempted(true);
    const found = check(values);
    setErrors(found);
    if (Object.keys(found).length) return;
    onSave(values, changed);
    onOpenChange(false);
  }

  function requestClose(next: boolean) {
    if (!next && dirty) {
      setConfirmDiscard(true);
      return;
    }
    onOpenChange(next);
  }

  return (
    <>
      <Sheet open={open} onOpenChange={requestClose}>
        <SheetContent side="right" className="flex w-full flex-col gap-0 overflow-y-auto sm:max-w-lg">
          <SheetHeader>
            <SheetTitle>{title}</SheetTitle>
            {description ? <SheetDescription>{description}</SheetDescription> : null}
          </SheetHeader>

          <div className="flex-1 space-y-4 px-4">
            {fields.map((f) => {
              const err = attempted ? errors[f.name] : undefined;
              const isChanged = changed.includes(f.name);
              return (
                <div key={f.name}>
                  <Label htmlFor={f.name}>
                    {f.label}
                    {f.required ? null : (
                      <span className="ml-1 text-xs font-normal text-muted-foreground">(optional)</span>
                    )}
                  </Label>

                  {f.type === "select" ? (
                    <Select
                      value={values[f.name] ?? ""}
                      onValueChange={(v) => setValues((s) => ({ ...s, [f.name]: v }))}
                    >
                      <SelectTrigger id={f.name} className="mt-1" aria-invalid={Boolean(err) || undefined}>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {(f.options ?? []).map((o) => (
                          <SelectItem key={o} value={o}>
                            {o}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  ) : (
                    <Input
                      id={f.name}
                      type={f.type ?? "text"}
                      className="mt-1"
                      value={values[f.name] ?? ""}
                      aria-invalid={Boolean(err) || undefined}
                      aria-describedby={err ? `${f.name}-err` : f.hint ? `${f.name}-hint` : undefined}
                      onChange={(e) => setValues((s) => ({ ...s, [f.name]: e.target.value }))}
                      onBlur={() => attempted && setErrors(check(values))}
                    />
                  )}

                  {err ? (
                    <p id={`${f.name}-err`} role="alert" className="mt-1 text-xs text-danger">
                      {err}
                    </p>
                  ) : f.hint ? (
                    <p id={`${f.name}-hint`} className="mt-1 text-xs text-muted-foreground">
                      {f.hint}
                    </p>
                  ) : null}

                  {isChanged ? (
                    <p className="mt-1 text-xs text-info">
                      {initial[f.name] || "empty"} → <span className="font-medium">{values[f.name] || "empty"}</span>
                    </p>
                  ) : null}
                </div>
              );
            })}
          </div>

          <div className="sticky bottom-0 mt-4 space-y-3 border-t bg-surface px-4 py-4">
            {attempted && Object.keys(errors).length ? (
              <p role="alert" className="flex gap-2 rounded-md border border-danger/40 bg-danger-soft p-2 text-xs text-danger">
                <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                {Object.keys(errors).length} field
                {Object.keys(errors).length === 1 ? " needs" : "s need"} attention before this can be saved.
              </p>
            ) : null}

            <p className="text-xs text-muted-foreground">
              {dirty ? `${changed.length} change${changed.length === 1 ? "" : "s"} not yet saved.` : "No changes yet."}
            </p>

            <div className="flex flex-wrap gap-2">
              <Button onClick={submit} disabled={!dirty}>
                {saveLabel}
              </Button>
              <Button variant="outline" onClick={() => requestClose(false)}>
                Cancel
              </Button>
              {dirty ? (
                <Button variant="ghost" className="gap-1.5" onClick={() => setValues(initial)}>
                  <Undo2 className="size-4" aria-hidden />
                  Reset
                </Button>
              ) : null}
            </div>

            {footerNote ? <div className="text-xs text-muted-foreground">{footerNote}</div> : null}
          </div>
        </SheetContent>
      </Sheet>

      <ConfirmDialog
        open={confirmDiscard}
        onOpenChange={setConfirmDiscard}
        title="Discard your changes?"
        consequence={`${changed.length} unsaved change${changed.length === 1 ? "" : "s"} will be lost. This cannot be recovered.`}
        confirmLabel="Discard changes"
        cancelLabel="Keep editing"
        destructive
        onConfirm={() => {
          setConfirmDiscard(false);
          onOpenChange(false);
        }}
      />
    </>
  );
}
