import { createFileRoute, Link, Outlet, useChildMatches } from "@tanstack/react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { money, payrollRunApi } from "@/mock/payrollrun";
import type { PayRun } from "@/mock/payrollrun";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/payroll/runs")({
  head: () => ({
    meta: [
      { title: "Pay runs — Mightyfin ERP HRM" },
      { name: "description", content: "Every pay run with its stage, control totals, owner and next action." },
      { property: "og:title", content: "Pay runs — Mightyfin ERP HRM" },
      { property: "og:description", content: "Every pay run with its stage, control totals, owner and next action." },
    ],
  }),
  component: RunsList,
});

const closed = new Set(["Closed", "Paid", "Reversed"]);

function RunsList() {
  const state = useMock(() => payrollRunApi.runs());
  const [view, setView] = useState("open");
  // `/payroll/runs/$id` is generated as a child of this route, so hand the
  // screen over whenever a run is open.
  const childMatches = useChildMatches();
  if (childMatches.length > 0) return <Outlet />;

  return (
    <AppShell>
      <PageHeader
        eyebrow="Payroll"
        title="Pay runs"
        description="A run moves through fixed stages. Calculating, approving, releasing payslips and paying people are deliberately separate steps."
        primaryAction={
          <Button asChild>
            <Link to="/hrm/payroll/runs/new">Start a pay run</Link>
          </Button>
        }
      />
      <Async state={state} rows={4}>
        {(rows) => (
          <ListPage<PayRun>
            rows={rows.filter((r) => (view === "open" ? !closed.has(r.status) : view === "closed" ? closed.has(r.status) : true))}
            savedViews={[
              { id: "open", label: "In progress" },
              { id: "closed", label: "Paid or closed" },
              { id: "all", label: "All runs" },
            ]}
            activeView={view}
            onViewChange={setView}
            searchPlaceholder="Search reference, period or entity"
            searchFields={(r) => `${r.id} ${r.period} ${r.entityName} ${r.payGroup}`}
            filters={[
              { id: "status", label: "Status", options: ["Draft", "Calculated", "In review", "Approved", "Paid", "Closed"], match: (r, v) => r.status === v },
              { id: "entity", label: "Entity", options: ["Mighty Finance Solutions Industrial Services Zambia Ltd", "Mighty Finance Solutions Copperbelt Services Ltd", "Mighty Finance Solutions Engineering Zambia Ltd"], match: (r, v) => r.entityName === v },
            ]}
            columns={[
              {
                id: "ref",
                header: "Run",
                cell: (r) => (
                  <span className="block min-w-0 max-w-64">
                    <Link to="/hrm/payroll/runs/$id" params={{ id: r.id }} className="block truncate font-medium text-primary underline-offset-2 hover:underline">
                      {r.period} · {r.payGroup}
                    </Link>
                    <span className="block truncate font-mono text-xs text-muted-foreground">{r.id}</span>
                  </span>
                ),
              },
              { id: "entity", header: "Entity", cell: (r) => <span className="block max-w-48 truncate">{r.entityName}</span> },
              { id: "people", header: "Employees", cell: (r) => <span className="tabular">{r.included}</span> },
              { id: "gross", header: "Gross", cell: (r) => <span className="tabular">{r.totals.gross ? money(r.totals.gross, r.currency) : "—"}</span> },
              { id: "net", header: "Net", cell: (r) => <span className="tabular font-medium">{r.totals.net ? money(r.totals.net, r.currency) : "—"}</span> },
              { id: "status", header: "Status", cell: (r) => <StatusBadge status={r.status} /> },
              { id: "next", header: "Next action", cell: (r) => <span className="block max-w-56 truncate text-xs">{r.nextAction} · due {r.dueDate}</span> },
              { id: "owner", header: "Owner", defaultVisible: false, cell: (r) => r.owner },
              { id: "prepared", header: "Prepared by", defaultVisible: false, cell: (r) => r.preparedBy },
            ]}
            emptyBody="No pay runs match the current view."
          />
        )}
      </Async>
    </AppShell>
  );
}
