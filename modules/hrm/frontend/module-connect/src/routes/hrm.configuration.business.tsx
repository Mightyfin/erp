import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { Check, CircleDashed, Pencil, TriangleAlert } from "lucide-react";
import { Button } from "@/components/ui/button";
import { EditDrawer } from "@/platform/components/EditDrawer";
import { feedback } from "@/platform/feedback";
import { configurationApi, money } from "@/mock/configuration";
import { Async } from "@/platform/components/Async";
import { ConfigPage, ConfigTable } from "@/platform/components/ConfigPage";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/configuration/business")({
  head: () => ({
    meta: [
      { title: "Business setup — New World Cargo HRM" },
      { name: "description", content: "Grades and pay ranges, calendars, country packs, languages and payroll components." },
      { property: "og:title", content: "Business setup — New World Cargo HRM" },
      { property: "og:description", content: "Grades and pay ranges, calendars, country packs, languages and payroll components." },
    ],
  }),
  component: BusinessConfig,
});

const SECTIONS = [
  { id: "grades", label: "Jobs, grades and pay ranges" },
  { id: "calendar", label: "Calendars and holidays" },
  { id: "packs", label: "Country packs" },
  { id: "lang", label: "Language" },
  { id: "payroll", label: "Payroll components" },
];

function BusinessConfig() {
  const [tab, setTab] = useState("grades");
  const [editing, setEditing] = useState<{ code: string; label: string; basis: string; effectiveFrom: string } | null>(null);
  const grades = useMock(() => configurationApi.grades());
  const holidays = useMock(() => configurationApi.holidays());
  const packs = useMock(() => configurationApi.countryPacks());
  const languages = useMock(() => configurationApi.languages());
  const components = useMock(() => configurationApi.payComponents());

  return (
    <ConfigPage
      title="Business setup"
      description="Who you are as an employer, and the rules that follow from where you operate."
      sections={SECTIONS}
      active={tab}
      onSelect={setTab}
    >
      {tab === "grades" ? (
        <Async state={grades} rows={4}>
          {(rows) => (
            <ConfigTable
              caption="Grades with pay ranges and current holders"
              headers={["Grade", "Job family", "Minimum", "Midpoint", "Maximum", "Holders"]}
              rows={rows.map((g) => [
                <span className="font-medium">{g.grade}</span>,
                g.family,
                <span className="tabular">{money(g.min)}</span>,
                <span className="tabular font-medium">{money(g.mid)}</span>,
                <span className="tabular">{money(g.max)}</span>,
                <span className="tabular">{g.holders || <span className="text-muted-foreground">None</span>}</span>,
              ])}
            />
          )}
        </Async>
      ) : null}

      {tab === "calendar" ? (
        <Async state={holidays} rows={5}>
          {(rows) => (
            <>
              <p className="text-xs text-muted-foreground">
                Zambian public holidays for 2026, plus any site-specific closure. A day marked here
                is not a working day, so it never counts against annual leave.
              </p>
              <ConfigTable
                caption="Public holidays and site closures for 2026"
                minWidth="28rem"
                headers={["Date", "Name", "Scope", "Note"]}
                rows={rows.map((h) => [
                  <span className="tabular">{h.date}</span>,
                  <span className="font-medium">{h.name}</span>,
                  h.scope === "Site" ? (
                    <span className="inline-flex items-center gap-1.5 text-xs text-warning">
                      <TriangleAlert className="size-3.5 shrink-0" aria-hidden />
                      Site only
                    </span>
                  ) : (
                    <span className="text-xs">National</span>
                  ),
                  <span className="text-xs text-muted-foreground">{h.note ?? "—"}</span>,
                ])}
              />
            </>
          )}
        </Async>
      ) : null}

      {tab === "packs" ? (
        <Async state={packs} rows={3}>
          {(rows) => (
            <ul className="space-y-3">
              {rows.map((p) => (
                <li key={p.id} className="rounded-lg border bg-surface p-4">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="text-sm font-medium">
                      {p.country} {p.version}
                    </span>
                    <span
                      className={`rounded-full border px-2 py-0.5 text-[11px] font-medium ${
                        p.status === "Active"
                          ? "border-success/30 bg-success-soft text-success"
                          : p.status === "Draft"
                            ? "border-warning/40 bg-warning-soft text-warning"
                            : "border-border bg-muted text-muted-foreground"
                      }`}
                    >
                      {p.status}
                    </span>
                    <span className="font-mono text-[11px] text-muted-foreground">{p.id}</span>
                    <span className="text-xs text-muted-foreground">from {p.effectiveFrom}</span>
                  </div>
                  <ul className="mt-2 flex flex-wrap gap-1.5">
                    {p.covers.map((c) => (
                      <li key={c} className="rounded-full border bg-surface-muted px-2 py-0.5 text-[11px]">
                        {c}
                      </li>
                    ))}
                  </ul>
                  <p className="mt-2 text-xs text-muted-foreground">{p.source}</p>
                  {p.approvedBy ? (
                    <p className="mt-1 text-xs text-muted-foreground">Approved by {p.approvedBy}</p>
                  ) : null}
                </li>
              ))}
              <li className="rounded-md border border-info/30 bg-info-soft p-3 text-xs text-info">
                A superseded pack is kept, not deleted. A 2025 payslip must stay reproducible against
                the rates that actually applied when it was paid.
              </li>
            </ul>
          )}
        </Async>
      ) : null}

      {tab === "lang" ? (
        <Async state={languages} rows={3}>
          {(rows) => (
            <ConfigTable
              caption="Languages available in the interface"
              minWidth="26rem"
              headers={["Language", "Code", "State", "Translated"]}
              rows={rows.map((l) => [
                <span className="font-medium">{l.name}</span>,
                <span className="font-mono text-xs">{l.code}</span>,
                <span className="text-xs">{l.state}</span>,
                <span className="flex items-center gap-2">
                  <span className="block h-1.5 w-20 overflow-hidden rounded-full bg-muted" role="presentation">
                    <span className="block h-full rounded-full bg-primary" style={{ width: `${l.coverage}%` }} />
                  </span>
                  <span className="tabular text-xs">{l.coverage}%</span>
                </span>,
              ])}
            />
          )}
        </Async>
      ) : null}

      {tab === "payroll" ? (
        <Async state={components} rows={5}>
          {(rows) => (
            <>
              <ConfigTable
                caption="Salary components and how each is calculated"
                headers={["Code", "Component", "Type", "Basis", "Taxable", "Pensionable", "From", ""]}
                rows={rows.map((c) => [
                  <span className="font-mono text-xs">{c.code}</span>,
                  <span className="font-medium">{c.label}</span>,
                  c.kind,
                  <span className="text-xs">{c.basis}</span>,
                  c.taxable ? <Check className="size-4 text-success" aria-label="Taxable" /> : <CircleDashed className="size-4 text-muted-foreground" aria-label="Not taxable" />,
                  c.pensionable ? <Check className="size-4 text-success" aria-label="Pensionable" /> : <CircleDashed className="size-4 text-muted-foreground" aria-label="Not pensionable" />,
                  <span className="tabular text-xs">{c.effectiveFrom}</span>,
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-7 gap-1.5 px-2 text-xs"
                    onClick={() => setEditing({ code: c.code, label: c.label, basis: c.basis, effectiveFrom: c.effectiveFrom })}
                  >
                    <Pencil className="size-3.5" aria-hidden />
                    Edit
                  </Button>,
                ])}
              />
              <p className="mt-3 text-xs text-muted-foreground">
                Also reachable from the Payroll workspace under Setup — it is the same configuration,
                not a second copy.
              </p>
            </>
          )}
        </Async>
      ) : null}

      <EditDrawer
        open={editing !== null}
        onOpenChange={(o) => !o && setEditing(null)}
        title={editing ? `Edit ${editing.label}` : "Edit component"}
        description="A change takes effect from the date you give and never alters a payslip already released."
        initial={{
          label: editing?.label ?? "",
          basis: editing?.basis ?? "",
          effectiveFrom: editing?.effectiveFrom ?? "",
        }}
        fields={[
          { name: "label", label: "Component name", required: true },
          { name: "basis", label: "How it is calculated", required: true, hint: "Shown to employees on the payslip explanation." },
          {
            name: "effectiveFrom",
            label: "Effective from",
            type: "date",
            required: true,
            validate: (v) =>
              v && v < "2026-08-01"
                ? "Choose a date in an open pay period. July 2026 is already released."
                : null,
          },
        ]}
        saveLabel="Save component"
        onSave={(v) =>
          feedback.saved(
            `${v.label} updated, effective ${v.effectiveFrom}.`,
            () => feedback.note("Component change reverted."),
          )
        }
        footerNote="Payroll is notified so the next run picks up the change."
      />
    </ConfigPage>
  );
}
