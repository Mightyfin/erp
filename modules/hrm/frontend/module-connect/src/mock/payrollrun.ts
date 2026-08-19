/**
 * Payroll processing cycle (HRM-033 / UI-PAY-002..005).
 *
 * Two product rules are encoded in this data and must survive into any real
 * implementation:
 *  1. Segregation of duties — whoever prepared a run may not approve it.
 *  2. Releasing payslips, paying people and posting to accounting are three
 *     separate controlled stages, never one button.
 */
import { employees } from "./data";
import type { TimelineEvent } from "./types";

const delay = (ms = 420) => new Promise((r) => setTimeout(r, ms));

export type RunStatus =
  | "Draft"
  | "Calculating"
  | "Calculated"
  | "In review"
  | "Approved"
  | "Paid"
  | "Closed"
  | "Reversed";

/** M48: one row on the top-HR approval queue — a branch run awaiting review
 * (or a calculated branch run not yet submitted). Maps PayrollQueueItemDto. */
export interface PayQueueItem {
  runId: string;
  status: string; // "in-review" | "calculated" | ...
  periodLabel: string;
  branchId?: string;
  branchName?: string;
  entityId: string;
  employeeCount: number;
  totalGross: number;
  totalNet: number;
  totalDeductions: number;
  totalEmployerCost: number;
  exceptionCount: number;
  preparedBySubjectId?: string;
  submittedAt?: string;
  createdAt: string;
}

export type StageState = "done" | "current" | "blocked" | "pending";

export interface RunStage {
  id: string;
  label: string;
  purpose: string;
  state: StageState;
  at?: string;
  by?: string;
  note?: string;
}

export interface ControlTotals {
  headcount: number;
  gross: number;
  deductions: number;
  employerCost: number;
  net: number;
}

export interface PayRun {
  id: string;
  period: string;
  entityId: string;
  entityName: string;
  payGroup: string;
  currency: string;
  status: RunStatus;
  owner: string;
  nextAction: string;
  dueDate: string;
  /** Segregation of duties: these two must never be the same person. */
  preparedBy: string;
  approvedBy?: string;
  /** M46: the branch this run was prepared for (undefined = org-wide run). */
  branchId?: string;
  /** The signed-in user, for the self-approval block. */
  totals: ControlTotals;
  priorTotals?: ControlTotals;
  included: number;
  excluded: { employee: string; reason: string }[];
  stages: RunStage[];
  timeline: TimelineEvent[];
}

export type Severity = "Blocking" | "Warning" | "Advisory";

export interface PayrollException {
  id: string;
  runId: string;
  severity: Severity;
  kind: string;
  affects: string;
  what: string;
  impact: string;
  recommended: string;
  escalation: string;
  resolvable: boolean;
}

/** Who is signed in for this demo. Used to demonstrate the self-approval block. */
export const CURRENT_USER = "Nalukui Simasiku";

export type OutcomeKind = "Resolved" | "Waived" | "Excluded";

export interface ExceptionOutcome {
  kind: OutcomeKind;
  reason: string;
  by: string;
  at: string;
}

/**
 * Outcomes recorded against exceptions during this session.
 *
 * Held here rather than in one screen's state so that dealing with an exception
 * actually unblocks the run — otherwise the exceptions page and the run page
 * disagree about whether a run can be approved, which is the whole thing this
 * stage exists to settle. Not persisted: a reload starts over.
 */
const exceptionOutcomes = new Map<string, ExceptionOutcome>();

export const recordExceptionOutcome = (id: string, outcome: ExceptionOutcome) => {
  exceptionOutcomes.set(id, outcome);
};
export const clearExceptionOutcome = (id: string) => {
  exceptionOutcomes.delete(id);
};
export const getExceptionOutcome = (id: string) => exceptionOutcomes.get(id);
/** An exception still counts against a run until it has been dealt with. */
export const isOutstanding = (e: PayrollException) => !exceptionOutcomes.has(e.id);

export const payRuns: PayRun[] = [
  {
    id: "RUN-2026-08-ZM1-M",
    period: "August 2026",
    entityId: "ent-zm1",
    entityName: "Mighty Finance Solutions Industrial Services Zambia Ltd",
    payGroup: "Lusaka monthly salaried",
    currency: "ZMW",
    status: "Calculated",
    owner: "Nalukui Simasiku (Payroll)",
    nextAction: "Resolve 2 blocking exceptions, then send for approval",
    dueDate: "2026-08-24",
    preparedBy: "Nalukui Simasiku",
    included: 4,
    excluded: [
      { employee: "Natasha Chirwa", reason: "Starts 14 Sep 2026 — not yet in a paid period" },
      { employee: "Gift Zulu", reason: "Contractor, paid through accounts payable, not payroll" },
    ],
    totals: { headcount: 4, gross: 71_095.01, deductions: 21_046.06, employerCost: 4_236.91, net: 50_048.95 },
    priorTotals: { headcount: 3, gross: 55_350.0, deductions: 16_662.0, employerCost: 3_304.5, net: 38_688.0 },
    stages: [
      { id: "s1", label: "Confirm period and population", purpose: "Who is in this run, and who is deliberately out.", state: "done", at: "2026-08-10", by: "Nalukui Simasiku", note: "4 included, 2 excluded with reasons recorded." },
      { id: "s2", label: "Review readiness blockers", purpose: "Anything that would make the calculation wrong.", state: "done", at: "2026-08-11", by: "Nalukui Simasiku" },
      { id: "s3", label: "Calculate", purpose: "Gross to net for every included employee. Resumable — not a black box.", state: "done", at: "2026-08-12", by: "System", note: "Completed in 4 batches. No employee left uncalculated." },
      { id: "s4", label: "Review variances and exceptions", purpose: "Explain anything that moved materially since last period.", state: "current", note: "2 blocking, 1 warning outstanding." },
      { id: "s5", label: "Resolve, waive or exclude", purpose: "Every exception needs an owner and a reason.", state: "pending" },
      { id: "s6", label: "Approve (segregation of duties)", purpose: "A second person, never the preparer.", state: "blocked", note: "Blocked until stage 5 completes." },
      { id: "s7", label: "Release payslips", purpose: "Makes payslips visible. Does not move money.", state: "pending" },
      { id: "s8", label: "Release payments", purpose: "Creates the bank instruction. Separate from payslip release.", state: "pending" },
      { id: "s9", label: "Post to accounting", purpose: "Journals and cost allocation.", state: "pending" },
      { id: "s10", label: "Reconcile and close", purpose: "Control totals agreed, period locked.", state: "pending" },
    ],
    timeline: [
      { id: "t1", at: "2026-08-10T08:00:00Z", actor: "Nalukui Simasiku", event: "Run created", after: "Draft" },
      { id: "t2", at: "2026-08-12T09:14:00Z", actor: "System", event: "Calculation completed", before: "Calculating", after: "Calculated", reason: "4 of 4 employees calculated" },
      { id: "t3", at: "2026-08-12T09:15:00Z", actor: "System", event: "3 exceptions raised", reason: "2 blocking, 1 warning" },
    ],
  },
  {
    id: "RUN-2026-07-ZM1-M",
    period: "July 2026",
    entityId: "ent-zm1",
    entityName: "Mighty Finance Solutions Industrial Services Zambia Ltd",
    payGroup: "Lusaka monthly salaried",
    currency: "ZMW",
    status: "Closed",
    owner: "Payroll",
    nextAction: "Closed — no action required",
    dueDate: "2026-07-24",
    preparedBy: "Nalukui Simasiku",
    approvedBy: "Thandiwe Banda",
    included: 3,
    excluded: [
      { employee: "Temwani Phiri", reason: "On unpaid leave for the whole of July — nothing to pay" },
    ],
    totals: { headcount: 3, gross: 55_350.0, deductions: 16_662.0, employerCost: 3_304.5, net: 38_688.0 },
    stages: [],
    timeline: [
      { id: "t1", at: "2026-07-24T14:00:00Z", actor: "Thandiwe Banda", event: "Approved", before: "In review", after: "Approved" },
      { id: "t2", at: "2026-07-27T06:00:00Z", actor: "System", event: "Payments released", after: "Paid" },
      { id: "t3", at: "2026-07-31T10:00:00Z", actor: "Nalukui Simasiku", event: "Period reconciled and closed", after: "Closed" },
    ],
  },
  {
    id: "RUN-2026-08-ZM2-M",
    period: "August 2026",
    entityId: "ent-zm2",
    entityName: "Mighty Finance Solutions Copperbelt Services Ltd",
    payGroup: "Copperbelt monthly salaried",
    currency: "ZMW",
    status: "Draft",
    owner: "Nalukui Simasiku (Payroll)",
    nextAction: "Confirm period and population",
    dueDate: "2026-08-24",
    preparedBy: "Nalukui Simasiku",
    included: 1,
    excluded: [{ employee: "Gift Zulu", reason: "Contractor, paid through accounts payable" }],
    totals: { headcount: 1, gross: 0, deductions: 0, employerCost: 0, net: 0 },
    stages: [],
    timeline: [{ id: "t1", at: "2026-08-10T08:05:00Z", actor: "Nalukui Simasiku", event: "Run created", after: "Draft" }],
  },
  {
    id: "RUN-2026-08-ZM3-M",
    period: "August 2026",
    entityId: "ent-zm3",
    entityName: "Mighty Finance Solutions Engineering Zambia Ltd",
    payGroup: "Livingstone monthly salaried",
    currency: "ZMW",
    status: "In review",
    owner: "Thandiwe Banda (approver)",
    nextAction: "Approval decision",
    dueDate: "2026-08-22",
    preparedBy: "Nalukui Simasiku",
    included: 1,
    excluded: [],
    totals: { headcount: 1, gross: 12_400.0, deductions: 3_620.8, employerCost: 744.0, net: 8_779.2 },
    priorTotals: { headcount: 1, gross: 12_400.0, deductions: 3_620.8, employerCost: 744.0, net: 8_779.2 },
    stages: [],
    timeline: [{ id: "t1", at: "2026-08-13T11:00:00Z", actor: "Nalukui Simasiku", event: "Sent for approval", after: "In review" }],
  },
  {
    // Approved but not yet released — the state where the three release
    // actions are actually available, one after another.
    id: "RUN-2026-08-ZM2-W",
    period: "August 2026",
    entityId: "ent-zm2",
    entityName: "Mighty Finance Solutions Services Zambia Ltd",
    payGroup: "Kitwe weekly site crew",
    currency: "ZMW",
    status: "Approved",
    owner: "Nalukui Simasiku (Payroll)",
    nextAction: "Release payments — payslips are already visible to employees",
    dueDate: "2026-08-26",
    preparedBy: "Chembe Nkandu",
    approvedBy: "Thandiwe Banda",
    included: 9,
    excluded: [
      { employee: "Kondwani Zulu", reason: "Suspended without pay for the whole period. Recorded, not omitted." },
    ],
    totals: { headcount: 9, gross: 61_240.0, deductions: 6_859.0, employerCost: 3_640.0, net: 54_381.0 },
    priorTotals: { headcount: 9, gross: 60_180.0, deductions: 6_742.0, employerCost: 3_578.0, net: 53_438.0 },
    stages: [
      { id: "s1", label: "Confirm period and population", purpose: "Who is in this run, and who is deliberately out.", state: "done", at: "2026-08-17", by: "Chembe Nkandu", note: "9 included, 1 excluded with a reason recorded." },
      { id: "s2", label: "Review readiness blockers", purpose: "Anything that would make the calculation wrong.", state: "done", at: "2026-08-18", by: "Chembe Nkandu" },
      { id: "s3", label: "Calculate", purpose: "Gross to net for every included employee. Resumable — not a black box.", state: "done", at: "2026-08-19", by: "System", note: "9 of 9 calculated." },
      { id: "s4", label: "Review variances and exceptions", purpose: "Explain anything that moved materially since last period.", state: "done", at: "2026-08-20", by: "Chembe Nkandu", note: "One variance above 2%, explained as approved overtime." },
      { id: "s5", label: "Resolve, waive or exclude", purpose: "Every exception needs an owner and a reason.", state: "done", at: "2026-08-20", by: "Chembe Nkandu" },
      { id: "s6", label: "Approve (segregation of duties)", purpose: "A second person, never the preparer.", state: "done", at: "2026-08-21", by: "Thandiwe Banda", note: "Approved by someone other than the preparer." },
      { id: "s7", label: "Release payslips", purpose: "Makes payslips visible. Does not move money.", state: "done", at: "2026-08-24", by: "Nalukui Simasiku", note: "9 employees can now see their August payslip. Nobody has been paid." },
      { id: "s8", label: "Release payments", purpose: "Creates the bank instruction. Separate from payslip release.", state: "current" },
      { id: "s9", label: "Post to accounting", purpose: "Journals and cost allocation.", state: "pending" },
      { id: "s10", label: "Reconcile and close", purpose: "Control totals agreed, period locked.", state: "pending" },
    ],
    timeline: [
      { id: "t1", at: "2026-08-17T07:30:00Z", actor: "Chembe Nkandu", event: "Run created", after: "Draft" },
      { id: "t2", at: "2026-08-19T10:02:00Z", actor: "System", event: "Calculation completed", before: "Calculating", after: "Calculated", reason: "9 of 9 employees calculated" },
      { id: "t3", at: "2026-08-20T14:40:00Z", actor: "Chembe Nkandu", event: "Variance explained", reason: "Overtime up 2.4% on approved shift cover" },
      { id: "t4", at: "2026-08-21T09:05:00Z", actor: "Thandiwe Banda", event: "Run approved", before: "In review", after: "Approved" },
      { id: "t5", at: "2026-08-24T07:15:00Z", actor: "Nalukui Simasiku", event: "Payslips released", reason: "Visible to 9 employees. No payment made." },
    ],
  },
];

export const payrollExceptions: PayrollException[] = [
  {
    id: "EXC-8801",
    runId: "RUN-2026-08-ZM1-M",
    severity: "Blocking",
    kind: "Missing bank details",
    affects: "Kondwani Mwanza · EMP-1004",
    what: "No bank account is recorded, so no payment instruction can be created.",
    impact: "This employee would be calculated but not paid. Net pay of K8,240.60 would sit unremitted.",
    recommended: "Ask the employee to add their account through Privacy and consent, then recalculate this employee only.",
    escalation: "If unresolved by the 24 Aug cutoff, exclude with a reason and pay in an off-cycle run.",
    resolvable: true,
  },
  {
    id: "EXC-8802",
    runId: "RUN-2026-08-ZM1-M",
    severity: "Blocking",
    kind: "Unresolved attendance correction",
    affects: "Kondwani Mwanza · EMP-1004 · AT-2026-1180",
    what: "A correction for 21 July is still In review, so the hours feeding overtime are not final.",
    impact: "Overtime may be understated by up to 5.0 hours (about K780.00 gross).",
    recommended: "Approve or reject the correction, then recalculate. It is already with HR operations.",
    escalation: "If it cannot be decided in time, pay the undisputed hours now and the remainder as arrears next period.",
    resolvable: true,
  },
  {
    id: "EXC-8803",
    runId: "RUN-2026-08-ZM1-M",
    severity: "Warning",
    kind: "Material variance",
    affects: "Chanda Mwansa-Chileshe · EMP-1001",
    what: "Gross is K1,530.00 higher than last period, a 8.7% increase.",
    impact: "Not an error on its own, but unexplained variances should not reach approval.",
    recommended: "Confirmed as the G7 to G8 grade change effective 1 September, plus 3 extra qualifying shifts. Record the explanation and continue.",
    escalation: "None needed once explained.",
    resolvable: true,
  },
  {
    id: "EXC-8804",
    runId: "RUN-2026-08-ZM2-M",
    severity: "Advisory",
    kind: "Statutory identifier missing",
    affects: "Nalukui Simasiku · EMP-1003",
    what: "No TPIN is recorded against the employee payroll profile.",
    impact: "The run can complete, but the statutory return cannot be filed without it.",
    recommended: "Collect the identifier before the filing deadline, not necessarily before this run.",
    escalation: "Raise with the local statutory contact if not supplied within 14 days.",
    resolvable: false,
  },
];

/* -------------------------------------------------------------------------- */
/* Per-employee pay lines — the substance of a run.                            */
/*                                                                             */
/* Figures reconcile to the control totals on the run, and follow the same     */
/* Zambian rules the payslip screens use: PAYE on the 2026 monthly bands,      */
/* NAPSA at 5% of pensionable pay, NHIMA at 1% of basic.                       */
/* -------------------------------------------------------------------------- */

export type ComponentKind = "Earning" | "Deduction" | "Employer";

/** Where a figure came from, which decides whether it can be edited here. */
export type ComponentSource = "Contract" | "Attendance" | "Statutory" | "One-off";

export interface LineComponent {
  code: string;
  label: string;
  kind: ComponentKind;
  amount: number;
  source: ComponentSource;
  basis: string;
  /**
   * Everything needed to explain the figure to the person being paid. A
   * payslip is a projection of these components, not a separate record — so
   * the explanation lives here once and cannot drift from the run.
   */
  inputs: { label: string; value: string }[];
  ruleVersion: string;
  effectiveFrom: string;
  explanation: string;
  priorAmount?: number;
}

export interface RunLine {
  id: string;
  runId: string;
  employeeId: string;
  employee: string;
  jobTitle: string;
  grade: string;
  components: LineComponent[];
  gross: number;
  deductions: number;
  employerCost: number;
  net: number;
  priorNet?: number;
  /** Anything an approver should look at before signing the run off. */
  flags: string[];
}

/** A statutory line is never edited by hand — it is recalculated from the pack. */
export const isEditableSource = (s: ComponentSource) => s !== "Statutory";

/**
 * Build a pay line.
 *
 * Identity comes from the employee directory whenever the person is in it, so
 * a payslip can never name someone differently from their own record. The
 * literals passed in are the fallback for a population the current workspace's
 * directory does not hold — a site crew at another entity, for instance.
 */
function line(
  runId: string,
  employeeId: string,
  employee: string,
  jobTitle: string,
  grade: string,
  components: LineComponent[],
  priorNet?: number,
  flags: string[] = [],
): RunLine {
  const sum = (k: ComponentKind) =>
    components.filter((c) => c.kind === k).reduce((t, c) => t + c.amount, 0);
  const gross = sum("Earning");
  const deductions = sum("Deduction");
  const onFile = employees.find((e) => e.id === employeeId);
  return {
    id: `${runId}:${employeeId}`,
    runId,
    employeeId,
    employee: onFile?.fullName ?? employee,
    jobTitle: onFile?.jobTitle ?? jobTitle,
    grade: onFile?.grade ?? grade,
    components,
    gross,
    deductions,
    employerCost: sum("Employer"),
    net: gross - deductions,
    priorNet,
    flags,
  };
}

const money2 = (v: number) => `K${v.toLocaleString("en-ZM", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

const basic = (amount: number, prior?: number): LineComponent => ({
  code: "BASIC",
  label: "Basic pay",
  kind: "Earning",
  amount,
  source: "Contract",
  basis: "Annual salary ÷ 12",
  inputs: [
    { label: "Annual basic", value: money2(amount * 12) },
    { label: "Pay frequency", value: "Monthly (12)" },
    { label: "FTE", value: "1.0" },
  ],
  ruleVersion: "PAY-BASE v4.2",
  effectiveFrom: "2026-01-01",
  explanation: "Annual basic ÷ 12 pay periods × FTE.",
  priorAmount: prior,
});

/** Basic pay for someone who was not employed, or not paid, for the whole period. */
const basicPartPeriod = (amount: number, daysPaid: number, daysInPeriod: number, fullMonth: number): LineComponent => ({
  code: "BASIC",
  label: "Basic pay (part period)",
  kind: "Earning",
  amount,
  source: "Contract",
  basis: `${money2(fullMonth)} × ${daysPaid} of ${daysInPeriod} days`,
  inputs: [
    { label: "Full month basic", value: money2(fullMonth) },
    { label: "Days paid", value: `${daysPaid} of ${daysInPeriod}` },
  ],
  ruleVersion: "PAY-BASE v4.2",
  effectiveFrom: "2026-01-01",
  explanation: "Monthly basic apportioned across the days actually paid in the period.",
});

const shift = (amount: number, shifts: number, rate: number, prior?: number): LineComponent => ({
  code: "SHIFT",
  label: "Shift allowance",
  kind: "Earning",
  amount,
  source: "Attendance",
  basis: `${shifts} qualifying shifts × ${money2(rate)}`,
  inputs: [
    { label: "Qualifying shifts", value: String(shifts) },
    { label: "Rate per shift", value: money2(rate) },
    { label: "Source", value: "Approved attendance, to the period cutoff" },
  ],
  ruleVersion: "ALLOW-SHIFT v2.1",
  effectiveFrom: "2026-04-01",
  explanation: "Qualifying night and weekend shifts × the rate in force for the period.",
  priorAmount: prior,
});

const overtime = (amount: number, hours: number, hourly: number, prior?: number): LineComponent => ({
  code: "OT",
  label: "Overtime",
  kind: "Earning",
  amount,
  source: "Attendance",
  basis: "Approved hours × hourly × 1.5",
  inputs: [
    { label: "Approved hours", value: hours.toFixed(1) },
    { label: "Hourly rate", value: money2(hourly) },
    { label: "Multiplier", value: "1.5" },
  ],
  ruleVersion: "OT-STD v3.0",
  effectiveFrom: "2025-07-01",
  explanation: "Approved overtime hours × hourly rate × multiplier, rounded to 2 decimals.",
  priorAmount: prior,
});

const paye = (amount: number, taxable: number, prior?: number): LineComponent => ({
  code: "PAYE",
  label: "PAYE",
  kind: "Deduction",
  amount,
  source: "Statutory",
  basis: "ZRA 2026 monthly bands",
  inputs: [
    { label: "Taxable pay", value: money2(taxable) },
    { label: "Exempt band", value: "First K5,100.00 at 0%" },
    { label: "Bands applied", value: "20%, 30%, then 37%" },
  ],
  ruleVersion: "ZM-PAYE 2026.1",
  effectiveFrom: "2026-01-01",
  explanation:
    "Taxable pay charged through the 2026 monthly bands: nothing on the first K5,100, then 20%, 30% and 37% on each band above it.",
  priorAmount: prior,
});

const napsa = (amount: number, pensionable: number, prior?: number): LineComponent => ({
  code: "NAPSA",
  label: "NAPSA (employee)",
  kind: "Deduction",
  amount,
  source: "Statutory",
  basis: "5% of pensionable pay, capped",
  inputs: [
    { label: "Pensionable pay", value: money2(pensionable) },
    { label: "Rate", value: "5%" },
    { label: "Monthly ceiling", value: money2(1_491.6) },
  ],
  ruleVersion: "ZM-NAPSA 2026.1",
  effectiveFrom: "2026-01-01",
  explanation: "5% of pensionable pay, stopping at the monthly ceiling. The employer contributes the same again.",
  priorAmount: prior,
});

const nhima = (amount: number, basicPay: number, prior?: number): LineComponent => ({
  code: "NHIMA",
  label: "NHIMA (employee)",
  kind: "Deduction",
  amount,
  source: "Statutory",
  basis: "1% of basic pay",
  inputs: [
    { label: "Basic pay", value: money2(basicPay) },
    { label: "Rate", value: "1%" },
  ],
  ruleVersion: "ZM-NHIMA 2026.1",
  effectiveFrom: "2026-01-01",
  explanation: "1% of basic pay. The employer contributes the same again.",
  priorAmount: prior,
});

const employerContribs = (napsaEr: number, nhimaEr: number, pensionable: number, basicPay: number): LineComponent[] => [
  {
    code: "NAPSA-ER", label: "NAPSA (employer)", kind: "Employer", amount: napsaEr, source: "Statutory",
    basis: "5% of pensionable pay, capped",
    inputs: [{ label: "Pensionable pay", value: money2(pensionable) }, { label: "Rate", value: "5%" }],
    ruleVersion: "ZM-NAPSA 2026.1", effectiveFrom: "2026-01-01",
    explanation: "Paid by the employer on top of pay. It is a cost to the business, never deducted from the employee.",
  },
  {
    code: "NHIMA-ER", label: "NHIMA (employer)", kind: "Employer", amount: nhimaEr, source: "Statutory",
    basis: "1% of basic pay",
    inputs: [{ label: "Basic pay", value: money2(basicPay) }, { label: "Rate", value: "1%" }],
    ruleVersion: "ZM-NHIMA 2026.1", effectiveFrom: "2026-01-01",
    explanation: "Paid by the employer on top of pay. It is a cost to the business, never deducted from the employee.",
  },
];

export const runLines: RunLine[] = [
  /* ---- RUN-2026-07-ZM1-M — July, closed. The period August is compared to. */
  line(
    "RUN-2026-07-ZM1-M", "w-1001", "Chanda Mwansa-Chileshe", "Production supervisor", "G6",
    [
      basic(18_000),
      shift(1_650, 11, 150),
      paye(4_896.5, 19_650),
      napsa(982.5, 19_650),
      nhima(180, 18_000),
      ...employerContribs(982.5, 180, 19_650, 18_000),
    ],
  ),
  line(
    "RUN-2026-07-ZM1-M", "w-1002", "Bwalya Musonda", "Maintenance technician", "G5",
    [basic(16_500), paye(3_731, 16_500), napsa(825, 16_500), nhima(165, 16_500), ...employerContribs(825, 165, 16_500, 16_500)],
  ),
  line(
    "RUN-2026-07-ZM1-M", "w-1003", "Nalukui Simasiku", "Payroll officer", "G6",
    [basic(19_200), paye(4_730, 19_200), napsa(960, 19_200), nhima(192, 19_200), ...employerContribs(960, 192, 19_200, 19_200)],
  ),

  /* ---- RUN-2026-08-ZM1-M — Lusaka monthly salaried, 4 employees ---------- */
  line(
    "RUN-2026-08-ZM1-M", "w-1001", "Chanda Mwansa-Chileshe", "Production supervisor", "G6",
    [
      basic(18_000, 18_000),
      shift(2_100, 14, 150, 1_650),
      overtime(778.88, 5, 103.85),
      paye(5_351.18, 20_878.88, 4_896.5),
      napsa(1_043.94, 20_878.88, 982.5),
      nhima(180, 18_000, 180),
      ...employerContribs(1_043.94, 180, 20_878.88, 18_000),
    ],
    13_591,
    ["Shift allowance up 27% on last period — approved cover for two absences."],
  ),
  line(
    "RUN-2026-08-ZM1-M", "w-1002", "Bwalya Musonda", "Maintenance technician", "G5",
    [basic(16_500, 16_500), paye(3_731, 16_500, 3_731), napsa(825, 16_500, 825), nhima(165, 16_500, 165), ...employerContribs(825, 165, 16_500, 16_500)],
    11_779,
  ),
  line(
    "RUN-2026-08-ZM1-M", "w-1003", "Nalukui Simasiku", "Payroll officer", "G6",
    [basic(19_200, 19_200), paye(4_730, 19_200, 4_730), napsa(960, 19_200, 960), nhima(192, 19_200, 192), ...employerContribs(960, 192, 19_200, 19_200)],
    13_318,
    ["Prepared this run, so cannot approve it."],
  ),
  line(
    "RUN-2026-08-ZM1-M", "w-1004", "Temwani Phiri", "Quality inspector", "G4",
    [
      basicPartPeriod(14_516.13, 25, 31, 18_000),
      paye(2_996.97, 14_516.13),
      napsa(725.81, 14_516.13),
      nhima(145.16, 14_516.13),
      ...employerContribs(725.81, 145.16, 14_516.13, 14_516.13),
    ],
    0,
    ["Returned from unpaid leave on 3 Aug — part period, so pay is below a full month.", "Not paid in July, so there is nothing to compare against."],
  ),

  /* ---- RUN-2026-08-ZM2-W — Kitwe weekly site crew, 9 employees ----------- */
  line("RUN-2026-08-ZM2-W", "w-2101", "Mulenga Chanda", "Site foreman", "G4",
    [basic(6_800), overtime(620, 4, 103.33), paye(496, 7_420), napsa(371, 7_420), nhima(68, 6_800), ...employerContribs(371, 68, 7_420, 6_800)], 6_312),
  line("RUN-2026-08-ZM2-W", "w-2102", "Grace Mwape", "Site clerk", "G3",
    [basic(6_800), paye(340, 6_800), napsa(340, 6_800), nhima(68, 6_800), ...employerContribs(340, 68, 6_800, 6_800)], 6_052),
  line("RUN-2026-08-ZM2-W", "w-2103", "Justin Banda", "Rigger", "G3",
    [basic(6_200), overtime(480, 3.5, 91.43), paye(316, 6_680), napsa(334, 6_680), nhima(62, 6_200), ...employerContribs(334, 62, 6_680, 6_200)], 5_770),
  line("RUN-2026-08-ZM2-W", "w-2104", "Priscilla Zulu", "Store controller", "G3",
    [basic(6_200), overtime(310, 2.5, 82.67), paye(282, 6_510), napsa(325.5, 6_510), nhima(62, 6_200), ...employerContribs(325.5, 62, 6_510, 6_200)], 5_698),
  line("RUN-2026-08-ZM2-W", "w-2105", "Emmanuel Tembo", "Plant operator", "G4",
    [basic(7_400), overtime(890, 6, 98.89), paye(757, 8_290), napsa(414.5, 8_290), nhima(74, 7_400), ...employerContribs(414.5, 74, 8_290, 7_400)], 6_602,
    ["Overtime up 2.4% on last period — approved shift cover, explained before approval."]),
  line("RUN-2026-08-ZM2-W", "w-2106", "Loveness Mubita", "Site cook", "G2",
    [basic(5_900), paye(160, 5_900), napsa(295, 5_900), nhima(59, 5_900), ...employerContribs(295, 59, 5_900, 5_900)], 5_386),
  line("RUN-2026-08-ZM2-W", "w-2107", "Davies Sikaonga", "Welder", "G3",
    [basic(6_500), overtime(540, 4, 90), paye(388, 7_040), napsa(352, 7_040), nhima(65, 6_500), ...employerContribs(352, 65, 7_040, 6_500)], 5_910),
  line("RUN-2026-08-ZM2-W", "w-2108", "Agness Mwila", "Site administrator", "G3",
    [basic(5_900), overtime(220, 1.5, 97.78), paye(204, 6_120), napsa(306, 6_120), nhima(59, 5_900), ...employerContribs(306, 59, 6_120, 5_900)], 5_400),
  line("RUN-2026-08-ZM2-W", "w-2109", "Peter Chomba", "General hand", "G2",
    [basic(6_100), overtime(380, 2.5, 101.33), paye(276, 6_480), napsa(324, 6_480), nhima(61, 6_100), ...employerContribs(324, 61, 6_480, 6_100)], 5_640),
];

/* -------------------------------------------------------------------------- */
/* Payslips are derived, never stored.                                        */
/*                                                                             */
/* A payslip is what one employee sees of one run line. Deriving it means the  */
/* two can never disagree — the commonest and most damaging payroll bug is a   */
/* payslip that says one thing and the run that paid it saying another.        */
/* Only a run that has released its payslips produces any.                     */
/* -------------------------------------------------------------------------- */

export interface DerivedPayslip {
  id: string;
  runId: string;
  employeeId: string;
  employee: string;
  period: string;
  entityName: string;
  currency: string;
  payDate: string;
  gross: number;
  deductions: number;
  net: number;
  employerCost: number;
  components: LineComponent[];
  /** Whether the money has actually moved, which is not the same as being visible. */
  paid: boolean;
}

/** A payslip exists once its run has reached payslip release. */
function payslipsReleased(run: PayRun) {
  if (run.status === "Paid" || run.status === "Closed") return true;
  return run.stages.some((st) => st.id === "s7" && st.state === "done");
}

const PAY_DATES: Record<string, string> = {
  "RUN-2026-07-ZM1-M": "2026-07-27",
  "RUN-2026-08-ZM1-M": "2026-08-28",
  "RUN-2026-08-ZM2-W": "2026-08-28",
};

export function derivePayslips(): DerivedPayslip[] {
  const out: DerivedPayslip[] = [];
  for (const run of payRuns) {
    if (!payslipsReleased(run)) continue;
    for (const l of runLines.filter((x) => x.runId === run.id)) {
      out.push({
        id: `PS-${run.id.replace("RUN-", "")}-${l.employeeId}`,
        runId: run.id,
        employeeId: l.employeeId,
        employee: l.employee,
        period: run.period,
        entityName: run.entityName,
        currency: run.currency,
        payDate: PAY_DATES[run.id] ?? run.dueDate,
        gross: l.gross,
        deductions: l.deductions,
        net: l.net,
        employerCost: l.employerCost,
        components: l.components,
        paid: run.status === "Paid" || run.status === "Closed",
      });
    }
  }
  return out;
}

export const payrollRunApi = {
  runs: async () => {
    await delay();
    return payRuns;
  },
  linesFor: async (runId: string) => {
    await delay(280);
    return runLines.filter((l) => l.runId === runId);
  },
  line: async (lineId: string) => {
    await delay(260);
    return runLines.find((l) => l.id === lineId) ?? null;
  },
  payslips: async () => {
    await delay(480);
    return derivePayslips();
  },
  payslip: async (id: string) => {
    await delay(480);
    return derivePayslips().find((ps) => ps.id === id) ?? null;
  },
  run: async (id: string) => {
    await delay();
    return payRuns.find((r) => r.id === id) ?? null;
  },
  exceptions: async () => {
    await delay();
    return payrollExceptions;
  },
  exceptionsFor: async (runId: string) => {
    await delay(260);
    return payrollExceptions.filter((e) => e.runId === runId);
  },
};

export const money = (v: number, currency: string) =>
  new Intl.NumberFormat(undefined, { style: "currency", currency }).format(v);
