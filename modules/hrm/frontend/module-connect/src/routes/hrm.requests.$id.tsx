import { createFileRoute } from "@tanstack/react-router";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { ApprovalPanel } from "@/platform/components/ApprovalPanel";
import { RecordDetail } from "@/platform/components/RecordDetail";
import { RestrictedState } from "@/platform/components/States";
import { StatusTimeline } from "@/platform/components/StatusTimeline";
import { realApi, useApi } from "@/platform/use-api";

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

const workflowStatus: Record<string, string> = {
  submitted: "In review",
  "in-review": "In review",
  approved: "Approved",
  rejected: "Rejected",
  returned: "Returned",
  escalated: "Escalated",
};

function RequestDetail() {
  const { id } = Route.useParams();
  const state = useApi(
    () => realApi.workflowRequest(id).then((raw) => raw as unknown as Record<string, unknown>),
    [id],
  );

  return (
    <AppShell>
      <Async state={state} rows={3}>
        {(raw) => {
          if (!raw) return <RestrictedState />;
          const requestId = String(raw.requestId ?? raw.id ?? "");
          const subjectName = String(raw.subjectName ?? "HR request");
          const workflowType = String(raw.workflowType ?? "General");
          const status = workflowStatus[String(raw.status ?? "")] ?? String(raw.status ?? "");
          const decisions = Array.isArray(raw.decisions)
            ? (raw.decisions as unknown[]).map((d, i) => {
                const x = d as Record<string, unknown>;
                return {
                  id: String(x.id ?? `dec-${i}`),
                  at: typeof x.createdAt === "string" ? String(x.createdAt).slice(0, 16).replace("T", " ") : "",
                  actor: String(x.actorName ?? "System"),
                  event: `${String(x.action ?? "decision")}${x.reason ? ` — ${String(x.reason)}` : ""}`,
                  detail: String(x.delegatedToName ?? ""),
                };
              })
            : [];
          const payload = (() => {
            try {
              return typeof raw.payloadJson === "string" ? JSON.parse(raw.payloadJson) : null;
            } catch {
              return null;
            }
          })();

          return (
            <RecordDetail
              reference={requestId}
              title={subjectName}
              subtitle={`${workflowType} · status ${status}`}
              status={status}
              owner={String(raw.currentApproverName ?? "")}
              nextAction={`${String(raw.status ?? "") === "in-review" ? "Awaiting decision" : "Closed"}${raw.dueAt ? ` · due ${String(raw.dueAt).slice(0, 10)}` : ""}`}
              summary={[
                { label: "Subject", value: subjectName },
                { label: "Category", value: workflowType },
                { label: "Priority", value: "Normal" },
                { label: "Detail", value: payload ? JSON.stringify(payload, null, 1) : "Not provided" },
              ]}
              timeline={<StatusTimeline title="Conversation and status" events={decisions} />}
            >
              <ApprovalPanel
                decisionSummary={`Resolve "${subjectName}" for ${subjectName}.`}
                policy={[]}
                conflicts={[]}
                onDecision={async (d, reason) => {
                  await realApi.workflowDecide(requestId, d, reason || undefined);
                  state.reload();
                }}
              />
            </RecordDetail>
          );
        }}
      </Async>
    </AppShell>
  );
}
