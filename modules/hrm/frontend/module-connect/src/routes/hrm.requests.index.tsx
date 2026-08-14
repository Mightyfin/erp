import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { employees } from "@/mock/data";
import type { HrCase } from "@/mock/types";
import { realApi, useApi } from "@/platform/use-api";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";

export const Route = createFileRoute("/hrm/requests/")({
  head: () => ({
    meta: [
      { title: "HR requests — Mightyfin ERP HRM" },
      { name: "description", content: "Raise and track HR cases: letters, data changes and queries." },
      { property: "og:title", content: "HR requests — Mightyfin ERP HRM" },
      { property: "og:description", content: "Raise and track HR cases: letters, data changes and queries." },
    ],
  }),
  component: RequestsList,
});

const name = (id: string) => employees.find((w) => w.id === id)?.fullName ?? "Unknown employee";

const workflowStatus: Record<string, string> = {
  submitted: "In review",
  "in-review": "In review",
  approved: "Approved",
  rejected: "Rejected",
  returned: "Returned",
  escalated: "Escalated",
};

function adaptWorkflow(rows: unknown[]): HrCase[] {
  return rows.map((raw) => {
    const r = raw as Record<string, unknown>;
    const status = String(r.status ?? "");
    return {
      id: String(r.requestId ?? r.id ?? ""),
      employeeId: String(r.subjectWorkerId ?? ""),
      category: String(r.workflowType ?? "General"),
      subject: String(r.subjectName ?? "Workflow request"),
      detail: "",
      priority: "Normal",
      status: (workflowStatus[status] ?? "In review") as HrCase["status"],
      owner: String(r.currentApproverName ?? ""),
      nextAction: status === "submitted" || status === "in-review" ? "Awaiting decision" : "Closed",
      dueDate: typeof r.dueAt === "string" ? String(r.dueAt).slice(0, 10) : "—",
      timeline: [],
    } satisfies HrCase;
  });
}

function RequestsList() {
  const state = useApi(
    async () => adaptWorkflow((await realApi.workflowQueue()).items),
    [],
  );
  const [view, setView] = useState("all");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="HR requests"
        title="HR requests"
        description="Every request in one thread: conversation, status and next update together, never scattered across email."
        primaryAction={
          <Button asChild>
            <Link to="/hrm/requests/new">Raise a request</Link>
          </Button>
        }
      />
      <Async state={state}>
        {(rows) => (
          <ListPage<HrCase>
            rows={rows.filter((r) => (view === "open" ? !["Approved", "Rejected", "Cancelled"].includes(r.status) : true))}
            savedViews={[
              { id: "all", label: "All requests" },
              { id: "open", label: "Awaiting action" },
            ]}
            activeView={view}
            onViewChange={setView}
            searchPlaceholder="Search reference, employee or subject"
            searchFields={(r) => `${r.id} ${r.owner} ${r.category} ${r.subject}`}
            filters={[
              { id: "category", label: "Category", options: ["leave", "letter", "General"] as string[], match: (r, v) => r.category === v },
              { id: "priority", label: "Priority", options: ["Low", "Normal", "High"] as string[], match: (r, v) => r.priority === v },
            ]}
            columns={[
              { id: "ref", header: "Reference", cell: (r) => <Link to="/hrm/requests/$id" params={{ id: r.id }} className="font-mono text-xs text-primary underline underline-offset-2">{r.id}</Link> },
              { id: "employee", header: "Employee", cell: (r) => <span className="block max-w-56 truncate">{r.owner || name(r.employeeId)}</span> },
              { id: "category", header: "Category", cell: (r) => r.category },
              { id: "subject", header: "Subject", cell: (r) => <span className="block max-w-64 truncate">{r.subject}</span> },
              { id: "priority", header: "Priority", cell: (r) => r.priority },
              { id: "status", header: "Status", cell: (r) => <StatusBadge status={r.status} /> },
              { id: "next", header: "Next action", cell: (r) => <span className="block max-w-56 truncate text-xs">{r.nextAction} · due {r.dueDate}</span> },
              { id: "owner", header: "Owner", defaultVisible: false, cell: (r) => r.owner },
            ]}
            emptyBody="No HR requests match the current view."
          />
        )}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
