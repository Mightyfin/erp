import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { Lock, ShieldAlert, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { configurationApi } from "@/mock/configuration";
import { Async } from "@/platform/components/Async";
import { ConfigPage, ConfigTable } from "@/platform/components/ConfigPage";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/configuration/compliance")({
  head: () => ({
    meta: [
      { title: "Security and compliance — Mightyfin ERP HRM" },
      { name: "description", content: "Protected-disclosure handling, privacy and consent administration, retention and audit." },
      { property: "og:title", content: "Security and compliance — Mightyfin ERP HRM" },
      { property: "og:description", content: "Protected-disclosure handling, privacy and consent administration, retention and audit." },
    ],
  }),
  component: ComplianceConfig,
});

const SECTIONS = [
  { id: "disclosure", label: "Protected disclosures" },
  { id: "privacy", label: "Privacy and consent" },
  { id: "retention", label: "Retention and audit" },
];

function ComplianceConfig() {
  const [tab, setTab] = useState("disclosure");
  const handlers = useMock(() => configurationApi.disclosureHandlers());
  const retention = useMock(() => configurationApi.retentionRules());

  return (
    <ConfigPage
      title="Security and compliance"
      description="Who can see the most sensitive things, and how long anything is kept."
      sections={SECTIONS}
      active={tab}
      onSelect={setTab}
    >
      {tab === "disclosure" ? (
        <Async state={handlers} rows={2}>
          {(rows) => (
            <>
              <div className="rounded-lg border border-danger/40 bg-danger-soft p-4">
                <p className="flex items-start gap-2 text-sm font-medium text-danger">
                  <ShieldAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
                  This cannot be granted to an HR administrator role
                </p>
                <p className="mt-1.5 text-sm text-foreground">
                  Protected disclosures are handled by named people only. The point of the channel is
                  that someone can report a concern about HR itself, so it is never a permission that
                  comes with the HR admin job.
                </p>
              </div>

              <ul className="space-y-2">
                {rows.map((h) => (
                  <li key={h.name} className="flex flex-wrap items-center gap-3 rounded-lg border bg-surface p-4">
                    <span className="min-w-0 flex-1">
                      <span className="block text-sm font-medium">{h.name}</span>
                      <span className="block text-xs text-muted-foreground">{h.role}</span>
                    </span>
                    {h.independent ? (
                      <span className="inline-flex shrink-0 items-center gap-1.5 rounded-full border border-success/30 bg-success-soft px-2.5 py-0.5 text-[11px] font-medium text-success">
                        <ShieldCheck className="size-3.5 shrink-0" aria-hidden />
                        Independent of the HR line
                      </span>
                    ) : (
                      <span className="shrink-0 text-[11px] text-muted-foreground">Internal handler</span>
                    )}
                  </li>
                ))}
              </ul>

              <p className="text-xs text-muted-foreground">
                At least one handler must sit outside the HR reporting line. Removing the independent
                handler is blocked until a replacement is named.
              </p>

              <Button variant="outline" size="sm" asChild>
                <Link to="/speak-up">View the reporting channel as an employee sees it</Link>
              </Button>
            </>
          )}
        </Async>
      ) : null}

      {tab === "privacy" ? (
        <>
          <ConfigTable
            caption="Processing purposes and their lawful basis"
            minWidth="36rem"
            headers={["Purpose", "Lawful basis", "Consent needed", "Withdrawable"]}
            rows={[
              ["Paying you", "Contract and legal obligation", <span className="text-xs text-muted-foreground">No</span>, <span className="text-xs text-muted-foreground">No</span>],
              ["Recording working time", "Contract", <span className="text-xs text-muted-foreground">No</span>, <span className="text-xs text-muted-foreground">No</span>],
              ["Biometric clocking", "Explicit consent", <span className="text-xs">Yes</span>, <span className="text-xs">Yes</span>],
              ["Occupational health", "Explicit consent", <span className="text-xs">Yes</span>, <span className="text-xs">Yes</span>],
              ["Background checks", "Explicit consent", <span className="text-xs">Yes</span>, <span className="text-xs text-muted-foreground">Not retrospectively</span>],
              ["Workforce analytics", "Legitimate interests", <span className="text-xs text-muted-foreground">No</span>, <span className="text-xs">Objection allowed</span>],
            ].map((r) => [<span className="font-medium">{r[0]}</span>, r[1], r[2], r[3]])}
          />
          <p className="mt-3 flex gap-2 rounded-md border border-info/30 bg-info-soft p-3 text-xs text-info">
            <Lock className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            <span>
              Subject-rights requests have a statutory response deadline. Configure the deadline and
              the responsible owner here; employees raise and track requests from their own privacy
              page.
            </span>
          </p>
          <Button variant="outline" size="sm" className="mt-3" asChild>
            <Link to="/hrm/people/privacy">View the employee privacy page</Link>
          </Button>
        </>
      ) : null}

      {tab === "retention" ? (
        <Async state={retention} rows={4}>
          {(rows) => (
            <>
              <ConfigTable
                caption="How long each record type is kept, and why"
                minWidth="40rem"
                headers={["Record", "Kept for", "Basis", "Then"]}
                rows={rows.map((r) => [
                  <span className="font-medium">{r.record}</span>,
                  <span className="text-sm">{r.keepFor}</span>,
                  <span className="text-xs text-muted-foreground">{r.basis}</span>,
                  <span className="text-xs">{r.thenWhat}</span>,
                ])}
              />
              <p className="mt-3 text-xs text-muted-foreground">
                A legal hold overrides every rule above. Nothing under hold is disposed of, even once
                its retention period has passed.
              </p>
            </>
          )}
        </Async>
      ) : null}
    </ConfigPage>
  );
}
