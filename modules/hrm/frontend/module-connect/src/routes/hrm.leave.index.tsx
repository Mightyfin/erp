import { createFileRoute, Link } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { api } from "@/mock/service";
import type { LeaveRequest } from "@/mock/types";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { adaptWorkers, realApi, useApi } from "@/platform/use-api";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/leave/")({
  head: () => ({
    meta: [
      { title: "Leave requests — Mightyfin ERP HRM" },
      { name: "description", content: "Every leave request with status, owner, next action and due date." },
      { property: "og:title", content: "Leave requests — Mightyfin ERP HRM" },
      { property: "og:description", content: "Every leave request with status, owner, next action and due date." },
    ],
  }),
  component: LeaveList,
});

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

const mockStatus: Record<string, string> = {
  submitted: "Submitted",
  approved: "Approved",
  rejected: "Rejected",
  cancelled: "Cancelled",
  in_review: "In review",
};

function toMockLeave(rows: unknown[]): LeaveRequest[] {
  return rows.map((raw) => {
    const r = raw as Record<string, unknown>;
    const status = String(r.status ?? "");
    const type = String(r.leaveTypeCode ?? "");
    return {
      id: String(r.id),
      employeeId: String(r.workerId),
      type: (["Annual", "Sick", "Parental", "Unpaid", "Study"].includes(type) ? type : "Annual") as LeaveRequest["type"],
      from: String(r.startDate),
      to: String(r.endDate),
      days: Number(r.requestedDays ?? 0),
      status: (mockStatus[status] ?? status) as LeaveRequest["status"],
      nextAction: status === "submitted" ? "Awaiting manager approval" : status === "rejected" ? "Closed" : "Scheduled",
      dueDate: String(r.startDate),
      owner: String(r.workerName ?? ""),
      submittedAt: String(r.startDate),
      policy: [],
      timeline: [],
      conflicts: [],
    } satisfies LeaveRequest;
  });
}

function LeaveList() {
  const state = useApi(
    async () => {
      const [leave, workers] = await Promise.all([
        realApi.leaveRequests({ page: 1, pageSize: 200 }),
        USE_REAL ? realApi.employees({ page: 1, pageSize: 200 }) : undefined,
      ]);
      const rows = toMockLeave(leave.items);
      const index = new Map(
        workers ? adaptWorkers(workers).map((w) => [w.id, w.fullName]) : [],
      );
      return rows.map((r) => ({
        ...r,
        employeeName: index.get(r.employeeId) ?? r.owner ?? "Unknown employee",
      }));
    },
    [],
  );

  // Worker name lookups for mock mode (only ever needed when USE_REAL=false).
  const [mockNames, setMockNames] = useState<Record<string, string>>({});
  useEffect(() => {
    if (USE_REAL) return;
    let live = true;
    api.employees().then((ws) => {
      if (!live) return;
      setMockNames(Object.fromEntries(ws.map((w) => [w.id, w.fullName])));
    });
    return () => {
      live = false;
    };
  }, []);

  const [view, setView] = useState("all");

  return (
    <AppShell>
      <PageHeader
        eyebrow="Leave"
        title="Leave requests"
        description="Nothing here is a bare status: each row shows who owns it, what happens next and when it's due."
        primaryAction={
          <Button asChild>
            <Link to="/hrm/leave/new">Request leave</Link>
          </Button>
        }
      />
      <Async state={state}>
        {(rows) => (
          <ListPage<LeaveRequest & { employeeName: string }>
            rows={rows.filter((r) =>
              view === "open"
                ? !["Approved", "Rejected", "Cancelled"].includes(r.status)
                : view === "mine"
                  ? r.employeeId === "w-1001"
                  : true,
            )}
            savedViews={[
              { id: "all", label: "All requests" },
              { id: "open", label: "Awaiting action" },
              { id: "mine", label: "My requests" },
            ]}
            activeView={view}
            onViewChange={setView}
            searchPlaceholder="Search reference or employee"
            searchFields={(r) => `${r.id} ${r.employeeName} ${r.type}`}
            filters={[
              { id: "type", label: "Type", options: ["Annual", "Sick", "Parental", "Unpaid", "Study"], match: (r, v) => r.type === v },
              { id: "status", label: "Status", options: ["Submitted", "In review", "Approved", "Returned"], match: (r, v) => r.status === v },
            ]}
            bulkActions={[{ label: "Export selection", onSelect: () => undefined }]}
            columns={[
              { id: "ref", header: "Reference", cell: (r) => <Link to="/hrm/leave/$id" params={{ id: r.id }} className="font-mono text-xs text-primary underline underline-offset-2">{r.id}</Link> },
              { id: "employee", header: "Employee", cell: (r) => <span className="block max-w-56 truncate">{r.employeeName}</span> },
              { id: "type", header: "Type", cell: (r) => r.type },
              { id: "dates", header: "Dates", cell: (r) => `${r.from} → ${r.to}` },
              { id: "days", header: "Days", cell: (r) => <span className="tabular">{r.days}</span> },
              { id: "status", header: "Status", cell: (r) => <StatusBadge status={r.status} /> },
              { id: "next", header: "Next action", cell: (r) => <span className="block max-w-56 truncate text-xs">{r.nextAction} · due {r.dueDate}</span> },
              { id: "owner", header: "Owner", defaultVisible: false, cell: (r) => r.owner },
            ]}
          />
        )}
      </Async>
    </AppShell>
  );
}
