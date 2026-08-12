import { Link } from "@tanstack/react-router";
import { ArrowRight, Clock } from "lucide-react";
import { Button } from "@/components/ui/button";
import { COMING_SOON_AREAS } from "@/modules/hrm/scope";
import { PageHeader } from "./PageHeader";

/**
 * Shown instead of a screen that is built but not in this release. Says what is
 * coming rather than pretending the area does not exist — a blank "not found"
 * would read as a broken product.
 */
export function ComingSoon({ area }: { area?: string }) {
  return (
    <>
      <PageHeader
        eyebrow="Coming soon"
        title={area ?? "Not in this release"}
        description="This part of the module is ready but switched off while we focus the first release on people, pay and setup."
        meta={
          <span className="inline-flex items-center gap-1.5 rounded-full border border-info/30 bg-info-soft px-2.5 py-0.5 text-xs font-medium text-info">
            <Clock className="size-3.5" aria-hidden />
            Planned, not abandoned
          </span>
        }
      />

      <div className="rounded-lg border bg-surface p-5">
        <h2 className="text-sm font-semibold">In this release</h2>
        <ul className="mt-2 grid gap-2 sm:grid-cols-2">
          {[
            ["Setup", "Get the organisation configured from empty", "/hrm/setup"],
            ["People", "Employee records, positions, structure, documents", "/hrm/employees"],
            ["Payroll", "Runs, exceptions, payslips and everything feeding them", "/hrm/payroll"],
            ["Configuration", "Policies, routing, statutory packs and integrations", "/hrm/configuration"],
          ].map(([label, detail, to]) => (
            <li key={label}>
              <Link
                to={to}
                className="flex items-start justify-between gap-3 rounded-md border p-3 transition-colors hover:border-primary hover:bg-primary-soft"
              >
                <span className="min-w-0">
                  <span className="block text-sm font-medium">{label}</span>
                  <span className="block text-xs text-muted-foreground">{detail}</span>
                </span>
                <ArrowRight className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden />
              </Link>
            </li>
          ))}
        </ul>
      </div>

      <div className="rounded-lg border bg-surface p-5">
        <h2 className="text-sm font-semibold">Coming next</h2>
        <ul className="mt-2 space-y-1.5">
          {COMING_SOON_AREAS.map((a) => (
            <li key={a} className="flex items-start gap-2 text-sm text-muted-foreground">
              <Clock className="mt-0.5 size-3.5 shrink-0" aria-hidden />
              {a}
            </li>
          ))}
        </ul>
      </div>

      <Button asChild variant="outline" className="self-start">
        <Link to="/hrm">Back to Home</Link>
      </Button>
    </>
  );
}
