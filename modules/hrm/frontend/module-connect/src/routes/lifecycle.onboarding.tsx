import { createFileRoute, Link, Outlet, useChildMatches } from "@tanstack/react-router";
import { useState } from "react";
import { AlertTriangle } from "lucide-react";
import { blockedTasks, displayName, lifecycleApi, overdueTasks, taskProgress, TODAY } from "@/mock/lifecycle";
import type { OnboardingCase } from "@/mock/lifecycle";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/lifecycle/onboarding")({
  head: () => ({
    meta: [
      { title: "Onboarding — Meridian ERP HRM" },
      { name: "description", content: "Every joiner's onboarding case with progress, blockers, owner, next action and due date." },
      { property: "og:title", content: "Onboarding — Meridian ERP HRM" },
      { property: "og:description", content: "Every joiner's onboarding case with progress, blockers, owner, next action and due date." },
    ],
  }),
  component: OnboardingRoute,
});

const closed = ["Completed", "Cancelled"];

/** The detail route nests under this one; show the case when one is open, the list otherwise. */
function OnboardingRoute() {
  const children = useChildMatches();
  if (children.length > 0) return <Outlet />;
  return <OnboardingList />;
}

function Progress({ done, total }: { done: number; total: number }) {
  const pct = total === 0 ? 0 : Math.round((done / total) * 100);
  return (
    <span className="block w-28">
      <span className="tabular text-xs">
        {done} of {total} done
      </span>
      <span
        role="progressbar"
        aria-valuenow={pct}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label={`${done} of ${total} onboarding tasks done`}
        className="mt-1 block h-1.5 w-full overflow-hidden rounded-full bg-muted"
      >
        <span className="block h-full rounded-full bg-primary" style={{ width: `${pct}%` }} />
      </span>
    </span>
  );
}

function OnboardingList() {
  const state = useMock(() => lifecycleApi.onboardings());
  const [view, setView] = useState("all");

  return (
    <AppShell>
      <PageHeader
        eyebrow="Lifecycle"
        title="Onboarding"
        description="One case per joiner, from accepted offer to the end of probation. Every row shows who owns the case, what happens next and when it's due."
      />
      <Async state={state}>
        {(rows) => {
          const attention = (c: OnboardingCase) =>
            !closed.includes(c.status) && (blockedTasks(c.tasks).length > 0 || overdueTasks(c.tasks).length > 0);

          return (
            <ListPage<OnboardingCase>
              rows={rows.filter((c) =>
                view === "attention"
                  ? attention(c)
                  : view === "open"
                    ? !closed.includes(c.status)
                    : view === "closed"
                      ? closed.includes(c.status)
                      : true,
              )}
              savedViews={[
                { id: "all", label: "All cases" },
                { id: "open", label: "In progress" },
                { id: "attention", label: "Blocked or overdue" },
                { id: "closed", label: "Closed" },
              ]}
              activeView={view}
              onViewChange={setView}
              searchPlaceholder="Search reference, joiner or role"
              searchFields={(c) => `${c.id} ${displayName(c)} ${c.jobTitle} ${c.branch}`}
              filters={[
                {
                  id: "status",
                  label: "Status",
                  options: ["Draft", "Ready", "Active", "Blocked", "Completed", "Cancelled"],
                  match: (c, v) => c.status === v,
                },
                {
                  id: "branch",
                  label: "Branch",
                  options: ["Lusaka HQ", "Ndola Plant", "Kitwe Depot", "Livingstone Works", "Chingola Office", "Solwezi Yard"],
                  match: (c, v) => c.branch === v,
                },
                {
                  id: "owner",
                  label: "Owned by",
                  options: ["HR operations", "Hiring manager"],
                  match: (c, v) => (v === "Hiring manager" ? c.owner.includes("Hiring manager") : c.owner.includes("HR operations")),
                },
              ]}
              bulkActions={[{ label: "Export selection", onSelect: () => undefined }]}
              columns={[
                {
                  id: "ref",
                  header: "Reference",
                  cell: (c) => (
                    <Link
                      to="/lifecycle/onboarding/$id"
                      params={{ id: c.id }}
                      className="font-mono text-xs text-primary underline underline-offset-2"
                    >
                      {c.id}
                    </Link>
                  ),
                },
                {
                  id: "joiner",
                  header: "Joiner",
                  cell: (c) => (
                    <span className="block max-w-56 truncate">
                      {displayName(c)}
                      <span className="block truncate text-xs text-muted-foreground">{c.jobTitle}</span>
                    </span>
                  ),
                },
                { id: "start", header: "Start date", cell: (c) => <span className="tabular text-xs">{c.startDate}</span> },
                {
                  id: "progress",
                  header: "Checklist",
                  cell: (c) => {
                    const p = taskProgress(c.tasks);
                    return <Progress done={p.done} total={p.total} />;
                  },
                },
                {
                  id: "blockers",
                  header: "Blocked / overdue",
                  cell: (c) => {
                    const b = blockedTasks(c.tasks).length;
                    const o = overdueTasks(c.tasks).length;
                    if (b === 0 && o === 0) return <span className="text-xs text-muted-foreground">None</span>;
                    return (
                      <span className="inline-flex items-start gap-1.5 text-xs">
                        <AlertTriangle className="mt-0.5 size-3.5 shrink-0 text-warning" aria-hidden />
                        <span>
                          {b} blocked · {o} overdue
                        </span>
                      </span>
                    );
                  },
                },
                { id: "status", header: "Status", cell: (c) => <StatusBadge status={c.status} /> },
                {
                  id: "next",
                  header: "Next action",
                  cell: (c) => (
                    <span className="block max-w-64 truncate text-xs" title={c.nextAction}>
                      {c.nextAction}
                      <span className="block truncate text-muted-foreground">
                        {c.owner} · due {c.dueDate}
                      </span>
                    </span>
                  ),
                },
                { id: "owner", header: "Owner", defaultVisible: false, cell: (c) => <span className="text-xs">{c.owner}</span> },
                { id: "branch", header: "Branch", defaultVisible: false, cell: (c) => <span className="text-xs">{c.branch}</span> },
                { id: "type", header: "Employment type", defaultVisible: false, cell: (c) => <span className="text-xs">{c.employmentType}</span> },
                { id: "probation", header: "Probation start", defaultVisible: false, cell: (c) => <span className="tabular text-xs">{c.probationStart}</span> },
                { id: "manager", header: "Hiring manager", defaultVisible: false, cell: (c) => <span className="text-xs">{c.hiringManager}</span> },
              ]}
              emptyBody={`No onboarding cases match the current view. Today is ${TODAY}.`}
            />
          );
        }}
      </Async>
    </AppShell>
  );
}
