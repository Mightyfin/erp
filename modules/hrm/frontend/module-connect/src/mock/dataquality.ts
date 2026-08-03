/**
 * Data quality and stewardship (HRM-011).
 *
 * Product rules encoded here:
 *  - A bulk change is previewed (dry run) before it commits, and stays
 *    reversible afterwards. "Apply to 412 records" with no preview is how
 *    payroll accidents happen.
 *  - A merge must be reversible. Merging two people who are not the same person
 *    is a recoverable mistake only if the system kept both originals.
 */
const delay = (ms = 420) => new Promise((r) => setTimeout(r, ms));

export interface QualityRule {
  id: string;
  rule: string;
  scope: string;
  severity: "Blocking" | "Warning" | "Advisory";
  /** Why it matters downstream — never just "field is empty". */
  consequence: string;
  passing: number;
  failing: number;
  owner: string;
}

export interface DuplicateCandidate {
  id: string;
  a: { id: string; name: string; detail: string };
  b: { id: string; name: string; detail: string };
  score: number;
  matchedOn: string[];
  differsOn: string[];
  recommendation: "Likely the same person" | "Probably different people" | "Needs a human decision";
  status: "Open" | "Merged" | "Confirmed different";
  mergedOn?: string;
  reversibleUntil?: string;
}

export interface BulkJob {
  id: string;
  what: string;
  requestedBy: string;
  scope: string;
  affected: number;
  status: "Dry run" | "Awaiting approval" | "Applied" | "Rolled back" | "Rejected";
  dryRun: {
    willChange: number;
    noChange: number;
    wouldFail: number;
    failReason?: string;
  };
  appliedOn?: string;
  reversibleUntil?: string;
  sample: { employee: string; field: string; before: string; after: string; ok: boolean; note?: string }[];
}

export interface ImportRun {
  id: string;
  source: string;
  received: string;
  rows: number;
  accepted: number;
  rejected: number;
  status: "Reconciled" | "Exceptions open" | "Rejected";
  exceptions: { row: number; problem: string; action: string }[];
}

export const rules: QualityRule[] = [
  {
    id: "DQ-001",
    rule: "Every paid employee has bank details",
    scope: "All employees in a pay group",
    severity: "Blocking",
    consequence: "Payroll calculates but cannot pay. Net pay sits unremitted and the employee chases HR.",
    passing: 7,
    failing: 1,
    owner: "Payroll",
  },
  {
    id: "DQ-002",
    rule: "Every employee has a statutory tax identifier",
    scope: "All employees",
    severity: "Blocking",
    consequence: "The statutory return cannot be filed, which is a compliance breach rather than an inconvenience.",
    passing: 7,
    failing: 1,
    owner: "Payroll",
  },
  {
    id: "DQ-003",
    rule: "Every employee has a current line manager",
    scope: "All active employees",
    severity: "Warning",
    consequence: "Leave and expense requests have nowhere to route, so they silently stall.",
    passing: 8,
    failing: 0,
    owner: "HR operations",
  },
  {
    id: "DQ-004",
    rule: "Fixed-term contracts have an end date",
    scope: "Fixed-term and intern engagements",
    severity: "Blocking",
    consequence: "No expiry alert is raised, so someone works past the end of their legal right to.",
    passing: 2,
    failing: 0,
    owner: "HR operations",
  },
  {
    id: "DQ-005",
    rule: "Emergency contact recorded",
    scope: "All employees",
    severity: "Warning",
    consequence: "Nobody to call in an incident. Matters most for site-based and lone-working roles.",
    passing: 5,
    failing: 3,
    owner: "HR operations",
  },
  {
    id: "DQ-006",
    rule: "No employee assigned to a closed organisation unit",
    scope: "All assignments",
    severity: "Advisory",
    consequence: "Headcount reports double-count or lose people at the boundary of a restructure.",
    passing: 8,
    failing: 0,
    owner: "HR operations",
  },
];

export const duplicates: DuplicateCandidate[] = [
  {
    id: "DUP-0031",
    a: { id: "w-1006", name: "Gift Zulu", detail: "EMP-1006 · Contractor · Solwezi Yard · started 2025-11-03" },
    b: { id: "cand-8841", name: "Gift M. Zulu", detail: "Candidate record · applied 2026-06-14 · Solwezi" },
    score: 0.86,
    matchedOn: ["Family name", "First name", "Location", "Mobile number last 4 digits"],
    differsOn: ["Date of birth (not recorded on the candidate record)", "Email domain"],
    recommendation: "Likely the same person",
    status: "Open",
  },
  {
    id: "DUP-0028",
    a: { id: "w-1004", name: "Kondwani Mwanza", detail: "EMP-1004 · Fixed term · Livingstone Works" },
    b: { id: "w-9902", name: "Kondwani Mwansa", detail: "Archived pre-hire record · created 2023-11-20, never activated" },
    score: 0.94,
    matchedOn: ["Family name (spelling variant)", "First name", "Date of birth", "National identifier"],
    differsOn: ["Employee number", "Record state"],
    recommendation: "Likely the same person",
    status: "Merged",
    mergedOn: "2026-07-02",
    reversibleUntil: "2026-10-02",
  },
  {
    id: "DUP-0034",
    a: { id: "w-1002", name: "Mutale Kabwe", detail: "EMP-1002 · Operations Manager · Lusaka HQ" },
    b: { id: "w-1099", name: "Mutale Kabwelu", detail: "EMP-1099 · Warehouse Operative · Kitwe Depot" },
    score: 0.71,
    matchedOn: ["Similar family name", "Same entity"],
    differsOn: ["First initial of middle name", "Date of birth", "National identifier", "Branch", "Start date"],
    recommendation: "Probably different people",
    status: "Confirmed different",
  },
];

export const bulkJobs: BulkJob[] = [
  {
    id: "BLK-2026-0014",
    what: "Move Ndola Plant employees to the new CC-OPS-LUS-02 cost centre",
    requestedBy: "Thandiwe Banda (HR operations)",
    scope: "Active employees at Ndola Plant, effective 1 September 2026",
    affected: 1,
    status: "Dry run",
    dryRun: {
      willChange: 1,
      noChange: 0,
      wouldFail: 0,
    },
    sample: [
      { employee: "Thandiwe Banda", field: "Cost centre", before: "CC-OPS-LUS", after: "CC-OPS-LUS-02", ok: true },
    ],
  },
  {
    id: "BLK-2026-0011",
    what: "Set the 2026 work calendar on all Zambian employees",
    requestedBy: "Thandiwe Banda (HR operations)",
    scope: "All active employees in the Zambian entity",
    affected: 4,
    status: "Applied",
    dryRun: {
      willChange: 3,
      noChange: 1,
      wouldFail: 0,
    },
    appliedOn: "2026-01-06",
    reversibleUntil: "2026-04-06",
    sample: [
      { employee: "Chanda Mwansa-Chileshe", field: "Work calendar", before: "ZM 2025", after: "ZM 2026", ok: true },
      { employee: "Mutale Kabwe", field: "Work calendar", before: "ZM 2025", after: "ZM 2026", ok: true },
      { employee: "Emmanuel Sakala", field: "Work calendar", before: "ZM 2026", after: "ZM 2026", ok: true, note: "Already correct — left unchanged rather than rewritten." },
    ],
  },
  {
    id: "BLK-2026-0009",
    what: "Backfill missing emergency contacts from the 2024 onboarding forms",
    requestedBy: "Thandiwe Banda (HR operations)",
    scope: "Employees with no emergency contact recorded",
    affected: 3,
    status: "Rejected",
    dryRun: {
      willChange: 1,
      noChange: 0,
      wouldFail: 2,
      failReason:
        "Two source records are older than the retention period for onboarding forms, so the data should no longer exist and must not be reinstated.",
    },
    sample: [
      { employee: "Kondwani Mwanza", field: "Emergency contact", before: "Not recorded", after: "From 2024 form", ok: true },
      { employee: "Gift Zulu", field: "Emergency contact", before: "Not recorded", after: "—", ok: false, note: "Source form past retention. Ask the employee directly instead." },
      { employee: "Natasha Chirwa", field: "Emergency contact", before: "Not recorded", after: "—", ok: false, note: "Pre-hire — will be collected during onboarding." },
    ],
  },
];

export const imports: ImportRun[] = [
  {
    id: "IMP-2026-0044",
    source: "Attendance terminal export — Livingstone Works",
    received: "2026-07-28",
    rows: 62,
    accepted: 60,
    rejected: 2,
    status: "Exceptions open",
    exceptions: [
      { row: 18, problem: "Clock-out earlier than clock-in", action: "Held for review — likely a terminal clock drift, not a real record." },
      { row: 41, problem: "Employee number not recognised", action: "Held for review — badge issued to a leaver." },
    ],
  },
  {
    id: "IMP-2026-0041",
    source: "NAPSA membership return",
    received: "2026-07-01",
    rows: 8,
    accepted: 8,
    rejected: 0,
    status: "Reconciled",
    exceptions: [],
  },
];

export const dataQualityApi = {
  rules: async () => {
    await delay();
    return rules;
  },
  duplicates: async () => {
    await delay();
    return duplicates;
  },
  bulkJobs: async () => {
    await delay();
    return bulkJobs;
  },
  imports: async () => {
    await delay(320);
    return imports;
  },
};
