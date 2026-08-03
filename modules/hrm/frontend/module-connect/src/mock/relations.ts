/**
 * Employee relations, discipline, safety and ethics (HRM-049/050/052/054).
 *
 * Product rules encoded here:
 *  - A case list must not disclose the allegation. Only enough to triage.
 *  - Language stays neutral before findings: "allegation", never "offence".
 *  - Subjects of serious allegations are anonymised by default.
 *  - Occupational health records an OUTCOME (fit / fit with adjustments), never
 *    a diagnosis.
 */
import type { TimelineEvent } from "./types";

const delay = (ms = 420) => new Promise((r) => setTimeout(r, ms));

export type CaseType =
  | "Grievance"
  | "Misconduct allegation"
  | "Bullying or harassment"
  | "Discrimination"
  | "Workplace dispute";

export type CaseStage =
  | "Intake"
  | "Conflict check"
  | "Investigation"
  | "Hearing"
  | "Findings"
  | "Appeal"
  | "Closed";

export interface RelationsCase {
  id: string;
  type: CaseType;
  /** Deliberately vague. The detail lives behind the conflict-of-interest gate. */
  summary: string;
  /** Anonymised where the allegation is serious. */
  subject: string;
  anonymised: boolean;
  raisedBy: string;
  stage: CaseStage;
  owner: string;
  nextAction: string;
  dueDate: string;
  opened: string;
  /** People who must not see this case. */
  conflicted: string[];
  representation?: string;
  allegations: string[];
  evidence: { label: string; kind: string; restricted: boolean }[];
  findings?: string;
  outcome?: string;
  appeal?: string;
  timeline: TimelineEvent[];
}

export type WarningLevel =
  | "Verbal caution"
  | "Written warning"
  | "Final written warning"
  | "Suspension";

export interface Warning {
  id: string;
  employee: string;
  anonymised: boolean;
  level: WarningLevel;
  reason: string;
  issued: string;
  /** Warnings lapse. A lapsed warning must not be relied on later. */
  expires: string;
  caseId?: string;
  appeal: "None lodged" | "Appeal lodged" | "Appeal upheld" | "Appeal dismissed";
  issuedBy: string;
}

export type IncidentKind = "Injury" | "Near miss" | "Hazard" | "Work-related illness";

export interface Incident {
  id: string;
  kind: IncidentKind;
  what: string;
  location: string;
  occurred: string;
  severity: "Minor" | "Moderate" | "Serious";
  reportable: boolean;
  reportableNote?: string;
  status: "Reported" | "Under investigation" | "Actions open" | "Closed";
  owner: string;
  nextAction: string;
  dueDate: string;
  correctiveActions: { action: string; owner: string; due: string; done: boolean }[];
  /** Outcome only — never a diagnosis. */
  fitnessOutcome?: string;
}

export type DeclarationType =
  | "Conflict of interest"
  | "Outside employment"
  | "Gift or hospitality"
  | "Related party";

export interface Declaration {
  id: string;
  employee: string;
  type: DeclarationType;
  what: string;
  declared: string;
  status: "Submitted" | "Under review" | "Accepted with mitigation" | "Accepted" | "Refused";
  reviewer: string;
  mitigation?: string;
  expires?: string;
}

export interface Campaign {
  id: string;
  name: string;
  population: string;
  due: string;
  completed: number;
  total: number;
}

export const relationsCases: RelationsCase[] = [
  {
    id: "ER-2026-0042",
    type: "Grievance",
    summary: "Concern raised about fairness of shift allocation",
    subject: "Employee A · Livingstone Works",
    anonymised: true,
    raisedBy: "Employee A",
    stage: "Investigation",
    owner: "Thandiwe Banda (HR operations)",
    nextAction: "Complete fact-finding interviews",
    dueDate: "2026-08-12",
    opened: "2026-07-15",
    conflicted: ["Mutale Kabwe"],
    representation: "The employee has asked to be accompanied by a colleague at any hearing.",
    allegations: [
      "That overtime shifts at Livingstone Works were allocated without following the published rota order.",
      "That a request to raise this informally with the line manager was not acknowledged.",
    ],
    evidence: [
      { label: "Published rota, June–July 2026", kind: "Document", restricted: false },
      { label: "Shift allocation export", kind: "System record", restricted: false },
      { label: "Witness account — name withheld", kind: "Statement", restricted: true },
    ],
    timeline: [
      { id: "t1", at: "2026-07-15T09:00:00Z", actor: "Employee A", event: "Grievance raised", after: "Intake" },
      { id: "t2", at: "2026-07-16T10:00:00Z", actor: "Thandiwe Banda", event: "Conflict check completed", reason: "Line manager conflicted — reassigned", after: "Investigation" },
    ],
  },
  {
    id: "ER-2026-0039",
    type: "Misconduct allegation",
    summary: "Alleged breach of the procurement conflict policy",
    subject: "Employee B · Lusaka HQ",
    anonymised: true,
    raisedBy: "Internal audit",
    stage: "Hearing",
    owner: "Thandiwe Banda (HR operations)",
    nextAction: "Hold hearing — employee notified 5 working days in advance",
    dueDate: "2026-08-06",
    opened: "2026-07-02",
    conflicted: ["Nalukui Simasiku"],
    representation: "Trade union representative confirmed.",
    allegations: [
      "That a supplier relationship was not declared before a purchase decision was taken.",
    ],
    evidence: [
      { label: "Procurement approval trail", kind: "System record", restricted: false },
      { label: "Declaration register extract", kind: "System record", restricted: false },
    ],
    timeline: [
      { id: "t1", at: "2026-07-02T08:30:00Z", actor: "Internal audit", event: "Referral received", after: "Intake" },
      { id: "t2", at: "2026-07-09T14:00:00Z", actor: "Thandiwe Banda", event: "Investigation report completed", after: "Hearing" },
      { id: "t3", at: "2026-07-28T09:00:00Z", actor: "Thandiwe Banda", event: "Hearing scheduled and employee notified" },
    ],
  },
  {
    id: "ER-2026-0031",
    type: "Workplace dispute",
    summary: "Disagreement over handover responsibilities between two teams",
    subject: "Logistics and Manufacturing, Livingstone Works",
    anonymised: false,
    raisedBy: "Mutale Kabwe",
    stage: "Closed",
    owner: "HR operations",
    nextAction: "Closed — mediated agreement in place",
    dueDate: "2026-06-30",
    opened: "2026-05-20",
    conflicted: [],
    allegations: ["That end-of-shift handover duties were not being consistently completed."],
    evidence: [{ label: "Mediation note", kind: "Document", restricted: false }],
    findings:
      "No individual fault found. The handover procedure was ambiguous about who closes the shift log when a shift overruns.",
    outcome:
      "Procedure clarified and reissued. Both teams briefed. No disciplinary action taken against any individual.",
    timeline: [
      { id: "t1", at: "2026-05-20T11:00:00Z", actor: "Mutale Kabwe", event: "Dispute referred", after: "Intake" },
      { id: "t2", at: "2026-06-18T15:00:00Z", actor: "HR operations", event: "Mediation held" },
      { id: "t3", at: "2026-06-30T16:00:00Z", actor: "HR operations", event: "Closed with agreed actions", after: "Closed" },
    ],
  },
];

export const warnings: Warning[] = [
  {
    id: "DSC-2026-0018",
    employee: "Employee C · Solwezi Yard",
    anonymised: true,
    level: "Written warning",
    reason: "Repeated late arrival without notification, after an informal discussion.",
    issued: "2026-05-12",
    expires: "2027-05-12",
    appeal: "None lodged",
    issuedBy: "Mutale Kabwe",
  },
  {
    id: "DSC-2025-0044",
    employee: "Employee D · Lusaka HQ",
    anonymised: true,
    level: "Verbal caution",
    reason: "Failure to complete the shift log on two occasions.",
    issued: "2025-09-03",
    expires: "2026-03-03",
    appeal: "None lodged",
    issuedBy: "Mutale Kabwe",
  },
  {
    id: "DSC-2026-0021",
    employee: "Employee B · Lusaka HQ",
    anonymised: true,
    level: "Final written warning",
    reason: "Pending outcome of ER-2026-0039. Not yet issued — shown as proposed only.",
    issued: "2026-08-06",
    expires: "2027-08-06",
    caseId: "ER-2026-0039",
    appeal: "None lodged",
    issuedBy: "Pending hearing outcome",
  },
];

export const incidents: Incident[] = [
  {
    id: "HS-2026-0077",
    kind: "Near miss",
    what: "A pallet shifted during unloading when a strap failed. Nobody was struck.",
    location: "Solwezi Yard — bay 3",
    occurred: "2026-07-24",
    severity: "Moderate",
    reportable: false,
    reportableNote: "Not reportable — no injury and no dangerous occurrence category met. Recorded because the failure mode could injure someone next time.",
    status: "Actions open",
    owner: "Mutale Kabwe (Manager)",
    nextAction: "Complete strap inspection across all bays",
    dueDate: "2026-08-08",
    correctiveActions: [
      { action: "Inspect and replace all load straps over 2 years old", owner: "Yard supervisor", due: "2026-08-08", done: false },
      { action: "Add strap condition to the pre-shift checklist", owner: "Mutale Kabwe", due: "2026-08-01", done: true },
    ],
  },
  {
    id: "HS-2026-0071",
    kind: "Injury",
    what: "Minor hand laceration while handling sheet metal. First aid given on site.",
    location: "Livingstone Works — fabrication",
    occurred: "2026-07-11",
    severity: "Minor",
    reportable: false,
    reportableNote: "Below the reporting threshold — no lost time beyond the day of the incident.",
    status: "Closed",
    owner: "HR operations",
    nextAction: "Closed — actions complete",
    dueDate: "2026-07-25",
    correctiveActions: [
      { action: "Reissue cut-resistant gloves to the fabrication team", owner: "Stores", due: "2026-07-18", done: true },
    ],
    fitnessOutcome: "Fit for normal duties from 14 July 2026. No adjustments required.",
  },
  {
    id: "HS-2026-0064",
    kind: "Hazard",
    what: "Standing water near the loading door after heavy rain, creating a slip risk.",
    location: "Kitwe Depot — loading door 2",
    occurred: "2026-06-28",
    severity: "Minor",
    reportable: false,
    status: "Under investigation",
    owner: "Facilities",
    nextAction: "Assess drainage and propose a permanent fix",
    dueDate: "2026-08-15",
    correctiveActions: [
      { action: "Temporary matting and signage in place", owner: "Depot supervisor", due: "2026-06-29", done: true },
      { action: "Drainage survey", owner: "Facilities", due: "2026-08-15", done: false },
    ],
  },
];

export const declarations: Declaration[] = [
  {
    id: "ETH-2026-0112",
    employee: "Nalukui Simasiku",
    type: "Outside employment",
    what: "Part-time bookkeeping for a local sports club, 4 hours a month, unpaid.",
    declared: "2026-03-04",
    status: "Accepted",
    reviewer: "Thandiwe Banda",
    expires: "2027-03-04",
  },
  {
    id: "ETH-2026-0108",
    employee: "Mutale Kabwe",
    type: "Conflict of interest",
    what: "A close family member is employed by a supplier that tenders for maintenance work.",
    declared: "2026-02-18",
    status: "Accepted with mitigation",
    reviewer: "Thandiwe Banda",
    mitigation:
      "Excluded from any evaluation, award or approval involving that supplier. Approvals reroute to the Operations Director.",
    expires: "2027-02-18",
  },
  {
    id: "ETH-2026-0121",
    employee: "Chanda Mwansa-Chileshe",
    type: "Gift or hospitality",
    what: "Lunch hosted by an equipment vendor during a site visit, approximately K35.",
    declared: "2026-06-11",
    status: "Accepted",
    reviewer: "Thandiwe Banda",
  },
  {
    id: "ETH-2026-0125",
    employee: "Gift Zulu",
    type: "Related party",
    what: "Holds a minority interest in a haulage firm used occasionally by the Solwezi yard.",
    declared: "2026-07-20",
    status: "Under review",
    reviewer: "Thandiwe Banda",
  },
];

export const campaigns: Campaign[] = [
  {
    id: "CMP-2026-ANNUAL",
    name: "Annual conflict-of-interest declaration 2026",
    population: "All employees and contractors",
    due: "2026-09-30",
    completed: 5,
    total: 8,
  },
  {
    id: "CMP-2026-PROC",
    name: "Procurement-facing roles — supplier interests",
    population: "Anyone with purchase approval authority",
    due: "2026-08-31",
    completed: 2,
    total: 3,
  },
];

export const relationsApi = {
  cases: async () => {
    await delay();
    return relationsCases;
  },
  caseItem: async (id: string) => {
    await delay();
    return relationsCases.find((c) => c.id === id) ?? null;
  },
  warnings: async () => {
    await delay();
    return warnings;
  },
  incidents: async () => {
    await delay();
    return incidents;
  },
  declarations: async () => {
    await delay();
    return declarations;
  },
  campaigns: async () => {
    await delay(300);
    return campaigns;
  },
};

export const TODAY = new Date("2026-07-29");
export const hasLapsed = (iso: string) => new Date(iso).getTime() < TODAY.getTime();
