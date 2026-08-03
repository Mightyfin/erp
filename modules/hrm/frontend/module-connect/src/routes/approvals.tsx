import { createFileRoute, Link } from "@tanstack/react-router";
import { employees } from "@/mock/data";
import { api } from "@/mock/service";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/approvals")({
  head: () => ({
    meta: [
      { title: "Approvals — Meridian ERP HRM" },
      { name: "description", content: "Everything waiting on your decision, oldest and highest risk first." },
      { property: "og:title", content: "Approvals — Meridian ERP HRM" },
      { property: "og:description", content: "Everything waiting on your decision, oldest and highest risk first." },
    ],
  }),
  component: Approvals,
});

const name = (id: string) => employees.find((w) => w.id === id)?.fullName ?? "Unknown employee";
const open = new Set(["Submitted", "In review", "Returned"]);

interface Row {
  id: string;
  kind: "Leave" | "Attendance" | "HR request";
  title: string;
  employeeName: string;
  status: string;
  dueDate: string;
  to: string;
}

async function loadQueue(): Promise<Row[]> {
  const [leave, attendance, cases] = await Promise.all([api.leaveRequests(), api.attendance(), api.cases()]);
  const rows: Row[] = [
    ...leave.filter((r) => open.has(r.status)).map((r) => ({
      id: r.id,
      kind: "Leave" as const,
      title: `${r.type} leave · ${r.days} days`,
      employeeName: name(r.employeeId),
      status: r.status,
      dueDate: r.dueDate,
      to: "/leave/$id",
    })),
    ...attendance.filter((r) => open.has(r.status)).map((r) => ({
      id: r.id,
      kind: "Attendance" as const,
      title: `Correction · ${r.date}`,
      employeeName: name(r.employeeId),
      status: r.status,
      dueDate: r.dueDate,
      to: "/attendance/$id",
    })),
    ...cases.filter((r) => open.has(r.status)).map((r) => ({
      id: r.id,
      kind: "HR request" as const,
      title: r.subject,
      employeeName: name(r.employeeId),
      status: r.status,
      dueDate: r.dueDate,
      to: "/requests/$id",
    })),
  ];
  return rows.sort((a, b) => a.dueDate.localeCompare(b.dueDate));
}

function Approvals() {
  const state = useMock(loadQueue);

  return (
    <AppShell>
      <PageHeader
        eyebrow="Approvals"
        title="Approvals"
        description="Everything waiting on your decision across leave, attendance and HR requests, oldest due date first."
      />
      <Async state={state}>
        {(rows) => (
          <ListPage<Row>
            rows={rows}
            searchPlaceholder="Search reference, employee or title"
            searchFields={(r) => `${r.id} ${r.employeeName} ${r.title}`}
            filters={[
              { id: "kind", label: "Type", options: ["Leave", "Attendance", "HR request"], match: (r, v) => r.kind === v },
              { id: "status", label: "Status", options: ["Submitted", "In review", "Returned"], match: (r, v) => r.status === v },
            ]}
            columns={[
              { id: "ref", header: "Reference", cell: (r) => <Link to={r.to} params={{ id: r.id }} className="font-mono text-xs text-primary underline underline-offset-2">{r.id}</Link> },
              { id: "kind", header: "Type", cell: (r) => r.kind },
              { id: "title", header: "Item", cell: (r) => <span className="block max-w-64 truncate">{r.title}</span> },
              { id: "employee", header: "Employee", cell: (r) => <span className="block max-w-56 truncate">{r.employeeName}</span> },
              { id: "status", header: "Status", cell: (r) => <StatusBadge status={r.status} /> },
              { id: "due", header: "Due", cell: (r) => r.dueDate },
            ]}
            emptyBody="Nothing is waiting on a decision right now."
          />
        )}
      </Async>
    </AppShell>
  );
}
