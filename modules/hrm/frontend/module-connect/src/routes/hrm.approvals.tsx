import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { realApi, useApi } from "@/platform/use-api";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";

export const Route = createFileRoute("/hrm/approvals")({
  head: () => ({
    meta: [
      { title: "Approvals — Mightyfin ERP HRM" },
      { name: "description", content: "Everything waiting on your decision, oldest and highest risk first." },
      { property: "og:title", content: "Approvals — Mightyfin ERP HRM" },
      { property: "og:description", content: "Everything waiting on your decision, oldest and highest risk first." },
    ],
  }),
  component: Approvals,
});

interface Row {
  id: string;
  kind: "Leave" | "Attendance" | "HR request" | "Workflow";
  title: string;
  employeeName: string;
  status: string;
  opened: string;
  to: string;
}

const approvedStatuses: Record<string, string> = {
  pending: "Pending",
  submitted: "Submitted",
  open: "Open",
  "in-progress": "In progress",
  "in-review": "In review",
  "awaiting-employee": "Awaiting employee",
  returned: "Returned",
  approved: "Approved",
  rejected: "Rejected",
  resolved: "Resolved",
  closed: "Closed",
  cancelled: "Cancelled",
};

function labelStatus(raw: string): string {
  return approvedStatuses[String(raw ?? "").toLowerCase()] ?? String(raw ?? "").replace(/^(.)/, (c) => c.toUpperCase());
}

function isDecidable(status: string): boolean {
  return status === "Pending" || status === "Submitted" || status === "In review" || status === "Returned" || status === "Open" || status === "In progress" || status === "Awaiting employee";
}

async function loadQueue(): Promise<Row[]> {
  const [leave, corrections, exp, workflow] = await Promise.all([
    realApi.leaveRequests({ page: 1, pageSize: 50 }),
    realApi.timeCorrections({ page: 1, pageSize: 50 }),
    realApi.experienceRequests({ page: 1, pageSize: 50 }),
    realApi.workflowQueue(),
  ]);
  const rows: Row[] = [
    ...(Array.isArray(leave.items) ? leave.items : []).map((r) => {
      const x = r as Record<string, unknown>;
      return {
        id: String(x.id ?? ""),
        kind: "Leave" as const,
        title: `${String(x.leaveTypeCode ?? "leave")} · ${Number(x.requestedDays ?? 0)} days`,
        employeeName: String(x.workerName ?? "Unknown"),
        status: labelStatus(String(x.status ?? "")),
        opened: typeof x.createdAt === "string" ? String(x.createdAt).slice(0, 10) : "—",
        to: "/hrm/leave/$id",
      };
    }),
    ...(Array.isArray(corrections.items) ? corrections.items : []).map((r) => {
      const x = r as Record<string, unknown>;
      return {
        id: String(x.id ?? ""),
        kind: "Attendance" as const,
        title: `Correction · ${String(x.workDate ?? String(x.date ?? String(x.claimDate ?? "—")))}`,
        employeeName: String(x.workerName ?? "Unknown"),
        status: labelStatus(String(x.status ?? "")),
        opened: typeof x.createdAt === "string" ? String(x.createdAt).slice(0, 10) : "—",
        to: "/hrm/attendance/$id",
      };
    }),
    ...(Array.isArray(exp.items) ? exp.items : []).map((r) => {
      const x = r as Record<string, unknown>;
      return {
        id: String(x.id ?? ""),
        kind: "HR request" as const,
        title: String(x.subject ?? "HR request"),
        employeeName: String(x.workerName ?? "Unknown"),
        status: labelStatus(String(x.status ?? "")),
        opened: typeof x.createdAt === "string" ? String(x.createdAt).slice(0, 10) : "—",
        to: "/hrm/requests/$id",
      };
    }),
    ...(Array.isArray(workflow.items) ? workflow.items : []).map((r) => {
      const x = r as Record<string, unknown>;
      return {
        id: String(x.requestId ?? ""),
        kind: "Workflow" as const,
        title: `${String(x.workflowType ?? "request")} · ${String(x.subjectName ?? "Workflow item")}`,
        employeeName: String(x.subjectName ?? x.currentApproverName ?? "Workflow queue"),
        status: labelStatus(String(x.status ?? "")),
        opened: typeof x.dueAt === "string" ? String(x.dueAt).slice(0, 10) : "—",
        to: "/hrm/approvals/$id",
      };
    }),
  ];
  return rows.filter((r) => isDecidable(r.status));
}

function Approvals() {
  const state = useApi(loadQueue, []);
  const [view, setView] = useState("all");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Approvals"
        title="Approvals"
        description="Everything waiting on your decision across leave, attendance, HR requests and workflow items, oldest first."
      />
      <Async state={state}>
        {(rows) => (
          <ListPage<Row>
            rows={rows.filter((r) => (view === "open" ? true : r.kind === view))}
            savedViews={[
              { id: "all", label: "All types" },
              { id: "Leave", label: "Leave" },
              { id: "Attendance", label: "Attendance" },
              { id: "HR request", label: "HR requests" },
              { id: "Workflow", label: "Workflow" },
            ]}
            activeView={view}
            onViewChange={setView}
            searchPlaceholder="Search reference, employee or title"
            searchFields={(r) => `${r.id} ${r.employeeName} ${r.title}`}
            filters={[
              { id: "kind", label: "Type", options: ["Leave", "Attendance", "HR request", "Workflow"], match: (r, v) => r.kind === v },
            ]}
            columns={[
              {
                id: "ref",
                header: "Reference",
                cell: (r) => (
                  <Link to={r.to} params={{ id: r.id }} className="font-mono text-xs text-primary underline underline-offset-2">
                    {r.id.slice(0, 13)}…
                  </Link>
                ),
              },
              { id: "kind", header: "Type", cell: (r) => r.kind },
              { id: "title", header: "Item", cell: (r) => <span className="block max-w-64 truncate">{r.title}</span> },
              {
                id: "employee",
                header: "Employee",
                cell: (r) => <span className="block max-w-56 truncate">{r.employeeName}</span>,
              },
              { id: "status", header: "Status", cell: (r) => <StatusBadge status={r.status} /> },
              { id: "opened", header: "Opened", cell: (r) => r.opened },
            ]}
            emptyBody="Nothing is waiting on a decision right now."
          />
        )}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
