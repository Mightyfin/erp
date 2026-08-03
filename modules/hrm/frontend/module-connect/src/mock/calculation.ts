/**
 * Payroll calculation — a backend job the frontend orchestrates but never performs.
 *
 * The calculation itself belongs on the server: gross to net, statutory bands,
 * proration, retro. This module fakes only the *shape* of that conversation so
 * the screens are built against the real contract:
 *
 *   POST /payroll/runs/:runId/calculate        -> { jobId }
 *   POST /payroll/runs/:runId/calculate?only=  -> { jobId }   (one employee)
 *   GET  /payroll/jobs/:jobId                  -> CalculationJob
 *   POST /payroll/jobs/:jobId/cancel
 *
 * Replacing this file with real fetch calls should not require touching the UI.
 * That is the point: no figure in here is computed by the frontend, and none
 * should ever be.
 */
import { getExceptionOutcome, runLines } from "./payrollrun";

const delay = (ms: number) => new Promise((r) => setTimeout(r, ms));

export type JobState = "queued" | "running" | "completed" | "failed" | "cancelled";

export type LineState = "pending" | "calculating" | "done" | "failed";

export interface CalculationLineProgress {
  employeeId: string;
  employee: string;
  state: LineState;
  /** Present only when the backend could not calculate this employee. */
  error?: string;
}

export interface CalculationJob {
  jobId: string;
  runId: string;
  state: JobState;
  startedAt: string;
  finishedAt?: string;
  /** Batch n of m, because a real run is chunked rather than one transaction. */
  batch: number;
  batches: number;
  lines: CalculationLineProgress[];
  /** Set when the job ends without calculating everyone. */
  summary?: string;
}

const jobs = new Map<string, CalculationJob>();

/**
 * Employees a backend would refuse to calculate, and why.
 *
 * Each is tied to the exception that reports it, so dealing with the exception
 * actually fixes the calculation. Otherwise "resolve, then recalculate" would
 * be advice the product cannot honour.
 */
const SEEDED_FAILURES: Record<string, { exceptionId: string; error: string }> = {
  "w-1004": {
    exceptionId: "EXC-8801",
    error:
      "No bank account on file. The calculation is refused rather than producing a payment that cannot be made.",
  },
};

const BATCH_SIZE = 2;
const TICK_MS = 700;

function newJobId() {
  return `CALC-${Math.random().toString(36).slice(2, 8).toUpperCase()}`;
}

/**
 * Start a calculation. Returns immediately with a job id, exactly as a real
 * endpoint would — the work continues server-side and the UI polls for it.
 */
export async function startCalculation(
  runId: string,
  opts?: { onlyEmployeeId?: string; ignoreFailures?: boolean },
): Promise<{ jobId: string }> {
  await delay(260);

  const all = runLines.filter((l) => l.runId === runId);
  const scope = opts?.onlyEmployeeId ? all.filter((l) => l.employeeId === opts.onlyEmployeeId) : all;

  const job: CalculationJob = {
    jobId: newJobId(),
    runId,
    state: "queued",
    startedAt: new Date().toISOString(),
    batch: 0,
    batches: Math.max(1, Math.ceil(scope.length / BATCH_SIZE)),
    lines: scope.map((l) => ({
      employeeId: l.employeeId,
      employee: l.employee,
      state: "pending" as LineState,
    })),
  };
  jobs.set(job.jobId, job);

  void run(job, Boolean(opts?.ignoreFailures));
  return { jobId: job.jobId };
}

/** Drives the fake job forward on a timer. Stands in for server-side work. */
async function run(job: CalculationJob, ignoreFailures: boolean) {
  // Read through the map: `cancelJob` mutates the record from outside this
  // function, so a narrowed local would make the check look unreachable.
  const cancelled = () => jobs.get(job.jobId)?.state === "cancelled";

  await delay(TICK_MS);
  if (cancelled()) return;
  job.state = "running";

  for (let i = 0; i < job.lines.length; i++) {
    if (cancelled()) return;

    const line = job.lines[i];
    line.state = "calculating";
    job.batch = Math.floor(i / BATCH_SIZE) + 1;
    await delay(TICK_MS);
    if (cancelled()) return;

    const failure = SEEDED_FAILURES[line.employeeId];
    // A dealt-with exception clears the block: the input it complained about
    // is now either fixed, accepted, or the employee is out of the run.
    const stillBlocked = failure && !getExceptionOutcome(failure.exceptionId);
    if (stillBlocked && !ignoreFailures) {
      line.state = "failed";
      line.error = failure.error;
    } else {
      line.state = "done";
    }
  }

  const failed = job.lines.filter((l) => l.state === "failed");
  job.finishedAt = new Date().toISOString();

  if (failed.length === 0) {
    job.state = "completed";
    job.summary = `${job.lines.length} of ${job.lines.length} calculated. No employee was left out.`;
  } else if (failed.length === job.lines.length) {
    job.state = "failed";
    job.summary = "Nothing could be calculated. Fix the inputs and start again.";
  } else {
    // Partial completion is a real outcome, not an error state: the employees
    // that succeeded keep their figures and the run can be resumed.
    job.state = "completed";
    job.summary = `${job.lines.length - failed.length} of ${job.lines.length} calculated. ${failed.length} could not be and ${failed.length === 1 ? "is" : "are"} listed below — their previous figures are unchanged.`;
  }
}

export async function getJob(jobId: string): Promise<CalculationJob | null> {
  await delay(120);
  const j = jobs.get(jobId);
  return j ? structuredClone(j) : null;
}

export async function cancelJob(jobId: string) {
  await delay(180);
  const j = jobs.get(jobId);
  if (!j) return;
  if (j.state === "completed" || j.state === "failed") return;
  j.state = "cancelled";
  j.finishedAt = new Date().toISOString();
  const done = j.lines.filter((l) => l.state === "done").length;
  j.summary = `Cancelled after ${done} of ${j.lines.length}. Those already calculated keep their figures.`;
}

/* -------------------------------------------------------------------------- */
/* Staleness — the reason a Calculate button needs to exist at all.            */
/* -------------------------------------------------------------------------- */

const staleRuns = new Map<string, string>();

/** Something changed that the current figures do not reflect. */
export const markRunStale = (runId: string, why: string) => staleRuns.set(runId, why);
export const clearRunStale = (runId: string) => staleRuns.delete(runId);
export const whyStale = (runId: string) => staleRuns.get(runId);

/** The last job seen for a run, so the screen can show its result on return. */
const lastJobForRun = new Map<string, string>();
export const rememberJob = (runId: string, jobId: string) => lastJobForRun.set(runId, jobId);
export const lastJob = (runId: string) => lastJobForRun.get(runId);
