import { createFileRoute, Link, Outlet, useChildMatches } from "@tanstack/react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { entities } from "@/mock/data";
import {
  demoEntityTree,
  flattenEntityTree,
  treePathLabel,
  treeToSelectOptions,
  type OrgTreeNode,
} from "@/platform/orgTree";
import { money, recruitmentApi } from "@/mock/recruitment";
import type { Requisition } from "@/mock/recruitment";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";
import { realApi, useApi } from "@/platform/use-api";

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

export const Route = createFileRoute("/hrm/recruitment/requisitions")({
  head: () => ({
    meta: [
      { title: "Requisitions — Mightyfin ERP HRM" },
      {
        name: "description",
        content:
          "Every request to fill a post: replacement or new position, establishment check, budget, approver and due date.",
      },
      { property: "og:title", content: "Requisitions — Mightyfin ERP HRM" },
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

interface RequisitionRow extends Requisition {}

function adaptVacancy(
  v: Record<string, unknown>,
  unitNames: Record<string, string>,
  unitEntity: Record<string, string>,
): RequisitionRow {
  const status = String(v.status ?? "draft");
  const label = status === "draft" ? "Draft" : status === "published" ? "Approved" : status === "closed" ? "Rejected" : "Returned";
  return {
    id: String(v.id ?? ""),
    jobTitle: String(v.jobTitle ?? ""),
    reason: "New position" as Requisition["reason"],
    replacementFor: undefined,
    businessCase: String(v.description ?? "Raised as a vacancy on the requisition page."),
    hiringManager: "",
    recruiter: "Talent Acquisition",
    entityId: unitEntity[String(v.orgUnitId ?? "")] ?? "",
    branch: "—",
    department: unitNames[String(v.orgUnitId ?? "")] ?? "",
    grade: String(v.grade ?? "—"),
    employmentType: "Permanent" as Requisition["employmentType"],
    headcount: 1,
    targetStartDate: "",
    raisedBy: "HR",
    raisedOn: "",
    establishment: { approvedPosts: 0, filledPosts: 0, vacantPosts: 0, requested: 1, within: true, detail: "Establishment position confirmed in the requisition flow." } as Requisition["establishment"],
    budgetSource: "",
    annualCost: 0,
    currency: "ZMW",
    status: label as Requisition["status"],
    owner: "HR",
    nextAction: status === "published" ? "Advertised — collecting candidates" : "Publish the vacancy to start collecting candidates",
    dueDate: "",
    approvers: [],
    policy: [],
    conflicts: [],
    timeline: [],
  } as unknown as RequisitionRow;
}

function RequisitionsList() {
  const [view, setView] = useState("all");
  const mockState = useMock(() => recruitmentApi.requisitions());
  const realState = useApi(async () => {
    const [vacancies, units] = await Promise.all([
      realApi.recruitmentVacancies(),
      realApi.orgUnits(),
    ]);
    const unitNames: Record<string, string> = {};
    const unitEntity: Record<string, string> = {};
    for (const u of (units ?? []) as Record<string, unknown>[]) {
      unitNames[String(u.id ?? "")] = String(u.name ?? "");
      unitEntity[String(u.id ?? "")] = String(u.legalEntityId ?? "");
    }
    return (((vacancies as { items?: Record<string, unknown>[] } | undefined)?.items ?? []) as Record<string, unknown>[]).map((v) => adaptVacancy(v, unitNames, unitEntity));
  }, [view]);

  const treeState = useApi<OrgTreeNode[]>(async () => {
    if (USE_REAL) return (await realApi.entityTree()) as OrgTreeNode[];
    return demoEntityTree;
  }, [view]);
  const entityTreeOptions = treeToSelectOptions(treeState.data ?? []).map((o) => ({
    ...o,
    entity: o.value.startsWith("entity:"),
  }));
  const entityUnits = flattenEntityTree(treeState.data ?? []);
  const deptTreeOptions = entityUnits.map((e) => ({ value: e.unitName, label: treePathLabel(e.path) }));
  const state = USE_REAL ? realState : mockState;

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Recruitment"
        title="Requisitions"
        description="A requisition is the authority to fill a post. Each row shows why the post is needed, whether it sits within the approved establishment, who owns the next decision and when it is due."
        primaryAction={
          <Button asChild>
            <Link to="/hrm/recruitment/requisitions/new">Raise a requisition</Link>
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
                label: "Entity & branch",
                options: treeToSelectOptions(treeState.data ?? []).map((o) => o.value),
                treeOptions: entityTreeOptions,
                match: (r, v) =>
                  v.startsWith("entity:")
                    ? r.entityId === v.slice(7)
                    : r.department === v,
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
      </AuthGate>
  );
}
