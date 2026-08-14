import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { entities } from "@/mock/data";
import { api } from "@/mock/service";
import type { Employee } from "@/mock/types";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import type { ColumnDef } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";
import { adaptWorkers, realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/employees/")({
  head: () => ({
    meta: [
      { title: "Employees — Mightyfin ERP HRM" },
      { name: "description", content: "Filterable employee directory across entities, branches and employment types." },
      { property: "og:title", content: "Employees — Mightyfin ERP HRM" },
      { property: "og:description", content: "Filterable employee directory across entities, branches and employment types." },
    ],
  }),
  component: EmployeesPage,
});

const views = [
  { id: "all", label: "All employees" },
  { id: "active", label: "Active only" },
  { id: "ending", label: "Contracts ending" },
  { id: "prehire", label: "Pre-hire" },
];

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

function EmployeesPage() {
  const state = USE_REAL
    ? useApi(() => realApi.employees().then((page) => adaptWorkers(page.items)))
    : useMock(() => api.employees());
  const [view, setView] = useState("all");

  const columns: ColumnDef<Employee>[] = [
    {
      id: "name",
      header: "Employee",
      cell: (e) => (
        <div className="min-w-0 max-w-64">
          <Link to="/hrm/employees/$id" params={{ id: e.id }} className="block truncate font-medium text-primary underline-offset-2 hover:underline">
            {e.fullName}
          </Link>
          <span className="block truncate text-xs text-muted-foreground">{e.employeeNo}</span>
        </div>
      ),
    },
    { id: "title", header: "Job title", cell: (e) => <span className="block max-w-56 truncate">{e.jobTitle}</span> },
    { id: "dept", header: "Department", cell: (e) => e.department },
    { id: "entity", header: "Entity", cell: (e) => entities.find((x) => x.id === e.entityId)?.name.split(" ").slice(0, 2).join(" ") },
    { id: "branch", header: "Branch", cell: (e) => e.branch },
    { id: "type", header: "Type", cell: (e) => e.employmentType },
    { id: "status", header: "Status", cell: (e) => <StatusBadge status={e.status} /> },
    { id: "grade", header: "Grade", defaultVisible: false, cell: (e) => e.grade },
    { id: "start", header: "Start date", defaultVisible: false, cell: (e) => e.startDate },
    { id: "email", header: "Work email", defaultVisible: false, cell: (e) => e.email ?? <span className="text-muted-foreground">Not recorded</span> },
  ];

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="People"
        title="Employees"
        description="Everyone in scope for your entity and branch access. Select rows for bulk actions."
        primaryAction={
          <Button asChild>
            <Link to="/hrm/employees/new">Add employee</Link>
          </Button>
        }
      />
      <Async state={state} rows={6}>
        {(rows) => (
          <ListPage
            rows={rows.filter((e) =>
              view === "active" ? e.status === "Active" : view === "ending" ? Boolean(e.endDate) : view === "prehire" ? e.status === "Pre-hire" : true,
            )}
            columns={columns}
            savedViews={views}
            activeView={view}
            onViewChange={setView}
            searchPlaceholder="Search name, number or job title"
            searchFields={(e) => `${e.fullName} ${e.employeeNo} ${e.jobTitle} ${e.department}`}
            filters={[
              { id: "entity", label: "Entity", options: entities.map((x) => x.name), match: (e, v) => entities.find((x) => x.id === e.entityId)?.name === v },
              { id: "type", label: "Employment type", options: ["Permanent", "Fixed term", "Contractor", "Intern", "Part time"], match: (e, v) => e.employmentType === v },
              { id: "status", label: "Status", options: ["Active", "On leave", "Notice period", "Pre-hire"], match: (e, v) => e.status === v },
            ]}
            bulkActions={[
              { label: "Export selection", onSelect: () => undefined },
              { label: "Assign to manager", onSelect: () => undefined },
            ]}
            rowHref={(e) => (
              <Link to="/hrm/employees/$id" params={{ id: e.id }} className="text-xs font-medium text-primary underline underline-offset-2">
                Open
              </Link>
            )}
          />
        )}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
