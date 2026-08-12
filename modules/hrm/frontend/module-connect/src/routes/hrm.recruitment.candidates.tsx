import { createFileRoute, Link, Outlet, useChildMatches } from "@tanstack/react-router";
import { useState } from "react";
import { ShieldCheck } from "lucide-react";
import { recruitmentApi, vacancyLabel } from "@/mock/recruitment";
import type { Candidate } from "@/mock/recruitment";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/recruitment/candidates")({
  head: () => ({
    meta: [
      { title: "Candidates — Mightyfin ERP HRM" },
      {
        name: "description",
        content:
          "Applicants by vacancy and selection stage, with the owner, the next action, the due date and the retention basis for each record.",
      },
      { property: "og:title", content: "Candidates — Mightyfin ERP HRM" },
      {
        property: "og:description",
        content: "Applicants by vacancy and selection stage, with owner, next action, due date and retention basis.",
      },
    ],
  }),
  component: CandidatesRoute,
});

const inProgress = ["Applied", "Screening", "Shortlisted", "Interview", "Offer"];
const closed = ["Hired", "Rejected", "Withdrawn"];

/**
 * "/recruitment/candidates/$id" is nested under this route, so hand the screen
 * over to the child when one is matched instead of rendering the list behind it.
 */
function CandidatesRoute() {
  const children = useChildMatches();
  return children.length ? <Outlet /> : <CandidatesList />;
}

function CandidatesList() {
  const state = useMock(() => recruitmentApi.candidates());
  const [view, setView] = useState("live");

  return (
    <AppShell>
      <PageHeader
        eyebrow="Recruitment"
        title="Candidates"
        description="Candidates are people outside the organisation. Only the information needed to run a fair selection process is held, and every record shows the stage it has reached, who owns it and what happens next."
        meta={
          <span className="inline-flex items-center gap-2 rounded-md border border-info/30 bg-info-soft px-3 py-1.5 text-xs text-info">
            <ShieldCheck className="size-3.5 shrink-0" aria-hidden />
            Candidate records are kept for six months from application unless the candidate is hired or asks for earlier
            deletion.
          </span>
        }
      />
      <Async state={state}>
        {(rows) => (
          <ListPage<Candidate>
            rows={rows.filter((c) =>
              view === "live"
                ? inProgress.includes(c.stage)
                : view === "offer"
                  ? c.stage === "Offer"
                  : view === "closed"
                    ? closed.includes(c.stage)
                    : true,
            )}
            savedViews={[
              { id: "live", label: "In the process" },
              { id: "offer", label: "At offer" },
              { id: "closed", label: "Closed" },
              { id: "all", label: "All candidates" },
            ]}
            activeView={view}
            onViewChange={setView}
            searchPlaceholder="Search candidate, reference or vacancy"
            searchFields={(c) => `${c.reference} ${c.fullName} ${vacancyLabel(c.vacancyId)} ${c.source}`}
            filters={[
              {
                id: "stage",
                label: "Stage",
                options: ["Applied", "Screening", "Shortlisted", "Interview", "Offer", "Hired", "Rejected", "Withdrawn"],
                match: (c, v) => c.stage === v,
              },
              {
                id: "source",
                label: "Source",
                options: ["Careers portal", "Referral", "Agency"],
                match: (c, v) => c.source === v,
              },
              {
                id: "vacancy",
                label: "Vacancy",
                options: Array.from(new Set(rows.map((c) => vacancyLabel(c.vacancyId)))),
                match: (c, v) => vacancyLabel(c.vacancyId) === v,
              },
              {
                id: "consent",
                label: "Consent",
                options: ["Consent current", "Consent expiring", "Consent withdrawn"],
                match: (c, v) => c.consent.state === v,
              },
            ]}
            bulkActions={[
              { label: "Send a holding update", onSelect: () => undefined },
              { label: "Export shortlist", onSelect: () => undefined },
            ]}
            emptyBody="No candidates match this view. Clear a filter, or check the vacancy is still open for applications."
            columns={[
              {
                id: "ref",
                header: "Reference",
                cell: (c) => (
                  <Link
                    to="/hrm/recruitment/candidates/$id"
                    params={{ id: c.id }}
                    className="font-mono text-xs text-primary underline underline-offset-2"
                  >
                    {c.reference}
                  </Link>
                ),
              },
              {
                id: "name",
                header: "Candidate",
                cell: (c) => (
                  <span className="block max-w-56 truncate font-medium">{c.fullName}</span>
                ),
              },
              {
                id: "vacancy",
                header: "Applied for",
                cell: (c) => <span className="block max-w-56 truncate text-xs">{vacancyLabel(c.vacancyId)}</span>,
              },
              {
                id: "stage",
                header: "Stage",
                cell: (c) => (
                  <span className="block text-xs">
                    {c.stage}
                    <span className="block text-muted-foreground">Applied {c.appliedOn}</span>
                  </span>
                ),
              },
              {
                id: "source",
                header: "Source",
                cell: (c) => <span className="block max-w-40 truncate text-xs">{c.source}</span>,
              },
              { id: "status", header: "Status", cell: (c) => <StatusBadge status={c.status} /> },
              {
                id: "next",
                header: "Next action",
                cell: (c) => (
                  <span className="block max-w-64 text-xs">
                    {c.nextAction}
                    <span className="block text-muted-foreground">Due {c.dueDate}</span>
                  </span>
                ),
              },
              {
                id: "owner",
                header: "Owner",
                defaultVisible: false,
                cell: (c) => <span className="block max-w-56 truncate text-xs">{c.owner}</span>,
              },
              {
                id: "consent",
                header: "Consent and retention",
                defaultVisible: false,
                cell: (c) => (
                  <span className="block max-w-56 text-xs">
                    {c.consent.state}
                    <span className="block text-muted-foreground">Retain until {c.consent.retainUntil}</span>
                  </span>
                ),
              },
              {
                id: "location",
                header: "Location",
                defaultVisible: false,
                cell: (c) => <span className="block max-w-48 truncate text-xs">{c.location}</span>,
              },
              {
                id: "notice",
                header: "Notice period",
                defaultVisible: false,
                cell: (c) => <span className="text-xs">{c.noticePeriod}</span>,
              },
              {
                id: "scorecards",
                header: "Scorecards",
                defaultVisible: false,
                cell: (c) => <span className="tabular text-xs">{c.scorecards.length}</span>,
              },
            ]}
          />
        )}
      </Async>
    </AppShell>
  );
}
