import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import {
  ArrowDownRight,
  ArrowUpRight,
  BadgeCheck,
  CalendarClock,
  Minus,
  ShieldCheck,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { reportsApi } from "@/mock/reports";
import type { Metric, ReportDef } from "@/mock/reports";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/reports/")({
  head: () => ({
    meta: [
      { title: "Reports — Mightyfin ERP HRM" },
      { name: "description", content: "Operational reports and workforce analytics, with certified definitions and privacy suppression." },
      { property: "og:title", content: "Reports — Mightyfin ERP HRM" },
      { property: "og:description", content: "Operational reports and workforce analytics, with certified definitions and privacy suppression." },
    ],
  }),
  component: ReportsPage,
});

/** Direction is shape + text; sentiment is never communicated by colour alone. */
function MetricCard({ m }: { m: Metric }) {
  const Arrow = m.direction === "up" ? ArrowUpRight : m.direction === "down" ? ArrowDownRight : Minus;
  const sentimentLabel =
    m.sentiment === "good" ? "Favourable" : m.sentiment === "bad" ? "Needs attention" : "Informational";

  return (
    <div className="rounded-lg border bg-surface p-4">
      <p className="text-xs text-muted-foreground">{m.label}</p>
      <p className="tabular mt-1 text-2xl font-semibold">{m.value}</p>
      <p className="mt-1 flex items-center gap-1.5 text-xs">
        <Arrow
          className={`size-3.5 shrink-0 ${
            m.sentiment === "good" ? "text-success" : m.sentiment === "bad" ? "text-warning" : "text-muted-foreground"
          }`}
          aria-hidden
        />
        <span className="text-muted-foreground">{m.change}</span>
      </p>
      <p className="mt-2 text-[11px] font-medium text-muted-foreground">{sentimentLabel}</p>
      <details className="mt-2">
        <summary className="cursor-pointer text-[11px] text-primary">How this is calculated</summary>
        <p className="mt-1 text-[11px] text-muted-foreground">{m.definition}</p>
        <p className="mt-1 text-[11px] text-muted-foreground">Source: {m.source}</p>
      </details>
    </div>
  );
}

function ReportsPage() {
  const metrics = useMock(() => reportsApi.metrics());
  const list = useMock(() => reportsApi.list());
  const [view, setView] = useState("all");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Reports"
        title="Reports and analytics"
        description="Reports respect the same permissions and scope as the screens they draw from — you only ever see what you could see anyway."
        primaryAction={<Button>New report</Button>}
      />

      <section aria-label="Headline metrics">
        <h2 className="text-sm font-semibold">This period</h2>
        <Async state={metrics} rows={2}>
          {(rows) => (
            <div className="mt-3 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              {rows.map((m) => (
                <MetricCard key={m.id} m={m} />
              ))}
            </div>
          )}
        </Async>
      </section>

      <section aria-label="Report catalogue" className="pt-2">
        <h2 className="text-sm font-semibold">Report catalogue</h2>
        <Async state={list} rows={5}>
          {(rows) => (
            <div className="mt-3">
              <ListPage<ReportDef>
                rows={rows.filter((r) =>
                  view === "certified" ? r.certified : view === "scheduled" ? Boolean(r.schedule) : true,
                )}
                savedViews={[
                  { id: "all", label: "All reports" },
                  { id: "certified", label: "Certified only" },
                  { id: "scheduled", label: "Scheduled" },
                ]}
                activeView={view}
                onViewChange={setView}
                searchPlaceholder="Search report name or description"
                searchFields={(r) => `${r.id} ${r.name} ${r.description} ${r.category}`}
                filters={[
                  {
                    id: "category",
                    label: "Category",
                    options: ["Workforce", "Time and absence", "Payroll and cost", "Compliance"],
                    match: (r, v) => r.category === v,
                  },
                ]}
                bulkActions={[{ label: "Export selection", onSelect: () => undefined }]}
                columns={[
                  {
                    id: "name",
                    header: "Report",
                    cell: (r) => (
                      <span className="block min-w-0 max-w-72">
                        <span className="flex items-center gap-1.5">
                          <span className="truncate text-sm font-medium">{r.name}</span>
                          {r.certified ? (
                            <BadgeCheck className="size-3.5 shrink-0 text-success" aria-label="Certified definition" />
                          ) : null}
                        </span>
                        <span className="block truncate text-xs text-muted-foreground">{r.description}</span>
                      </span>
                    ),
                  },
                  { id: "category", header: "Category", cell: (r) => r.category },
                  { id: "owner", header: "Data owner", cell: (r) => r.owner },
                  {
                    id: "schedule",
                    header: "Schedule",
                    cell: (r) =>
                      r.schedule ? (
                        <span className="inline-flex items-center gap-1.5 text-xs">
                          <CalendarClock className="size-3.5 shrink-0 text-muted-foreground" aria-hidden />
                          {r.schedule}
                        </span>
                      ) : (
                        <span className="text-xs text-muted-foreground">On demand</span>
                      ),
                  },
                  {
                    id: "privacy",
                    header: "Privacy",
                    cell: (r) =>
                      r.privacySuppression ? (
                        <span className="inline-flex items-center gap-1.5 text-xs">
                          <ShieldCheck className="size-3.5 shrink-0 text-info" aria-hidden />
                          Small groups suppressed
                        </span>
                      ) : (
                        <span className="text-xs text-muted-foreground">Standard scope</span>
                      ),
                  },
                  { id: "lastRun", header: "Last run", cell: (r) => r.lastRun },
                  {
                    id: "pit",
                    header: "Point-in-time",
                    defaultVisible: false,
                    cell: (r) => (r.pointInTime ? "Yes — reproducible" : "Live data"),
                  },
                  { id: "ref", header: "Reference", defaultVisible: false, cell: (r) => <span className="font-mono text-xs">{r.id}</span> },
                ]}
                emptyBody="No reports match the current view."
              />
            </div>
          )}
        </Async>
      </section>
    </AppShell>
      </AuthGate>
  );
}
