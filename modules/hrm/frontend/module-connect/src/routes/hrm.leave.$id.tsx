import { createFileRoute } from "@tanstack/react-router";
import { employees } from "@/mock/data";
import { balanceFor } from "@/mock/leavebalance";
import { api } from "@/mock/service";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { ApprovalPanel } from "@/platform/components/ApprovalPanel";
import { RecordDetail } from "@/platform/components/RecordDetail";
import { RestrictedState } from "@/platform/components/States";
import { StatusTimeline } from "@/platform/components/StatusTimeline";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/leave/$id")({
  head: () => ({
    meta: [
      { title: "Leave decision — Mightyfin ERP HRM" },
      { name: "description", content: "Approve, return, reject or delegate a leave request with full policy context." },
      { property: "og:title", content: "Leave decision — Mightyfin ERP HRM" },
      { property: "og:description", content: "Approve, return, reject or delegate a leave request with full policy context." },
    ],
  }),
  component: LeaveDetail,
});

function LeaveDetail() {
  const { id } = Route.useParams();
  const state = useMock(() => api.leaveRequest(id), [id]);

  return (
    <AppShell>
      <Async state={state} rows={3}>
        {(r) => {
          if (!r) return <RestrictedState />;
          const w = employees.find((x) => x.id === r.employeeId);
          const bal = r.type === "Annual" ? balanceFor(r.employeeId) : null;
          return (
            <RecordDetail
              reference={r.id}
              title={`${r.type} leave — ${w?.fullName ?? "Unknown employee"}`}
              subtitle={`${r.from} → ${r.to} · ${r.days} days`}
              status={r.status}
              owner={r.owner}
              nextAction={`${r.nextAction} · due ${r.dueDate}`}
              summary={[
                { label: "Employee", value: w?.fullName },
                { label: "Job title", value: w?.jobTitle },
                { label: "Leave type", value: r.type },
                { label: "Days", value: r.days },
                {
                  label: "Balance if approved",
                  value: bal
                    ? `${Math.round((bal.available - r.days) * 10) / 10} days, from ${bal.available} now`
                    : "Not applicable",
                },
                { label: "Reason", value: r.reason ?? "Not given" },
              ]}
              timeline={<StatusTimeline title="History" events={r.timeline} />}
            >
              <ApprovalPanel
                decisionSummary={`Approve ${r.days} days of ${r.type.toLowerCase()} leave for ${w?.fullName ?? "this employee"}.`}
                policy={r.policy}
                conflicts={r.conflicts}
                onDecision={() => undefined}
              />
            </RecordDetail>
          );
        }}
      </Async>
    </AppShell>
  );
}
