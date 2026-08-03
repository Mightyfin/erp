import { Link } from "@tanstack/react-router";
import { ArrowRight, CircleDashed, CircleDot } from "lucide-react";
import { AppShell } from "./AppShell";
import { PageHeader } from "./PageHeader";

export type CapabilityState = "built" | "planned";

export interface Capability {
  label: string;
  detail: string;
  /** Catalogue tier, shown so scope expectations stay honest. */
  tier: "Essentials" | "Advanced" | "Enterprise";
  state: CapabilityState;
  /** Present only when the capability is actually navigable today. */
  to?: string;
  params?: Record<string, string>;
}

export interface AreaDefinition {
  eyebrow: string;
  title: string;
  description: string;
  /** Parent features from the HRM catalogue this area covers, e.g. "HRM-013". */
  catalogueRefs: string;
  capabilities: Capability[];
}

const tierClass: Record<Capability["tier"], string> = {
  Essentials: "border-primary/30 bg-primary-soft text-primary",
  Advanced: "border-info/30 bg-info-soft text-info",
  Enterprise: "border-warning/40 bg-warning-soft text-warning",
};

function CapabilityRow({ c }: { c: Capability }) {
  const body = (
    <>
      <span className="mt-0.5 shrink-0">
        {c.state === "built" ? (
          <CircleDot className="size-4 text-success" aria-hidden />
        ) : (
          <CircleDashed className="size-4 text-muted-foreground" aria-hidden />
        )}
      </span>
      <span className="min-w-0 flex-1">
        <span className="flex flex-wrap items-center gap-2">
          <span className="text-sm font-medium">{c.label}</span>
          <span className={`rounded-full border px-2 py-0.5 text-[11px] font-medium ${tierClass[c.tier]}`}>
            {c.tier}
          </span>
          {c.state === "planned" ? (
            <span className="text-[11px] text-muted-foreground">Not built yet</span>
          ) : null}
        </span>
        <span className="mt-0.5 block text-xs text-muted-foreground">{c.detail}</span>
      </span>
      {c.to ? <ArrowRight className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden /> : null}
    </>
  );

  if (c.to) {
    return (
      <li>
        <Link
          to={c.to}
          params={c.params as never}
          className="flex gap-3 rounded-md px-3 py-3 transition-colors hover:bg-surface-muted"
        >
          {body}
        </Link>
      </li>
    );
  }

  return <li className="flex gap-3 px-3 py-3 opacity-70">{body}</li>;
}

/**
 * Standard landing page for an HRM area: what it covers, which catalogue
 * features sit under it, and which of those are navigable today.
 */
export function AreaOverview({ area }: { area: AreaDefinition }) {
  const built = area.capabilities.filter((c) => c.state === "built").length;

  return (
    <AppShell>
      <PageHeader
        eyebrow={area.eyebrow}
        title={area.title}
        description={area.description}
        meta={
          <>
            <span className="rounded-full border bg-surface-muted px-2.5 py-0.5 text-xs text-muted-foreground">
              Catalogue: {area.catalogueRefs}
            </span>
            <span className="rounded-full border bg-surface-muted px-2.5 py-0.5 text-xs text-muted-foreground">
              {built} of {area.capabilities.length} available in this build
            </span>
          </>
        }
      />
      <ul className="divide-y rounded-lg border bg-surface">
        {area.capabilities.map((c) => (
          <CapabilityRow key={c.label} c={c} />
        ))}
      </ul>
    </AppShell>
  );
}
