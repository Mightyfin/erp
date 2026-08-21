import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { AlertTriangle, Ban, Check, Info, OctagonAlert, Undo2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  CURRENT_USER,
  clearExceptionOutcome,
  getExceptionOutcome,
  payRuns,
  payrollRunApi,
  recordExceptionOutcome,
} from "@/mock/payrollrun";
import type { ExceptionOutcome, OutcomeKind, PayrollException, Severity } from "@/mock/payrollrun";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { ReasonDialog } from "@/platform/components/ReasonDialog";
import { feedback } from "@/platform/feedback";
import { realApi, useApi } from "@/platform/use-api";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/payroll/exceptions")({
  head: () => ({
    meta: [
      { title: "Payroll exceptions — New World Cargo HRM" },
      {
        name: "description",
        content: "What is blocking release, what it would cost, and the safe way to resolve it.",
      },
      { property: "og:title", content: "Payroll exceptions — New World Cargo HRM" },
      {
        property: "og:description",
        content: "What is blocking release, what it would cost, and the safe way to resolve it.",
      },
    ],
  }),
  component: ExceptionsPage,
});

/** Severity is icon + word, never colour alone. */
const severityMeta: Record<Severity, { icon: typeof Info; cls: string; frame: string }> = {
  Blocking: { icon: OctagonAlert, cls: "text-danger", frame: "border-danger/40 bg-danger-soft" },
  Warning: { icon: AlertTriangle, cls: "text-warning", frame: "border-warning/40 bg-warning-soft" },
  Advisory: { icon: Info, cls: "text-info", frame: "border-info/30 bg-info-soft" },
};

const outcomeMeta: Record<OutcomeKind, { icon: typeof Check; cls: string; verb: string }> = {
  Resolved: { icon: Check, cls: "text-success", verb: "resolved" },
  Waived: { icon: AlertTriangle, cls: "text-warning", verb: "waived" },
  Excluded: { icon: Ban, cls: "text-muted-foreground", verb: "excluded from the run" },
};

/* -------------------------------------------------------------------------- */

function ExceptionCard({
  e,
  outcome,
  onAction,
  onReopen,
}: {
  e: PayrollException;
  outcome?: ExceptionOutcome;
  onAction: (kind: OutcomeKind) => void;
  onReopen: () => void;
}) {
  const { icon: Icon, cls, frame } = severityMeta[e.severity];

  return (
    <li className={`rounded-lg border p-5 ${outcome ? "border-border bg-surface" : frame}`}>
      <div className="flex flex-wrap items-start gap-2">
        <Icon
          className={`mt-0.5 size-4 shrink-0 ${outcome ? "text-muted-foreground" : cls}`}
          aria-hidden
        />
        <span className="min-w-0 flex-1">
          <span className="flex flex-wrap items-center gap-2">
            <span className={`text-sm font-semibold ${outcome ? "text-muted-foreground" : cls}`}>
              {e.severity}
            </span>
            <span className="text-sm font-medium">{e.kind}</span>
            <span className="font-mono text-xs text-muted-foreground">{e.id}</span>
          </span>
          <span className="mt-0.5 block text-xs text-muted-foreground">{e.affects}</span>
        </span>
        <Link
          to="/hrm/payroll/runs/$id"
          params={{ id: e.runId }}
          className="shrink-0 font-mono text-xs text-primary underline underline-offset-2"
        >
          {e.runId}
        </Link>
      </div>

      <dl className="mt-4 grid gap-3 text-sm sm:grid-cols-2">
        <div>
          <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            What failed
          </dt>
          <dd className="mt-0.5">{e.what}</dd>
        </div>
        <div>
          <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            Business impact
          </dt>
          <dd className="mt-0.5">{e.impact}</dd>
        </div>
        <div>
          <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            Recommended action
          </dt>
          <dd className="mt-0.5">{e.recommended}</dd>
        </div>
        <div>
          <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            If it cannot be resolved
          </dt>
          <dd className="mt-0.5">{e.escalation}</dd>
        </div>
      </dl>

      {outcome ? (
        <div className="mt-4 flex flex-wrap items-start gap-3 rounded-md border bg-surface-muted p-3">
          {(() => {
            const { icon: OIcon, cls: ocls, verb } = outcomeMeta[outcome.kind];
            return (
              <>
                <OIcon className={`mt-0.5 size-4 shrink-0 ${ocls}`} aria-hidden />
                <span className="min-w-0 flex-1 text-sm">
                  <span className="block font-medium">
                    {outcome.kind} — {verb} by {outcome.by}
                  </span>
                  <span className="mt-0.5 block text-xs text-muted-foreground">
                    {outcome.reason}
                  </span>
                  <span className="mt-0.5 block text-xs text-muted-foreground">{outcome.at}</span>
                </span>
                <Button
                  variant="ghost"
                  size="sm"
                  className="shrink-0 gap-1.5 text-xs"
                  onClick={onReopen}
                >
                  <Undo2 className="size-3.5" aria-hidden />
                  Reopen
                </Button>
              </>
            );
          })()}
        </div>
      ) : (
        <div className="mt-4 flex flex-wrap gap-2">
          <Button
            size="sm"
            disabled={!e.resolvable}
            onClick={() => onAction("Resolved")}
            title={e.resolvable ? undefined : "This one cannot be fixed inside payroll."}
          >
            Mark resolved
          </Button>
          <Button variant="outline" size="sm" onClick={() => onAction("Waived")}>
            Waive
          </Button>
          <Button variant="outline" size="sm" onClick={() => onAction("Excluded")}>
            Exclude from the run
          </Button>
          {!e.resolvable ? (
            <p className="basis-full text-xs text-muted-foreground">
              This cannot be resolved inside payroll — it needs information from outside the run.
              Waive it to let the run proceed, or exclude the employee.
            </p>
          ) : null}
        </div>
      )}
    </li>
  );
}

/* -------------------------------------------------------------------------- */

function ExceptionsPage() {
  const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";
  const mockState = useMock(() => payrollRunApi.exceptions());
  const realState = useApi(async (): Promise<PayrollException[]> => {
    if (!USE_REAL) return [];
    const runs = await realApi.payrollRuns();
    const calculated = (runs.items ?? []).filter(
      (raw) => String((raw as Record<string, unknown>).status ?? "") === "calculated",
    );
    const results = await Promise.all(
      calculated.map(async (rawRun) => {
        const run = rawRun as Record<string, unknown>;
        const lines = (await realApi.payrollRunLines(String(run.id ?? ""))) as {
          items?: unknown[];
        };
        return (lines.items ?? [])
          .filter((raw) => {
            const l = raw as Record<string, unknown>;
            return Boolean(l.hasException) && String(l.exceptionStatus ?? "open") === "open";
          })
          .map((raw) => {
            const l = raw as Record<string, unknown>;
            const reason = String(l.exceptionReason ?? "payroll-check");
            const name = String(l.workerName ?? l.employeeNo ?? "Worker");
            return {
              id: String(l.id ?? ""),
              runId: String(run.id ?? ""),
              severity: "Blocking" as const,
              kind: reason.replaceAll("-", " "),
              affects: `${name} · ${String(l.employeeNo ?? "")}`,
              what:
                reason === "missing-bank"
                  ? "No primary bank account is recorded."
                  : reason === "negative-net"
                    ? "Deductions exceed earnings."
                    : reason,
              impact:
                reason === "missing-bank"
                  ? "A bank payment instruction cannot be created."
                  : "The employee's net pay may be invalid.",
              recommended:
                reason === "missing-bank"
                  ? "Add and verify primary bank details, then recalculate."
                  : "Correct the payroll input and recalculate.",
              escalation: "Waive with an independent approver or exclude the worker with a reason.",
              resolvable: reason !== "missing-bank",
            } satisfies PayrollException;
          });
      }),
    );
    return results.flat();
  }, []);
  const state = USE_REAL ? realState : mockState;
  const [only, setOnly] = useState<Severity | "All">("All");
  // Mirrors the shared store, so a re-render picks the store's state up.
  const [outcomes, setOutcomes] = useState<Record<string, ExceptionOutcome>>({});
  const [pending, setPending] = useState<{ e: PayrollException; kind: OutcomeKind } | null>(null);

  async function record(e: PayrollException, kind: OutcomeKind, reason: string) {
    if (USE_REAL) {
      try {
        await realApi.payrollExceptionDecision(e.runId, e.id, kind.toLowerCase(), reason);
        feedback.saved(`${e.id} ${kind.toLowerCase()}.`);
        await realState.reload();
      } catch (error) {
        feedback.blocked(
          "Exception decision failed",
          error instanceof Error ? error.message : "Unknown error.",
        );
      }
      return;
    }
    const outcome: ExceptionOutcome = {
      kind,
      reason,
      by: CURRENT_USER,
      at: new Date().toLocaleString(),
    };
    recordExceptionOutcome(e.id, outcome);
    setOutcomes((s) => ({ ...s, [e.id]: outcome }));

    if (kind === "Resolved") {
      feedback.saved(`${e.id} marked resolved.`, () => {
        clearExceptionOutcome(e.id);
        setOutcomes((s) => {
          const n = { ...s };
          delete n[e.id];
          return n;
        });
      });
    } else if (kind === "Waived") {
      feedback.submitted(
        `${e.id} waived.`,
        "The run can proceed. The waiver and your reason are on the run's audit trail for the approver to see.",
      );
    } else {
      feedback.submitted(
        `${e.affects.split(" · ")[0]} excluded from ${e.runId}.`,
        "They are not paid in this run. Pay them in an off-cycle run once the problem is fixed.",
      );
    }
  }

  /** Copy and rules for the dialog, which differ sharply by action. */
  function dialogFor(e: PayrollException, kind: OutcomeKind) {
    const run = payRuns.find((r) => r.id === e.runId);
    const preparedByMe = run?.preparedBy === CURRENT_USER;

    if (kind === "Resolved") {
      return {
        title: `Mark ${e.id} resolved`,
        consequence:
          "Say what was actually done. The run still has to be recalculated before the fix shows in anyone's pay.",
        reasonLabel: "What did you do?",
        placeholder: e.recommended,
        confirmLabel: "Mark resolved",
        destructive: false,
        blockedBecause: undefined as string | undefined,
      };
    }

    if (kind === "Waived") {
      return {
        title: `Waive ${e.id}`,
        consequence:
          e.severity === "Blocking"
            ? "This lets a run proceed with a known, unfixed problem. The impact below happens anyway."
            : "The run proceeds without this being fixed. Your reason goes to the approver.",
        reasonLabel: "Why is it acceptable to proceed?",
        placeholder: "What you have weighed up, and who agreed to it.",
        confirmLabel: "Waive and let the run proceed",
        destructive: e.severity === "Blocking",
        // Same rule as approval: the preparer cannot also override the control.
        blockedBecause:
          e.severity === "Blocking" && preparedByMe
            ? `You prepared ${e.runId}, so you cannot waive a blocking exception on it. Ask an approver — one person must not both create a run and override the checks on it.`
            : undefined,
      };
    }

    return {
      title: `Exclude from ${e.runId}`,
      consequence: `${e.affects.split(" · ")[0]} will not be paid in this run. The exclusion and your reason are recorded against it — nobody is ever silently left out.`,
      reasonLabel: "Why are they being left out?",
      placeholder: e.escalation,
      confirmLabel: "Exclude from the run",
      destructive: true,
      blockedBecause: undefined as string | undefined,
    };
  }

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Payroll"
          title="Exceptions"
          description="Every exception says what failed, what it would cost if ignored, the safe next step, and what to do when it cannot be fixed in time."
        />
        <Async state={state} rows={3}>
          {(rows) => {
            const outcomeFor = (id: string) => outcomes[id] ?? getExceptionOutcome(id);
            const outstanding = rows.filter((r) => !outcomeFor(r.id));
            const counts = {
              Blocking: outstanding.filter((r) => r.severity === "Blocking").length,
              Warning: outstanding.filter((r) => r.severity === "Warning").length,
              Advisory: outstanding.filter((r) => r.severity === "Advisory").length,
            };
            const shown = only === "All" ? rows : rows.filter((r) => r.severity === only);
            const dealtWith = rows.length - outstanding.length;

            return (
              <>
                <div
                  className="flex flex-wrap gap-2"
                  role="tablist"
                  aria-label="Filter by severity"
                >
                  {(["All", "Blocking", "Warning", "Advisory"] as const).map((s) => (
                    <button
                      key={s}
                      role="tab"
                      aria-selected={only === s}
                      onClick={() => setOnly(s)}
                      className={`rounded-full border px-3 py-1 text-sm transition-colors ${
                        only === s
                          ? "border-primary bg-primary-soft font-medium text-primary"
                          : "bg-surface text-muted-foreground hover:border-border-strong"
                      }`}
                    >
                      {s}
                      {s !== "All" ? ` (${counts[s]})` : ` (${outstanding.length})`}
                    </button>
                  ))}
                </div>

                {counts.Blocking > 0 ? (
                  <p className="flex gap-2 rounded-md border border-danger/40 bg-danger-soft p-3 text-sm text-danger">
                    <OctagonAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
                    <span>
                      {counts.Blocking} blocking exception{counts.Blocking === 1 ? "" : "s"} — no
                      affected run can be approved or released until each one is resolved, waived by
                      someone with authority, or the employee is excluded with a recorded reason.
                    </span>
                  </p>
                ) : (
                  <p className="flex gap-2 rounded-md border border-success/30 bg-success-soft p-3 text-sm text-success">
                    <Check className="mt-0.5 size-4 shrink-0" aria-hidden />
                    Nothing blocking is outstanding. Runs can go for approval.
                  </p>
                )}

                {dealtWith > 0 ? (
                  <p className="text-xs text-muted-foreground">
                    {dealtWith} dealt with in this session. Each one stays listed with its outcome —
                    clearing an exception never removes the record of it.
                  </p>
                ) : null}

                <ul className="space-y-4">
                  {shown.map((e) => (
                    <ExceptionCard
                      key={e.id}
                      e={e}
                      outcome={outcomeFor(e.id)}
                      onAction={(kind) => setPending({ e, kind })}
                      onReopen={() => {
                        clearExceptionOutcome(e.id);
                        setOutcomes((s) => {
                          const n = { ...s };
                          delete n[e.id];
                          return n;
                        });
                        feedback.note(`${e.id} reopened.`, "It counts against the run again.");
                      }}
                    />
                  ))}
                </ul>

                {pending
                  ? (() => {
                      const cfg = dialogFor(pending.e, pending.kind);
                      return (
                        <ReasonDialog
                          open
                          onOpenChange={(o) => !o && setPending(null)}
                          title={cfg.title}
                          consequence={cfg.consequence}
                          detail={
                            pending.kind === "Waived" ? (
                              <span className="block">
                                <span className="block font-medium">{pending.e.kind}</span>
                                <span className="mt-0.5 block text-xs text-muted-foreground">
                                  {pending.e.impact}
                                </span>
                              </span>
                            ) : undefined
                          }
                          reasonLabel={cfg.reasonLabel}
                          placeholder={cfg.placeholder}
                          confirmLabel={cfg.confirmLabel}
                          destructive={cfg.destructive}
                          blockedBecause={cfg.blockedBecause}
                          onConfirm={(reason) => {
                            void record(pending.e, pending.kind, reason);
                            setPending(null);
                          }}
                        />
                      );
                    })()
                  : null}
              </>
            );
          }}
        </Async>
      </AppShell>
    </AuthGate>
  );
}
