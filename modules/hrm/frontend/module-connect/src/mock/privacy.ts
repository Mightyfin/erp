/**
 * Employee data privacy, consent and subject rights (HRM-059).
 * Self-contained mock data + async reader, matching the mock-service pattern.
 */
import type { RequestStatus, TimelineEvent } from "./types";

const delay = (ms = 420) => new Promise((r) => setTimeout(r, ms));

export type ConsentState = "granted" | "withdrawn" | "not-required";

export interface ProcessingPurpose {
  id: string;
  purpose: string;
  /** Plain-language description of what is actually held. */
  dataHeld: string;
  lawfulBasis: string;
  /** Consent only applies where the lawful basis is consent. */
  consent: ConsentState;
  withdrawable: boolean;
  /** What the employee loses if they withdraw — shown before confirming. */
  consequenceOfWithdrawal?: string;
  /** Why there is no consent toggle. Wording differs per purpose, so never hardcode it. */
  notRequiredReason?: string;
  retention: string;
  crossBorder?: string;
}

export type SubjectRequestType =
  | "Access"
  | "Correction"
  | "Erasure"
  | "Consent withdrawal";

export interface SubjectRequest {
  id: string;
  employeeId: string;
  type: SubjectRequestType;
  raisedOn: string;
  status: RequestStatus;
  owner: string;
  nextAction: string;
  dueDate: string;
  /** Statutory response window, shown so the deadline is never implicit. */
  statutoryDeadline: string;
  scope: string;
  legalHold?: string;
  timeline: TimelineEvent[];
}

export const processingPurposes: ProcessingPurpose[] = [
  {
    id: "pp-payroll",
    purpose: "Paying you",
    dataHeld: "Bank account, tax identifiers, salary, deductions and payslip history.",
    lawfulBasis: "Contract and legal obligation",
    consent: "not-required",
    withdrawable: false,
    notRequiredReason: "Required to employ and pay you, so it cannot be switched off.",
    retention: "7 years after employment ends (statutory)",
  },
  {
    id: "pp-attendance",
    purpose: "Recording your working time",
    dataHeld: "Clock-in and clock-out events, shift assignment and corrections.",
    lawfulBasis: "Contract",
    consent: "not-required",
    withdrawable: false,
    notRequiredReason: "Required to record the hours you are paid for.",
    retention: "3 years after the pay period",
  },
  {
    id: "pp-biometric",
    purpose: "Biometric clocking at Gate 3",
    dataHeld: "A one-way fingerprint template. No fingerprint image is stored.",
    lawfulBasis: "Explicit consent",
    consent: "granted",
    withdrawable: true,
    consequenceOfWithdrawal:
      "You would clock in with a badge or at a supervisor terminal instead. Your pay is not affected.",
    retention: "Deleted within 30 days of withdrawal or leaving",
  },
  {
    id: "pp-health",
    purpose: "Occupational health and fitness to work",
    dataHeld: "Fitness-to-work outcome and any workplace adjustments. Not your diagnosis.",
    lawfulBasis: "Explicit consent",
    consent: "granted",
    withdrawable: true,
    consequenceOfWithdrawal:
      "Existing workplace adjustments would need to be reassessed before they continue.",
    retention: "Duration of employment plus 3 years",
  },
  {
    id: "pp-background",
    purpose: "Pre-employment background checks",
    dataHeld: "Verification outcome only — pass, fail or pending. Not the underlying report.",
    lawfulBasis: "Explicit consent",
    consent: "granted",
    withdrawable: false,
    notRequiredReason: "Given before you joined. It cannot be withdrawn retrospectively, but the record is deleted on the retention date below.",
    retention: "12 months after the check completed",
  },
  {
    id: "pp-ai",
    purpose: "AI-assisted CV screening",
    dataHeld: "Not applied to you. Recruitment candidates only.",
    lawfulBasis: "Explicit consent",
    consent: "not-required",
    withdrawable: false,
    notRequiredReason: "Nothing to consent to — this is not used on employees.",
    retention: "Not applicable",
  },
  {
    id: "pp-analytics",
    purpose: "Workforce analytics",
    dataHeld: "Aggregated headcount, turnover and absence trends.",
    lawfulBasis: "Legitimate interests",
    consent: "withdrawn",
    withdrawable: true,
    consequenceOfWithdrawal:
      "You are already excluded from named analytics. Aggregate totals still include you, with small-group suppression applied.",
    retention: "Aggregates retained indefinitely; identifiers removed after 24 months",
    crossBorder: "Processed in the EU. Not transferred outside the EEA.",
  },
];

export const subjectRequests: SubjectRequest[] = [
  {
    id: "DSR-2026-0031",
    employeeId: "w-1001",
    type: "Access",
    raisedOn: "2026-07-20",
    status: "In review",
    owner: "HR operations (privacy)",
    nextAction: "Compile and review export pack",
    dueDate: "2026-08-14",
    statutoryDeadline: "30 days from 20 Jul 2026",
    scope: "All personal data held across HR, payroll and attendance.",
    timeline: [
      { id: "t1", at: "2026-07-20T09:00:00Z", actor: "Chanda Mwansa-Chileshe", event: "Request raised", after: "Submitted" },
      { id: "t2", at: "2026-07-20T09:02:00Z", actor: "System", event: "Identity confirmed from authenticated session" },
      { id: "t3", at: "2026-07-22T11:30:00Z", actor: "Thandiwe Banda", event: "Scope confirmed with requester", after: "In review" },
    ],
  },
  {
    id: "DSR-2026-0028",
    employeeId: "w-1008",
    type: "Correction",
    raisedOn: "2026-07-11",
    status: "Approved",
    owner: "Payroll",
    nextAction: "Effective from August pay run",
    dueDate: "2026-08-01",
    statutoryDeadline: "30 days from 11 Jul 2026",
    scope: "Bank account details following a bank merger.",
    timeline: [
      { id: "t1", at: "2026-07-11T12:00:00Z", actor: "Emmanuel Sakala", event: "Correction requested", after: "Submitted" },
      { id: "t2", at: "2026-07-14T09:00:00Z", actor: "Thandiwe Banda", event: "Verified against ID evidence", evidence: { label: "Verification record", href: "#" } },
      { id: "t3", at: "2026-07-14T09:05:00Z", actor: "Thandiwe Banda", event: "Approved", before: "In review", after: "Approved" },
    ],
  },
  {
    id: "DSR-2026-0024",
    employeeId: "w-1006",
    type: "Erasure",
    raisedOn: "2026-06-30",
    status: "Returned",
    owner: "HR operations (privacy)",
    nextAction: "Explain retained records to requester",
    dueDate: "2026-07-30",
    statutoryDeadline: "30 days from 30 Jun 2026",
    scope: "Requested deletion of all records held.",
    legalHold:
      "Payroll and tax records are under a statutory 7-year retention obligation and cannot be erased. Contact details and optional profile data can be.",
    timeline: [
      { id: "t1", at: "2026-06-30T14:00:00Z", actor: "Gift Zulu", event: "Erasure requested", after: "Submitted" },
      { id: "t2", at: "2026-07-03T10:15:00Z", actor: "Thandiwe Banda", event: "Partial refusal recorded", reason: "Statutory payroll retention applies", after: "Returned" },
    ],
  },
];

export const privacyApi = {
  purposes: async () => {
    await delay();
    return processingPurposes;
  },
  requests: async () => {
    await delay();
    return subjectRequests;
  },
  request: async (id: string) => {
    await delay();
    return subjectRequests.find((r) => r.id === id) ?? null;
  },
};
