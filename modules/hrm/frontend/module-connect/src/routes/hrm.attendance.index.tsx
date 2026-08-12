import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { employees } from "@/mock/data";
import { api } from "@/mock/service";
import type { AttendanceCorrection } from "@/mock/types";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/attendance/")({
  head: () => ({
    meta: [
      { title: "Attendance corrections — Mightyfin ERP HRM" },
      { name: "description", content: "Review and correct clocking exceptions with a full audit trail." },
      { property: "og:title", content: "Attendance corrections — Mightyfin ERP HRM" },
      { property: "og:description", content: "Review and correct clocking exceptions with a full audit trail." },
    ],
  }),
  component: AttendanceList,
});

const name = (id: string) => employees.find((w) => w.id === id)?.fullName ?? "Unknown employee";

function AttendanceList() {
  const state = useMock(() => api.attendance());
  const [view, setView] = useState("all");

  return (
    <AppShell>
      <PageHeader
        eyebrow="Attendance"
        title="Attendance corrections"
        description="Every correction shows the recorded clock data next to what's claimed, so nothing gets approved blind."
        primaryAction={
          <Button asChild>
            <Link to="/hrm/attendance/new">Raise a correction</Link>
          </Button>
        }
      />
      <Async state={state}>
        {(rows) => (
          <ListPage<AttendanceCorrection>
            rows={rows.filter((r) => (view === "open" ? !["Approved", "Rejected", "Cancelled"].includes(r.status) : true))}
            savedViews={[
              { id: "all", label: "All corrections" },
              { id: "open", label: "Awaiting action" },
            ]}
            activeView={view}
            onViewChange={setView}
            searchPlaceholder="Search reference or employee"
            searchFields={(r) => `${r.id} ${name(r.employeeId)} ${r.reason}`}
            filters={[
              { id: "status", label: "Status", options: ["Submitted", "In review", "Approved", "Returned"], match: (r, v) => r.status === v },
            ]}
            columns={[
              { id: "ref", header: "Reference", cell: (r) => <Link to="/hrm/attendance/$id" params={{ id: r.id }} className="font-mono text-xs text-primary underline underline-offset-2">{r.id}</Link> },
              { id: "employee", header: "Employee", cell: (r) => <span className="block max-w-56 truncate">{name(r.employeeId)}</span> },
              { id: "date", header: "Date", cell: (r) => r.date },
              { id: "recorded", header: "Recorded", cell: (r) => `${r.recordedIn ?? "—"}–${r.recordedOut ?? "—"}` },
              { id: "claimed", header: "Claimed", cell: (r) => `${r.claimedIn}–${r.claimedOut}` },
              { id: "status", header: "Status", cell: (r) => <StatusBadge status={r.status} /> },
              { id: "next", header: "Next action", cell: (r) => <span className="block max-w-56 truncate text-xs">{r.nextAction} · due {r.dueDate}</span> },
              { id: "owner", header: "Owner", defaultVisible: false, cell: (r) => r.owner },
            ]}
            emptyBody="No attendance corrections match the current view."
          />
        )}
      </Async>
    </AppShell>
  );
}
