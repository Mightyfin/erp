import type { ReactNode } from "react";
import { Check, CircleDashed } from "lucide-react";

/**
 * Presentation for a full record's worth of fields.
 *
 * The rule these encode: a field with no value still appears, saying "Not
 * recorded". Hiding empty fields makes a half-finished record look complete,
 * which is how a payroll run ends up missing a bank account nobody noticed.
 */

export function FieldGrid({ children }: { children: ReactNode }) {
  return <dl className="grid gap-x-6 gap-y-4 sm:grid-cols-2 lg:grid-cols-3">{children}</dl>;
}

export function Field({
  label,
  value,
  hint,
  wide,
}: {
  label: string;
  value?: ReactNode;
  hint?: string;
  wide?: boolean;
}) {
  const empty = value === undefined || value === null || value === "";
  return (
    <div className={wide ? "sm:col-span-2 lg:col-span-3" : undefined}>
      <dt className="text-xs uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className={`mt-0.5 text-sm ${empty ? "text-muted-foreground" : ""}`}>
        {empty ? "Not recorded" : value}
      </dd>
      {hint ? <p className="mt-0.5 text-xs text-muted-foreground">{hint}</p> : null}
    </div>
  );
}

export function YesNo({ value, yes = "Yes", no = "No" }: { value: boolean; yes?: string; no?: string }) {
  return (
    <span className="inline-flex items-center gap-1.5">
      {value ? (
        <Check className="size-3.5 shrink-0 text-success" aria-hidden />
      ) : (
        <CircleDashed className="size-3.5 shrink-0 text-muted-foreground" aria-hidden />
      )}
      {value ? yes : no}
    </span>
  );
}

/** A repeating block — schooling, prior jobs, dependants, next of kin. */
export function SubRecords<T>({
  items,
  empty,
  render,
}: {
  items: T[];
  empty: string;
  render: (item: T) => ReactNode;
}) {
  if (!items.length) {
    return <p className="text-sm text-muted-foreground">{empty}</p>;
  }
  return <ul className="space-y-2">{items.map((item, i) => <li key={i}>{render(item)}</li>)}</ul>;
}

export function SubRecordCard({
  title,
  meta,
  children,
}: {
  title: ReactNode;
  meta?: ReactNode;
  children?: ReactNode;
}) {
  return (
    <div className="rounded-md border bg-surface p-3">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <p className="text-sm font-medium">{title}</p>
        {meta ? <p className="text-xs text-muted-foreground">{meta}</p> : null}
      </div>
      {children ? <div className="mt-1 text-xs text-muted-foreground">{children}</div> : null}
    </div>
  );
}
