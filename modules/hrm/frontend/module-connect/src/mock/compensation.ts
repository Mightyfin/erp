/**
 * Compensation, benefits, pay equity and insurance (HRM-034..037).
 *
 * Product rules encoded here:
 *  - Pay is restricted data. A list view shows a band or a ratio, not another
 *    person's salary.
 *  - Pay-equity output is suppressed below a group-size threshold, because a
 *    small group makes an individual's pay inferable.
 *  - Insurance claims reference an outcome and a settlement, never a diagnosis.
 */
const delay = (ms = 420) => new Promise((r) => setTimeout(r, ms));

export const money = (v: number, currency: string) =>
  new Intl.NumberFormat(undefined, { style: "currency", currency, maximumFractionDigits: 0 }).format(v);

export interface Band {
  grade: string;
  currency: string;
  min: number;
  mid: number;
  max: number;
}

export interface CompRecord {
  /** ListPage requires an `id`; for a compensation record it is the employee. */
  id: string;
  employeeId: string;
  employee: string;
  grade: string;
  currency: string;
  /** Only visible to Payroll, HR admin and the employee themselves. */
  salary: number;
  fte: number;
  lastChange: string;
  lastChangeReason: string;
  /** Salary ÷ band midpoint. 1.0 means paid at the midpoint. */
  compaRatio: number;
  /** Where in the band, 0% = min, 100% = max. */
  rangePenetration: number;
  nextReview: string;
}

export interface BenefitEnrolment {
  id: string;
  employee: string;
  plan: string;
  kind: "Pension" | "Medical" | "Life cover" | "Income protection";
  coverage: string;
  employeeContribution: string;
  employerContribution: string;
  currency: string;
  status: "Enrolled" | "Pending evidence" | "Waived" | "Ended";
  effectiveFrom: string;
  /** Set where a life event opened a window to change cover. */
  changeWindow?: string;
  dependants: number;
}

export interface ReviewCycle {
  id: string;
  name: string;
  population: string;
  budgetPct: number;
  allocatedPct: number;
  opens: string;
  closes: string;
  status: "Not started" | "Manager input" | "Calibration" | "Approval" | "Closed";
  guidance: string;
}

export interface PayGapRow {
  group: string;
  headcount: number;
  /** Null when the group is too small to report without identifying someone. */
  medianGapPct: number | null;
  meanGapPct: number | null;
  suppressed: boolean;
  note?: string;
}

export interface InsuranceClaim {
  id: string;
  employee: string;
  policy: string;
  provider: string;
  raised: string;
  status: "Notified" | "With provider" | "Settled" | "Declined";
  /** Outcome and money only. No diagnosis is ever recorded. */
  outcome: string;
  settlement?: number;
  currency: string;
  reference: string;
}

export const bands: Band[] = [
  { grade: "G4", currency: "ZMW", min: 96_000, mid: 120_000, max: 144_000 },
  { grade: "G5", currency: "ZMW", min: 120_000, mid: 150_000, max: 180_000 },
  { grade: "G6", currency: "ZMW", min: 156_000, mid: 192_000, max: 228_000 },
  { grade: "G7", currency: "ZMW", min: 192_000, mid: 228_000, max: 264_000 },
  { grade: "G9", currency: "ZMW", min: 300_000, mid: 360_000, max: 420_000 },
];

export const compRecords: CompRecord[] = [
  {
    id: "w-1001",
    employeeId: "w-1001",
    employee: "Chanda Mwansa-Chileshe",
    grade: "G7",
    currency: "ZMW",
    salary: 216_000,
    fte: 1,
    lastChange: "2025-01-01",
    lastChangeReason: "Annual review — 3.2% merit increase",
    compaRatio: 0.947,
    rangePenetration: 31,
    nextReview: "2026-09-01",
  },
  {
    id: "w-1002",
    employeeId: "w-1002",
    employee: "Mutale Kabwe",
    grade: "G9",
    currency: "ZMW",
    salary: 378_000,
    fte: 1,
    lastChange: "2025-01-01",
    lastChangeReason: "Annual review — 2.8% merit increase",
    compaRatio: 1.05,
    rangePenetration: 68,
    nextReview: "2027-01-01",
  },
  {
    id: "w-1004",
    employeeId: "w-1004",
    employee: "Kondwani Mwanza",
    grade: "G4",
    currency: "ZMW",
    salary: 108_000,
    fte: 1,
    lastChange: "2024-02-01",
    lastChangeReason: "Starting salary on appointment",
    compaRatio: 0.9,
    rangePenetration: 17,
    nextReview: "Contract ends 2026-08-31",
  },
  {
    id: "w-1005",
    employeeId: "w-1005",
    employee: "Thandiwe Banda",
    grade: "G5",
    currency: "ZMW",
    salary: 90_000,
    fte: 0.6,
    lastChange: "2025-01-01",
    lastChangeReason: "Annual review — 3.0% merit increase",
    compaRatio: 0.6,
    rangePenetration: 23,
    nextReview: "2027-01-01",
  },
  {
    id: "w-1008",
    employeeId: "w-1008",
    employee: "Emmanuel Sakala",
    grade: "G6",
    currency: "ZMW",
    salary: 204_000,
    fte: 1,
    lastChange: "2025-01-01",
    lastChangeReason: "Annual review — 2.5% merit increase",
    compaRatio: 1.063,
    rangePenetration: 70,
    nextReview: "2027-01-01",
  },
];

export const enrolments: BenefitEnrolment[] = [
  {
    id: "BEN-1001-PEN",
    employee: "Chanda Mwansa-Chileshe",
    plan: "NAPSA — statutory",
    kind: "Pension",
    coverage: "Employee only",
    employeeContribution: "6.0% of pensionable pay",
    employerContribution: "12.0% of pensionable pay",
    currency: "ZMW",
    status: "Enrolled",
    effectiveFrom: "2019-04-01",
    dependants: 0,
  },
  {
    id: "BEN-1001-MED",
    employee: "Chanda Mwansa-Chileshe",
    plan: "NHIMA plus private cover — family",
    kind: "Medical",
    coverage: "Employee plus 2 dependants",
    employeeContribution: money(1_200, "ZMW") + " per month",
    employerContribution: money(3_300, "ZMW") + " per month",
    currency: "ZMW",
    status: "Enrolled",
    effectiveFrom: "2024-01-01",
    dependants: 2,
  },
  {
    id: "BEN-1005-PEN",
    employee: "Thandiwe Banda",
    plan: "NAPSA — statutory",
    kind: "Pension",
    coverage: "Employee only",
    employeeContribution: "6.0% of pensionable pay",
    employerContribution: "12.0% of pensionable pay",
    currency: "ZMW",
    status: "Enrolled",
    effectiveFrom: "2023-06-01",
    dependants: 0,
  },
  {
    id: "BEN-1005-MED",
    employee: "Thandiwe Banda",
    plan: "NHIMA plus private cover — single",
    kind: "Medical",
    coverage: "Employee only",
    employeeContribution: money(550, "ZMW") + " per month",
    employerContribution: money(2_200, "ZMW") + " per month",
    currency: "ZMW",
    status: "Pending evidence",
    effectiveFrom: "2026-08-01",
    changeWindow:
      "Life event recorded 12 Jul 2026. The window to add a dependant closes 11 Sep 2026.",
    dependants: 0,
  },
  {
    id: "BEN-1004-LIFE",
    employee: "Kondwani Mwanza",
    plan: "Group life cover",
    kind: "Life cover",
    coverage: "2× annual salary",
    employeeContribution: "None — employer paid",
    employerContribution: "0.4% of salary",
    currency: "ZMW",
    status: "Enrolled",
    effectiveFrom: "2024-02-01",
    dependants: 1,
  },
  {
    id: "BEN-1008-IP",
    employee: "Emmanuel Sakala",
    plan: "Income protection",
    kind: "Income protection",
    coverage: "60% of salary after 26 weeks",
    employeeContribution: "None — employer paid",
    employerContribution: "1.1% of salary",
    currency: "ZMW",
    status: "Enrolled",
    effectiveFrom: "2011-10-01",
    dependants: 0,
  },
];

export const cycles: ReviewCycle[] = [
  {
    id: "CMP-2027-ANNUAL",
    name: "Annual compensation review 2027",
    population: "All permanent employees, Zambia",
    budgetPct: 3.5,
    allocatedPct: 0,
    opens: "2026-11-01",
    closes: "2026-12-12",
    status: "Not started",
    guidance:
      "Increases are proposed by the line manager against a 3.5% departmental budget, calibrated across the department, then approved. A proposal taking someone above the band maximum needs a written justification.",
  },
  {
    id: "CMP-2026-MIDYEAR",
    name: "Mid-year market adjustment 2026",
    population: "Roles flagged as below market by the 2026 benchmark",
    budgetPct: 1.2,
    allocatedPct: 0.9,
    opens: "2026-05-01",
    closes: "2026-06-15",
    status: "Closed",
    guidance:
      "Targeted at roles where the benchmark showed a gap of more than 8% to market median. Not a performance mechanism.",
  },
];

export const payGap: PayGapRow[] = [
  { group: "All employees", headcount: 8, medianGapPct: null, meanGapPct: null, suppressed: true, note: "Fewer than the 20-employee reporting threshold. Publishing a figure for this population would make individual pay inferable." },
  { group: "the Zambian entity", headcount: 4, medianGapPct: null, meanGapPct: null, suppressed: true, note: "Group too small to report." },
  { group: "Grade G5 to G7", headcount: 3, medianGapPct: null, meanGapPct: null, suppressed: true, note: "Group too small to report." },
];

export const claims: InsuranceClaim[] = [
  {
    id: "INS-2026-0031",
    employee: "Emmanuel Sakala",
    policy: "Income protection",
    provider: "Madison Life Insurance",
    raised: "2026-06-05",
    status: "With provider",
    outcome: "Eligibility confirmed. Awaiting the provider's assessment of the deferred period.",
    currency: "ZMW",
    reference: "NV-88214-2026",
  },
  {
    id: "INS-2026-0018",
    employee: "Kondwani Mwanza",
    policy: "NHIMA plus private cover",
    provider: "Prudential Zambia",
    raised: "2026-03-14",
    status: "Settled",
    outcome: "Treatment costs reimbursed directly to the employee by the provider.",
    settlement: 10_400,
    currency: "ZMW",
    reference: "ZP-4471-2026",
  },
];

export const compensationApi = {
  records: async () => {
    await delay();
    return compRecords;
  },
  bands: async () => {
    await delay(280);
    return bands;
  },
  enrolments: async () => {
    await delay();
    return enrolments;
  },
  cycles: async () => {
    await delay(300);
    return cycles;
  },
  payGap: async () => {
    await delay(320);
    return payGap;
  },
  claims: async () => {
    await delay();
    return claims;
  },
};
