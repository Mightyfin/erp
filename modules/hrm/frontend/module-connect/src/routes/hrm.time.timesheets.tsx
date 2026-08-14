import { createFileRoute } from "@tanstack/react-router";
import { Check, Clock, Lock } from "lucide-react";
import { Button } from "@/components/ui/button";
import { expensesApi } from "@/mock/expenses";
import type { Timesheet } from "@/mock/expenses";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/time/timesheets")({
  head: () => ({
    meta: [
      { title: "Timesheets — Mightyfin ERP HRM" },
      { name: "description", content: "Weekly hours by project, with separate project and line-manager approvals." },
      { property: "og:title", content: "Timesheets — Mightyfin ERP HRM" },
      { property: "og:description", content: "Weekly hours by project, with separate project and line-manager approvals." },
    ],
  }),
  component: TimesheetsPage,
});

const DAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

function sum(a: number[]) {
  return a.reduce((x, y) => x + y, 0);
}

function Grid({ ts }: { ts: Timesheet }) {
  const dayTotals = DAYS.map((_, i) => sum(ts.rows.map((r) => r.hours[i] + r.overtime[i])));
  const grand = sum(dayTotals);
  const ordinary = sum(ts.rows.map((r) => sum(r.hours)));
  const overtime = sum(ts.rows.map((r) => sum(r.overtime)));
  const billable = sum(ts.rows.filter((r) => r.billable).map((r) => sum(r.hours) + sum(r.overtime)));

  return (
    <>
      <div className="overflow-x-auto rounded-lg border bg-surface">
        <table className="w-full min-w-[46rem] text-left text-sm">
          <caption className="sr-only">
            Weekly hours by project for the week beginning {ts.weekStarting}
          </caption>
          <thead className="border-b bg-surface-muted">
            <tr>
              <th scope="col" className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                Project
              </th>
              {DAYS.map((d) => (
                <th key={d} scope="col" className="px-2 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  {d}
                </th>
              ))}
              <th scope="col" className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                Total
              </th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {ts.rows.map((r) => (
              <tr key={r.id}>
                <th scope="row" className="px-3 py-2 font-normal">
                  <span className="block max-w-56 truncate font-medium">{r.project}</span>
                  <span className="block text-xs text-muted-foreground">
                    {r.costCentre} · {r.billable ? "Billable" : "Non-billable"}
                  </span>
                </th>
                {DAYS.map((_, i) => (
                  <td key={i} className="tabular px-2 py-2 text-right">
                    {r.hours[i] || r.overtime[i] ? (
                      <>
                        <span>{r.hours[i] || "—"}</span>
                        {r.overtime[i] ? (
                          <span className="block text-[11px] text-warning">+{r.overtime[i]} OT</span>
                        ) : null}
                      </>
                    ) : (
                      <span className="text-muted-foreground">—</span>
                    )}
                  </td>
                ))}
                <td className="tabular px-3 py-2 text-right font-medium">
                  {sum(r.hours) + sum(r.overtime)}
                </td>
              </tr>
            ))}
          </tbody>
          <tfoot className="border-t bg-surface-muted">
            <tr>
              <th scope="row" className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                Daily total
              </th>
              {dayTotals.map((t, i) => (
                <td key={i} className="tabular px-2 py-2 text-right font-medium">
                  {t || "—"}
                </td>
              ))}
              <td className="tabular px-3 py-2 text-right font-semibold">{grand}</td>
            </tr>
          </tfoot>
        </table>
      </div>

      <dl className="mt-4 grid gap-3 sm:grid-cols-4">
        {[
          { label: "Ordinary hours", value: `${ordinary}` },
          { label: "Overtime", value: `${overtime}` },
          { label: "Billable", value: `${billable} of ${grand}` },
          {
            label: "Against contracted",
            value: `${grand} of ${ts.contractedHours}${grand > ts.contractedHours ? ` · +${grand - ts.contractedHours}` : ""}`,
          },
        ].map((s) => (
          <div key={s.label} className="rounded-lg border bg-surface p-3">
            <dt className="text-xs text-muted-foreground">{s.label}</dt>
            <dd className="tabular mt-0.5 text-lg font-semibold">{s.value}</dd>
          </div>
        ))}
      </dl>
    </>
  );
}

function TimesheetsPage() {
  const state = useMock(() => expensesApi.timesheets());

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Time and leave"
        title="Timesheets"
        description="Hours by project for the week. Two people approve a timesheet, and they are checking different things."
        primaryAction={<Button
            onClick={() =>
              feedback.submitted(
                "Timesheet opened for this week.",
                "Submit it before the payroll cutoff or the hours roll into the next run.",
              )
            }
          >
            Start this week&apos;s timesheet
          </Button>}
      />

      <Async state={state} rows={3}>
        {(rows) =>
          rows.map((ts) => (
            <section key={ts.id} aria-label={`Timesheet ${ts.id}`} className="space-y-4">
              <div className="flex flex-wrap items-center gap-3">
                <span className="font-mono text-xs text-muted-foreground">{ts.id}</span>
                <span className="text-sm font-medium">
                  Week of {ts.weekStarting} to {ts.weekEnding}
                </span>
                <StatusBadge status={ts.status} />
              </div>

              <Grid ts={ts} />

              <div className="grid gap-3 sm:grid-cols-2">
                {[ts.projectApproval, ts.lineApproval].map((a) => (
                  <div key={a.by} className="rounded-lg border bg-surface p-4">
                    <p className="flex items-center gap-2 text-sm font-medium">
                      {a.state === "Approved" ? (
                        <Check className="size-4 shrink-0 text-success" aria-hidden />
                      ) : (
                        <Clock className="size-4 shrink-0 text-muted-foreground" aria-hidden />
                      )}
                      {a.by}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">{a.purpose}</p>
                    <p className="mt-2 text-xs font-medium">
                      {a.state === "Approved" ? "Approved" : "Awaiting decision"}
                    </p>
                  </div>
                ))}
              </div>

              <p className="flex gap-2 rounded-md border border-info/30 bg-info-soft p-3 text-sm text-info">
                <Lock className="mt-0.5 size-4 shrink-0" aria-hidden />
                <span>{ts.lockedNote}</span>
              </p>
            </section>
          ))
        }
      </Async>
    </AppShell>
      </AuthGate>
  );
}
