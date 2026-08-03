import { createFileRoute, Link } from "@tanstack/react-router";
import { AlertTriangle, CheckCircle2, CircleDashed, Clock } from "lucide-react";
import { Button } from "@/components/ui/button";
import { employees } from "@/mock/data";
import {
  blockedTasks,
  displayName,
  lifecycleApi,
  overdueTasks,
  ownerLabel,
  taskOwners,
  taskProgress,
  TODAY,
} from "@/mock/lifecycle";
import type { LifecycleTask, TaskState } from "@/mock/lifecycle";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { DetailSection, RecordDetail } from "@/platform/components/RecordDetail";
import { RestrictedState } from "@/platform/components/States";
import { StatusTimeline } from "@/platform/components/StatusTimeline";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/lifecycle/onboarding/$id")({
  head: () => ({
    meta: [
      { title: "Onboarding case — Meridian ERP HRM" },
      { name: "description", content: "Onboarding checklist grouped by owner, with blockers and overdue tasks surfaced first." },
      { property: "og:title", content: "Onboarding case — Meridian ERP HRM" },
      { property: "og:description", content: "Onboarding checklist grouped by owner, with blockers and overdue tasks surfaced first." },
    ],
  }),
  component: OnboardingDetail,
});

const stateMeta: Record<TaskState, { icon: typeof Clock; cls: string }> = {
  Done: { icon: CheckCircle2, cls: "border-success/30 bg-success-soft text-success" },
  "In progress": { icon: Clock, cls: "border-info/30 bg-info-soft text-info" },
  "Not started": { icon: CircleDashed, cls: "border-border bg-muted text-muted-foreground" },
  Blocked: { icon: AlertTriangle, cls: "border-danger/30 bg-danger-soft text-danger" },
};

/** Task state carries an icon and a word — never colour on its own. */
function TaskStatePill({ state }: { state: TaskState }) {
  const m = stateMeta[state];
  const Icon = m.icon;
  return (
    <span className={`inline-flex shrink-0 items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium ${m.cls}`}>
      <Icon aria-hidden className="size-3.5 shrink-0" />
      {state}
    </span>
  );
}

function TaskRow({ task }: { task: LifecycleTask }) {
  const overdue = task.state !== "Done" && task.dueDate < TODAY;
  return (
    <li className="rounded-md border bg-surface-muted p-3">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="text-sm font-medium">{task.label}</p>
          <p className="mt-0.5 text-xs text-muted-foreground">{task.detail}</p>
        </div>
        <TaskStatePill state={task.state} />
      </div>
      <p className="mt-2 text-xs text-muted-foreground">
        {task.ownerName} · due {task.dueDate}
        {overdue ? <span className="ml-1 font-medium text-danger">· overdue</span> : null}
      </p>
      {task.blocker ? (
        <p className="mt-2 rounded border border-danger/30 bg-danger-soft px-2 py-1 text-xs text-danger">
          Blocked: {task.blocker}
        </p>
      ) : null}
    </li>
  );
}

/** Blocked first, then overdue, then everything else in due-date order. */
function rank(t: LifecycleTask) {
  if (t.state === "Blocked") return 0;
  if (t.state !== "Done" && t.dueDate < TODAY) return 1;
  if (t.state === "Done") return 3;
  return 2;
}

function OnboardingDetail() {
  const { id } = Route.useParams();
  const state = useMock(() => lifecycleApi.onboarding(id), [id]);

  return (
    <AppShell>
      <Async state={state} rows={3}>
        {(c) => {
          if (!c) return <RestrictedState />;
          const person = displayName(c);
          const progress = taskProgress(c.tasks);
          const blocked = blockedTasks(c.tasks);
          const overdue = overdueTasks(c.tasks).filter((t) => t.state !== "Blocked");
          const directoryRecord = employees.find((e) => e.id === c.employeeId);

          return (
            <RecordDetail
              reference={c.id}
              title={`Onboarding — ${person}`}
              subtitle={`${c.jobTitle} · ${c.branch} · starts ${c.startDate}`}
              status={c.status}
              owner={c.owner}
              nextAction={c.nextAction}
              dueDate={c.dueDate}
              primaryAction={
                <Button onClick={() => undefined}>
                  {blocked.length ? "Resolve blockers" : "Update checklist"}
                </Button>
              }
              secondaryActions={
                directoryRecord ? (
                  <Button variant="outline" asChild>
                    <Link to="/employees/$id" params={{ id: directoryRecord.id }}>
                      Open employee record
                    </Link>
                  </Button>
                ) : (
                  <span className="text-xs text-muted-foreground">
                    Not in the directory yet — the employee record is created when the contract is returned.
                  </span>
                )
              }
              summary={[
                { label: "Joiner", value: person },
                { label: "Role", value: c.jobTitle },
                { label: "Organisation", value: `${c.entity} · ${c.branch}` },
                { label: "Employment type", value: c.employmentType },
                { label: "Start date", value: c.startDate },
                { label: "Probation", value: `${c.probationStart} → ${c.probationEnd}` },
                { label: "Hiring manager", value: c.hiringManager },
                { label: "Checklist progress", value: `${progress.label} · ${blocked.length} blocked · ${overdue.length} overdue` },
              ]}
              timeline={<StatusTimeline title="Case history" events={c.timeline} />}
              related={
                <>
                  <p>
                    <Link to="/lifecycle/onboarding" className="text-primary underline underline-offset-2">
                      All onboarding cases
                    </Link>
                  </p>
                  <p>
                    <Link to="/lifecycle/movements" className="text-primary underline underline-offset-2">
                      Movements for this organisation
                    </Link>
                  </p>
                  <p className="text-xs text-muted-foreground">
                    Probation starts on {c.probationStart}. The review must be booked before the checklist can close.
                  </p>
                </>
              }
            >
              <DetailSection
                title="Blockers and overdue tasks"
                description="Everything standing between this joiner and a clean start, in one place."
              >
                {blocked.length === 0 && overdue.length === 0 ? (
                  <p className="text-sm text-muted-foreground">
                    Nothing is blocked and nothing is overdue. {progress.label}.
                  </p>
                ) : (
                  <ul className="space-y-2">
                    {[...blocked, ...overdue].map((t) => (
                      <TaskRow key={`blocker-${t.id}`} task={t} />
                    ))}
                  </ul>
                )}
              </DetailSection>

              <DetailSection
                title="Checklist by owner"
                description="Grouped so each owner sees only what they must do. Blocked and overdue tasks sit at the top of every group."
              >
                <div className="space-y-6">
                  {taskOwners.map((owner) => {
                    const tasks = c.tasks.filter((t) => t.owner === owner).sort((a, b) => rank(a) - rank(b) || a.dueDate.localeCompare(b.dueDate));
                    if (tasks.length === 0) return null;
                    const p = taskProgress(tasks);
                    return (
                      <div key={owner}>
                        <div className="flex flex-wrap items-baseline justify-between gap-2">
                          <h3 className="text-sm font-semibold">{ownerLabel[owner]}</h3>
                          <p className="text-xs text-muted-foreground">{p.label}</p>
                        </div>
                        <ul className="mt-2 space-y-2">
                          {tasks.map((t) => (
                            <TaskRow key={t.id} task={t} />
                          ))}
                        </ul>
                      </div>
                    );
                  })}
                </div>
              </DetailSection>
            </RecordDetail>
          );
        }}
      </Async>
    </AppShell>
  );
}
