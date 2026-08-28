import { useEffect, useRef, useState } from "react";
import { AlertTriangle, Check, CircleDashed, Info, Loader2, OctagonAlert, RotateCcw } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  cancelJob,
  clearRunStale,
  getJob,
  lastJob,
  rememberJob,
  startCalculation,
  whyStale,
} from "@/mock/calculation";
import type { CalculationJob, LineState } from "@/mock/calculation";
import { feedback } from "@/platform/feedback";

/**
 * Runs the calculation by asking the backend to, then reporting what it did.
 *
 * The frontend computes nothing here. Its whole responsibility is:
 *  - start the job and keep the button honest while it runs,
 *  - show progress per employee rather than one opaque spinner,
 *  - report partial completion truthfully, because "23 of 24" is a real and
 *    common outcome that a boolean success flag would hide,
 *  - let the operator retry just the employees that failed.
 */

const lineIcon: Record<LineState, typeof Check> = {
  pending: CircleDashed,
  calculating: Loader2,
  done: Check,
  failed: OctagonAlert,
};

const lineCls: Record<LineState, string> = {
  pending: "text-muted-foreground",
  calculating: "text-primary animate-spin",
  done: "text-success",
  failed: "text-danger",
};

export function CalculationPanel({
  runId,
  locked,
  lockedReason,
  liveCalculatedCount,
  liveTotalCount,
  liveStatus,
}: {
  runId: string;
  /** A released or closed run must not be recalculated. */
  locked?: boolean;
  lockedReason?: string;
  /** Production payroll runs are calculated by the real API, not the mock job store. */
  liveCalculatedCount?: number;
  liveTotalCount?: number;
  liveStatus?: string;
}) {
  const [job, setJob] = useState<CalculationJob | null>(null);
  const [starting, setStarting] = useState(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const stale = whyStale(runId);
  const running = job?.state === "queued" || job?.state === "running";

  // Pick up a job already in flight when returning to this screen.
  useEffect(() => {
    const known = lastJob(runId);
    if (known && !job) void getJob(known).then((j) => j && setJob(j));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [runId]);

  // Poll while the backend is working. A real implementation swaps this for
  // the same poll against the real endpoint, or a websocket.
  useEffect(() => {
    if (!job || !running) return;
    timer.current = setTimeout(async () => {
      const next = await getJob(job.jobId);
      if (!next) return;
      setJob(next);
      if (next.state === "completed") {
        const failed = next.lines.filter((l) => l.state === "failed").length;
        if (failed === 0) {
          clearRunStale(runId);
          feedback.saved(`${runId} calculated.`);
        } else {
          feedback.blocked(
            `${failed} employee${failed === 1 ? "" : "s"} could not be calculated`,
            "The rest are done. Fix the listed problems and calculate just those employees.",
          );
        }
      } else if (next.state === "failed") {
        feedback.blocked("Calculation failed", "Nothing was calculated. The inputs need fixing first.");
      }
    }, 500);
    return () => {
      if (timer.current) clearTimeout(timer.current);
    };
  }, [job, running, runId]);

  async function begin(opts?: { onlyEmployeeId?: string; ignoreFailures?: boolean }) {
    setStarting(true);
    const { jobId } = await startCalculation(runId, opts);
    rememberJob(runId, jobId);
    const j = await getJob(jobId);
    setJob(j);
    setStarting(false);
  }

  const failedLines = job?.lines.filter((l) => l.state === "failed") ?? [];
  const doneCount = job?.lines.filter((l) => l.state === "done").length ?? 0;

  if (locked) {
    return (
      <p className="flex gap-2 rounded-md border bg-surface-muted p-3 text-sm text-muted-foreground">
        <Info className="mt-0.5 size-4 shrink-0" aria-hidden />
        {lockedReason ??
          "This run has been released, so it cannot be recalculated. A change now needs a correction run."}
      </p>
    );
  }

  if (liveCalculatedCount !== undefined || liveTotalCount !== undefined) {
    const calculated = Math.max(0, liveCalculatedCount ?? 0);
    const total = Math.max(calculated, liveTotalCount ?? calculated);
    const complete = total > 0 && calculated >= total;
    return (
      <div className="space-y-3">
        <div className="rounded-lg border bg-surface p-4">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <p className="text-sm font-medium">
              {complete ? "Finished" : total === 0 ? "Waiting for calculation" : "Calculation in progress"}
            </p>
            <p className="tabular text-xs text-muted-foreground">
              {calculated} of {total} calculated
            </p>
          </div>

          <div
            className="mt-2 h-1.5 w-full overflow-hidden rounded-full bg-muted"
            role="progressbar"
            aria-valuenow={calculated}
            aria-valuemin={0}
            aria-valuemax={total}
            aria-label="Calculation progress"
          >
            <div
              className="h-full rounded-full bg-primary transition-[width] duration-300"
              style={{ width: `${(calculated / Math.max(total, 1)) * 100}%` }}
            />
          </div>

          <p
            className={`mt-3 flex gap-2 rounded-md border p-3 text-xs ${
              complete
                ? "border-success/30 bg-success-soft text-success"
                : "border-warning/40 bg-warning-soft text-warning"
            }`}
          >
            {complete ? (
              <Check className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            ) : (
              <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            )}
            {complete
              ? `${calculated} of ${total} calculated. No employee was left out.`
              : total === 0
                ? `No payroll lines exist yet. Current backend status is ${liveStatus ?? "unknown"}.`
                : `${calculated} of ${total} calculated. Review the pay lines below for any missing employees.`}
          </p>

          <p className="mt-3 flex gap-2 text-xs text-muted-foreground">
            <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            These counts come from the live payroll run and pay-line records returned by the backend.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      {stale && !running ? (
        <p className="flex gap-2 rounded-md border border-warning/40 bg-warning-soft p-3 text-sm text-warning">
          <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />
          {stale} The figures shown are from the last calculation and do not include it yet.
        </p>
      ) : null}

      <div className="flex flex-wrap items-center gap-2">
        <Button onClick={() => begin()} disabled={running || starting}>
          {running ? (
            <>
              <Loader2 className="size-4 animate-spin" aria-hidden />
              Calculating…
            </>
          ) : job ? (
            "Calculate again"
          ) : (
            "Calculate"
          )}
        </Button>

        {running ? (
          <Button
            variant="outline"
            onClick={async () => {
              await cancelJob(job!.jobId);
              const next = await getJob(job!.jobId);
              setJob(next);
              feedback.note(
                "Calculation cancelled.",
                "Employees already calculated keep their figures — nothing is half-written.",
              );
            }}
          >
            Stop
          </Button>
        ) : null}

        {job && !running ? (
          <span className="text-xs text-muted-foreground">
            Job {job.jobId} · {job.state}
          </span>
        ) : null}
      </div>

      {job ? (
        <div className="rounded-lg border bg-surface p-4">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <p className="text-sm font-medium">
              {running
                ? `Batch ${job.batch} of ${job.batches}`
                : job.state === "cancelled"
                  ? "Stopped"
                  : "Finished"}
            </p>
            <p className="tabular text-xs text-muted-foreground">
              {doneCount} of {job.lines.length} calculated
            </p>
          </div>

          <div
            className="mt-2 h-1.5 w-full overflow-hidden rounded-full bg-muted"
            role="progressbar"
            aria-valuenow={doneCount}
            aria-valuemin={0}
            aria-valuemax={job.lines.length}
            aria-label="Calculation progress"
          >
            <div
              className="h-full rounded-full bg-primary transition-[width] duration-300"
              style={{ width: `${(doneCount / Math.max(job.lines.length, 1)) * 100}%` }}
            />
          </div>

          <ul className="mt-3 space-y-1.5">
            {job.lines.map((l) => {
              const Icon = lineIcon[l.state];
              return (
                <li key={l.employeeId} className="flex items-start gap-2 text-sm">
                  <Icon className={`mt-0.5 size-3.5 shrink-0 ${lineCls[l.state]}`} aria-hidden />
                  <span className="min-w-0 flex-1">
                    <span className={l.state === "failed" ? "font-medium text-danger" : ""}>
                      {l.employee}
                    </span>
                    {l.error ? (
                      <span className="mt-0.5 block text-xs text-muted-foreground">{l.error}</span>
                    ) : null}
                  </span>
                  {l.state === "failed" ? (
                    <Button
                      variant="ghost"
                      size="sm"
                      className="h-7 shrink-0 gap-1.5 px-2 text-xs"
                      onClick={() => begin({ onlyEmployeeId: l.employeeId })}
                    >
                      <RotateCcw className="size-3.5" aria-hidden />
                      Retry this one
                    </Button>
                  ) : null}
                </li>
              );
            })}
          </ul>

          {job.summary ? (
            <p
              className={`mt-3 flex gap-2 rounded-md border p-3 text-xs ${
                failedLines.length
                  ? "border-warning/40 bg-warning-soft text-warning"
                  : "border-success/30 bg-success-soft text-success"
              }`}
            >
              {failedLines.length ? (
                <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
              ) : (
                <Check className="mt-0.5 size-3.5 shrink-0" aria-hidden />
              )}
              {job.summary}
            </p>
          ) : null}

          <p className="mt-3 flex gap-2 text-xs text-muted-foreground">
            <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            Every figure here is produced by the payroll engine, not by this screen. Calculating
            again never changes a released payslip.
          </p>
        </div>
      ) : null}
    </div>
  );
}
