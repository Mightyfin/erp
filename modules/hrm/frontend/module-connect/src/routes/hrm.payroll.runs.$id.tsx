import { createFileRoute, Link, Outlet, useChildMatches } from "@tanstack/react-router";
import { Fragment, useEffect, useState } from "react";
import { AlertTriangle, Ban, Check, CircleDashed, Info, Lock, ShieldAlert, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { CURRENT_USER, isOutstanding, money, payrollRunApi } from "@/mock/payrollrun";
import { CalculationPanel } from "@/platform/components/CalculationPanel";
import type { ControlTotals, PayRun, RunLine, RunStage } from "@/mock/payrollrun";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ApprovalPanel } from "@/platform/components/ApprovalPanel";
import { ConfirmDialog } from "@/platform/components/ConfirmDialog";
import { feedback } from "@/platform/feedback";
import { CalculationExplainer } from "@/platform/components/CalculationExplainer";
import { DetailSection, RecordDetail } from "@/platform/components/RecordDetail";
import { RestrictedState } from "@/platform/components/States";
import { StatusTimeline } from "@/platform/components/StatusTimeline";
import { hrmApi } from "@/platform/api-client";
import { realApi, useApi } from "@/platform/use-api";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/payroll/runs/$id")({
  head: () => ({
    meta: [
      { title: "Pay run — Mightyfin ERP HRM" },
      {
        name: "description",
        content:
          "A pay run stage by stage: population, calculation, variances, approval and controlled release.",
      },
      { property: "og:title", content: "Pay run — Mightyfin ERP HRM" },
      {
        property: "og:description",
        content:
          "A pay run stage by stage: population, calculation, variances, approval and controlled release.",
      },
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
                {s.state === "done" || s.state === "blocked" ? (
                  <Icon className="size-3.5" aria-hidden />
                ) : (
                  i + 1
                )}
              </span>
              {i < stages.length - 1 ? (
                <span className="min-h-6 w-px flex-1 bg-border" aria-hidden />
              ) : null}
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
        <caption className="sr-only">
          Control totals for this run compared with the previous period
        </caption>
        <thead className="border-b bg-surface-muted">
          <tr>
            <th
              scope="col"
              className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Control total
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              This period
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Previous
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Change
            </th>
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
                <th scope="row" className="px-3 py-2 font-normal">
                  {r.label}
                </th>
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
type StatutoryWorker = {
  workerId?: string;
  employeeNo?: string;
  fullName?: string;
  hasNrc?: boolean;
  hasTpin?: boolean;
  hasNapsaNumber?: boolean;
  hasNhimaNumber?: boolean;
  ready?: boolean;
};

/**
 * M24: the backend refuses to release while any worker in the run is missing an
 * NRC, TPIN, NAPSA or NHIMA number. This card shows exactly who blocks release
 * before the button is even pressed — so the "blocked" message is never a
 * surprise after clicking.
 */
function StatutoryReadinessCard({
  readiness,
}: {
  readiness: { isReady?: boolean; workers?: StatutoryWorker[] } | null;
}) {
  if (!readiness) return null;
  const missing = (readiness.workers ?? []).filter((w) => !w.ready);
  if (!missing.length) return null;
  const checks: Array<{ key: keyof StatutoryWorker; label: string }> = [
    { key: "hasNrc", label: "NRC" },
    { key: "hasTpin", label: "TPIN" },
    { key: "hasNapsaNumber", label: "NAPSA" },
    { key: "hasNhimaNumber", label: "NHIMA" },
  ];
  return (
    <div className="rounded-lg border border-danger/40 bg-danger-soft p-4">
      <p className="flex items-start gap-2 text-sm font-medium text-danger">
        <X className="mt-0.5 size-4 shrink-0" aria-hidden />
        Release blocked — {missing.length} worker{missing.length === 1 ? "" : "s"} missing statutory
        identity references
      </p>
      <p className="mt-2 text-xs text-foreground">
        Every worker in this run must carry an NRC, TPIN, NAPSA and NHIMA number before payslips can
        be released. Collect the missing references on the worker record, then re-check.
      </p>
      <ul className="mt-3 space-y-1.5 text-xs">
        {missing.map((w) => (
          <li
            key={w.workerId ?? w.employeeNo ?? Math.random()}
            className="flex flex-wrap gap-x-3 gap-y-0.5"
          >
            <span className="font-medium">
              {w.fullName ?? w.employeeNo ?? "Worker"}
              {w.employeeNo ? (
                <span className="text-muted-foreground"> ({w.employeeNo})</span>
              ) : null}
            </span>
            <span className="flex gap-2 text-warning">
              {checks.map((c) =>
                !w[c.key] ? (
                  <span key={c.key} className="inline-flex items-center gap-1">
                    <X className="size-3" aria-hidden />
                    missing {c.label}
                  </span>
                ) : null,
              )}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

function ReleaseActions({
  run,
  readiness,
  onReleased,
}: {
  run: PayRun;
  readiness: { isReady?: boolean } | null;
  onReleased: () => Promise<unknown> | void;
}) {
  const [done, setDone] = useState<string[]>(() => {
    const stages = run.stages.filter((s) => s.state === "done").map((s) => s.id);
    return run.status === "Paid" || run.status === "Closed"
      ? [...new Set([...stages, "s7"])]
      : stages;
  });
  const [confirming, setConfirming] = useState<string | null>(null);
  const [releasing, setReleasing] = useState(false);

  const approved = run.status === "Approved" || run.status === "Paid" || run.status === "Closed";
  // M24: until every worker carries NRC + TPIN + NAPSA + NHIMA, the backend
  // refuses to release — surface that here so the button never fails silently.
  const statutoryBlocked =
    USE_REAL && approved && readiness && !readiness.isReady
      ? "Workers in this run are still missing statutory identity references — see the banner above."
      : null;

  const steps = [
    {
      id: "s7",
      label: "Release payslips",
      detail:
        "Makes payslips visible to employees. Does not move money. A released payslip is never silently overwritten — a correction creates a new linked version.",
      action: "Release payslips",
      requiresText: "",
      destructive: true,
      consequence: `${run.included} employees at ${run.entityName} will be able to see their ${run.period} payslip immediately. No money moves.`,
      blockedBy: approved ? null : "The run must be approved first.",
      toast: `Payslips released to ${run.included} employees.`,
      next: "Payments are still to be released — nobody has been paid yet.",
      api: true,
    },
  ];

    const active = steps.find((s) => s.id === confirming);
  return (
    <>
      <ul className="space-y-3 text-sm">
        {steps.map((s) => {
          const isDone = done.includes(s.id);
          const missing: string | null = null;
          const why = s.blockedBy ?? missing ?? (s.api ? statutoryBlocked : null);

          return (
            <li
              key={s.id}
              className="flex flex-wrap items-start justify-between gap-4 rounded-md border p-3"
            >
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
                  {releasing && s.id === "s7" ? "Releasing…" : s.action}
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
        onConfirm={async () => {
          if (!active) return;
          if ("api" in active && active.api) {
            setReleasing(true);
            try {
              await realApi.payrollRunRelease(run.id);
              setDone((d) => [...d, active.id]);
              feedback.submitted(active.toast, active.next);
              await onReleased();
            } catch (e) {
              const msg = e instanceof Error ? e.message : "Unknown error.";
              if (/statutory/i.test(msg)) {
                feedback.blocked(
                  "Release blocked by statutory identity check",
                  "One or more workers are missing an NRC, TPIN, NAPSA or NHIMA number. " +
                    "Add the references on the worker record, then try again.",
                );
              } else {
                feedback.blocked("Release failed", msg);
              }
            } finally {
              setReleasing(false);
              setConfirming(null);
            }
            return;
          }
          setDone((d) => [...d, active.id]);
          setConfirming(null);
          feedback.submitted(active.toast, active.next);
        }}
      />
    </>
  );
}

function PaymentWorkflow({
  run,
  onChanged,
}: {
  run: OperationalPayRun;
  onChanged: () => Promise<unknown>;
}) {
  const [busy, setBusy] = useState(false);
  const [reference, setReference] = useState("");
  const status = run.paymentStatus;
  const invoke = async (label: string, action: () => Promise<unknown>) => {
    setBusy(true);
    try {
      await action();
      feedback.submitted(label, "The action and actor were added to the payroll audit trail.");
      await onChanged();
    } catch (e) {
      feedback.blocked(`${label} failed`, e instanceof Error ? e.message : "Unknown error.");
    } finally {
      setBusy(false);
    }
  };
  const download = async (kind: "payment" | "audit") => {
    const blob =
      kind === "payment"
        ? await realApi.payrollPaymentFile(run.id)
        : await realApi.payrollAuditExport(run.id);
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `${kind === "payment" ? (run.paymentFileReference ?? `payment-${run.id}`) : `payroll-audit-${run.id}`}.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
  };

  // M41: accounting-facing reports — the accounts team books the salary from
  // these (JV = debits/credits per transaction; dept = net per department).
  const downloadReport = async (kind: string, format: "csv" | "pdf") => {
    const blob = await hrmApi.getBlob(
      `/hrm/payroll/runs/${run.id}/reports/${kind}`,
      { format },
    );
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `payroll-report-${kind}-${run.id}.${format}`;
    anchor.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="mt-4 rounded-lg border p-4" data-testid="payment-workflow">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-sm font-medium">Payment file and reconciliation</p>
          <p className="mt-1 text-xs text-muted-foreground">
            Status:{" "}
            <span className="font-medium text-foreground">{status.replaceAll("-", " ")}</span>
            {run.paymentFileReference ? ` · ${run.paymentFileReference}` : ""}
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={() => void download("audit")}>
          Export audit CSV
        </Button>
      </div>

      {/* M41: accounting reports for the accounts team. The run must be
          released first — the backend enforces the same rule server-side. */}
      <div className="mt-4 border-t pt-4">
        <p className="text-sm font-medium">Accounting reports</p>
        <p className="mt-1 text-xs text-muted-foreground">
          JV = debits and credits by transaction; department reports = net pay per
          department with bank details. Reports are only available once the run is
          released or closed. GL accounts marked * need mapping before booking.
        </p>
        <div className="mt-3 flex flex-wrap gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={run.status !== "Released" && run.status !== "Closed"}
            onClick={() => void downloadReport("jv-summary", "csv")}
          >
            JV summary CSV
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={run.status !== "Released" && run.status !== "Closed"}
            onClick={() => void downloadReport("jv-summary", "pdf")}
          >
            JV summary PDF
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={run.status !== "Released" && run.status !== "Closed"}
            onClick={() => void downloadReport("jv-detailed", "csv")}
          >
            JV detailed CSV
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={run.status !== "Released" && run.status !== "Closed"}
            onClick={() => void downloadReport("jv-detailed", "pdf")}
          >
            JV detailed PDF
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={run.status !== "Released" && run.status !== "Closed"}
            onClick={() => void downloadReport("dept-summary", "csv")}
          >
            By department CSV
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={run.status !== "Released" && run.status !== "Closed"}
            onClick={() => void downloadReport("dept-detailed", "pdf")}
          >
            By department PDF
          </Button>
        </div>
      </div>
      <div className="mt-4 flex flex-wrap gap-2">
        {status === "not-created" ? (
          <Button
            disabled={busy || run.status !== "Paid"}
            onClick={() =>
              void invoke("Payment file generated", () => realApi.payrollPaymentGenerate(run.id))
            }
          >
            Generate bank file
          </Button>
        ) : null}
        {status !== "not-created" ? (
          <Button variant="outline" disabled={busy} onClick={() => void download("payment")}>
            Download bank CSV
          </Button>
        ) : null}
        {status === "generated" ? (
          <Button
            disabled={busy}
            onClick={() =>
              void invoke("Payment file approved", () =>
                realApi.payrollPaymentApprove(run.id, "Reviewed against payroll control totals"),
              )
            }
          >
            Approve payment file
          </Button>
        ) : null}
        {status === "approved" ? (
          <Button
            disabled={busy}
            onClick={() =>
              void invoke("Payment instruction released", () =>
                realApi.payrollPaymentRelease(run.id),
              )
            }
          >
            Release to bank
          </Button>
        ) : null}
      </div>
      {status === "released" ? (
        <div className="mt-4 flex flex-wrap items-end gap-2">
          <label className="min-w-64 flex-1 text-xs font-medium">
            Bank acknowledgement reference
            <input
              className="mt-1 h-9 w-full rounded-md border bg-background px-3 text-sm"
              value={reference}
              onChange={(e) => setReference(e.target.value)}
              placeholder="e.g. ZANACO-ACK-202608-001"
            />
          </label>
          <Button
            disabled={busy || !reference.trim()}
            onClick={() =>
              void invoke("Payroll reconciled and closed", () =>
                realApi.payrollReconcile(
                  run.id,
                  reference.trim(),
                  run.totals.net,
                  "Bank total matched payroll net",
                ),
              )
            }
          >
            Reconcile and close
          </Button>
        </div>
      ) : null}
      {status === "reconciled" ? (
        <p className="mt-4 text-sm text-success">
          Reconciled and closed
          {run.reconciliationReference ? ` · ${run.reconciliationReference}` : ""}
        </p>
      ) : null}
    </div>
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
        <caption className="sr-only">
          Every employee in this run with their gross, deductions and net pay
        </caption>
        <thead className="border-b bg-surface-muted">
          <tr>
            <th
              scope="col"
              className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Employee
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Gross
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Deductions
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Net
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              vs last
            </th>
          </tr>
        </thead>
        <tbody className="divide-y">
          {rows.map((l) => {
            const shown = open === l.id;
            const variance =
              l.priorNet && l.priorNet > 0 ? ((l.net - l.priorNet) / l.priorNet) * 100 : null;
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
                      <span className="block font-medium underline-offset-2 hover:underline">
                        {l.employee}
                      </span>
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
                  <td className="tabular px-3 py-2 text-right text-muted-foreground">
                    −{money(l.deductions, currency)}
                  </td>
                  <td className="tabular px-3 py-2 text-right font-medium">
                    {money(l.net, currency)}
                  </td>
                  <td className="tabular px-3 py-2 text-right">
                    {variance === null ? (
                      <span className="text-muted-foreground">New</span>
                    ) : Math.abs(variance) < 0.05 ? (
                      <span className="text-muted-foreground">No change</span>
                    ) : (
                      <span
                        className={material ? "font-medium text-warning" : "text-muted-foreground"}
                      >
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
                              {c.kind === "Deduction"
                                ? "−"
                                : c.kind === "Employer"
                                  ? "employer "
                                  : ""}
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

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

type OperationalPayRun = PayRun & {
  backendStatus: string;
  paymentStatus: string;
  paymentFileReference?: string;
  reconciliationReference?: string;
};

function adaptRun(raw: unknown, auditRows: unknown[] = []): OperationalPayRun {
  const r = raw as Record<string, unknown>;
  const statusMap: Record<string, string> = {
    draft: "Draft",
    calculated: "Calculated",
    "in-review": "In review",
    approved: "Approved",
    released: "Paid",
    paid: "Paid",
    locked: "Draft",
    closed: "Closed",
    reversed: "Reversed",
  };
  const status = statusMap[String(r.status ?? "")] ?? String(r.status ?? "Draft");
  return {
    id: String(r.id ?? ""),
    period: String(r.periodLabel ?? r.period ?? ""),
    entityId: "",
    entityName: String(r.entityName ?? "Mighty Finance Solutions Industrial Zambia Ltd"),
    payGroup: String(r.payGroup ?? "Monthly ZMW"),
    currency: String(r.currency ?? "ZMW"),
    status: status as PayRun["status"],
    owner: String(r.preparedBy ?? r.owner ?? "Payroll officer"),
    nextAction:
      status === "Calculated"
        ? "Send for review"
        : status === "In review"
          ? "Awaiting top-HR approval"
          : status === "Approved"
          ? "Release payslips"
          : status === "Paid"
            ? "Complete payment workflow"
            : status === "Closed"
              ? "Cycle complete"
              : "Calculate run",
    dueDate: String(r.cutoffDate ?? ""),
    branchId: r.locationId ? String(r.locationId) : undefined,
    preparedBy: String(r.preparedBySubjectId ?? r.preparedBy ?? "Payroll officer"),
    approvedBy: r.approvedBySubjectId ? String(r.approvedBySubjectId) : undefined,
    totals: {
      headcount: Number(r.employeeCount ?? 0),
      gross: Number(r.totalGross ?? 0),
      deductions: Number(r.totalDeductions ?? 0),
      employerCost: Number(r.totalEmployerCost ?? 0),
      net: Number(r.totalNet ?? 0),
    },
    included: Number(r.employeeCount ?? 0),
    excluded: [],
    stages: [],
    timeline: auditRows.map((rawEvent) => {
      const e = rawEvent as Record<string, unknown>;
      return {
        id: String(e.id ?? ""),
        at: String(e.createdAt ?? ""),
        actor: String(e.actorSubjectId ?? "system"),
        event: String(e.action ?? "Payroll action").replaceAll("-", " "),
        reason: e.reason ? String(e.reason) : undefined,
        before: e.fromStatus ? String(e.fromStatus) : undefined,
        after: e.toStatus ? String(e.toStatus) : undefined,
      };
    }),
    paymentStatus: String(r.paymentStatus ?? "not-created"),
    backendStatus: String(r.status ?? "draft"),
    paymentFileReference: r.paymentFileReference ? String(r.paymentFileReference) : undefined,
    reconciliationReference: r.reconciliationReference
      ? String(r.reconciliationReference)
      : undefined,
  };
}

function adaptLines(raw: unknown, runId: string): RunLine[] {
  const envelope = raw as { items?: unknown[] };
  return (envelope.items ?? []).map((item) => {
    const l = item as Record<string, unknown>;
    const status = String(l.exceptionStatus ?? "open");
    const components = (l.components as Record<string, unknown>[] | undefined) ?? [];
    return {
      id: String(l.id ?? ""),
      runId,
      employeeId: String(l.workerId ?? ""),
      employee: String(l.workerName ?? ""),
      jobTitle: "",
      grade: "",
      components: components.map((c) => ({
        code: String(c.componentCode ?? ""),
        label: String(c.componentName ?? ""),
        kind:
          String(c.componentType ?? "earning") === "earning"
            ? "Earning"
            : String(c.componentType ?? "") === "employer-contribution"
              ? "Employer"
              : "Deduction",
        amount: Number(c.amount ?? 0),
        source: c.isStatutory ? "Statutory" : "One-off",
        basis: String(c.explanation ?? ""),
        inputs: [],
        ruleVersion: "engine-v1",
        effectiveFrom: "",
        explanation: String(c.explanation ?? ""),
      })),
      gross: Number(l.grossPay ?? 0),
      deductions: Number(l.totalDeductions ?? 0),
      employerCost: Number(l.employerCost ?? 0),
      net: Number(l.netPay ?? 0),
      flags:
        l.hasException && status === "open"
          ? [String(l.exceptionReason ?? "Payroll exception")]
          : [],
    } as RunLine;
  });
}

function RunDetail() {
  const { id } = Route.useParams();
  const state = useApi(async (): Promise<OperationalPayRun | null> => {
    if (!USE_REAL) {
      const mock = await payrollRunApi.run(id);
      return mock
        ? { ...mock, backendStatus: mock.status.toLowerCase(), paymentStatus: "not-created" }
        : null;
    }
    try {
      const [run, audit] = await Promise.all([realApi.payrollRun(id), realApi.payrollRunAudit(id)]);
      return adaptRun(run, audit);
    } catch {
      return null;
    }
  }, [id]);
  // M46: branch names for the branch pill — fetched once per view, best effort.
  const [scopeLocations, setScopeLocations] = useState<{ id: string; name: string }[]>([]);
  useEffect(() => {
    if (!USE_REAL) return;
    let live = true;
    realApi
      .locations()
      .then((raw) => {
        if (!live) return;
        const items = Array.isArray(raw)
          ? raw
          : (((raw as Record<string, unknown>)?.items as unknown[]) ?? []);
        setScopeLocations(
          items.map((l) => ({
            id: String((l as Record<string, unknown>).id ?? ""),
            name: String((l as Record<string, unknown>).name ?? ""),
          })),
        );
      })
      .catch(() => {});
    return () => {
      live = false;
    };
  }, []);
  const exceptions = useMock(() => payrollRunApi.exceptionsFor(id), [id]);
  const lines = useApi(async (): Promise<RunLine[]> => {
    if (!USE_REAL) return payrollRunApi.linesFor(id);
    try {
      return adaptLines(await realApi.payrollRunLines(id), id);
    } catch {
      return [];
    }
  }, [id]);
  /** M24: who blocks the release gate — loaded only while the run can still be released. */
  const readiness = useApi(async () => {
    if (!USE_REAL) return null;
    try {
      return await realApi.payrollRunStatutoryReadiness(id);
    } catch {
      return null;
    }
  }, [id]);
  const [calculating, setCalculating] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [locking, setLocking] = useState(false);

  // `/payroll/runs/$id/edit` is generated as a child of this route.
  const childMatches = useChildMatches();
  if (childMatches.length > 0) return <Outlet />;

  return (
    <AuthGate>
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
                  {
                    label: "Approved by",
                    value: run.approvedBy ?? (
                      <span className="text-muted-foreground">Not yet approved</span>
                    ),
                  },
                  { label: "Currency", value: run.currency },
                  run.branchId
                    ? {
                        label: "Branch",
                        value: scopeLocations?.find((l) => l.id === run.branchId)?.name ?? run.branchId,
                      }
                    : { label: "Scope", value: "Organisation-wide" },
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
                  action={
                    USE_REAL ? (
                      <div className="flex gap-2">
                        <Button
                          variant="outline"
                          size="sm"
                          disabled={
                            calculating ||
                            (run.backendStatus !== "locked" && run.backendStatus !== "calculated")
                          }
                          onClick={async () => {
                            setCalculating(true);
                            try {
                              await realApi.calculatePayrollRun(run.id);
                              feedback.submitted(
                                "Calculation complete.",
                                "Review pay lines and variances before sending for review.",
                              );
                              await state.reload();
                              await lines.reload();
                            } catch (e) {
                              feedback.blocked(
                                "Calculation failed",
                                e instanceof Error ? e.message : "Unknown error.",
                              );
                            } finally {
                              setCalculating(false);
                            }
                          }}
                        >
                          {calculating ? "Calculating…" : "Calculate run"}
                        </Button>
                        {/* M46: branch payroll drafts flow up for organisation-wide
                            HR approval. Organisation-wide runs skip review and
                            go straight to approval. */}
                        <Button
                          variant="default"
                          size="sm"
                          disabled={
                            submitting || run.backendStatus !== "calculated" || !run.branchId
                          }
                          onClick={async () => {
                            setSubmitting(true);
                            try {
                              await realApi.submitPayrollRun(run.id);
                              feedback.submitted(
                                "Branch run sent for review.",
                                "Organisation-wide HR can now review and approve this draft.",
                              );
                              await state.reload();
                            } catch (e) {
                              feedback.blocked(
                                "Submit failed",
                                e instanceof Error ? e.message : "Unknown error.",
                              );
                            } finally {
                              setSubmitting(false);
                            }
                          }}
                        >
                          {submitting ? "Sending…" : "Send for review"}
                        </Button>
                        <Button
                          variant="outline"
                          size="sm"
                          className="gap-1.5"
                          disabled={locking || run.backendStatus !== "draft"}
                          onClick={async () => {
                            setLocking(true);
                            try {
                              await realApi.lockPayrollRun(run.id);
                              feedback.submitted(
                                "Payroll inputs locked.",
                                "The run is ready to calculate.",
                              );
                              await state.reload();
                            } catch (e) {
                              feedback.blocked(
                                "Lock failed",
                                e instanceof Error ? e.message : "Unknown error.",
                              );
                            } finally {
                              setLocking(false);
                            }
                          }}
                        >
                          {locking ? "Locking…" : "Lock inputs"}
                        </Button>
                      </div>
                    ) : undefined
                  }
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

                {/* M34: admin payslip surface — list of payslips for this run with bulk PDF generate */}
                {USE_REAL && (run.status === "Paid" || run.status === "Closed" || run.backendStatus === "released") ? (
                  <DetailSection
                    title="Payslips"
                    description="One payslip per released pay line. Generate PDFs for the whole run, or preview/download an individual one."
                  >
                    <Async
                      state={
                        useApi(async () => {
                          const raw = await realApi.payrollRunPayslips(run.id);
                          return (Array.isArray(raw) ? raw : []) as Array<Record<string, unknown>>;
                        })
                      }
                      rows={4}
                    >
                      {(slips) => {
                        if (!slips.length) {
                          return (
                            <div className="flex flex-wrap items-center gap-3">
                              <p className="text-sm text-muted-foreground">
                                Run is released but no payslips were found — generate them.
                              </p>
                            </div>
                          );
                        }
                        return (
                          <div className="space-y-3">
                            <div className="flex flex-wrap items-center gap-3">
                              <Button
                                variant="outline"
                                size="sm"
                                className="gap-2"
                                onClick={async () => {
                                  try {
                                    await realApi.payrollGenerateAllPayslips(run.id);
                                    feedback.submitted(
                                      "Payslip PDFs generated",
                                      `All ${slips.length} payslip documents are ready. Re-open to see the download links.`,
                                    );
                                    await state.reload();
                                  } catch (e) {
                                    feedback.blocked(
                                      "Payslip PDF generation failed",
                                      e instanceof Error ? e.message : "Unknown error.",
                                    );
                                  }
                                }}
                              >
                                <Download className="size-4" aria-hidden />
                                Generate PDFs for all payslips
                              </Button>
                              <span className="text-xs text-muted-foreground">
                                Idempotent — already-generated slips are returned as-is.
                              </span>
                            </div>
                            <div className="overflow-x-auto">
                              <table className="w-full text-left text-xs">
                                <thead>
                                  <tr className="border-b bg-surface-muted text-muted-foreground">
                                    <th className="px-2 py-1.5">Payslip</th>
                                    <th className="px-2 py-1.5">Employee</th>
                                    <th className="px-2 py-1.5 text-right">Net</th>
                                    <th className="px-2 py-1.5">Status</th>
                                    <th className="px-2 py-1.5" />
                                  </tr>
                                </thead>
                                <tbody>
                                  {slips.map((s) => (
                                    <tr key={String(s.id ?? "")} className="border-b last:border-0">
                                      <td className="px-2 py-1.5 font-mono text-primary">
                                        {String(s.payslipNo ?? s.id ?? "")}
                                      </td>
                                      <td className="px-2 py-1.5">
                                        {String(s.employee ?? s.workerName ?? "")}
                                      </td>
                                      <td className="px-2 py-1.5 text-right tabular">
                                        {money(Number(s.netPay ?? 0), run.currency)}
                                      </td>
                                      <td className="px-2 py-1.5">
                                        <span
                                          className={
                                            String(s.status ?? "") === "released" || String(s.status ?? "") === "final"
                                              ? "text-success"
                                              : "text-muted-foreground"
                                          }
                                        >
                                          {String(s.status ?? "draft")}
                                        </span>
                                      </td>
                                      <td className="px-2 py-1.5 text-right">
                                        <Link
                                          to="/hrm/payslips/$id"
                                          params={{ id: String(s.id ?? "") }}
                                          className="mr-2 text-primary underline underline-offset-2"
                                        >
                                          Open
                                        </Link>
                                        <Link
                                          to="/hrm/payslips/$id"
                                          params={{ id: String(s.id ?? "") }}
                                          className="text-primary underline underline-offset-2"
                                        >
                                          PDF
                                        </Link>
                                      </td>
                                    </tr>
                                  ))}
                                </tbody>
                              </table>
                            </div>
                          </div>
                        );
                      }}
                    </Async>
                  </DetailSection>
                ) : null}

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
                          <Ban
                            className="mt-0.5 size-4 shrink-0 text-muted-foreground"
                            aria-hidden
                          />
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
                        explanation:
                          "Qualifying night and weekend shifts multiplied by the rate in force for the period.",
                        priorAmount: 1_650,
                      },
                    ]}
                  />
                </DetailSection>

                {run.status !== "Closed" && run.status !== "Paid" ? (
                  <DetailSection
                    title="Approval"
                    description={
                      run.branchId
                        ? "Branch payroll drafts flow up for organisation-wide HR approval. Top HR approves; branch HR cannot approve their own draft."
                        : "Segregation of duties is enforced here, not assumed."
                    }
                  >
                    {selfApproval ? (
                      <div className="rounded-lg border border-danger/40 bg-danger-soft p-4">
                        <p className="flex items-start gap-2 text-sm font-medium text-danger">
                          <ShieldAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
                          You prepared this run, so you cannot approve it
                        </p>
                        <p className="mt-2 text-sm text-foreground">
                          {run.preparedBy} prepared this run. Approval must come from a different
                          authorised person — this is what stops one individual creating and
                          releasing a payment on their own.
                        </p>
                        <Button variant="outline" className="mt-3" disabled>
                          Approve run — not available to you
                        </Button>
                        <p className="mt-2 text-xs text-muted-foreground">
                          Send it to an approver instead. The attempt and the reason it was blocked
                          are both recorded.
                        </p>
                      </div>
                    ) : blocking.length ? (
                      <div className="rounded-lg border border-warning/40 bg-warning-soft p-4">
                        <p className="flex items-start gap-2 text-sm font-medium text-warning">
                          <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />
                          {blocking.length} blocking exception{blocking.length === 1 ? "" : "s"}{" "}
                          must be resolved first
                        </p>
                        <Button variant="outline" className="mt-3" asChild>
                          <Link to="/hrm/payroll/exceptions">Review exceptions</Link>
                        </Button>
                      </div>
                    ) : (
                      <ApprovalPanel
                        decisionSummary={`Approve ${run.period} for ${run.included} employees at ${run.entityName}. Net pay ${money(run.totals.net, run.currency)}.`}
                        policy={[
                          {
                            id: "p1",
                            label: "Segregation of duties",
                            outcome: "pass",
                            detail: `Prepared by ${run.preparedBy}; you are a different person.`,
                          },
                          {
                            id: "p2",
                            label: "Blocking exceptions",
                            outcome: "pass",
                            detail: "None outstanding.",
                          },
                          {
                            id: "p3",
                            label: "Material variance explained",
                            outcome: "warn",
                            detail: "One variance above 2% has a recorded explanation.",
                          },
                        ]}
                        conflicts={[]}
                        onDecision={async (decision, reason) => {
                          if (USE_REAL && decision === "approve") {
                            try {
                              await realApi.payrollRunApprove(run.id);
                              feedback.submitted(
                                `${run.period} approved for ${run.included} employees.`,
                                run.branchId
                                  ? "Branch draft merged into the mainstream payroll. Payslips can now be released."
                                  : "Payslips can now be released. Releasing them does not pay anyone.",
                              );
                              await state.reload();
                              return;
                            } catch (e) {
                              feedback.blocked(
                                "Approval failed",
                                e instanceof Error ? e.message : "Unknown error.",
                              );
                              return;
                            }
                          }
                          if (decision === "approve") {
                            feedback.submitted(
                              `${run.period} approved for ${run.included} employees.`,
                              "Payslips can now be released. Releasing them does not pay anyone.",
                            );
                          } else if (USE_REAL && decision === "reject") {
                            try {
                              await realApi.payrollRunReverse(run.id);
                              feedback.submitted(
                                `${run.period} rejected and reversed.`,
                                "The preparer will see your reason in the audit trail and can recalculate.",
                              );
                              await state.reload();
                              return;
                            } catch (e) {
                              feedback.blocked(
                                "Rejection failed",
                                e instanceof Error ? e.message : "Unknown error.",
                              );
                              return;
                            }
                          } else if (decision === "return" || decision === "reject") {
                            feedback.submitted(
                              `${run.period} sent back to ${run.preparedBy}.`,
                              reason || "The preparer will see your reason and can recalculate.",
                            );
                          } else {
                            feedback.note(
                              "Decision delegated.",
                              reason ||
                                "The delegate must still be someone other than the preparer.",
                            );
                          }
                        }}
                      />
                    )}
                    {canApprove ? null : null}
                  </DetailSection>
                ) : null}

                <DetailSection
                  title="Release"
                  description="Three separate actions. They are never combined into one button."
                >
                  <StatutoryReadinessCard readiness={readiness.data} />
                  <div className={readiness.data ? "mt-3" : ""}>
                    <ReleaseActions
                      run={run}
                      readiness={readiness.data}
                      onReleased={async () => {
                        await readiness.reload();
                        await state.reload();
                      }}
                    />
                    {USE_REAL ? <PaymentWorkflow run={run} onChanged={async () => { await state.reload(); }} /> : null}
                  </div>
                  <p className="mt-3 flex gap-2 text-xs text-muted-foreground">
                    <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                    {USE_REAL
                      ? "Payslip release is recorded against the run; payment and ledger posting are handled downstream."
                      : "Nothing in this build pays anyone, files anything or posts to a ledger."}
                  </p>
                </DetailSection>
              </RecordDetail>
            );
          }}
        </Async>
      </AppShell>
    </AuthGate>
  );
}
