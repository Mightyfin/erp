import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { Check, TriangleAlert } from "lucide-react";
import { Button } from "@/components/ui/button";
import { extrasApi } from "@/mock/extras";
import type { ToilEntry } from "@/mock/extras";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { useMock } from "@/platform/use-mock";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/time/toil")({
  head: () => ({
    meta: [
      { title: "Time off in lieu — Mightyfin ERP HRM" },
      { name: "description", content: "Hours banked instead of paid, when they expire, and the working-time limits." },
      { property: "og:title", content: "Time off in lieu — Mightyfin ERP HRM" },
      { property: "og:description", content: "Hours banked instead of paid, when they expire, and the working-time limits." },
    ],
  }),
  component: ToilPage,
});

function ToilPage() {
  const toil = useMock(() => extrasApi.toil());
  const rules = useMock(() => extrasApi.fatigueRules());
  const [view, setView] = useState("available");

  return (
    <AppShell>
      <PageHeader
        eyebrow="Time and leave"
        title="Time off in lieu"
        description="Hours banked instead of paid. They expire, so the balance is worth watching."
        primaryAction={<Button
            onClick={() =>
              feedback.submitted(
                "Time off in lieu booked.",
                "Your manager approves it. Taking the time instead of the pay keeps it out of payroll.",
              )
            }
          >
            Book time off in lieu
          </Button>}
      />

      <Async state={toil} rows={3}>
        {(rows) => {
          const available = rows.filter((t) => t.state === "Available");
          const total = available.reduce((s, t) => s + t.hours, 0);
          return (
            <>
              <div className="grid gap-3 sm:grid-cols-3">
                <div className="rounded-lg border bg-surface p-4">
                  <p className="text-xs text-muted-foreground">Available to book</p>
                  <p className="tabular mt-1 text-2xl font-semibold">{total}h</p>
                </div>
                <div className="rounded-lg border bg-surface p-4">
                  <p className="text-xs text-muted-foreground">Expiring within 90 days</p>
                  <p className="tabular mt-1 text-2xl font-semibold">0h</p>
                </div>
                <div className="rounded-lg border border-warning/40 bg-warning-soft p-4">
                  <p className="text-xs text-warning">Already expired</p>
                  <p className="tabular mt-1 text-2xl font-semibold">
                    {rows.filter((t) => t.state === "Expired").reduce((s, t) => s + t.hours, 0)}h
                  </p>
                  <p className="mt-1 text-[11px] text-muted-foreground">Unused and no longer bookable.</p>
                </div>
              </div>

              <ListPage<ToilEntry>
                rows={rows.filter((t) => (view === "available" ? t.state === "Available" : true))}
                savedViews={[
                  { id: "available", label: "Available" },
                  { id: "all", label: "All entries" },
                ]}
                activeView={view}
                onViewChange={setView}
                searchPlaceholder="Search employee or source"
                searchFields={(t) => `${t.id} ${t.employee} ${t.source}`}
                filters={[{ id: "state", label: "State", options: ["Available", "Booked", "Expired", "Paid out"], match: (t, v) => t.state === v }]}
                columns={[
                  { id: "employee", header: "Employee", cell: (t) => <span className="block max-w-48 truncate">{t.employee}</span> },
                  { id: "hours", header: "Hours", cell: (t) => <span className="tabular font-medium">{t.hours}h</span> },
                  { id: "source", header: "Earned from", cell: (t) => t.source },
                  { id: "earned", header: "Earned", cell: (t) => t.earned },
                  { id: "expires", header: "Expires", cell: (t) => t.expires },
                  { id: "state", header: "State", cell: (t) => (
                    t.state === "Expired" ? (
                      <span className="inline-flex items-center gap-1.5 text-xs text-warning">
                        <TriangleAlert className="size-3.5 shrink-0" aria-hidden />
                        Expired
                      </span>
                    ) : (
                      <span className="text-xs">{t.state}</span>
                    )
                  ) },
                ]}
                emptyBody="Nothing banked in this view."
              />
            </>
          );
        }}
      </Async>

      <section aria-label="Working time limits" className="rounded-lg border bg-surface p-5">
        <h2 className="text-sm font-semibold">Working time limits</h2>
        <p className="mt-1 text-xs text-muted-foreground">
          Checked automatically when a roster is published.
        </p>
        <Async state={rules} rows={2}>
          {(rows) => (
            <ul className="mt-3 divide-y">
              {rows.map((r) => (
                <li key={r.id} className="flex flex-wrap items-center gap-3 py-2.5">
                  <span className="min-w-0 flex-1">
                    <span className="block text-sm">{r.rule}</span>
                    <span className="block text-xs text-muted-foreground">{r.note}</span>
                  </span>
                  <span className="text-xs font-medium">{r.limit}</span>
                  {r.breaches ? (
                    <span className="inline-flex items-center gap-1.5 text-xs text-warning">
                      <TriangleAlert className="size-3.5 shrink-0" aria-hidden />
                      {r.breaches} breach
                    </span>
                  ) : (
                    <span className="inline-flex items-center gap-1.5 text-xs text-success">
                      <Check className="size-3.5 shrink-0" aria-hidden />
                      Within limit
                    </span>
                  )}
                </li>
              ))}
            </ul>
          )}
        </Async>
      </section>
    </AppShell>
  );
}
