import { createFileRoute } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { BadgeCheck, Download, FileText, Landmark } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { useRoleGate } from "@/platform/app-context";
import { useApi, realApi } from "@/platform/use-api";
import { reportsApi } from "@/mock/reports";
import type { ReportDef } from "@/mock/reports";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/reports/")({
  head: () => ({
    meta: [
      { title: "Statutory filings — Mightyfin ERP HRM" },
      {
        name: "description",
        content:
          "ZRA PAYE monthly returns and NAPSA / NHIMA remittance files generated from released pay runs, ready for filing and payment.",
      },
      { property: "og:title", content: "Statutory filings — Mightyfin ERP HRM" },
      {
        property: "og:description",
        content:
          "ZRA PAYE monthly returns and NAPSA / NHIMA remittance files generated from released pay runs, ready for filing and payment.",
      },
    ],
  }),
  component: ReportsPage,
});

/** The four statutory filings the HRM module produces. */
const FILINGS = [
  {
    id: "paye-return",
    name: "ZRA PAYE return",
    bureau: "Zambia Revenue Authority",
    description:
      "Monthly PAYE return with the employer registration block, one row per worker and period totals — the figures copied onto the ZRA return.",
    icon: Landmark,
  },
  { id: "zra", name: "ZRA schedule", bureau: "Zambia Revenue Authority", description: "Gross / PAYE / net schedule in ZRA layout.", icon: FileText },
  { id: "napsa", name: "NAPSA remittance", bureau: "National Pension Scheme Authority", description: "Employee + employer NAPSA contributions per worker.", icon: FileText },
  { id: "nhima", name: "NHIMA remittance", bureau: "National Health Insurance Management Authority", description: "Employee + employer NHIMA contributions per worker.", icon: FileText },
] as const;

function fmt(value: unknown) {
  const n = typeof value === "number" ? value : Number(value);
  if (Number.isNaN(n)) return "—";
  return n.toLocaleString("en-ZM", { style: "currency", currency: "ZMW", minimumFractionDigits: 2 });
}

/** Liability summary grid for the selected period. */
function SummaryGrid({ summary }: { summary: Record<string, unknown> }) {
  const rows: { label: string; value: string }[] = [
    { label: "Workers paid", value: String(summary.workerCount ?? "—") },
    { label: "Total gross pay", value: fmt(summary.totalGross) },
    { label: "PAYE payable (ZRA)", value: fmt(summary.totalPaye) },
    { label: "NAPSA — employee share", value: fmt(summary.totalNapsaEe) },
    { label: "NAPSA — employer share", value: fmt(summary.totalNapsaEr) },
    { label: "NHIMA — employee share", value: fmt(summary.totalNhimaEe) },
    { label: "NHIMA — employer share", value: fmt(summary.totalNhimaEr) },
    { label: "Total net pay", value: fmt(summary.totalNet) },
  ];
  return (
    <div className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
      {rows.map((r) => (
        <div key={r.label} className="rounded-lg border bg-surface p-4">
          <p className="text-xs text-muted-foreground">{r.label}</p>
          <p className="tabular mt-1 text-xl font-semibold">{r.value}</p>
        </div>
      ))}
    </div>
  );
}

function StatutorySection() {
  const canFile = useRoleGate()(["payroll", "hr_admin"]);
  const [periodId, setPeriodId] = useState<string>("");
  const [periodLabel, setPeriodLabel] = useState<string>("");

  // Default pay group is monthly ZMW — where all released runs live.
  const payGroups = useApi(() => realApi.payrollPayGroups(), []);
  const defaultGroupId = useMemo(
    () =>
      (Array.isArray(payGroups.data)
        ? (payGroups.data.find((g) => (g as Record<string, unknown>)?.code === "MONTHLY-ZMW") as Record<string, unknown> | undefined)
        : undefined)?.id,
    [payGroups.data],
  );

  const periods = useApi(
    () => (defaultGroupId ? realApi.payrollPayGroupPeriods(String(defaultGroupId)) : Promise.resolve([])),
    [defaultGroupId],
  );
  const summary = useApi(
    () => (periodId ? realApi.statutorySummary(periodId) : Promise.resolve(null)),
    [periodId],
  );

  // Pick the most recent released-period automatically on first load.
  const periodRows = Array.isArray(periods.data)
    ? (periods.data as Array<Record<string, unknown>>).filter((p) => p.status === "released")
    : [];
  const autoPick = useMemo(
    () => (!periodId && periodRows.length > 0 ? periodRows[0] : undefined),
    [periodId, periodRows],
  );
  if (autoPick) {
    setPeriodId(String(autoPick.id));
    setPeriodLabel(String(autoPick.periodLabel ?? ""));
  }

  const [downloading, setDownloading] = useState<Record<string, boolean>>({});

  async function download(exportType: string) {
    if (!periodId || !canFile) return;
    setDownloading((d) => ({ ...d, [exportType]: true }));
    try {
      const { url, fileName } = await realApi.statutoryGenerate(exportType, periodId);
      const a = document.createElement("a");
      a.href = url;
      a.download = fileName;
      document.body.appendChild(a);
      a.click();
      a.remove();
      // Blob URLs free memory once clicked.
      URL.revokeObjectURL(url);
    } finally {
      setDownloading((d) => ({ ...d, [exportType]: false }));
    }
  }

  const hasFilingData = Boolean(summary.data);

  return (
    <section aria-label="Statutory filings">
      <h2 className="text-sm font-semibold">Statutory filings</h2>
      <p className="mt-1 text-xs text-muted-foreground">
        Files are generated from released pay runs only. Select a period, review the liability summary, then download
        the filing for the relevant bureau.
      </p>

      <div className="mt-3 flex flex-wrap items-center gap-3">
        <Select
          value={periodId}
          onValueChange={(v) => {
            const row = (Array.isArray(periods.data) ? (periods.data as Array<Record<string, unknown>>) : []).find(
              (p) => String(p.id) === v,
            );
            setPeriodId(v);
            setPeriodLabel(String(row?.periodLabel ?? ""));
          }}
        >
          <SelectTrigger className="w-48">
            <SelectValue placeholder="Select a period" />
          </SelectTrigger>
          <SelectContent>
            {(Array.isArray(periods.data) ? (periods.data as Array<Record<string, unknown>>) : []).map((p) => (
              <SelectItem key={String(p.id)} value={String(p.id)}>
                {String(p.periodLabel ?? p.id)}
                {String(p.status) !== "released" ? " (not released)" : ""}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        {periodId &&
          FILINGS.map((f) => (
            <Button
              key={f.id}
              size="sm"
              variant="outline"
              disabled={!canFile || !periodId || !hasFilingData || downloading[f.id]}
              onClick={() => download(f.id)}
            >
              <Download className="mr-1.5 size-3.5 shrink-0" aria-hidden />
              {downloading[f.id] ? "Generating…" : f.name}
            </Button>
          ))}
      </div>

      {!canFile && periodId && (
        <p className="mt-2 text-xs text-warning">
          Statutory filings are restricted to Payroll and HR Admin roles.
        </p>
      )}

      <Async state={summary}>
        {(rows) => (rows && Object.keys(rows).length > 0 ? <SummaryGrid summary={rows} /> : (
          <p className="mt-3 rounded-lg border border-dashed bg-muted/40 p-4 text-xs text-muted-foreground">
            No released run found for this period — filings are only generated from released pay runs.
          </p>
        ))}
      </Async>
    </section>
  );
}

function ReportsPage() {
  const list = useMock(() => reportsApi.list());
  const [view, setView] = useState("all");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Reports"
        title="Statutory filings"
        description="ZRA PAYE returns and NAPSA / NHIMA remittance files generated from released pay runs — ready for filing and payment."
      />

      <StatutorySection />

      <section aria-label="Report catalogue" className="pt-6">
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
                          <span className="text-muted-foreground">{r.schedule}</span>
                        </span>
                      ) : (
                        <span className="text-xs text-muted-foreground">On demand</span>
                      ),
                  },
                  { id: "lastRun", header: "Last run", cell: (r) => r.lastRun },
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
