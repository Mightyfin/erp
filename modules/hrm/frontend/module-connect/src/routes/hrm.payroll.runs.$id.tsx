import { createFileRoute, Link, Outlet, useChildMatches } from "@tanstack/react-router";
import { Fragment, useState } from "react";
import {
  AlertTriangle,
  Ban,
  Check,
  CircleDashed,
  Info,
  Lock,
  ShieldAlert,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { CURRENT_USER, isOutstanding, money, payrollRunApi } from "@/mock/payrollrun";
import { CalculationPanel } from "@/platform/components/CalculationPanel";
import type { ControlTotals, PayRun, RunLine, RunStage } from "@/mock/payrollrun";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { ApprovalPanel } from "@/platform/components/ApprovalPanel";
import { ConfirmDialog } from "@/platform/components/ConfirmDialog";
import { feedback } from "@/platform/feedback";
import { CalculationExplainer } from "@/platform/components/CalculationExplainer";
import { DetailSection, RecordDetail } from "@/platform/components/RecordDetail";
import { RestrictedState } from "@/platform/components/States";
import { StatusTimeline } from "@/platform/components/StatusTimeline";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/payroll/runs/$id")({
  head: () => ({
    meta: [
      { title: "Pay run — Mightyfin ERP HRM" },
      { name: "description", content: "A pay run stage by stage: population, calculation, variances, approval and controlled release." },
      { property: "og:title", content: "Pay run — Mightyfin ERP HRM" },
      { property: "og:description", content: "A pay run stage by stage: population, calculation, variances, approval and controlled release." },
    ],
  }),
  component: RunDetail,
});

const stageIcon = {
  done: Check,
  current: CircleDashed,
  blocked: Lock,
  pending: CircleDashed,
} as const;

const stageWord = {
  done: "Complete",
  current: "In progress",
  blocked: "Blocked",
  pending: "Not started",
} as const;

function Stages({ stages }: { stages: RunStage[] }) {
  return (
    <ol className="space-y-0">
      {stages.map((s, i) => {
        const Icon = stageIcon[s.state];
        return (
          <li key={s.id} className="flex gap-3">
            <div className="flex flex-col items-center">
              <span
                className={`grid size-7 shrink-0 place-items-center rounded-full border text-[11px] font-medium ${
                  s.state === "done"
                    ? "border-success bg-success-soft text-success"
                    : s.state === "current"
                      ? "border-primary bg-primary-soft text-primary"
                      : s.state === "blocked"
                        ? "border-warning bg-warning-soft text-warning"
                        : "border-border-strong text-muted-foreground"
                }`}
              >
                {s.state === "done" || s.state === "blocked" ? <Icon className="size-3.5" aria-hidden /> : i + 1}
              </span>
              {i < stages.length - 1 ? <span className="min-h-6 w-px flex-1 bg-border" aria-hidden /> : null}
            </div>
            <div className="min-w-0 flex-1 pb-5">
              <p className="flex flex-wrap items-center gap-2 text-sm font-medium">
                {s.label}
                <span
                  className={`rounded-full border px-2 py-0.5 text-[11px] font-normal ${
                    s.state === "done"
                      ? "border-success/30 bg-success-soft text-success"
                      : s.state === "current"
                        ? "border-primary/40 bg-primary-soft text-primary"
                        : s.state === "blocked"
                          ? "border-warning/40 bg-warning-soft text-warning"
                          : "border-border bg-muted text-muted-foreground"
                  }`}
                >
                  {stageWord[s.state]}
                </span>
              </p>
              <p className="mt-0.5 text-xs text-muted-foreground">{s.purpose}</p>
              {s.at ? (
                <p className="mt-1 text-xs text-muted-foreground">
                  {s.by} · {s.at}
                </p>
              ) : null}
              {s.note ? <p className="mt-1 text-xs">{s.note}</p> : null}
            </div>
          </li>
        );
      })}
    </ol>
  );
}

function Totals({ run }: { run: PayRun }) {
  const rows: { label: string; key: keyof ControlTotals; isMoney: boolean }[] = [
    { label: "Employees paid", key: "headcount", isMoney: false },
    { label: "Gross pay", key: "gross", isMoney: true },
    { label: "Deductions", key: "deductions", isMoney: true },
    { label: "Net pay", key: "net", isMoney: true },
    { label: "Employer cost (not deducted)", key: "employerCost", isMoney: true },
  ];

  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[34rem] text-left text-sm">
        <caption className="sr-only">Control totals for this run compared with the previous period</caption>
        <thead className="border-b bg-surface-muted">
          <tr>
            <th scope="col" className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Control total</th>
            <th scope="col" className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">This period</th>
            <th scope="col" className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">Previous</th>
            <th scope="col" className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">Change</th>
          </tr>
        </thead>
        <tbody className="divide-y">
          {rows.map((r) => {
            const now = run.totals[r.key];
            const before = run.priorTotals?.[r.key];
            const diff = before === undefined ? null : now - before;
            const pct = before ? (diff! / before) * 100 : null;
            const material = pct !== null && Math.abs(pct) >= 2;
            return (
              <tr key={r.key}>
                <th scope="row" className="px-3 py-2 font-normal">{r.label}</th>
                <td className="tabular px-3 py-2 text-right font-medium">
                  {r.isMoney ? money(now, run.currency) : now}
                </td>
                <td className="tabular px-3 py-2 text-right text-muted-foreground">
                  {before === undefined ? "—" : r.isMoney ? money(before, run.currency) : before}
                </td>
                <td className="tabular px-3 py-2 text-right">
                  {diff === null ? (
                    <span className="text-muted-foreground">—</span>
                  ) : diff === 0 ? (
                    <span className="text-muted-foreground">No change</span>
                  ) : (
                    <span className={material ? "font-medium text-warning" : ""}>
                      {diff > 0 ? "+" : "−"}
                      {r.isMoney ? money(Math.abs(diff), run.currency) : Math.abs(diff)}
                      {pct !== null ? ` (${pct > 0 ? "+" : "−"}${Math.abs(pct).toFixed(1)}%)` : ""}
                      {material ? " · material" : ""}
                    </span>
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

/**
 * The three release actions, each gated on the one before it.
 *
 * The rule this encodes: releasing a payslip, paying the money and posting the
 * journal are different decisions with different consequences, so they are
 * different buttons — and a step that is not available says why, rather than
 * being a dead control labelled "Not yet".
 */
function ReleaseActions({ run }: { run: PayRun }) {
  const [done, setDone] = useState<string[]>(() =>
    run.stages.filter((s) => s.state === "done").map((s) => s.id),
  );
  const [confirming, setConfirming] = useState<string | null>(null);

  const approved = run.status === "Approved" || run.status === "Paid" || run.status === "Closed";

  const steps = [
    {
      id: "s7",
      label: "Release payslips",
      detail:
        "Makes payslips visible to employees. Does not move money. A released payslip is never silently overwritten — a correction creates a new linked version.",
      action: "Release payslips",
      consequence: `${run.included} employees at ${run.entityName} will be able to see their ${run.period} payslip immediately. No money moves.`,
      blockedBy: approved ? null : "The run must be approved first.",
      toast: `Payslips released to ${run.included} employees.`,
      next: "Payments are still to be released — nobody has been paid yet.",
    },
    {
      id: "s8",
      label: "Release payments",
      detail:
        "Creates the bank instruction for the net amounts. This is the step that actually pays people.",
      action: "Release payments",
      consequence: `A bank instruction for ${money(run.totals.net, run.currency)} across ${run.included} employees will be created. This is the step that pays people and cannot be recalled once the bank accepts it.`,
      blockedBy: null as string | null,
      requires: "s7",
      requiresText: "Release the payslips first, so employees can check their pay before it lands.",
      toast: `Payment instruction created for ${money(run.totals.net, run.currency)}.`,
      next: "Sent to the bank for processing. Reconcile once the bank confirms.",
      destructive: true,
    },
    {
      id: "s9",
      label: "Post to accounting",
      detail: "Journals, cost-centre allocation and liabilities. Can happen after payment.",
      action: "Post to accounting",
      consequence: `Journals for ${money(run.totals.gross, run.currency)} gross and ${money(run.totals.employerCost, run.currency)} employer cost will be posted to the ${run.period} ledger period.`,
      blockedBy: null as string | null,
      requires: "s8",
      requiresText: "Post after the payment is released, so the ledger matches what was actually paid.",
      toast: `${run.period} posted to accounting.`,
      next: "The ledger period stays open until the run is reconciled and closed.",
    },
  ];

  const active = steps.find((s) => s.id === confirming);

  return (
    <>
      <ul className="space-y-3 text-sm">
        {steps.map((s) => {
          const isDone = done.includes(s.id);
          const missing = "requires" in s && s.requires && !done.includes(s.requires) ? s.requiresText : null;
          const why = s.blockedBy ?? missing;

          return (
            <li key={s.id} className="flex flex-wrap items-start justify-between gap-4 rounded-md border p-3">
              <span className="min-w-0 flex-1">
                <span className="block font-medium">{s.label}</span>
                <span className="block text-xs text-muted-foreground">{s.detail}</span>
                {why && !isDone ? (
                  <span className="mt-1 flex gap-1.5 text-xs text-warning">
                    <Lock className="mt-0.5 size-3 shrink-0" aria-hidden />
                    {why}
                  </span>
                ) : null}
              </span>

              {isDone ? (
                <span className="inline-flex shrink-0 items-center gap-1.5 rounded-full border border-success/30 bg-success-soft px-2.5 py-1 text-xs text-success">
                  <Check className="size-3.5 shrink-0" aria-hidden />
                  Released
                </span>
              ) : (
                <Button
                  variant="outline"
                  size="sm"
                  className="shrink-0"
                  disabled={Boolean(why)}
                  onClick={() => setConfirming(s.id)}
                >
                  {s.action}
                </Button>
              )}
            </li>
          );
        })}
      </ul>

      <ConfirmDialog
        open={active !== undefined}
        onOpenChange={(o) => !o && setConfirming(null)}
        title={active?.action ?? ""}
        consequence={active?.consequence ?? ""}
        detail={
          <span className="block text-xs text-muted-foreground">
            Recorded against {run.id} in the audit trail, with your name and the time.
          </span>
        }
        confirmLabel={active?.action ?? "Confirm"}
        cancelLabel="Not yet"
        destructive={active?.destructive}
        onConfirm={() => {
          if (!active) return;
          setDone((d) => [...d, active.id]);
          setConfirming(null);
          feedback.submitted(active.toast, active.next);
        }}
      />
    </>
  );
}

/** One employee's line on the run, expandable to show how it was derived. */
function PayLines({
  rows,
  currency,
  payslipsReleased,
}: {
  rows: RunLine[];
  currency: string;
  /** Once released, each line is also visible to the employee as a payslip. */
  payslipsReleased: boolean;
}) {
  const [open, setOpen] = useState<string | null>(null);

  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[38rem] text-left text-sm">
        <caption className="sr-only">Every employee in this run with their gross, deductions and net pay</caption>
        <thead className="border-b bg-surface-muted">
          <tr>
            <th scope="col" className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Employee</th>
            <th scope="col" className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">Gross</th>
            <th scope="col" className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">Deductions</th>
            <th scope="col" className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">Net</th>
            <th scope="col" className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">vs last</th>
          </tr>
        </thead>
        <tbody className="divide-y">
          {rows.map((l) => {
            const shown = open === l.id;
            const variance = l.priorNet && l.priorNet > 0 ? ((l.net - l.priorNet) / l.priorNet) * 100 : null;
            const material = variance !== null && Math.abs(variance) >= 2;
            return (
              <Fragment key={l.id}>
                <tr>
                  <th scope="row" className="px-3 py-2 font-normal">
                    <button
                      type="button"
                      onClick={() => setOpen(shown ? null : l.id)}
                      aria-expanded={shown}
                      className="text-left"
                    >
                      <span className="block font-medium underline-offset-2 hover:underline">{l.employee}</span>
                      <span className="block text-xs text-muted-foreground">
                        {l.jobTitle} · {l.grade}
                      </span>
                    </button>
                    {l.flags.map((f) => (
                      <span key={f} className="mt-1 flex gap-1.5 text-xs text-warning">
                        <AlertTriangle className="mt-0.5 size-3 shrink-0" aria-hidden />
                        {f}
                      </span>
                    ))}
                  </th>
                  <td className="tabular px-3 py-2 text-right">{money(l.gross, currency)}</td>
                  <td className="tabular px-3 py-2 text-right text-muted-foreground">−{money(l.deductions, currency)}</td>
                  <td className="tabular px-3 py-2 text-right font-medium">{money(l.net, currency)}</td>
                  <td className="tabular px-3 py-2 text-right">
                    {variance === null ? (
                      <span className="text-muted-foreground">New</span>
                    ) : Math.abs(variance) < 0.05 ? (
                      <span className="text-muted-foreground">No change</span>
                    ) : (
                      <span className={material ? "font-medium text-warning" : "text-muted-foreground"}>
                        {variance > 0 ? "+" : "−"}
                        {Math.abs(variance).toFixed(1)}%
                      </span>
                    )}
                  </td>
                </tr>
                {shown ? (
                  <tr>
                    <td colSpan={5} className="bg-surface-muted px-3 py-3">
                      <ul className="space-y-1 text-xs">
                        {l.components.map((c) => (
                          <li key={c.code} className="flex flex-wrap justify-between gap-2">
                            <span>
                              <span className="font-medium">{c.label}</span>
                              <span className="text-muted-foreground"> — {c.basis}</span>
                            </span>
                            <span className="tabular shrink-0">
                              {c.kind === "Deduction" ? "−" : c.kind === "Employer" ? "employer " : ""}
                              {money(c.amount, currency)}
                            </span>
                          </li>
                        ))}
                      </ul>
                      {payslipsReleased ? (
                        <Link
                          to="/hrm/payslips/$id"
                          params={{ id: `PS-${l.runId.replace("RUN-", "")}-${l.employeeId}` }}
                          className="mt-2 inline-block text-xs text-primary underline underline-offset-2"
                        >
                          Open the payslip {l.employee} sees
                        </Link>
                      ) : (
                        <p className="mt-2 text-xs text-muted-foreground">
                          Payslips are not released yet, so {l.employee} cannot see this.
                        </p>
                      )}
                    </td>
                  </tr>
                ) : null}
              </Fragment>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function RunDetail() {
  const { id } = Route.useParams();
  const state = useMock(() => payrollRunApi.run(id), [id]);
  const exceptions = useMock(() => payrollRunApi.exceptionsFor(id), [id]);
  const lines = useMock(() => payrollRunApi.linesFor(id), [id]);

  // `/payroll/runs/$id/edit` is generated as a child of this route.
  const childMatches = useChildMatches();
  if (childMatches.length > 0) return <Outlet />;

  return (
    <AppShell>
      <Async state={state} rows={4}>
        {(run) => {
          if (!run) return <RestrictedState />;

          const selfApproval = run.preparedBy === CURRENT_USER;
          // An exception that has been resolved, waived or excluded no longer
          // holds the run up — that is the point of dealing with it.
          const blocking = (exceptions.data ?? []).filter(
            (e) => e.severity === "Blocking" && isOutstanding(e),
          );
          const canApprove = !selfApproval && blocking.length === 0;

          return (
            <RecordDetail
              reference={run.id}
              title={`${run.period} — ${run.payGroup}`}
              subtitle={`${run.entityName} · ${run.included} employees`}
              status={run.status}
              owner={run.owner}
              nextAction={`${run.nextAction} · due ${run.dueDate}`}
              summary={[
                { label: "Period", value: run.period },
                { label: "Entity", value: run.entityName },
                { label: "Pay group", value: run.payGroup },
                { label: "Prepared by", value: run.preparedBy },
                { label: "Approved by", value: run.approvedBy ?? <span className="text-muted-foreground">Not yet approved</span> },
                { label: "Currency", value: run.currency },
              ]}
              timeline={<StatusTimeline title="Audit trail" events={run.timeline} />}
            >
              {run.stages.length ? (
                <DetailSection
                  title="Stages"
                  description="Payslip release, payment and accounting are separate stages on purpose — releasing a payslip does not move money."
                >
                  <Stages stages={run.stages} />
                </DetailSection>
              ) : null}

              <DetailSection
                title="Calculate"
                description="Gross to net for every included employee. The payroll engine does the work; this shows what it did, employee by employee."
              >
                <CalculationPanel
                  runId={run.id}
                  locked={run.status === "Paid" || run.status === "Closed"}
                  lockedReason={
                    run.status === "Closed"
                      ? "This period is closed and locked. A change now needs a correction run."
                      : "Payments have been released, so recalculating would change figures people have already been paid on."
                  }
                />
              </DetailSection>

              <DetailSection
                title="Pay lines"
                description="Every employee in this run. Open a name to see how each figure was derived — no line is a black box."
                action={
                  <Button variant="outline" size="sm" asChild>
                    <Link to="/hrm/payroll/runs/$id/edit" params={{ id: run.id }}>
                      Edit this run
                    </Link>
                  </Button>
                }
              >
                <Async state={lines} rows={4}>
                  {(rows) =>
                    rows.length ? (
                      <PayLines
                        rows={rows}
                        currency={run.currency}
                        payslipsReleased={
                          run.status === "Paid" ||
                          run.status === "Closed" ||
                          run.stages.some((st) => st.id === "s7" && st.state === "done")
                        }
                      />
                    ) : (
                      <p className="text-sm text-muted-foreground">
                        Nothing has been calculated yet, so this run has no pay lines. They appear
                        once the calculate stage completes.
                      </p>
                    )
                  }
                </Async>
              </DetailSection>

              <DetailSection
                title="Control totals"
                description="Compared with the previous period. Anything moving 2% or more is flagged as material and needs an explanation before approval."
              >
                <Totals run={run} />
              </DetailSection>

              {run.excluded.length ? (
                <DetailSection
                  title="Excluded from this run"
                  description="Exclusion is deliberate and recorded — an employee is never silently left out."
                >
                  <ul className="space-y-2 text-sm">
                    {run.excluded.map((x) => (
                      <li key={x.employee} className="flex gap-2">
                        <Ban className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden />
                        <span>
                          <span className="font-medium">{x.employee}</span>
                          <span className="block text-xs text-muted-foreground">{x.reason}</span>
                        </span>
                      </li>
                    ))}
                  </ul>
                </DetailSection>
              ) : null}

              <DetailSection
                title="Worked example — how one figure was derived"
                description="Every calculated line can be explained. This is the same component used on an employee payslip."
              >
                <CalculationExplainer
                  currency={run.currency}
                  caption="Shift allowance for Chanda Mwansa-Chileshe, August 2026."
                  lines={[
                    {
                      code: "SHIFT",
                      label: "Shift allowance",
                      amount: 2_100,
                      inputs: [
                        { label: "Qualifying shifts", value: "14" },
                        { label: "Rate per shift", value: money(150, run.currency) },
                        { label: "Source", value: "Approved attendance, cutoff 24 Aug" },
                      ],
                      ruleVersion: "ALLOW-SHIFT v2.1",
                      effectiveFrom: "2026-04-01",
                      explanation: "Qualifying night and weekend shifts multiplied by the rate in force for the period.",
                      priorAmount: 1_650,
                    },
                  ]}
                />
              </DetailSection>

              {run.status !== "Closed" && run.status !== "Paid" ? (
                <DetailSection
                  title="Approval"
                  description="Segregation of duties is enforced here, not assumed."
                >
                  {selfApproval ? (
                    <div className="rounded-lg border border-danger/40 bg-danger-soft p-4">
                      <p className="flex items-start gap-2 text-sm font-medium text-danger">
                        <ShieldAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
                        You prepared this run, so you cannot approve it
                      </p>
                      <p className="mt-2 text-sm text-foreground">
                        {run.preparedBy} prepared this run. Approval must come from a different
                        authorised person — this is what stops one individual creating and releasing
                        a payment on their own.
                      </p>
                      <Button variant="outline" className="mt-3" disabled>
                        Approve run — not available to you
                      </Button>
                      <p className="mt-2 text-xs text-muted-foreground">
                        Send it to an approver instead. The attempt and the reason it was blocked are
                        both recorded.
                      </p>
                    </div>
                  ) : blocking.length ? (
                    <div className="rounded-lg border border-warning/40 bg-warning-soft p-4">
                      <p className="flex items-start gap-2 text-sm font-medium text-warning">
                        <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />
                        {blocking.length} blocking exception{blocking.length === 1 ? "" : "s"} must be resolved first
                      </p>
                      <Button variant="outline" className="mt-3" asChild>
                        <Link to="/hrm/payroll/exceptions">Review exceptions</Link>
                      </Button>
                    </div>
                  ) : (
                    <ApprovalPanel
                      decisionSummary={`Approve ${run.period} for ${run.included} employees at ${run.entityName}. Net pay ${money(run.totals.net, run.currency)}.`}
                      policy={[
                        { id: "p1", label: "Segregation of duties", outcome: "pass", detail: `Prepared by ${run.preparedBy}; you are a different person.` },
                        { id: "p2", label: "Blocking exceptions", outcome: "pass", detail: "None outstanding." },
                        { id: "p3", label: "Material variance explained", outcome: "warn", detail: "One variance above 2% has a recorded explanation." },
                      ]}
                      conflicts={[]}
                      onDecision={(decision, reason) => {
                        if (decision === "approve") {
                          feedback.submitted(
                            `${run.period} approved for ${run.included} employees.`,
                            "Payslips can now be released. Releasing them does not pay anyone.",
                          );
                        } else if (decision === "return" || decision === "reject") {
                          feedback.submitted(
                            `${run.period} sent back to ${run.preparedBy}.`,
                            reason || "The preparer will see your reason and can recalculate.",
                          );
                        } else {
                          feedback.note(
                            "Decision delegated.",
                            reason || "The delegate must still be someone other than the preparer.",
                          );
                        }
                      }}
                    />
                  )}
                  {canApprove ? null : null}
                </DetailSection>
              ) : null}

              <DetailSection title="Release" description="Three separate actions. They are never combined into one button.">
                <ReleaseActions run={run} />
                <p className="mt-3 flex gap-2 text-xs text-muted-foreground">
                  <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                  Nothing in this build pays anyone, files anything or posts to a ledger.
                </p>
              </DetailSection>
            </RecordDetail>
          );
        }}
      </Async>
    </AppShell>
  );
}
