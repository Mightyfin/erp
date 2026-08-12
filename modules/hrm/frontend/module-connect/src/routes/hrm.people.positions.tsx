import { createFileRoute, Link, Outlet, useChildMatches } from "@tanstack/react-router";
import { AlertTriangle, BadgeCheck } from "lucide-react";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { entities } from "@/mock/data";
import type { Position } from "@/mock/structure";
import {
  licenceAttention,
  positionIncumbentName,
  shortEntityName,
  structureApi,
} from "@/mock/structure";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import type { ColumnDef } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/people/positions")({
  head: () => ({
    meta: [
      { title: "Positions — Mightyfin ERP HRM" },
      {
        name: "description",
        content:
          "Establishment register of positions: vacant, filled, frozen and closed, with incumbency, funding and mandatory licence status.",
      },
      { property: "og:title", content: "Positions — Mightyfin ERP HRM" },
      {
        property: "og:description",
        content:
          "Establishment register of positions: vacant, filled, frozen and closed, with incumbency, funding and mandatory licence status.",
      },
    ],
  }),
  component: PositionsRoute,
});

/** The position detail route nests under this one, so hand over when a child matches. */
function PositionsRoute() {
  const children = useChildMatches();
  return children.length ? <Outlet /> : <PositionsList />;
}

const views = [
  { id: "all", label: "All positions" },
  { id: "open", label: "Vacant and frozen" },
  { id: "attention", label: "Critical and licensed" },
  { id: "establishment", label: "Off establishment or unfunded" },
  { id: "filled", label: "Filled" },
];

const matchView = (p: Position, view: string) => {
  if (view === "open") return p.status === "Vacant" || p.status === "Frozen";
  if (view === "attention") return p.critical || Boolean(p.licence);
  if (view === "establishment") return !p.withinEstablishment || !p.funded;
  if (view === "filled") return p.status === "Filled";
  return true;
};

function EstablishmentFlag({ position }: { position: Position }) {
  if (!position.withinEstablishment) {
    return <span className="text-warning">Outside establishment</span>;
  }
  return position.funded ? (
    <span>Funded, within establishment</span>
  ) : (
    <span className="text-warning">Within establishment, not funded</span>
  );
}

function PositionsList() {
  const state = useMock(() => structureApi.positions());
  const [view, setView] = useState("all");

  const columns: ColumnDef<Position>[] = [
    {
      id: "position",
      header: "Position",
      cell: (p) => (
        <div className="min-w-0 max-w-72">
          <Link
            to="/hrm/people/positions/$id"
            params={{ id: p.id }}
            className="block truncate font-medium text-primary underline-offset-2 hover:underline"
          >
            {p.jobTitle}
          </Link>
          <span className="block truncate text-xs text-muted-foreground">{p.positionNo}</span>
          <span className="mt-1 flex flex-wrap gap-1">
            {p.critical ? (
              <span className="inline-flex items-center gap-1 rounded-full border border-warning/40 bg-warning-soft px-2 py-0.5 text-[11px] font-medium text-warning">
                <AlertTriangle aria-hidden className="size-3" />
                Critical position
              </span>
            ) : null}
            {p.licence ? (
              <span
                className={
                  licenceAttention(p)
                    ? "inline-flex items-center gap-1 rounded-full border border-warning/40 bg-warning-soft px-2 py-0.5 text-[11px] font-medium text-warning"
                    : "inline-flex items-center gap-1 rounded-full border bg-surface-muted px-2 py-0.5 text-[11px] font-medium text-muted-foreground"
                }
              >
                <BadgeCheck aria-hidden className="size-3" />
                Licence: {p.licence.status.toLowerCase()}
                {p.licence.holderExpiry ? ` — ${p.licence.holderExpiry}` : ""}
              </span>
            ) : null}
          </span>
        </div>
      ),
    },
    { id: "grade", header: "Grade", cell: (p) => p.grade },
    { id: "entity", header: "Entity", cell: (p) => shortEntityName(p.entityId) },
    { id: "branch", header: "Branch", cell: (p) => p.branch },
    {
      id: "department",
      header: "Department",
      cell: (p) => (
        <div className="min-w-0 max-w-48">
          <span className="block truncate">{p.department}</span>
          <span className="block truncate text-xs text-muted-foreground">{p.team}</span>
        </div>
      ),
    },
    {
      id: "incumbent",
      header: "Incumbent",
      cell: (p) =>
        p.incumbentId ? (
          <Link
            to="/hrm/employees/$id"
            params={{ id: p.incumbentId }}
            className="text-primary underline-offset-2 hover:underline"
          >
            {positionIncumbentName(p)}
          </Link>
        ) : (
          <span className="text-muted-foreground">No incumbent</span>
        ),
    },
    { id: "status", header: "Status", cell: (p) => <StatusBadge status={p.status} /> },
    { id: "fte", header: "FTE", defaultVisible: false, cell: (p) => p.fte.toFixed(1) },
    {
      id: "establishment",
      header: "Establishment",
      defaultVisible: false,
      cell: (p) => <EstablishmentFlag position={p} />,
    },
    {
      id: "licence",
      header: "Mandatory licence",
      defaultVisible: false,
      cell: (p) =>
        p.licence ? (
          <div className="min-w-0 max-w-64">
            <span className="block truncate">{p.licence.name}</span>
            <span className="block truncate text-xs text-muted-foreground">
              {p.licence.holderExpiry ? `Expires ${p.licence.holderExpiry}` : "No certified holder"}
            </span>
          </div>
        ) : (
          <span className="text-muted-foreground">None required</span>
        ),
    },
    { id: "family", header: "Job family", defaultVisible: false, cell: (p) => p.jobFamily },
    {
      id: "reportsTo",
      header: "Reports to",
      defaultVisible: false,
      cell: (p) => <span className="block max-w-56 truncate">{p.reportsTo}</span>,
    },
    {
      id: "effective",
      header: "Effective from",
      defaultVisible: false,
      cell: (p) => p.effectiveFrom,
    },
  ];

  return (
    <AppShell>
      <PageHeader
        eyebrow="People"
        title="Positions"
        description="The establishment register: every position, whether it is filled, and whether it is funded. A position exists independently of the employee who occupies it, so vacancies, freezes and closures stay visible."
        primaryAction={<Button
            onClick={() =>
              feedback.submitted(
                "New position started.",
                "A position needs a grade and a cost centre before it can be filled or budgeted.",
              )
            }
          >
            Create position
          </Button>}
      />
      <Async state={state} rows={6}>
        {(rows) => {
          const vacant = rows.filter((p) => p.status === "Vacant").length;
          const critical = rows.filter((p) => p.critical).length;
          const licences = rows.filter((p) => licenceAttention(p)).length;
          const unfunded = rows.filter((p) => !p.funded || !p.withinEstablishment).length;

          return (
            <div className="space-y-4">
              <dl className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                <div className="rounded-lg border bg-surface p-4">
                  <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                    Vacant positions
                  </dt>
                  <dd className="mt-1 text-lg font-semibold">{vacant}</dd>
                </div>
                <div className="rounded-lg border bg-surface p-4">
                  <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                    Critical positions
                  </dt>
                  <dd className="mt-1 text-lg font-semibold">{critical}</dd>
                </div>
                <div className="rounded-lg border border-warning/40 bg-warning-soft p-4">
                  <dt className="text-xs font-medium uppercase tracking-wide text-warning">
                    Licences needing action
                  </dt>
                  <dd className="mt-1 text-lg font-semibold text-warning">{licences}</dd>
                  <p className="mt-1 text-xs text-warning">
                    A lapsed mandatory licence makes the incumbent not fit to work in the position,
                    even though the position itself is unchanged.
                  </p>
                </div>
                <div className="rounded-lg border bg-surface p-4">
                  <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                    Unfunded or off establishment
                  </dt>
                  <dd className="mt-1 text-lg font-semibold">{unfunded}</dd>
                </div>
              </dl>

              <ListPage
                rows={rows.filter((p) => matchView(p, view))}
                columns={columns}
                savedViews={views}
                activeView={view}
                onViewChange={setView}
                searchPlaceholder="Search position number, title, department or incumbent"
                searchFields={(p) =>
                  `${p.positionNo} ${p.jobTitle} ${p.jobFamily} ${p.department} ${p.team} ${p.branch} ${
                    positionIncumbentName(p) ?? "vacant"
                  }`
                }
                filters={[
                  {
                    id: "entity",
                    label: "Entity",
                    options: entities.map((x) => x.name),
                    match: (p, v) => entities.find((x) => x.id === p.entityId)?.name === v,
                  },
                  {
                    id: "status",
                    label: "Status",
                    options: ["Vacant", "Filled", "Frozen", "Closed"],
                    match: (p, v) => p.status === v,
                  },
                  {
                    id: "branch",
                    label: "Branch",
                    options: entities.flatMap((x) => x.branches),
                    match: (p, v) => p.branch === v,
                  },
                  {
                    id: "establishment",
                    label: "Establishment",
                    options: [
                      "Funded and established",
                      "Not funded",
                      "Outside establishment",
                      "Critical position",
                    ],
                    match: (p, v) =>
                      v === "Funded and established"
                        ? p.funded && p.withinEstablishment
                        : v === "Not funded"
                          ? !p.funded
                          : v === "Outside establishment"
                            ? !p.withinEstablishment
                            : p.critical,
                  },
                  {
                    id: "licence",
                    label: "Mandatory licence",
                    options: ["Licence required", "Licence needs action", "No licence required"],
                    match: (p, v) =>
                      v === "Licence required"
                        ? Boolean(p.licence)
                        : v === "Licence needs action"
                          ? licenceAttention(p)
                          : !p.licence,
                  },
                ]}
                bulkActions={[
                  { label: "Export selection", onSelect: () => undefined },
                  { label: "Add to establishment review", onSelect: () => undefined },
                ]}
                rowHref={(p) => (
                  <Link
                    to="/hrm/people/positions/$id"
                    params={{ id: p.id }}
                    className="text-xs font-medium text-primary underline underline-offset-2"
                  >
                    Open
                  </Link>
                )}
                emptyBody="No positions match this view. Clear a filter, or switch back to all positions."
              />
            </div>
          );
        }}
      </Async>
    </AppShell>
  );
}
