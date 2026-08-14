import { createFileRoute, Link } from "@tanstack/react-router";
import { Download, Info, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { payrollRunApi } from "@/mock/payrollrun";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { CalculationExplainer } from "@/platform/components/CalculationExplainer";
import { PageHeader } from "@/platform/components/PageHeader";
import { RestrictedState } from "@/platform/components/States";
import { useMock } from "@/platform/use-mock";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/payslips/$id")({
  head: () => ({
    meta: [
      { title: "Payslip — Mightyfin ERP HRM" },
      { name: "description", content: "Every line explained: the calculation, the rule version and the difference from last period." },
      { property: "og:title", content: "Payslip — Mightyfin ERP HRM" },
      { property: "og:description", content: "Every line explained: the calculation, the rule version and the difference from last period." },
    ],
  }),
  component: PayslipDetail,
});

const money = (v: number, c: string) => new Intl.NumberFormat(undefined, { style: "currency", currency: c }).format(v);

function PayslipDetail() {
  const { id } = Route.useParams();
  const state = useMock(() => payrollRunApi.payslip(id), [id]);

  return (
    <AuthGate>
      <AppShell>
      <Async state={state} rows={4}>
        {(p) => {
          if (!p) return <RestrictedState />;

          const earnings = p.components.filter((c) => c.kind === "Earning");
          const deductions = p.components.filter((c) => c.kind === "Deduction");
          const employer = p.components.filter((c) => c.kind === "Employer");

          return (
            <div className="space-y-6">
              <PageHeader
                eyebrow="Pay"
                title={`Payslip — ${p.period}`}
                description={`${p.employee} · ${p.entityName}`}
                primaryAction={
                  <Button
                    variant="outline"
                    className="gap-2"
                    onClick={() =>
                      feedback.note(
                        "Payslip PDF is not generated in this build.",
                        "The released payslip is the record; the PDF is only a copy of it.",
                      )
                    }
                  >
                    <Download className="size-4" aria-hidden />
                    Download PDF (mock)
                  </Button>
                }
                meta={
                  <span className="inline-flex items-center gap-1.5 rounded-full border border-success/30 bg-success-soft px-2.5 py-0.5 text-xs font-medium text-success">
                    <ShieldCheck className="size-3.5" aria-hidden />
                    Released and unchanged since issue
                  </span>
                }
              />

              <div className="grid gap-4 sm:grid-cols-3">
                <div className="rounded-lg border bg-surface p-4">
                  <p className="text-xs text-muted-foreground">Gross</p>
                  <p className="tabular mt-1 text-xl font-semibold">{money(p.gross, p.currency)}</p>
                </div>
                <div className="rounded-lg border bg-surface p-4">
                  <p className="text-xs text-muted-foreground">Net pay</p>
                  <p className="tabular mt-1 text-xl font-semibold text-primary">
                    {money(p.net, p.currency)}
                  </p>
                </div>
                <div className="rounded-lg border bg-surface p-4">
                  <p className="text-xs text-muted-foreground">Payment status</p>
                  {p.paid ? (
                    <>
                      <p className="mt-1 text-sm font-medium text-success">
                        Paid to the bank account on file
                      </p>
                      <p className="mt-0.5 text-xs text-muted-foreground">On {p.payDate}.</p>
                    </>
                  ) : (
                    <>
                      <p className="mt-1 text-sm font-medium text-warning">
                        Released, not yet paid
                      </p>
                      <p className="mt-0.5 text-xs text-muted-foreground">
                        Expected {p.payDate}. Seeing a payslip does not mean the money has moved.
                      </p>
                    </>
                  )}
                </div>
              </div>

              {/* The whole point of deriving a payslip: it cannot disagree with its run. */}
              <p className="flex flex-wrap items-center gap-x-2 gap-y-1 rounded-md border border-info/30 bg-info-soft p-3 text-xs text-info">
                <Info className="size-3.5 shrink-0" aria-hidden />
                <span>
                  These are the figures held by pay run{" "}
                  <Link
                    to="/hrm/payroll/runs/$id"
                    params={{ id: p.runId }}
                    className="font-mono underline underline-offset-2"
                  >
                    {p.runId}
                  </Link>
                  , not a separate copy of them.
                </span>
              </p>

              <div>
                <h2 className="text-sm font-semibold">Earnings</h2>
                <div className="mt-2">
                  <CalculationExplainer
                    lines={earnings}
                    currency={p.currency}
                    caption="Every earning line shows the inputs used and the rule version applied."
                  />
                </div>
              </div>

              <div>
                <h2 className="text-sm font-semibold">Deductions</h2>
                <div className="mt-2">
                  <CalculationExplainer lines={deductions} currency={p.currency} />
                </div>
              </div>

              {employer.length ? (
                <div>
                  <h2 className="text-sm font-semibold">Employer contributions</h2>
                  <p className="mt-1 text-xs text-muted-foreground">
                    Shown for transparency — paid by the employer on top of pay, never deducted from
                    your net.
                  </p>
                  <div className="mt-2">
                    <CalculationExplainer lines={employer} currency={p.currency} />
                  </div>
                </div>
              ) : null}

              <div className="rounded-lg border bg-surface p-5">
                <h2 className="text-sm font-semibold">How this adds up</h2>
                <dl className="mt-3 max-w-md space-y-1.5 text-sm">
                  <div className="flex justify-between gap-4">
                    <dt>Total earnings</dt>
                    <dd className="tabular">{money(p.gross, p.currency)}</dd>
                  </div>
                  <div className="flex justify-between gap-4">
                    <dt>Total deductions</dt>
                    <dd className="tabular text-muted-foreground">
                      −{money(p.deductions, p.currency)}
                    </dd>
                  </div>
                  <div className="flex justify-between gap-4 border-t pt-1.5 font-medium">
                    <dt>Net pay</dt>
                    <dd className="tabular">{money(p.net, p.currency)}</dd>
                  </div>
                  <div className="flex justify-between gap-4 pt-1.5 text-xs text-muted-foreground">
                    <dt>Employer cost on top</dt>
                    <dd className="tabular">{money(p.employerCost, p.currency)}</dd>
                  </div>
                </dl>
              </div>

              <div className="rounded-lg border bg-surface p-5">
                <h2 className="text-sm font-semibold">Something doesn't look right?</h2>
                <p className="mt-1 max-w-2xl text-sm text-muted-foreground">
                  Raise a payroll query and this exact payslip reference is attached automatically.
                  If a correction is approved, a new linked version is created here — this original
                  stays retrievable and unchanged.
                </p>
                <Button asChild variant="outline" className="mt-3">
                  <Link to="/hrm/requests/new">Raise a payroll query</Link>
                </Button>
              </div>
            </div>
          );
        }}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
