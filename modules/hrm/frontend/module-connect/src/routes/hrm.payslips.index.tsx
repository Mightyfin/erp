import { createFileRoute, Link } from "@tanstack/react-router";
import { Info } from "lucide-react";
import { payrollRunApi } from "@/mock/payrollrun";
import type { DerivedPayslip } from "@/mock/payrollrun";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/payslips/")({
  head: () => ({
    meta: [
      { title: "Payslips — Mightyfin ERP HRM" },
      { name: "description", content: "Your pay history, each with a full explanation of how it was calculated." },
      { property: "og:title", content: "Payslips — Mightyfin ERP HRM" },
      { property: "og:description", content: "Your pay history, each with a full explanation of how it was calculated." },
    ],
  }),
  component: PayslipsList,
});

const money = (v: number, c: string) => new Intl.NumberFormat(undefined, { style: "currency", currency: c }).format(v);

function PayslipsList() {
  const state = useMock(() => payrollRunApi.payslips());

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Pay"
        title="Payslips"
        description="Released payslips stay retrievable exactly as issued — a correction creates a new linked version, never a silent overwrite."
      />

      <p className="flex gap-2 rounded-md border border-info/30 bg-info-soft p-3 text-xs text-info">
        <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
        A payslip exists only once its pay run has released payslips. It is the same record the run
        holds, so the two can never show different figures.
      </p>

      <Async state={state}>
        {(rows) => (
          <ListPage<DerivedPayslip>
            rows={rows}
            searchPlaceholder="Search period, employee or run"
            searchFields={(p) => `${p.id} ${p.employee} ${p.period} ${p.runId}`}
            columns={[
              {
                id: "ref",
                header: "Reference",
                cell: (p) => (
                  <Link
                    to="/hrm/payslips/$id"
                    params={{ id: p.id }}
                    className="font-mono text-xs text-primary underline underline-offset-2"
                  >
                    {p.id}
                  </Link>
                ),
              },
              { id: "employee", header: "Employee", cell: (p) => <span className="block max-w-56 truncate">{p.employee}</span> },
              { id: "period", header: "Period", cell: (p) => p.period },
              { id: "payDate", header: "Pay date", cell: (p) => p.payDate },
              { id: "gross", header: "Gross", cell: (p) => <span className="tabular">{money(p.gross, p.currency)}</span> },
              { id: "net", header: "Net", cell: (p) => <span className="tabular font-medium">{money(p.net, p.currency)}</span> },
              {
                id: "paid",
                header: "Payment",
                cell: (p) =>
                  p.paid ? (
                    <span className="text-xs text-success">Paid</span>
                  ) : (
                    <span className="text-xs text-warning">Not yet paid</span>
                  ),
              },
            ]}
            emptyBody="No payslips match the current view."
          />
        )}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
