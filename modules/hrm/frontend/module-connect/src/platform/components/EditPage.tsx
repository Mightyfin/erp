import { useEffect, useMemo, useRef, useState } from "react";
import type { ReactNode } from "react";
import { AlertTriangle, ArrowLeft, Check, Undo2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { ConfirmDialog } from "./ConfirmDialog";
import type { FieldDef } from "./EditDrawer";

export type { FieldDef };

export interface EditSection {
  id: string;
  title: string;
  description?: string;
  fields?: FieldDef[];
  /**
   * Anything a plain field list cannot express — a table of pay lines, a
   * population picker, a preview. Given the live values so it can react.
   */
  render?: (ctx: {
    values: Record<string, string>;
    setValue: (name: string, value: string) => void;
  }) => ReactNode;
}

/**
 * Full-page editing for a record with more surface area than a drawer can hold.
 *
 * A drawer is right for two or three fields. A pay run, an employee or an
 * entity has sections, cross-field rules and figures you need the width to
 * read — so those get a page, with the record's identity kept on screen and
 * the save bar always reachable.
 *
 * Rules this encodes, beyond the drawer's:
 *  - The section nav shows where the errors are, so a validation failure three
 *    sections down is never invisible.
 *  - Leaving with unsaved work is caught by the app's own guard AND the
 *    browser's, because a page is easy to navigate away from.
 *  - Everything changed is summarised as "was → becomes" before saving, in one
 *    place, so the user reviews the whole change rather than each field.
 */
/**
 * The options a select offers, always including whatever it is currently set to.
 *
 * A record can legitimately hold a value the configured list no longer offers —
 * a retired grade, a department since renamed. Dropping it would render the
 * field blank and quietly rewrite real data on the next save, so it is kept and
 * marked instead.
 */
function selectableOptions(f: FieldDef, current: string | undefined) {
  const options = (f.options ?? []).map((value) => ({ value, label: value }));
  if (current && !options.some((o) => o.value === current)) {
    return [{ value: current, label: `${current} — no longer offered` }, ...options];
  }
  return options;
}

export function EditPage({
  title,
  reference,
  description,
  sections,
  initial,
  extraChanges = [],
  saveLabel = "Save changes",
  onSave,
  onCancel,
  footerNote,
}: {
  title: string;
  reference?: string;
  description?: string;
  sections: EditSection[];
  initial: Record<string, string>;
  /**
   * Changes made inside a custom `render` section, which this component cannot
   * see. Without these a page could look unchanged while holding real edits —
   * so they count toward dirty and appear in the review summary.
   */
  extraChanges?: { id: string; label: string; detail: string }[];
  saveLabel?: string;
  onSave: (values: Record<string, string>, changed: string[]) => void;
  onCancel: () => void;
  footerNote?: ReactNode;
}) {
  const [values, setValues] = useState(initial);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [attempted, setAttempted] = useState(false);
  const [confirmDiscard, setConfirmDiscard] = useState(false);
  const [active, setActive] = useState(sections[0]?.id ?? "");
  const containers = useRef<Record<string, HTMLElement | null>>({});

  const allFields = useMemo(() => sections.flatMap((s) => s.fields ?? []), [sections]);

  const changed = useMemo(
    () => Object.keys(initial).filter((k) => (values[k] ?? "") !== (initial[k] ?? "")),
    [values, initial],
  );
  const totalChanges = changed.length + extraChanges.length;
  const dirty = totalChanges > 0;

  // A page is one link away from being abandoned, so warn at the browser level too.
  useEffect(() => {
    if (!dirty) return;
    const warn = (e: BeforeUnloadEvent) => e.preventDefault();
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [dirty]);

  function check(all: Record<string, string>) {
    const next: Record<string, string> = {};
    for (const f of allFields) {
      const v = (all[f.name] ?? "").trim();
      if (f.required && !v) next[f.name] = `${f.label} is required.`;
      else if (f.validate) {
        const msg = f.validate(v, all);
        if (msg) next[f.name] = msg;
      }
    }
    return next;
  }

  function setValue(name: string, value: string) {
    setValues((s) => {
      const next = { ...s, [name]: value };
      if (attempted) setErrors(check(next));
      return next;
    });
  }

  function errorsIn(section: EditSection) {
    if (!attempted) return 0;
    return (section.fields ?? []).filter((f) => errors[f.name]).length;
  }

  function submit() {
    setAttempted(true);
    const found = check(values);
    setErrors(found);
    const first = Object.keys(found)[0];
    if (first) {
      document.getElementById(first)?.focus();
      const owner = sections.find((s) => (s.fields ?? []).some((f) => f.name === first));
      if (owner) jumpTo(owner.id);
      return;
    }
    onSave(values, changed);
  }

  function jumpTo(id: string) {
    setActive(id);
    containers.current[id]?.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  function requestCancel() {
    if (dirty) setConfirmDiscard(true);
    else onCancel();
  }

  const errorCount = attempted ? Object.keys(errors).length : 0;

  return (
    <div className="pb-28">
      <div className="mb-6">
        <Button variant="ghost" size="sm" className="-ml-2 gap-1.5" onClick={requestCancel}>
          <ArrowLeft className="size-4" aria-hidden />
          Back without saving
        </Button>
        {reference ? (
          <p className="mt-2 font-mono text-xs text-muted-foreground">{reference}</p>
        ) : null}
        <h1 className="mt-1 text-2xl font-semibold tracking-tight">{title}</h1>
        {description ? (
          <p className="mt-1 max-w-2xl text-sm text-muted-foreground">{description}</p>
        ) : null}
      </div>

      <div className="grid gap-6 lg:grid-cols-[15rem_minmax(0,1fr)]">
        <nav aria-label="Sections" className="lg:sticky lg:top-20 lg:self-start">
          <ol className="space-y-1">
            {sections.map((s) => {
              const bad = errorsIn(s);
              return (
                <li key={s.id}>
                  <button
                    type="button"
                    onClick={() => jumpTo(s.id)}
                    aria-current={active === s.id ? "true" : undefined}
                    className={`flex w-full items-center justify-between gap-2 rounded-md px-3 py-2 text-left text-sm transition-colors ${
                      active === s.id
                        ? "bg-primary-soft font-medium text-primary"
                        : "hover:bg-surface-muted"
                    }`}
                  >
                    <span className="min-w-0 truncate">{s.title}</span>
                    {bad ? (
                      <span className="inline-flex shrink-0 items-center gap-1 rounded-full border border-danger/40 bg-danger-soft px-1.5 text-[11px] font-medium text-danger">
                        <AlertTriangle className="size-3" aria-hidden />
                        {bad}
                      </span>
                    ) : null}
                  </button>
                </li>
              );
            })}
          </ol>
        </nav>

        <div className="min-w-0 space-y-6">
          {sections.map((s) => (
            <section
              key={s.id}
              id={`section-${s.id}`}
              ref={(el) => {
                containers.current[s.id] = el;
              }}
              aria-labelledby={`heading-${s.id}`}
              className="scroll-mt-24 rounded-lg border bg-surface p-5"
            >
              <h2 id={`heading-${s.id}`} className="text-base font-semibold">
                {s.title}
              </h2>
              {s.description ? (
                <p className="mt-1 text-sm text-muted-foreground">{s.description}</p>
              ) : null}

              {s.fields?.length ? (
                <div className="mt-4 grid gap-4 sm:grid-cols-2">
                  {s.fields.map((f) => {
                    const err = attempted ? errors[f.name] : undefined;
                    const isChanged = changed.includes(f.name);
                    const wide = f.type === "textarea";
                    return (
                      <div key={f.name} className={wide ? "sm:col-span-2" : undefined}>
                        <Label htmlFor={f.name}>
                          {f.label}
                          {f.required ? null : (
                            <span className="ml-1 text-xs font-normal text-muted-foreground">
                              (optional)
                            </span>
                          )}
                        </Label>

                        {f.type === "select" ? (
                          <Select
                            value={values[f.name] ?? ""}
                            onValueChange={(v) => setValue(f.name, v)}
                          >
                            <SelectTrigger
                              id={f.name}
                              className="mt-1 w-full"
                              aria-invalid={Boolean(err) || undefined}
                            >
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              {selectableOptions(f, values[f.name]).map((o) => (
                                <SelectItem key={o.value} value={o.value}>
                                  {o.label}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        ) : f.type === "textarea" ? (
                          <Textarea
                            id={f.name}
                            rows={3}
                            className="mt-1"
                            value={values[f.name] ?? ""}
                            aria-invalid={Boolean(err) || undefined}
                            onChange={(e) => setValue(f.name, e.target.value)}
                          />
                        ) : (
                          <Input
                            id={f.name}
                            type={f.type ?? "text"}
                            className="mt-1"
                            value={values[f.name] ?? ""}
                            aria-invalid={Boolean(err) || undefined}
                            aria-describedby={
                              err ? `${f.name}-err` : f.hint ? `${f.name}-hint` : undefined
                            }
                            onChange={(e) => setValue(f.name, e.target.value)}
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
                            {initial[f.name] || "empty"} →{" "}
                            <span className="font-medium">{values[f.name] || "empty"}</span>
                          </p>
                        ) : null}
                      </div>
                    );
                  })}
                </div>
              ) : null}

              {s.render ? <div className="mt-4">{s.render({ values, setValue })}</div> : null}
            </section>
          ))}

          {dirty ? (
            <section
              aria-labelledby="heading-review"
              className="rounded-lg border border-info/40 bg-info-soft p-5"
            >
              <h2 id="heading-review" className="text-base font-semibold text-info">
                Review your changes
              </h2>
              <p className="mt-1 text-sm">
                {totalChanges} change{totalChanges === 1 ? "" : "s"} will be saved against this
                record and recorded in its history.
              </p>
              <ul className="mt-3 space-y-1.5 text-sm">
                {changed.map((name) => {
                  const field = allFields.find((f) => f.name === name);
                  return (
                    <li key={name} className="flex flex-wrap gap-x-2">
                      <span className="font-medium">{field?.label ?? name}:</span>
                      <span className="text-muted-foreground line-through">
                        {initial[name] || "empty"}
                      </span>
                      <span aria-hidden>→</span>
                      <span className="font-medium">{values[name] || "empty"}</span>
                    </li>
                  );
                })}
                {extraChanges.map((c) => (
                  <li key={c.id} className="flex flex-wrap gap-x-2">
                    <span className="font-medium">{c.label}:</span>
                    <span>{c.detail}</span>
                  </li>
                ))}
              </ul>
            </section>
          ) : null}
        </div>
      </div>

      <div className="fixed inset-x-0 bottom-0 z-20 border-t bg-surface/95 backdrop-blur">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center gap-3 px-4 py-3">
          <span className="min-w-0 flex-1 text-xs text-muted-foreground">
            {errorCount ? (
              <span role="alert" className="flex items-center gap-1.5 font-medium text-danger">
                <AlertTriangle className="size-3.5 shrink-0" aria-hidden />
                {errorCount} field{errorCount === 1 ? "" : "s"} need
                {errorCount === 1 ? "s" : ""} attention before this can be saved.
              </span>
            ) : dirty ? (
              <span className="flex items-center gap-1.5">
                <span className="size-1.5 shrink-0 rounded-full bg-warning" aria-hidden />
                {totalChanges} unsaved change{totalChanges === 1 ? "" : "s"}.
              </span>
            ) : (
              <span className="flex items-center gap-1.5">
                <Check className="size-3.5 shrink-0" aria-hidden />
                No changes yet.
              </span>
            )}
            {footerNote ? <span className="mt-0.5 block">{footerNote}</span> : null}
          </span>

          <div className="flex shrink-0 flex-wrap gap-2">
            {dirty ? (
              <Button variant="ghost" className="gap-1.5" onClick={() => setValues(initial)}>
                <Undo2 className="size-4" aria-hidden />
                Reset
              </Button>
            ) : null}
            <Button variant="outline" onClick={requestCancel}>
              Cancel
            </Button>
            <Button onClick={submit} disabled={!dirty}>
              {saveLabel}
            </Button>
          </div>
        </div>
      </div>

      <ConfirmDialog
        open={confirmDiscard}
        onOpenChange={setConfirmDiscard}
        title="Leave without saving?"
        consequence={`${totalChanges} unsaved change${
          totalChanges === 1 ? "" : "s"
        } will be lost. This cannot be recovered.`}
        confirmLabel="Discard changes"
        cancelLabel="Keep editing"
        destructive
        onConfirm={() => {
          setConfirmDiscard(false);
          onCancel();
        }}
      />
    </div>
  );
}
