import { createFileRoute, Link, Outlet, useChildMatches } from "@tanstack/react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { money, payrollRunApi } from "@/mock/payrollrun";
import type { PayRun } from "@/mock/payrollrun";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/payroll/runs")({
  head: () => ({
    meta: [
      { title: "Pay runs — New World Cargo HRM" },
      {
        name: "description",
        content: "Every pay run with its stage, control totals, owner and next action.",
      },
      { property: "og:title", content: "Pay runs — New World Cargo HRM" },
      {
        property: "og:description",
        content: "Every pay run with its stage, control totals, owner and next action.",
      },
    ],
  }),
  component: RunsList,
});

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

const closed = new Set(["Closed", "Paid", "Reversed", "Locked", "Calculated"]);

/** M27: adapt persisted runs, including live control totals and payment state. */
function adaptRunRows(rows: unknown[]): PayRun[] {
  const entity = "New World Cargo Logistics Zambia Ltd";
  return rows.map((raw) => {
    const r = raw as Record<string, unknown>;
    const backendStatus = String(r.status ?? "draft");
    const statusMap: Record<string, PayRun["status"]> = {
      draft: "Draft",
      locked: "Draft",
      calculating: "Calculating",
      calculated: "Calculated",
      "in-review": "In review",
      approved: "Approved",
      released: "Paid",
      closed: "Closed",
      reversed: "Reversed",
    };
    const status = statusMap[backendStatus] ?? "Draft";
    const payment = String(r.paymentStatus ?? "not-created");
    const nextAction =
      backendStatus === "draft"
        ? "Lock inputs"
        : backendStatus === "locked"
          ? "Calculate run"
          : backendStatus === "calculated"
            ? Number(r.exceptionCount ?? 0)
              ? "Resolve exceptions"
              : "Approve run"
            : backendStatus === "approved"
              ? "Release payslips"
              : backendStatus === "released" && payment === "not-created"
                ? "Generate payment file"
                : backendStatus === "released" && payment === "released"
                  ? "Reconcile bank result"
                  : backendStatus === "closed"
                    ? "Cycle complete"
                    : `Payment: ${payment}`;
    return {
      id: String(r.id ?? ""),
      period: String(r.periodLabel ?? ""),
      entityId: "",
      payGroup: "Monthly ZMW",
      currency: "ZMW",
      entityName: entity,
      included: Number(r.employeeCount ?? 0),
      excluded: [],
      stages: [],
      timeline: [],
      totals: {
        headcount: Number(r.employeeCount ?? 0),
        gross: Number(r.totalGross ?? 0),
        deductions: Number(r.totalDeductions ?? 0),
        employerCost: Number(r.totalEmployerCost ?? 0),
        net: Number(r.totalNet ?? 0),
      },
      status,
      nextAction,
      dueDate: "controlled workflow",
      branchId: r.locationId ? String(r.locationId) : undefined,
      owner: String(r.preparedBySubjectId ?? "Payroll officer"),
      preparedBy: String(r.preparedBySubjectId ?? ""),
      approvedBy: r.approvedBySubjectId ? String(r.approvedBySubjectId) : undefined,
    } satisfies PayRun;
  });
}

let liveLocationsCache: { id: string; name: string }[] | null = null;

function branchName(branchId: string): string | undefined {
  const loc = liveLocationsCache?.find((l) => l.id === branchId);
  return loc?.name;
}

function RunsList() {
  const state = useApi(async (): Promise<PayRun[]> => {
    if (!USE_REAL) return payrollRunApi.runs();
    if (!liveLocationsCache) {
      const raw = await realApi.locations().catch(() => ({ items: [] as unknown[] }));
      liveLocationsCache = (Array.isArray(raw)
        ? raw
        : ((raw as Record<string, unknown>)?.items as unknown[]) ?? []).map((l) => ({
        id: String((l as Record<string, unknown>).id ?? ""),
        name: String((l as Record<string, unknown>).name ?? ""),
      }));
    }
    const result = await realApi.payrollRuns();
    return adaptRunRows(result.items ?? []);
  }, []);
  const [view, setView] = useState("open");
  // `/payroll/runs/$id` is generated as a child of this route, so hand the
  // screen over whenever a run is open.
  const childMatches = useChildMatches();
  if (childMatches.length > 0) return <Outlet />;

  return (
    <AuthGate>
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
              rows={rows.filter((r) =>
                view === "open"
                  ? !closed.has(r.status)
                  : view === "closed"
                    ? closed.has(r.status)
                    : true,
              )}
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
                {
                  id: "status",
                  label: "Status",
                  options: [
                    "Draft",
                    "Calculating",
                    "Calculated",
                    "In review",
                    "Approved",
                    "Paid",
                    "Closed",
                    "Reversed",
                  ] as string[],
                  match: (r, v) => r.status === v,
                },
                {
                  id: "entity",
                  label: "Entity",
                  options: [
                    "New World Cargo Logistics Zambia Ltd",
                    "New World Cargo Copperbelt Services Ltd",
                    "New World Cargo Engineering Zambia Ltd",
                  ] as string[],
                  match: (r, v) => r.entityName === v,
                },
              ]}
              columns={[
                {
                  id: "ref",
                  header: "Run",
                  cell: (r) => (
                    <span className="block min-w-0 max-w-64">
                      <Link
                        to="/hrm/payroll/runs/$id"
                        params={{ id: r.id }}
                        className="block truncate font-medium text-primary underline-offset-2 hover:underline"
                      >
                        {r.period} · {r.payGroup}
                      </Link>
                      <span className="block truncate font-mono text-xs text-muted-foreground">
                        {r.id}
                      </span>
                    </span>
                  ),
                },
                {
                  id: "entity",
                  header: "Entity",
                  cell: (r) => <span className="block max-w-48 truncate">{r.entityName}</span>,
                },
                {
                  id: "branch",
                  header: "Branch",
                  defaultVisible: true,
                  cell: (r) =>
                    r.branchId ? (
                      <span className="block max-w-40 truncate text-xs text-muted-foreground">
                        {branchName(r.branchId) ?? "Branch"}
                      </span>
                    ) : (
                      <span className="text-xs text-muted-foreground">Organisation-wide</span>
                    ),
                },
                {
                  id: "people",
                  header: "Employees",
                  cell: (r) => (
                    <span className="tabular">{r.totals.headcount || r.included || "—"}</span>
                  ),
                },
                {
                  id: "gross",
                  header: "Gross",
                  cell: (r) => (
                    <span className="tabular">
                      {r.totals.gross ? money(r.totals.gross, r.currency) : "—"}
                    </span>
                  ),
                },
                {
                  id: "net",
                  header: "Net",
                  cell: (r) => (
                    <span className="tabular font-medium">
                      {r.totals.net ? money(r.totals.net, r.currency) : "—"}
                    </span>
                  ),
                },
                { id: "status", header: "Status", cell: (r) => <StatusBadge status={r.status} /> },
                {
                  id: "next",
                  header: "Next action",
                  cell: (r) => (
                    <span className="block max-w-56 truncate text-xs">
                      {r.nextAction} · due {r.dueDate}
                    </span>
                  ),
                },
                { id: "owner", header: "Owner", defaultVisible: false, cell: (r) => r.owner },
                {
                  id: "prepared",
                  header: "Prepared by",
                  defaultVisible: false,
                  cell: (r) => r.preparedBy,
                },
              ]}
              emptyBody="No pay runs match the current view."
            />
          )}
        </Async>
      </AppShell>
    </AuthGate>
  );
}
