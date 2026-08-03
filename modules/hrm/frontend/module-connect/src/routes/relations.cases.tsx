import { createFileRoute, Link, Outlet, useChildMatches } from "@tanstack/react-router";
import { useState } from "react";
import { EyeOff, ShieldAlert } from "lucide-react";
import { relationsApi } from "@/mock/relations";
import type { RelationsCase } from "@/mock/relations";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/relations/cases")({
  head: () => ({
    meta: [
      { title: "Employee relations cases — Meridian ERP HRM" },
      { name: "description", content: "A restricted case queue. The list shows only enough to triage — never the allegation itself." },
      { property: "og:title", content: "Employee relations cases — Meridian ERP HRM" },
      { property: "og:description", content: "A restricted case queue. The list shows only enough to triage — never the allegation itself." },
    ],
  }),
  component: CasesList,
});

function CasesList() {
  const state = useMock(() => relationsApi.cases());
  const [view, setView] = useState("open");
  // `/relations/cases/$id` is generated as a child of this route.
  const childMatches = useChildMatches();
  if (childMatches.length > 0) return <Outlet />;

  return (
    <AppShell>
      <PageHeader
        eyebrow="Relations and safety"
        title="Employee relations cases"
        description="Grievances, allegations and disputes. Access is restricted and every view is logged."
        meta={
          <span className="inline-flex items-center gap-1.5 rounded-full border border-danger/30 bg-danger-soft px-2.5 py-0.5 text-xs font-medium text-danger">
            <ShieldAlert className="size-3.5" aria-hidden />
            Restricted — your access to each case is recorded
          </span>
        }
      />

      <p className="flex gap-2 rounded-md border border-info/30 bg-info-soft p-3 text-sm text-info">
        <EyeOff className="mt-0.5 size-4 shrink-0" aria-hidden />
        <span>
          This list deliberately shows only enough to triage. The allegation, evidence and any
          findings sit behind a conflict-of-interest declaration on the case itself. Subjects of
          serious allegations are anonymised until findings are made.
        </span>
      </p>

      <Async state={state} rows={3}>
        {(rows) => (
          <ListPage<RelationsCase>
            rows={rows.filter((c) => (view === "open" ? c.stage !== "Closed" : view === "closed" ? c.stage === "Closed" : true))}
            savedViews={[
              { id: "open", label: "Open cases" },
              { id: "closed", label: "Closed" },
              { id: "all", label: "All" },
            ]}
            activeView={view}
            onViewChange={setView}
            searchPlaceholder="Search reference or type"
            searchFields={(c) => `${c.id} ${c.type} ${c.summary}`}
            filters={[
              {
                id: "type",
                label: "Type",
                options: ["Grievance", "Misconduct allegation", "Bullying or harassment", "Discrimination", "Workplace dispute"],
                match: (c, v) => c.type === v,
              },
              {
                id: "stage",
                label: "Stage",
                options: ["Intake", "Conflict check", "Investigation", "Hearing", "Findings", "Appeal", "Closed"],
                match: (c, v) => c.stage === v,
              },
            ]}
            columns={[
              {
                id: "ref",
                header: "Reference",
                cell: (c) => (
                  <Link to="/relations/cases/$id" params={{ id: c.id }} className="font-mono text-xs text-primary underline underline-offset-2">
                    {c.id}
                  </Link>
                ),
              },
              { id: "type", header: "Type", cell: (c) => c.type },
              {
                id: "subject",
                header: "Subject",
                cell: (c) => (
                  <span className="flex min-w-0 max-w-56 items-center gap-1.5">
                    {c.anonymised ? <EyeOff className="size-3.5 shrink-0 text-muted-foreground" aria-label="Anonymised" /> : null}
                    <span className="truncate">{c.subject}</span>
                  </span>
                ),
              },
              { id: "stage", header: "Stage", cell: (c) => c.stage },
              { id: "opened", header: "Opened", cell: (c) => c.opened },
              { id: "next", header: "Next action", cell: (c) => <span className="block max-w-56 truncate text-xs">{c.nextAction} · due {c.dueDate}</span> },
              { id: "owner", header: "Owner", defaultVisible: false, cell: (c) => c.owner },
            ]}
            emptyBody="No cases match the current view."
          />
        )}
      </Async>
    </AppShell>
  );
}
