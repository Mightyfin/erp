import { createFileRoute } from "@tanstack/react-router";
import { employees } from "@/mock/data";
import { api } from "@/mock/service";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { ApprovalPanel } from "@/platform/components/ApprovalPanel";
import { RecordDetail } from "@/platform/components/RecordDetail";
import { RestrictedState } from "@/platform/components/States";
import { StatusTimeline } from "@/platform/components/StatusTimeline";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/requests/$id")({
  head: () => ({
    meta: [
      { title: "HR request — Mightyfin ERP HRM" },
      { name: "description", content: "One thread: conversation, evidence and status together." },
      { property: "og:title", content: "HR request — Mightyfin ERP HRM" },
      { property: "og:description", content: "One thread: conversation, evidence and status together." },
    ],
  }),
  component: RequestDetail,
});

function RequestDetail() {
  const { id } = Route.useParams();
  const state = useMock(() => api.caseItem(id), [id]);

  return (
    <AppShell>
      <Async state={state} rows={3}>
        {(c) => {
          if (!c) return <RestrictedState />;
          const w = employees.find((x) => x.id === c.employeeId);
          return (
            <RecordDetail
              reference={c.id}
              title={c.subject}
              subtitle={`${c.category} · ${w?.fullName ?? "Unknown employee"}`}
              status={c.status}
              owner={c.owner}
              nextAction={`${c.nextAction} · due ${c.dueDate}`}
              summary={[
                { label: "Employee", value: w?.fullName },
                { label: "Category", value: c.category },
                { label: "Priority", value: c.priority },
                { label: "Detail", value: c.detail },
              ]}
              timeline={<StatusTimeline title="Conversation and status" events={c.timeline} />}
            >
              <ApprovalPanel
                decisionSummary={`Resolve "${c.subject}" for ${w?.fullName ?? "this employee"}.`}
                policy={[]}
                conflicts={[]}
                onDecision={() => undefined}
              />
            </RecordDetail>
          );
        }}
      </Async>
    </AppShell>
  );
}
