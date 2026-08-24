import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { AlertTriangle, Ban, Check, Info } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { money } from "@/mock/payrollrun";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { adaptWorkers, realApi, useApi } from "@/platform/use-api";
import {
  demoEntityTree,
  flattenEntityTree,
  treeToSelectOptions,
  type OrgTreeNode,
} from "@/platform/orgTree";
import { GuidedFlow, NextSteps } from "@/platform/components/GuidedFlow";
import type { FlowStep } from "@/platform/components/GuidedFlow";
import { PageHeader } from "@/platform/components/PageHeader";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/payroll/runs/new")({
  head: () => ({
    meta: [
      { title: "Start a pay run — New World Cargo HRM" },
      { name: "description", content: "Open a pay period: choose the entity and pay group, confirm who is in and who is deliberately out, and check readiness before calculating." },
      { property: "og:title", content: "Start a pay run — New World Cargo HRM" },
      { property: "og:description", content: "Open a pay period, confirm the population, and check readiness before calculating." },
    ],
  }),
  component: NewRun,
});

const ENTITIES = [
  { id: "ent-zm1", name: "New World Cargo Zambia Ltd", currency: "ZMW" },
  { id: "ent-zm2", name: "New World Cargo Services Zambia Ltd", currency: "ZMW" },
  { id: "ent-zm3", name: "New World Cargo Holdings Zambia Ltd", currency: "ZMW" },
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

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

type PayGroupRow = {
  id: string;
  code?: string;
  name: string;
  currency: string;
  isDefault?: boolean;
};

type PayPeriodRow = {
  id: string;
  periodLabel: string;
  startDate: string;
  endDate: string;
  cutoffDate: string;
  payDate: string;
  status: string;
};

type ProfileRow = {
  id: string;
  workerId: string;
  workerName?: string | null;
  payGroupId: string;
  payGroupName?: string | null;
  effectiveFrom: string;
  values?: Array<{ amount?: number | string | null }>;
};

type PayrollSetup = {
  groups: PayGroupRow[];
  periods: PayPeriodRow[];
  profiles: ProfileRow[];
  workers: ReturnType<typeof adaptWorkers>;
  tree: OrgTreeNode[];
};

const asArray = (raw: unknown): unknown[] =>
  Array.isArray(raw)
    ? raw
    : raw && typeof raw === "object" && "items" in raw
      ? ((raw as { items?: unknown[] }).items ?? [])
      : [];

const text = (value: unknown) => (value == null ? "" : String(value));

const moneyAmount = (value: unknown) => {
  const n = typeof value === "number" ? value : Number(value ?? 0);
  return Number.isFinite(n) ? n : 0;
};

function NewRun() {
  const navigate = useNavigate();
  const [ref, setRef] = useState<string | null>(null);
  const [entityId, setEntityId] = useState(ENTITIES[0].id);
  const [payGroup, setPayGroup] = useState("");
  const [periodId, setPeriodId] = useState("");
  const [period, setPeriod] = useState("2026-08");
  const [payDate, setPayDate] = useState("2026-08-28");
  const [cutoff, setCutoff] = useState("2026-08-24");
  const [excluded, setExcluded] = useState<string[]>(
    POPULATION.filter((p) => !p.in).map((p) => p.name),
  );
  const [note, setNote] = useState("");

  const setup = useApi(
    async (): Promise<PayrollSetup> => {
      if (!USE_REAL) {
        return { groups: [], periods: [], profiles: [], workers: [], tree: demoEntityTree };
      }
      const [groupsRaw, profilesRaw, workersRaw, treeRaw] = await Promise.all([
        realApi.payrollPayGroups(),
        realApi.payrollProfiles(),
        realApi.employees({ status: "active" }),
        realApi.entityTree(),
      ]);
      const groups = asArray(groupsRaw).map((g) => {
        const row = g as Record<string, unknown>;
        return {
          id: text(row.id),
          code: row.code ? text(row.code) : undefined,
          name: text(row.name || row.code || "Pay group"),
          currency: text(row.currency || "ZMW"),
          isDefault: Boolean(row.isDefault),
        };
      });
      const selectedGroupId = payGroup || groups.find((g) => g.isDefault)?.id || groups[0]?.id || "";
      const periods = selectedGroupId
        ? asArray(await realApi.payrollPayGroupPeriods(selectedGroupId)).map((p) => {
            const row = p as Record<string, unknown>;
            return {
              id: text(row.id),
              periodLabel: text(row.periodLabel),
              startDate: text(row.startDate),
              endDate: text(row.endDate),
              cutoffDate: text(row.cutoffDate),
              payDate: text(row.payDate),
              status: text(row.status),
            };
          })
        : [];
      const profiles = asArray(profilesRaw).map((p) => {
        const row = p as Record<string, unknown>;
        return {
          id: text(row.id),
          workerId: text(row.workerId),
          workerName: row.workerName ? text(row.workerName) : null,
          payGroupId: text(row.payGroupId),
          payGroupName: row.payGroupName ? text(row.payGroupName) : null,
          effectiveFrom: text(row.effectiveFrom),
          values: Array.isArray(row.values)
            ? (row.values as Array<{ amount?: number | string | null }>)
            : [],
        };
      });
      return {
        groups,
        periods,
        profiles,
        workers: adaptWorkers(workersRaw),
        tree: Array.isArray(treeRaw) ? (treeRaw as OrgTreeNode[]) : demoEntityTree,
      };
    },
    [payGroup],
  );

  const defaultGroupId =
    setup.data?.groups.find((g) => g.isDefault)?.id || setup.data?.groups[0]?.id || "";
  const selectedGroupId = USE_REAL ? payGroup || defaultGroupId : payGroup || PAY_GROUPS[0];
  const selectedGroup = setup.data?.groups.find((g) => g.id === selectedGroupId);

  useEffect(() => {
    if (!USE_REAL || !setup.data) return;
    if (!payGroup && defaultGroupId) setPayGroup(defaultGroupId);
  }, [defaultGroupId, payGroup, setup.data]);

  useEffect(() => {
    if (!USE_REAL || !setup.data?.periods.length) return;
    const current = setup.data.periods.find((p) => p.id === periodId);
    if (current) return;
    const open = setup.data.periods.find((p) => p.status === "open") ?? setup.data.periods[0];
    setPeriodId(open.id);
    setPeriod(open.periodLabel);
    setCutoff(open.cutoffDate);
    setPayDate(open.payDate);
  }, [periodId, setup.data]);

  const placementUnits = flattenEntityTree(setup.data?.tree ?? demoEntityTree);
  const placementOptions = treeToSelectOptions(setup.data?.tree ?? demoEntityTree).map((o) => ({
    ...o,
    entity: o.value.startsWith("entity:"),
  }));
  const chosenPeriod = USE_REAL
    ? setup.data?.periods.find((p) => p.id === periodId)
    : undefined;

  const entity = ENTITIES.find((e) => e.id === entityId) ?? ENTITIES[0];
  const entityEntityId = placementUnits.find((p) => p.unitType === "entity")?.entityId ?? entityId;
  const liveProfiles = (setup.data?.profiles ?? []).filter((p) => p.payGroupId === selectedGroupId);
  const liveWorkerById = new Map((setup.data?.workers ?? []).map((w) => [w.id, w]));
  const livePopulation = liveProfiles.map((profile) => {
    const worker = liveWorkerById.get(profile.workerId);
    return {
      id: profile.workerId,
      name: profile.workerName || worker?.fullName || "Unnamed worker",
      note: `${worker?.employeeNo ?? "No employee number"} · ${worker?.jobTitle || "No job title"} · profile effective ${profile.effectiveFrom || "unknown"}`,
      amount: (profile.values ?? []).reduce((sum, value) => sum + moneyAmount(value.amount), 0),
    };
  });
  const included = USE_REAL ? livePopulation : POPULATION.filter((p) => !excluded.includes(p.name));
  const estimate = USE_REAL
    ? livePopulation.reduce((sum, p) => sum + p.amount, 0)
    : included.length * 20_878.88;
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
            <Select
              value={USE_REAL && setup.data?.tree ? `entity:${entityEntityId}` : entityId}
              onValueChange={(v) =>
                v.startsWith("entity:") ? setEntityId(v.slice(7)) : setEntityId(v)
              }
            >
              <SelectTrigger id="entity" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {placementOptions.map((o) => (
                  <SelectItem
                    key={o.value}
                    value={o.value}
                    className={o.entity ? "font-semibold text-primary" : undefined}
                  >
                    {o.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="mt-1 text-xs text-muted-foreground">
              The entity is the employer of record, so it decides the currency ({entity.currency}) and
              which statutory rules apply. Branches and departments sit under their entity in the list below.
            </p>
          </div>

          <div>
            <Label htmlFor="group">Pay group</Label>
            <Select value={selectedGroupId} onValueChange={setPayGroup}>
              <SelectTrigger id="group" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {(USE_REAL
                  ? setup.data?.groups ?? []
                  : PAY_GROUPS.map((g) => ({ id: g, name: g }))
                ).map((g) => (
                  <SelectItem key={g.id} value={g.id}>
                    {g.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid gap-4 sm:grid-cols-3">
            <div>
              <Label htmlFor="period">Period</Label>
              {USE_REAL && setup.data?.periods.length ? (
                <Select
                  value={periodId}
                  onValueChange={(value) => {
                    const next = setup.data?.periods.find((p) => p.id === value);
                    setPeriodId(value);
                    if (next) {
                      setPeriod(next.periodLabel);
                      setCutoff(next.cutoffDate);
                      setPayDate(next.payDate);
                    }
                  }}
                >
                  <SelectTrigger id="period" className="mt-1"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {setup.data.periods.map((p) => (
                      <SelectItem key={p.id} value={p.id}>
                        {p.periodLabel} ({p.status})
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              ) : (
                <Input id="period" type="month" className="mt-1" value={period} onChange={(e) => setPeriod(e.target.value)} />
              )}
            </div>
            <div>
              <Label htmlFor="cutoff">Time cutoff</Label>
              <Input id="cutoff" type="date" className="mt-1" value={cutoff} readOnly={USE_REAL && !!chosenPeriod?.cutoffDate} onChange={(e) => setCutoff(e.target.value)} />
            </div>
            <div>
              <Label htmlFor="paydate">Pay date</Label>
              <Input id="paydate" type="date" className="mt-1" value={payDate} readOnly={USE_REAL && !!chosenPeriod?.payDate} onChange={(e) => setPayDate(e.target.value)} />
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
      purpose: USE_REAL
        ? "Production runs include workers with an active payroll profile in the selected pay group."
        : "Nobody is silently left out — every exclusion carries a reason.",
      render: () => (
        <div className="space-y-3">
          <p className="text-sm text-muted-foreground">
            {USE_REAL
              ? `${livePopulation.length} worker${livePopulation.length === 1 ? "" : "s"} will be picked up by the calculation engine for this pay group.`
              : `${included.length} in, ${excluded.length} out of ${POPULATION.length} in this pay group.`}
          </p>
          {USE_REAL && livePopulation.length === 0 ? (
            <p role="alert" className="flex gap-2 rounded-md border border-warning/40 bg-warning-soft p-3 text-xs text-warning">
              <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
              No active payroll profiles were found for this pay group. Assign pay profiles on
              Compensation and benefits before opening the run.
            </p>
          ) : null}
          <ul className="space-y-2">
            {(USE_REAL ? livePopulation : POPULATION).map((p) => {
              const isOut = !USE_REAL && excluded.includes(p.name);
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
                    {!USE_REAL ? (
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
                    ) : null}
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
              ["Entity", USE_REAL ? "Current organisation scope" : entity.name],
              ["Pay group", selectedGroup?.name ?? payGroup],
              ["Period", period],
              ["Pay date", payDate],
              ["Employees included", String(included.length)],
              ["Manual exclusions", USE_REAL ? "Not supported at run creation" : String(excluded.length)],
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
              <span className="tabular font-medium">{money(estimate, selectedGroup?.currency ?? entity.currency)}</span>
            </p>
            <p className="mt-1 text-xs text-muted-foreground">
              {USE_REAL
                ? "This adds the current configured payroll profile values. Statutory deductions, overtime, proration and exceptions are calculated only after the run is locked and calculated."
                : "An estimate to sense-check the population, not a calculation. The real figures come out of the calculate stage."}
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
    <AuthGate>
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
        onSubmit={async () => {
          if (dateProblem) {
            feedback.blocked("Cannot open this run", dateProblem);
            return;
          }
          if (USE_REAL) {
            if (!setup.data?.periods.length || !chosenPeriod) {
              feedback.blocked(
                "No open period available",
                "Ask an admin to open a pay period for this pay group first.",
              );
              return;
            }
            if (chosenPeriod.status !== "open") {
              feedback.blocked(
                "Period is not open",
                `${chosenPeriod.periodLabel} is ${chosenPeriod.status}. Choose an open period before creating a run.`,
              );
              return;
            }
            if (!selectedGroupId) {
              feedback.blocked("No pay group selected", "Choose a pay group before opening the run.");
              return;
            }
            if (livePopulation.length === 0) {
              feedback.blocked(
                "No workers in this pay group",
                "Assign payroll profiles before opening a run for this pay group.",
              );
              return;
            }
            try {
              const r = await realApi.createPayrollRun({ payPeriodId: chosenPeriod.id, payGroupId: selectedGroupId });
              setRef(String((r as { id?: string }).id ?? chosenPeriod.id));
              feedback.submitted(
                "Run opened against the selected period.",
                "Next: calculate gross to net. Nothing has been paid.",
              );
            } catch (e) {
              feedback.blocked("Could not open the run", e instanceof Error ? e.message : "Unknown error.");
            }
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
      </AuthGate>
  );
}
