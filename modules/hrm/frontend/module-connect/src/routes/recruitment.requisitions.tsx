import { createFileRoute, Link, Outlet, useChildMatches } from "@tanstack/react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { entities } from "@/mock/data";
import { money, recruitmentApi } from "@/mock/recruitment";
import type { Requisition } from "@/mock/recruitment";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/recruitment/requisitions")({
  head: () => ({
    meta: [
      { title: "Requisitions — Meridian ERP HRM" },
      {
        name: "description",
        content:
          "Every request to fill a post: replacement or new position, establishment check, budget, approver and due date.",
      },
      { property: "og:title", content: "Requisitions — Meridian ERP HRM" },
      {
        property: "og:description",
        content:
          "Every request to fill a post: replacement or new position, establishment check, budget, approver and due date.",
      },
    ],
  }),
  component: RequisitionsRoute,
});

const entityName = (id: string) => entities.find((e) => e.id === id)?.name ?? "Unknown entity";
const open = ["Draft", "Submitted", "In review", "Returned"];

/**
 * "/recruitment/requisitions/new" is nested under this route, so hand the screen
 * over to the child when one is matched instead of rendering the list behind it.
 */
function RequisitionsRoute() {
  const children = useChildMatches();
  return children.length ? <Outlet /> : <RequisitionsList />;
}

function RequisitionsList() {
  const state = useMock(() => recruitmentApi.requisitions());
  const [view, setView] = useState("all");

  return (
    <AppShell>
      <PageHeader
        eyebrow="Recruitment"
        title="Requisitions"
        description="A requisition is the authority to fill a post. Each row shows why the post is needed, whether it sits within the approved establishment, who owns the next decision and when it is due."
        primaryAction={
          <Button asChild>
            <Link to="/recruitment/requisitions/new">Raise a requisition</Link>
          </Button>
        }
      />
      <Async state={state}>
        {(rows) => (
          <ListPage<Requisition>
            rows={rows.filter((r) =>
              view === "open"
                ? open.includes(r.status)
                : view === "approved"
                  ? r.status === "Approved"
                  : view === "over"
                    ? !r.establishment.within
                    : true,
            )}
            savedViews={[
              { id: "all", label: "All requisitions" },
              { id: "open", label: "Awaiting action" },
              { id: "approved", label: "Approved to advertise" },
              { id: "over", label: "Over establishment" },
            ]}
            activeView={view}
            onViewChange={setView}
            searchPlaceholder="Search reference, job title or hiring manager"
            searchFields={(r) => `${r.id} ${r.jobTitle} ${r.hiringManager} ${r.branch} ${r.department}`}
            filters={[
              {
                id: "reason",
                label: "Reason",
                options: ["Replacement", "New position"],
                match: (r, v) => r.reason === v,
              },
              {
                id: "status",
                label: "Status",
                options: ["Draft", "Submitted", "In review", "Approved", "Returned", "Rejected"],
                match: (r, v) => r.status === v,
              },
              {
                id: "entity",
                label: "Legal entity",
                options: entities.map((e) => e.name),
                match: (r, v) => entityName(r.entityId) === v,
              },
              {
                id: "establishment",
                label: "Establishment",
                options: ["Within establishment", "Over establishment"],
                match: (r, v) => (v === "Within establishment" ? r.establishment.within : !r.establishment.within),
              },
            ]}
            bulkActions={[{ label: "Export selection", onSelect: () => undefined }]}
            emptyBody="No requisitions match this view. Clear a filter, or raise a requisition to start a new hire."
            columns={[
              {
                id: "ref",
                header: "Reference",
                cell: (r) => <span className="font-mono text-xs">{r.id}</span>,
              },
              {
                id: "job",
                header: "Job title",
                cell: (r) => (
                  <span className="block max-w-56 truncate font-medium">{r.jobTitle}</span>
                ),
              },
              {
                id: "reason",
                header: "Reason",
                cell: (r) => (
                  <span className="block max-w-56 text-xs">
                    {r.reason}
                    {r.replacementFor ? (
                      <span className="block truncate text-muted-foreground">{r.replacementFor}</span>
                    ) : null}
                  </span>
                ),
              },
              {
                id: "where",
                header: "Entity and branch",
                cell: (r) => (
                  <span className="block max-w-56 truncate text-xs">
                    {entityName(r.entityId)}
                    <span className="block text-muted-foreground">{r.branch}</span>
                  </span>
                ),
              },
              {
                id: "headcount",
                header: "Headcount",
                cell: (r) => (
                  <span className="block text-xs">
                    <span className="tabular font-medium">{r.headcount}</span> at {r.grade}
                    <span className="block text-muted-foreground">
                      {r.establishment.within ? "Within establishment" : "Over establishment"}
                    </span>
                  </span>
                ),
              },
              { id: "status", header: "Status", cell: (r) => <StatusBadge status={r.status} /> },
              {
                id: "next",
                header: "Next action",
                cell: (r) => (
                  <span className="block max-w-64 text-xs">
                    {r.nextAction}
                    <span className="block text-muted-foreground">Due {r.dueDate}</span>
                  </span>
                ),
              },
              {
                id: "owner",
                header: "Owner",
                defaultVisible: false,
                cell: (r) => <span className="block max-w-56 truncate text-xs">{r.owner}</span>,
              },
              {
                id: "manager",
                header: "Hiring manager",
                defaultVisible: false,
                cell: (r) => <span className="block max-w-56 truncate text-xs">{r.hiringManager}</span>,
              },
              {
                id: "budget",
                header: "Annual cost",
                defaultVisible: false,
                cell: (r) => <span className="tabular text-xs">{money(r.annualCost, r.currency)}</span>,
              },
              {
                id: "start",
                header: "Target start",
                defaultVisible: false,
                cell: (r) => <span className="text-xs">{r.targetStartDate}</span>,
              },
              {
                id: "raised",
                header: "Raised",
                defaultVisible: false,
                cell: (r) => (
                  <span className="block max-w-48 truncate text-xs">
                    {r.raisedBy}
                    <span className="block text-muted-foreground">{r.raisedOn}</span>
                  </span>
                ),
              },
            ]}
          />
        )}
      </Async>
    </AppShell>
  );
}
