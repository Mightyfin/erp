import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { Check, CircleDashed, Download, TriangleAlert, Upload } from "lucide-react";
import { Button } from "@/components/ui/button";
import { configurationApi } from "@/mock/configuration";
import { Async } from "@/platform/components/Async";
import { ConfigPage, ConfigTable } from "@/platform/components/ConfigPage";
import { useMock } from "@/platform/use-mock";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/configuration/technical")({
  head: () => ({
    meta: [
      { title: "Technical settings — Meridian ERP HRM" },
      { name: "description", content: "Integrations, import and export, numbering series and HR vendors." },
      { property: "og:title", content: "Technical settings — Meridian ERP HRM" },
      { property: "og:description", content: "Integrations, import and export, numbering series and HR vendors." },
    ],
  }),
  component: TechnicalConfig,
});

const SECTIONS = [
  { id: "integrations", label: "Integrations" },
  { id: "data", label: "Import and export" },
  { id: "numbering", label: "Numbering" },
  { id: "vendors", label: "Vendors" },
];

function TechnicalConfig() {
  const [tab, setTab] = useState("integrations");
  const integrations = useMock(() => configurationApi.integrations());
  const numbering = useMock(() => configurationApi.numberSeries());
  const vendors = useMock(() => configurationApi.vendors());

  return (
    <ConfigPage
      title="Technical settings"
      description="What HRM connects to, and what it hands over. Rarely changed after go-live."
      sections={SECTIONS}
      active={tab}
      onSelect={setTab}
    >
      {tab === "integrations" ? (
        <Async state={integrations} rows={4}>
          {(rows) => (
            <ul className="space-y-2">
              {rows.map((i) => (
                <li key={i.id} className="rounded-lg border bg-surface p-4">
                  <div className="flex flex-wrap items-center gap-2">
                    {i.state === "Connected" ? (
                      <Check className="size-4 shrink-0 text-success" aria-hidden />
                    ) : i.state === "Error" ? (
                      <TriangleAlert className="size-4 shrink-0 text-danger" aria-hidden />
                    ) : (
                      <CircleDashed className="size-4 shrink-0 text-muted-foreground" aria-hidden />
                    )}
                    <span className="text-sm font-medium">{i.name}</span>
                    <span className="rounded-full border bg-surface-muted px-2 py-0.5 text-[11px]">{i.direction}</span>
                    <span
                      className={`text-xs font-medium ${
                        i.state === "Connected" ? "text-success" : i.state === "Error" ? "text-danger" : "text-muted-foreground"
                      }`}
                    >
                      {i.state}
                    </span>
                    {i.lastSync ? (
                      <span className="text-[11px] text-muted-foreground">last sync {i.lastSync}</span>
                    ) : null}
                  </div>
                  <p className="mt-1.5 text-xs text-muted-foreground">{i.note}</p>
                </li>
              ))}
            </ul>
          )}
        </Async>
      ) : null}

      {tab === "data" ? (
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="rounded-lg border bg-surface p-5">
            <h2 className="flex items-center gap-2 text-sm font-semibold">
              <Upload className="size-4 shrink-0 text-muted-foreground" aria-hidden />
              Import
            </h2>
            <p className="mt-1 text-xs text-muted-foreground">
              Every import is validated and previewed before it commits, the same way a bulk change
              is.
            </p>
            <ul className="mt-3 space-y-1.5 text-sm">
              {["Employees", "Attendance", "Leave balances", "Salary components"].map((t) => (
                <li key={t} className="flex items-center justify-between gap-3 border-b py-1.5 last:border-0">
                  <span>{t}</span>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-7 gap-1.5 px-2 text-xs"
                    onClick={() =>
                      feedback.note(
                        `${t} import template.`,
                        "Files are not generated in this build. The template carries the exact column names the import expects.",
                      )
                    }
                  >
                    <Download className="size-3.5" aria-hidden />
                    Template
                  </Button>
                </li>
              ))}
            </ul>
          </div>

          <div className="rounded-lg border bg-surface p-5">
            <h2 className="flex items-center gap-2 text-sm font-semibold">
              <Download className="size-4 shrink-0 text-muted-foreground" aria-hidden />
              Export
            </h2>
            <p className="mt-1 text-xs text-muted-foreground">
              Exports respect your entity and branch access, and every export is recorded.
            </p>
            <ul className="mt-3 space-y-1.5 text-sm">
              {["Full employee data", "Payroll register", "Configuration package", "Audit log"].map((t) => (
                <li key={t} className="flex items-center justify-between gap-3 border-b py-1.5 last:border-0">
                  <span>{t}</span>
                  <span className="text-[11px] text-muted-foreground">CSV, JSON</span>
                </li>
              ))}
            </ul>
            <p className="mt-3 text-xs text-muted-foreground">
              A configuration package can be moved between environments, so a change can be tested
              before it reaches live.
            </p>
          </div>
        </div>
      ) : null}

      {tab === "numbering" ? (
        <Async state={numbering} rows={4}>
          {(rows) => (
            <ConfigTable
              caption="Reference formats and the next number in each series"
              minWidth="28rem"
              headers={["Record", "Format", "Next"]}
              rows={rows.map((n) => [
                <span className="font-medium">{n.what}</span>,
                <span className="font-mono text-xs">{n.format}</span>,
                <span className="font-mono text-xs">{n.next}</span>,
              ])}
            />
          )}
        </Async>
      ) : null}

      {tab === "vendors" ? (
        <Async state={vendors} rows={4}>
          {(rows) => (
            <>
              <ConfigTable
                caption="HR vendors, what they do and what data they receive"
                minWidth="44rem"
                headers={["Vendor", "Service", "Contract to", "Data shared", "Review"]}
                rows={rows.map((v) => [
                  <span className="font-medium">{v.name}</span>,
                  <span className="text-xs">{v.service}</span>,
                  <span className="tabular text-xs">{v.contractTo}</span>,
                  <span className="text-xs text-muted-foreground">{v.dataShared}</span>,
                  <span className="text-xs">{v.review}</span>,
                ])}
              />
              <p className="mt-3 text-xs text-muted-foreground">
                "Data shared" is the actual field list, not a category. A vendor that only needs a
                name and an amount should never receive a full employee record.
              </p>
            </>
          )}
        </Async>
      ) : null}
    </ConfigPage>
  );
}
