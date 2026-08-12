import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { AlertTriangle, Ban, Check, Info } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { money } from "@/mock/payrollrun";
import { AppShell } from "@/platform/components/AppShell";
import { GuidedFlow, NextSteps } from "@/platform/components/GuidedFlow";
import type { FlowStep } from "@/platform/components/GuidedFlow";
import { PageHeader } from "@/platform/components/PageHeader";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/payroll/runs/new")({
  head: () => ({
    meta: [
      { title: "Start a pay run — Mightyfin ERP HRM" },
      { name: "description", content: "Open a pay period: choose the entity and pay group, confirm who is in and who is deliberately out, and check readiness before calculating." },
      { property: "og:title", content: "Start a pay run — Mightyfin ERP HRM" },
      { property: "og:description", content: "Open a pay period, confirm the population, and check readiness before calculating." },
    ],
  }),
  component: NewRun,
});

const ENTITIES = [
  { id: "ent-zm1", name: "Mighty Finance Solutions Industrial Zambia Ltd", currency: "ZMW" },
  { id: "ent-zm2", name: "Mighty Finance Solutions Services Zambia Ltd", currency: "ZMW" },
  { id: "ent-zm3", name: "Mighty Finance Solutions Agri Holdings Ltd", currency: "ZMW" },
];

const PAY_GROUPS = ["Monthly salaried", "Monthly — management", "Weekly — site crew"];

/** Everything that must be true before a calculation can be trusted. */
const READINESS = [
  { id: "r1", label: "Country pack active for the period", detail: "Zambia 2026.1 — PAYE bands, NAPSA ceiling and NHIMA rate.", state: "pass" as const },
  { id: "r2", label: "Attendance approved to cutoff", detail: "24 of 26 timesheets approved. 2 still with the line manager.", state: "warn" as const },
  { id: "r3", label: "Bank details present and verified", detail: "Every included employee has a verified account.", state: "pass" as const },
  { id: "r4", label: "No employee on two pay groups", detail: "Nobody would be paid twice.", state: "pass" as const },
  { id: "r5", label: "Previous period closed", detail: "July 2026 reconciled and locked.", state: "pass" as const },
];

const POPULATION = [
  { name: "Chanda Mwansa-Chileshe", in: true, note: "Active, monthly salaried" },
  { name: "Bwalya Musonda", in: true, note: "Active, monthly salaried" },
  { name: "Nalukui Simasiku", in: true, note: "Active, monthly salaried" },
  { name: "Temwani Phiri", in: true, note: "Returned from unpaid leave on 3 Aug — part period" },
  { name: "Mwaba Kalunga", in: false, note: "Left on 18 July. Final pay already released in the July run." },
  { name: "Lubinda Sitali", in: false, note: "On unpaid study leave for the whole period." },
];

function ReadinessRow({ item }: { item: (typeof READINESS)[number] }) {
  const pass = item.state === "pass";
  return (
    <li className="flex items-start gap-2 rounded-md border p-3">
      {pass ? (
        <Check className="mt-0.5 size-4 shrink-0 text-success" aria-hidden />
      ) : (
        <AlertTriangle className="mt-0.5 size-4 shrink-0 text-warning" aria-hidden />
      )}
      <span className="min-w-0">
        <span className="block text-sm font-medium">
          {item.label}
          <span className={`ml-2 text-xs font-normal ${pass ? "text-success" : "text-warning"}`}>
            {pass ? "Ready" : "Needs attention"}
          </span>
        </span>
        <span className="block text-xs text-muted-foreground">{item.detail}</span>
      </span>
    </li>
  );
}

function NewRun() {
  const navigate = useNavigate();
  const [ref, setRef] = useState<string | null>(null);
  const [entityId, setEntityId] = useState(ENTITIES[0].id);
  const [payGroup, setPayGroup] = useState(PAY_GROUPS[0]);
  const [period, setPeriod] = useState("2026-08");
  const [payDate, setPayDate] = useState("2026-08-28");
  const [cutoff, setCutoff] = useState("2026-08-24");
  const [excluded, setExcluded] = useState<string[]>(
    POPULATION.filter((p) => !p.in).map((p) => p.name),
  );
  const [note, setNote] = useState("");

  const entity = ENTITIES.find((e) => e.id === entityId) ?? ENTITIES[0];
  const included = POPULATION.filter((p) => !excluded.includes(p.name));
  const estimate = included.length * 20_878.88;
  const dateProblem = useMemo(
    () => (cutoff > payDate ? "The cutoff is after the pay date, so approved time would miss this run." : null),
    [cutoff, payDate],
  );

  const steps: FlowStep[] = [
    {
      id: "period",
      title: "Choose the period and pay group",
      purpose: "Which employer, which group of employees, and the dates that bound the run.",
      render: () => (
        <div className="max-w-lg space-y-4">
          <div>
            <Label htmlFor="entity">Legal entity</Label>
            <Select value={entityId} onValueChange={setEntityId}>
              <SelectTrigger id="entity" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {ENTITIES.map((e) => (
                  <SelectItem key={e.id} value={e.id}>
                    {e.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="mt-1 text-xs text-muted-foreground">
              The entity is the employer of record, so it decides the currency ({entity.currency}) and
              which statutory rules apply.
            </p>
          </div>

          <div>
            <Label htmlFor="group">Pay group</Label>
            <Select value={payGroup} onValueChange={setPayGroup}>
              <SelectTrigger id="group" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {PAY_GROUPS.map((g) => (
                  <SelectItem key={g} value={g}>
                    {g}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid gap-4 sm:grid-cols-3">
            <div>
              <Label htmlFor="period">Period</Label>
              <Input id="period" type="month" className="mt-1" value={period} onChange={(e) => setPeriod(e.target.value)} />
            </div>
            <div>
              <Label htmlFor="cutoff">Time cutoff</Label>
              <Input id="cutoff" type="date" className="mt-1" value={cutoff} onChange={(e) => setCutoff(e.target.value)} />
            </div>
            <div>
              <Label htmlFor="paydate">Pay date</Label>
              <Input id="paydate" type="date" className="mt-1" value={payDate} onChange={(e) => setPayDate(e.target.value)} />
            </div>
          </div>

          {dateProblem ? (
            <p role="alert" className="flex gap-2 rounded-md border border-danger/40 bg-danger-soft p-3 text-xs text-danger">
              <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
              {dateProblem}
            </p>
          ) : (
            <p className="flex gap-2 text-xs text-muted-foreground">
              <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
              Time approved after {cutoff} rolls into the next run rather than delaying this one.
            </p>
          )}
        </div>
      ),
    },
    {
      id: "population",
      title: "Confirm who is in the run",
      purpose: "Nobody is silently left out — every exclusion carries a reason.",
      render: () => (
        <div className="space-y-3">
          <p className="text-sm text-muted-foreground">
            {included.length} in, {excluded.length} out of {POPULATION.length} in this pay group.
          </p>
          <ul className="space-y-2">
            {POPULATION.map((p) => {
              const isOut = excluded.includes(p.name);
              return (
                <li key={p.name} className="flex flex-wrap items-start justify-between gap-3 rounded-md border p-3">
                  <span className="min-w-0 flex-1">
                    <span className="block text-sm font-medium">{p.name}</span>
                    <span className="block text-xs text-muted-foreground">{p.note}</span>
                  </span>
                  <span className="flex shrink-0 items-center gap-2">
                    {isOut ? (
                      <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground">
                        <Ban className="size-3.5 shrink-0" aria-hidden />
                        Excluded
                      </span>
                    ) : (
                      <span className="text-xs text-success">Included</span>
                    )}
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() =>
                        setExcluded((x) =>
                          isOut ? x.filter((n) => n !== p.name) : [...x, p.name],
                        )
                      }
                    >
                      {isOut ? "Include" : "Exclude"}
                    </Button>
                  </span>
                </li>
              );
            })}
          </ul>
        </div>
      ),
    },
    {
      id: "readiness",
      title: "Check readiness",
      purpose: "Anything here that is wrong would make the calculation wrong.",
      render: () => (
        <div className="space-y-3">
          <ul className="space-y-2">
            {READINESS.map((r) => (
              <ReadinessRow key={r.id} item={r} />
            ))}
          </ul>
          <p className="flex gap-2 rounded-md border border-warning/40 bg-warning-soft p-3 text-xs text-warning">
            <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            You can calculate with an outstanding warning, but it is recorded against the run and the
            approver will see it.
          </p>
        </div>
      ),
    },
    {
      id: "review",
      title: "Review and open the run",
      purpose: "What you are about to create, before it exists.",
      render: () => (
        <div className="max-w-xl space-y-4">
          <dl className="grid gap-3 sm:grid-cols-2">
            {[
              ["Entity", entity.name],
              ["Pay group", payGroup],
              ["Period", period],
              ["Pay date", payDate],
              ["Employees included", String(included.length)],
              ["Deliberately excluded", String(excluded.length)],
            ].map(([k, v]) => (
              <div key={k}>
                <dt className="text-xs text-muted-foreground">{k}</dt>
                <dd className="text-sm font-medium">{v}</dd>
              </div>
            ))}
          </dl>

          <div className="rounded-md border bg-surface-muted p-3">
            <p className="text-sm">
              Indicative gross, based on the last period:{" "}
              <span className="tabular font-medium">{money(estimate, entity.currency)}</span>
            </p>
            <p className="mt-1 text-xs text-muted-foreground">
              An estimate to sense-check the population, not a calculation. The real figures come out
              of the calculate stage.
            </p>
          </div>

          <div>
            <Label htmlFor="note">
              Note for the approver
              <span className="ml-1 text-xs font-normal text-muted-foreground">(optional)</span>
            </Label>
            <Textarea
              id="note"
              className="mt-1"
              rows={3}
              value={note}
              placeholder="Anything unusual about this period — a backdated increase, a one-off payment, a late starter."
              onChange={(e) => setNote(e.target.value)}
            />
          </div>

          <p className="flex gap-2 text-xs text-muted-foreground">
            <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            Opening the run does not calculate anything and pays nobody. It creates the period so
            work can start against it.
          </p>
        </div>
      ),
    },
  ];

  return (
    <AppShell>
      <PageHeader
        eyebrow="Payroll"
        title="Start a pay run"
        description="Open a pay period, confirm who is in it, and check that the inputs are trustworthy before anything is calculated."
        meta={
          <Button variant="outline" size="sm" asChild>
            <Link to="/hrm/payroll/runs">Back to pay runs</Link>
          </Button>
        }
      />

      <GuidedFlow
        flowId="payroll-run-new"
        steps={steps}
        submitLabel="Open the run"
        onSubmit={() => {
          if (dateProblem) {
            feedback.blocked("Cannot open this run", dateProblem);
            return;
          }
          const created = `RUN-${period.replace("-", "-")}-${entity.id.replace("ent-", "").toUpperCase()}-M`;
          setRef(created);
          feedback.submitted(
            `Run opened for ${included.length} employees.`,
            "Next: calculate gross to net. Nothing has been paid.",
          );
        }}
        submitted={
          ref ? (
            <NextSteps
              reference={ref}
              title="Pay run opened"
              steps={[
                "Calculate gross to net for the included employees. The calculation is resumable and shows its working.",
                "Review variances and exceptions — anything moving 2% or more since last period needs an explanation.",
                "Send for approval. Because you opened this run, someone else has to approve it.",
              ]}
              actions={
                <>
                  <Button onClick={() => navigate({ to: "/hrm/payroll/runs" })}>View pay runs</Button>
                  <Button variant="outline" asChild>
                    <Link to="/hrm/payroll">Back to Payroll</Link>
                  </Button>
                </>
              }
            />
          ) : undefined
        }
      />
    </AppShell>
  );
}
