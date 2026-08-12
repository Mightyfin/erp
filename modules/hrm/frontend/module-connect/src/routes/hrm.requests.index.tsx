import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { employees } from "@/mock/data";
import { api } from "@/mock/service";
import type { HrCase } from "@/mock/types";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";

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

function RequestsList() {
  const state = useMock(() => api.cases());
  const [view, setView] = useState("all");

  return (
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
            searchFields={(r) => `${r.id} ${name(r.employeeId)} ${r.category} ${r.subject}`}
            filters={[
              { id: "category", label: "Category", options: ["Employment letter", "Personal data change"], match: (r, v) => r.category === v },
              { id: "priority", label: "Priority", options: ["Low", "Normal", "High"], match: (r, v) => r.priority === v },
            ]}
            columns={[
              { id: "ref", header: "Reference", cell: (r) => <Link to="/hrm/requests/$id" params={{ id: r.id }} className="font-mono text-xs text-primary underline underline-offset-2">{r.id}</Link> },
              { id: "employee", header: "Employee", cell: (r) => <span className="block max-w-56 truncate">{name(r.employeeId)}</span> },
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
  );
}
