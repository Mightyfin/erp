import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { EyeOff, Info, ShieldAlert, TrendingUp } from "lucide-react";
import { Button } from "@/components/ui/button";
import { compensationApi, money } from "@/mock/compensation";
import type { BenefitEnrolment, CompRecord } from "@/mock/compensation";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/pay/compensation")({
  head: () => ({
    meta: [
      { title: "Compensation and benefits — Mightyfin ERP HRM" },
      { name: "description", content: "Pay against band, benefit enrolment, review cycles, pay-gap reporting and insurance claims." },
      { property: "og:title", content: "Compensation and benefits — Mightyfin ERP HRM" },
      { property: "og:description", content: "Pay against band, benefit enrolment, review cycles, pay-gap reporting and insurance claims." },
    ],
  }),
  component: CompensationPage,
});

/** Compa-ratio is shown as a number and a word — never a bare colour. */
function CompaRatio({ value }: { value: number }) {
  const label =
    value < 0.9 ? "Below band midpoint" : value > 1.1 ? "Above band midpoint" : "Around midpoint";
  const tone = value < 0.9 ? "text-warning" : value > 1.1 ? "text-info" : "";
  return (
    <span className="block">
      <span className={`tabular text-sm font-medium ${tone}`}>{value.toFixed(2)}</span>
      <span className="block text-[11px] text-muted-foreground">{label}</span>
    </span>
  );
}

function RangeBar({ pct }: { pct: number }) {
  return (
    <span className="block">
      <span className="block h-1.5 w-24 overflow-hidden rounded-full bg-muted" role="presentation">
        <span className="block h-full rounded-full bg-primary" style={{ width: `${Math.min(pct, 100)}%` }} />
      </span>
      <span className="mt-0.5 block text-[11px] text-muted-foreground">{pct}% through band</span>
    </span>
  );
}

function CompensationPage() {
  const records = useMock(() => compensationApi.records());
  const bands = useMock(() => compensationApi.bands());
  const enrolments = useMock(() => compensationApi.enrolments());
  const cycles = useMock(() => compensationApi.cycles());
  const payGap = useMock(() => compensationApi.payGap());
  const claims = useMock(() => compensationApi.claims());
  const [tab, setTab] = useState<"pay" | "benefits" | "equity">("pay");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Payroll"
        title="Compensation and benefits"
        description="Pay is restricted data. This view shows position against band rather than putting one person's salary next to another's."
        primaryAction={<Button
            onClick={() =>
              feedback.submitted(
                "Compensation change request started.",
                "It goes to the grade owner, then to payroll. A change lands in the run for the period it is effective from.",
              )
            }
          >
            Request a compensation change
          </Button>}
        meta={
          <span className="inline-flex items-center gap-1.5 rounded-full border border-danger/30 bg-danger-soft px-2.5 py-0.5 text-xs font-medium text-danger">
            <ShieldAlert className="size-3.5" aria-hidden />
            Restricted — visible to Payroll and HR admin only
          </span>
        }
      />

      <div className="flex flex-wrap gap-2" role="tablist" aria-label="Compensation views">
        {([
          ["pay", "Pay and bands"],
          ["benefits", "Benefits and insurance"],
          ["equity", "Review cycles and pay gap"],
        ] as const).map(([id, label]) => (
          <button
            key={id}
            role="tab"
            aria-selected={tab === id}
            onClick={() => setTab(id)}
            className={`rounded-full border px-3 py-1 text-sm transition-colors ${
              tab === id
                ? "border-primary bg-primary-soft font-medium text-primary"
                : "bg-surface text-muted-foreground hover:border-border-strong"
            }`}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === "pay" ? (
        <>
          <Async state={records} rows={4}>
            {(rows) => (
              <ListPage<CompRecord>
                rows={rows}
                searchPlaceholder="Search employee or grade"
                searchFields={(r) => `${r.employee} ${r.grade}`}
                filters={[{ id: "grade", label: "Grade", options: ["G4", "G5", "G6", "G7", "G9"], match: (r, v) => r.grade === v }]}
                columns={[
                  { id: "employee", header: "Employee", cell: (r) => <span className="block max-w-56 truncate font-medium">{r.employee}</span> },
                  { id: "grade", header: "Grade", cell: (r) => r.grade },
                  { id: "fte", header: "FTE", cell: (r) => <span className="tabular">{r.fte}</span> },
                  { id: "compa", header: "Compa-ratio", cell: (r) => <CompaRatio value={r.compaRatio} /> },
                  { id: "range", header: "Position in band", cell: (r) => <RangeBar pct={r.rangePenetration} /> },
                  { id: "lastChange", header: "Last change", cell: (r) => <span className="block max-w-48 truncate text-xs">{r.lastChange} · {r.lastChangeReason}</span> },
                  { id: "next", header: "Next review", cell: (r) => <span className="text-xs">{r.nextReview}</span> },
                  {
                    id: "salary",
                    header: "Salary",
                    defaultVisible: false,
                    cell: (r) => (
                      <span className="inline-flex items-center gap-1.5">
                        <EyeOff className="size-3.5 shrink-0 text-muted-foreground" aria-hidden />
                        <span className="tabular text-sm">{money(r.salary, r.currency)}</span>
                      </span>
                    ),
                  },
                ]}
                emptyBody="No compensation records in scope."
              />
            )}
          </Async>

          <p className="flex gap-2 text-xs text-muted-foreground">
            <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            The salary column is hidden by default and available through the column picker. Opening it
            is recorded, because seeing another person's exact pay is a deliberate act, not a
            side-effect of browsing.
          </p>

          <section aria-label="Salary bands" className="rounded-lg border bg-surface p-5">
            <h2 className="text-sm font-semibold">Salary bands</h2>
            <p className="mt-1 text-xs text-muted-foreground">
              Bands are configuration. Compa-ratio and position in band are derived from them, so
              changing a band changes every derived figure from its effective date.
            </p>
            <Async state={bands} rows={2}>
              {(rows) => (
                <div className="mt-3 overflow-x-auto">
                  <table className="w-full min-w-[30rem] text-left text-sm">
                    <caption className="sr-only">Salary band minimum, midpoint and maximum by grade</caption>
                    <thead className="border-b bg-surface-muted">
                      <tr>
                        <th scope="col" className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Grade</th>
                        <th scope="col" className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">Minimum</th>
                        <th scope="col" className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">Midpoint</th>
                        <th scope="col" className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">Maximum</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y">
                      {rows.map((b) => (
                        <tr key={b.grade}>
                          <th scope="row" className="px-3 py-2 font-normal">{b.grade}</th>
                          <td className="tabular px-3 py-2 text-right">{money(b.min, b.currency)}</td>
                          <td className="tabular px-3 py-2 text-right font-medium">{money(b.mid, b.currency)}</td>
                          <td className="tabular px-3 py-2 text-right">{money(b.max, b.currency)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </Async>
          </section>
        </>
      ) : null}

      {tab === "benefits" ? (
        <>
          <Async state={enrolments} rows={4}>
            {(rows) => (
              <ListPage<BenefitEnrolment>
                rows={rows}
                searchPlaceholder="Search employee or plan"
                searchFields={(e) => `${e.employee} ${e.plan} ${e.kind}`}
                filters={[
                  { id: "kind", label: "Type", options: ["Pension", "Medical", "Life cover", "Income protection"], match: (e, v) => e.kind === v },
                  { id: "status", label: "Status", options: ["Enrolled", "Pending evidence", "Waived", "Ended"], match: (e, v) => e.status === v },
                ]}
                columns={[
                  { id: "employee", header: "Employee", cell: (e) => <span className="block max-w-48 truncate">{e.employee}</span> },
                  { id: "plan", header: "Plan", cell: (e) => <span className="block max-w-56 truncate font-medium">{e.plan}</span> },
                  { id: "coverage", header: "Coverage", cell: (e) => <span className="text-xs">{e.coverage}</span> },
                  { id: "ee", header: "Employee pays", cell: (e) => <span className="text-xs">{e.employeeContribution}</span> },
                  { id: "er", header: "Employer pays", cell: (e) => <span className="text-xs">{e.employerContribution}</span> },
                  { id: "status", header: "Status", cell: (e) => <StatusBadge status={e.status} /> },
                  { id: "from", header: "Effective from", cell: (e) => e.effectiveFrom },
                  { id: "dependants", header: "Dependants", defaultVisible: false, cell: (e) => <span className="tabular">{e.dependants}</span> },
                ]}
                emptyBody="No benefit enrolments in scope."
              />
            )}
          </Async>

          <Async state={enrolments} rows={1}>
            {(rows) => {
              const windows = rows.filter((e) => e.changeWindow);
              if (!windows.length) return null;
              return (
                <section aria-label="Open change windows" className="rounded-lg border border-warning/40 bg-warning-soft p-4">
                  <h2 className="text-sm font-semibold text-warning">Open change windows</h2>
                  <ul className="mt-2 space-y-1.5 text-sm">
                    {windows.map((e) => (
                      <li key={e.id}>
                        <span className="font-medium">{e.employee}</span> — {e.plan}
                        <span className="block text-xs text-foreground">{e.changeWindow}</span>
                      </li>
                    ))}
                  </ul>
                  <p className="mt-2 text-xs text-muted-foreground">
                    A life event opens a limited window to change cover. Outside it, changes wait for
                    the next open enrolment.
                  </p>
                </section>
              );
            }}
          </Async>

          <section aria-label="Insurance claims" className="rounded-lg border bg-surface p-5">
            <h2 className="text-sm font-semibold">Insurance claims</h2>
            <p className="mt-1 text-xs text-muted-foreground">
              HR records the claim reference, the provider's status and any settlement. No medical
              detail or diagnosis is held here or anywhere in HR.
            </p>
            <Async state={claims} rows={2}>
              {(rows) => (
                <ul className="mt-3 divide-y">
                  {rows.map((c) => (
                    <li key={c.id} className="py-3">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-mono text-xs text-muted-foreground">{c.id}</span>
                        <span className="text-sm font-medium">{c.policy}</span>
                        <StatusBadge status={c.status} />
                        <span className="text-xs text-muted-foreground">{c.provider} · ref {c.reference}</span>
                      </div>
                      <p className="mt-1 text-sm">{c.outcome}</p>
                      {c.settlement ? (
                        <p className="tabular mt-1 text-xs font-medium">
                          Settled: {money(c.settlement, c.currency)}
                        </p>
                      ) : null}
                    </li>
                  ))}
                </ul>
              )}
            </Async>
          </section>
        </>
      ) : null}

      {tab === "equity" ? (
        <>
          <section aria-label="Review cycles">
            <h2 className="text-sm font-semibold">Compensation review cycles</h2>
            <Async state={cycles} rows={2}>
              {(rows) => (
                <ul className="mt-3 space-y-3">
                  {rows.map((c) => (
                    <li key={c.id} className="rounded-lg border bg-surface p-4">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="text-sm font-medium">{c.name}</span>
                        <StatusBadge status={c.status} />
                        <span className="font-mono text-xs text-muted-foreground">{c.id}</span>
                      </div>
                      <p className="mt-1 text-xs text-muted-foreground">{c.population}</p>
                      <div className="mt-3 flex flex-wrap gap-4 text-sm">
                        <span>
                          <span className="tabular font-semibold">{c.budgetPct}%</span>
                          <span className="block text-xs text-muted-foreground">Budget</span>
                        </span>
                        <span>
                          <span className="tabular font-semibold">{c.allocatedPct}%</span>
                          <span className="block text-xs text-muted-foreground">Allocated</span>
                        </span>
                        <span>
                          <span className="tabular font-semibold">{(c.budgetPct - c.allocatedPct).toFixed(1)}%</span>
                          <span className="block text-xs text-muted-foreground">Remaining</span>
                        </span>
                        <span>
                          <span className="block text-sm">{c.opens} to {c.closes}</span>
                          <span className="block text-xs text-muted-foreground">Window</span>
                        </span>
                      </div>
                      <p className="mt-3 flex gap-2 rounded-md border bg-surface-muted p-2 text-xs">
                        <TrendingUp className="mt-0.5 size-3.5 shrink-0 text-muted-foreground" aria-hidden />
                        <span>{c.guidance}</span>
                      </p>
                    </li>
                  ))}
                </ul>
              )}
            </Async>
          </section>

          <section aria-label="Pay gap reporting" className="rounded-lg border bg-surface p-5">
            <h2 className="text-sm font-semibold">Pay gap reporting</h2>
            <p className="mt-1 text-xs text-muted-foreground">
              Reported by group, with suppression below the threshold. A gap is not by itself
              evidence of unequal pay for equal work — it is the starting point for asking why.
            </p>
            <Async state={payGap} rows={2}>
              {(rows) => (
                <ul className="mt-3 space-y-2">
                  {rows.map((g) => (
                    <li key={g.group} className="rounded-md border p-3">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <span className="text-sm font-medium">{g.group}</span>
                        <span className="text-xs text-muted-foreground">{g.headcount} employees</span>
                      </div>
                      {g.suppressed ? (
                        <p className="mt-1.5 flex gap-2 text-xs text-warning">
                          <EyeOff className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                          <span>
                            <span className="font-medium">Suppressed. </span>
                            {g.note}
                          </span>
                        </p>
                      ) : (
                        <p className="tabular mt-1.5 text-sm">
                          Median gap {g.medianGapPct}% · mean gap {g.meanGapPct}%
                        </p>
                      )}
                    </li>
                  ))}
                </ul>
              )}
            </Async>
            <p className="mt-3 flex gap-2 text-xs text-muted-foreground">
              <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
              Every group in this demonstration dataset falls below the reporting threshold, so
              nothing is published. That is the correct behaviour for an eight-person organisation,
              not a missing feature.
            </p>
          </section>
        </>
      ) : null}
    </AppShell>
      </AuthGate>
  );
}
