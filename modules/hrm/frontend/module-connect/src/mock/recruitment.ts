/**
 * Recruitment mock data and read API. Self-contained: nothing here is shared
 * with the rest of the mock layer, and nothing is persisted.
 *
 * Candidates are external people. Only the data needed to run a fair selection
 * process is held, and every candidate record carries the lawful basis and the
 * retention date that governs it.
 */
import type { PolicyResult, RequestStatus, TimelineEvent } from "./types";

const delay = (ms = 420) => new Promise((r) => setTimeout(r, ms));

/* ------------------------------------------------------------------ types */

export type RequisitionReason = "Replacement" | "New position";

export interface RequisitionApprover {
  step: number;
  name: string;
  role: string;
  decision: "Approved" | "Awaiting decision" | "Returned" | "Rejected" | "Not started";
  decidedOn?: string;
  sla: string;
}

export interface EstablishmentCheck {
  approvedPosts: number;
  filledPosts: number;
  vacantPosts: number;
  requested: number;
  within: boolean;
  detail: string;
}

export interface Requisition {
  id: string;
  jobTitle: string;
  reason: RequisitionReason;
  replacementFor?: string;
  businessCase: string;
  hiringManager: string;
  recruiter: string;
  entityId: string;
  branch: string;
  department: string;
  grade: string;
  employmentType: "Permanent" | "Fixed term" | "Contractor" | "Intern" | "Part time";
  headcount: number;
  targetStartDate: string;
  raisedBy: string;
  raisedOn: string;
  establishment: EstablishmentCheck;
  budgetSource: string;
  annualCost: number;
  currency: string;
  status: RequestStatus;
  owner: string;
  nextAction: string;
  dueDate: string;
  approvers: RequisitionApprover[];
  policy: PolicyResult[];
  conflicts: string[];
  timeline: TimelineEvent[];
}

export type PostingStatus = "Draft" | "Internal" | "External" | "Closed";

export interface Vacancy {
  id: string;
  requisitionId: string;
  jobTitle: string;
  entityId: string;
  branch: string;
  department: string;
  grade: string;
  postingStatus: PostingStatus;
  channels: string[];
  openedOn: string;
  closingDate: string;
  daysOpen: number;
  applicants: number;
  shortlisted: number;
  interviewsBooked: number;
  owner: string;
  nextAction: string;
  dueDate: string;
}

export type CandidateStage =
  | "Applied"
  | "Screening"
  | "Shortlisted"
  | "Interview"
  | "Offer"
  | "Hired"
  | "Rejected"
  | "Withdrawn";

export type CandidateSource = "Careers portal" | "Referral" | "Agency";

export interface ScorecardCriterion {
  label: string;
  rating: number;
  note: string;
}

export interface Scorecard {
  id: string;
  stage: string;
  interviewer: string;
  interviewerRole: string;
  heldOn: string;
  overall: number;
  recommendation: "Advance" | "Hold" | "Do not advance";
  criteria: ScorecardCriterion[];
  comment: string;
}

export interface BackgroundCheck {
  id: string;
  label: string;
  provider: string;
  outcome: "Cleared" | "In progress" | "Not started" | "Flagged";
  updatedOn: string;
  note: string;
}

export interface CandidateOffer {
  grade: string;
  baseSalary: number;
  currency: string;
  allowances: string;
  proposedStart: string;
  probationMonths: number;
  contractType: string;
  expiresOn: string;
  approvedBudget: number;
  comparatorNote: string;
}

export interface CandidateConsent {
  lawfulBasis: string;
  obtainedOn: string;
  retainUntil: string;
  state: "Consent current" | "Consent expiring" | "Consent withdrawn";
  note: string;
}

export interface Candidate {
  id: string;
  reference: string;
  fullName: string;
  pronouncedAs?: string;
  vacancyId: string;
  appliedOn: string;
  stage: CandidateStage;
  source: CandidateSource;
  sourceDetail: string;
  status: RequestStatus;
  owner: string;
  nextAction: string;
  dueDate: string;
  location: string;
  rightToWork: string;
  noticePeriod: string;
  currentRole: string;
  salaryExpectation: string;
  consent: CandidateConsent;
  scorecards: Scorecard[];
  checks: BackgroundCheck[];
  offer?: CandidateOffer;
  policy: PolicyResult[];
  conflicts: string[];
  timeline: TimelineEvent[];
}

/* -------------------------------------------------------------- constants */

/** The ordered selection pipeline. Rejected and Withdrawn are exits, not steps. */
export const pipelineStages: CandidateStage[] = [
  "Applied",
  "Screening",
  "Shortlisted",
  "Interview",
  "Offer",
  "Hired",
];

export const requisitionReasons: RequisitionReason[] = ["Replacement", "New position"];

export const grades = ["G2", "G4", "G5", "G6", "G7", "G8", "G9"];

/** Approved establishment by department and branch, used by the requisition check. */
const establishmentBook: Record<string, { approvedPosts: number; filledPosts: number }> = {
  "Operations · Lusaka HQ": { approvedPosts: 12, filledPosts: 11 },
  "Operations · Ndola Plant": { approvedPosts: 9, filledPosts: 9 },
  "Operations · Kitwe Depot": { approvedPosts: 6, filledPosts: 5 },
  "Manufacturing · Livingstone Works": { approvedPosts: 24, filledPosts: 20 },
  "Logistics · Solwezi Yard": { approvedPosts: 8, filledPosts: 8 },
  "Logistics · Kitwe Depot": { approvedPosts: 7, filledPosts: 6 },
  "Finance · Chingola Office": { approvedPosts: 3, filledPosts: 3 },
  "People · Ndola Plant": { approvedPosts: 4, filledPosts: 4 },
};

export function checkEstablishment(input: {
  department: string;
  branch: string;
  headcount: number;
}): EstablishmentCheck {
  const book = establishmentBook[`${input.department} · ${input.branch}`] ?? {
    approvedPosts: 5,
    filledPosts: 5,
  };
  const vacantPosts = Math.max(book.approvedPosts - book.filledPosts, 0);
  const within = input.headcount <= vacantPosts;
  return {
    approvedPosts: book.approvedPosts,
    filledPosts: book.filledPosts,
    vacantPosts,
    requested: input.headcount,
    within,
    detail: within
      ? `${vacantPosts} of ${book.approvedPosts} approved posts are vacant in ${input.department} at ${input.branch}. Requesting ${input.headcount} stays within establishment.`
      : `All ${book.approvedPosts} approved posts in ${input.department} at ${input.branch} are filled (${vacantPosts} vacant). Requesting ${input.headcount} takes the team over establishment and needs a Finance Director decision.`,
  };
}

export function approversFor(input: {
  reason: RequisitionReason;
  within: boolean;
  entityId: string;
}): RequisitionApprover[] {
  const financeName =
    input.entityId === "ent-zm2"
      ? "Chileshe Mumba (Finance Manager, Copperbelt)"
      : input.entityId === "ent-zm3"
        ? "Namakau Lubinda (Finance Business Partner, Southern)"
        : "Mwila Chibesakunda (Finance Business Partner, Zambia)";
  const base: RequisitionApprover[] = [
    {
      step: 1,
      name: "Mutale Kabwe",
      role: "Hiring manager's line manager",
      decision: "Not started",
      sla: "2 working days",
    },
    {
      step: 2,
      name: financeName.split(" (")[0],
      role: financeName.replace(/^[^(]+\(|\)$/g, ""),
      decision: "Not started",
      sla: "3 working days",
    },
    {
      step: 3,
      name: "Thandiwe Banda",
      role: "HR operations — establishment and grading check",
      decision: "Not started",
      sla: "2 working days",
    },
  ];
  if (!input.within || input.reason === "New position") {
    base.push({
      step: 4,
      name: "Elena Ruiz",
      role: "Finance Director — over-establishment or new position",
      decision: "Not started",
      sla: "5 working days",
    });
  }
  return base;
}

export const money = (amount: number, currency: string) =>
  `${currency} ${amount.toLocaleString("en-GB", { maximumFractionDigits: 0 })}`;

/* ----------------------------------------------------------- requisitions */

const requisitions: Requisition[] = [
  {
    id: "REQ-2026-041",
    jobTitle: "Maintenance Planning Coordinator",
    reason: "Replacement",
    replacementFor: "Bart Hendriks — resigned, last day 21 August 2026",
    businessCase:
      "Direct replacement for a resigning planner. Without the post the Lusaka shutdown schedule loses its only planner and preventive maintenance slips into overtime.",
    hiringManager: "Mutale Kabwe (Operations Manager)",
    recruiter: "Namakau Lubinda (Talent Acquisition)",
    entityId: "ent-zm1",
    branch: "Lusaka HQ",
    department: "Operations",
    grade: "G7",
    employmentType: "Permanent",
    headcount: 1,
    targetStartDate: "2026-09-15",
    raisedBy: "Mutale Kabwe",
    raisedOn: "2026-07-22",
    establishment: checkEstablishment({ department: "Operations", branch: "Lusaka HQ", headcount: 1 }),
    budgetSource: "Operations opex — like-for-like replacement, no incremental cost",
    annualCost: 68400,
    currency: "ZMW",
    status: "In review",
    owner: "Mwila Chibesakunda (Finance Business Partner, Zambia)",
    nextAction: "Finance to confirm the budget line before the vacancy is advertised",
    dueDate: "2026-08-04",
    approvers: [
      { step: 1, name: "Sanne Verhoeven", role: "Operations Director", decision: "Approved", decidedOn: "2026-07-24", sla: "2 working days" },
      { step: 2, name: "Mwila Chibesakunda", role: "Finance Business Partner, Zambia", decision: "Awaiting decision", sla: "3 working days" },
      { step: 3, name: "Thandiwe Banda", role: "HR operations — establishment and grading check", decision: "Not started", sla: "2 working days" },
    ],
    policy: [
      { id: "p1", label: "Within establishment", outcome: "pass", detail: "1 of 12 approved Operations posts at Lusaka HQ is vacant from 22 August." },
      { id: "p2", label: "Grade benchmark", outcome: "pass", detail: "G7 matches the existing job profile; no re-grading requested." },
      { id: "p3", label: "Redeployment pool", outcome: "warn", detail: "Two employees at risk of redundancy at Ndola Plant hold planning experience and must be considered first." },
    ],
    conflicts: ["Redeployment window closes 5 August; external advertising before then breaches the redundancy agreement."],
    timeline: [
      { id: "t1", at: "2026-07-22T09:14:00Z", actor: "Mutale Kabwe", event: "Requisition raised", after: "Submitted" },
      { id: "t2", at: "2026-07-24T11:02:00Z", actor: "Sanne Verhoeven", event: "Approved at step 1", reason: "Replacement is essential to keep the shutdown plan staffed." },
      { id: "t3", at: "2026-07-24T11:05:00Z", actor: "System", event: "Routed to Finance", before: "Submitted", after: "In review" },
    ],
  },
  {
    id: "REQ-2026-042",
    jobTitle: "Payroll Analyst",
    reason: "New position",
    businessCase:
      "Two additional analysts to bring Zambian payroll processing in-house from the outsourced provider, ending the ZMW 380,000 annual bureau fee from January 2027.",
    hiringManager: "Chileshe Mumba (Finance Manager, Copperbelt)",
    recruiter: "Achieng Otieno (Talent Acquisition, EA)",
    entityId: "ent-zm2",
    branch: "Chingola Office",
    department: "Finance",
    grade: "G6",
    employmentType: "Permanent",
    headcount: 2,
    targetStartDate: "2026-10-01",
    raisedBy: "Chileshe Mumba",
    raisedOn: "2026-07-27",
    establishment: checkEstablishment({ department: "Finance", branch: "Chingola Office", headcount: 2 }),
    budgetSource: "2026 growth plan — incremental headcount, offset by bureau fee savings from 2027",
    annualCost: 4320000,
    currency: "ZMW",
    status: "Submitted",
    owner: "Thandiwe Banda (HR operations)",
    nextAction: "HR operations to complete the establishment check and route to approvers",
    dueDate: "2026-07-31",
    approvers: [
      { step: 1, name: "Chileshe Mumba", role: "Finance Manager, Copperbelt", decision: "Approved", decidedOn: "2026-07-27", sla: "2 working days" },
      { step: 2, name: "Thandiwe Banda", role: "HR operations — establishment and grading check", decision: "Awaiting decision", sla: "2 working days" },
      { step: 3, name: "Elena Ruiz", role: "Finance Director — new position", decision: "Not started", sla: "5 working days" },
    ],
    policy: [
      { id: "p1", label: "Within establishment", outcome: "fail", detail: "All 3 approved Finance posts at Chingola Office are filled. Two new posts need a Finance Director decision." },
      { id: "p2", label: "Business case with payback", outcome: "pass", detail: "Bureau fee saving of ZMW 380,000 a year is evidenced in the 2027 plan." },
      { id: "p3", label: "Grade benchmark", outcome: "pass", detail: "G6 is consistent with the existing Payroll Analyst profile in Zambia." },
    ],
    conflicts: [],
    timeline: [
      { id: "t1", at: "2026-07-27T07:40:00Z", actor: "Chileshe Mumba", event: "Requisition raised", after: "Submitted" },
      { id: "t2", at: "2026-07-27T07:41:00Z", actor: "System", event: "Establishment check flagged over establishment", after: "Finance Director approval added" },
    ],
  },
  {
    id: "REQ-2026-043",
    jobTitle: "Welding Technician",
    reason: "Replacement",
    replacementFor: "Two retirements and one internal move to Quality",
    businessCase:
      "Three welding posts vacated in the second quarter. The Livingstone line is running two shifts on agency cover at 1.7 times standard cost.",
    hiringManager: "Stefan Brandt (Production Manager)",
    recruiter: "Namakau Lubinda (Talent Acquisition)",
    entityId: "ent-zm3",
    branch: "Livingstone Works",
    department: "Manufacturing",
    grade: "G4",
    employmentType: "Permanent",
    headcount: 3,
    targetStartDate: "2026-09-01",
    raisedBy: "Stefan Brandt",
    raisedOn: "2026-06-30",
    establishment: checkEstablishment({ department: "Manufacturing", branch: "Livingstone Works", headcount: 3 }),
    budgetSource: "Manufacturing establishment — replaces agency cover, net saving of ZMW 2,400,000 a year",
    annualCost: 154200,
    currency: "ZMW",
    status: "Approved",
    owner: "Namakau Lubinda (Talent Acquisition)",
    nextAction: "Screen the 11 applications received since the last review",
    dueDate: "2026-08-02",
    approvers: [
      { step: 1, name: "Dieter Falk", role: "Works Director", decision: "Approved", decidedOn: "2026-07-01", sla: "2 working days" },
      { step: 2, name: "Namakau Lubinda", role: "Finance Business Partner, Southern", decision: "Approved", decidedOn: "2026-07-03", sla: "3 working days" },
      { step: 3, name: "Thandiwe Banda", role: "HR operations — establishment and grading check", decision: "Approved", decidedOn: "2026-07-06", sla: "2 working days" },
    ],
    policy: [
      { id: "p1", label: "Within establishment", outcome: "pass", detail: "4 of 24 approved Manufacturing posts at Livingstone Works are vacant." },
      { id: "p2", label: "Works council consultation", outcome: "pass", detail: "Consulted 2 July 2026; no objection recorded." },
      { id: "p3", label: "Agency cost comparison", outcome: "pass", detail: "Permanent cover is ZMW 2,400,000 a year cheaper than the current agency arrangement." },
    ],
    conflicts: [],
    timeline: [
      { id: "t1", at: "2026-06-30T08:05:00Z", actor: "Stefan Brandt", event: "Requisition raised", after: "Submitted" },
      { id: "t2", at: "2026-07-06T14:20:00Z", actor: "Thandiwe Banda", event: "Approved at final step", before: "In review", after: "Approved" },
      { id: "t3", at: "2026-07-06T15:00:00Z", actor: "Namakau Lubinda", event: "Vacancy VAC-2026-018 created" },
    ],
  },
  {
    id: "REQ-2026-044",
    jobTitle: "HR Operations Specialist",
    reason: "New position",
    businessCase:
      "A second specialist at Ndola to absorb the case volume created by the new shift pattern.",
    hiringManager: "Grace Sinyangwe (HR Operations Lead)",
    recruiter: "Namakau Lubinda (Talent Acquisition)",
    entityId: "ent-zm1",
    branch: "Ndola Plant",
    department: "People",
    grade: "G5",
    employmentType: "Permanent",
    headcount: 1,
    targetStartDate: "2026-10-01",
    raisedBy: "Grace Sinyangwe",
    raisedOn: "2026-07-15",
    establishment: checkEstablishment({ department: "People", branch: "Ndola Plant", headcount: 1 }),
    budgetSource: "People opex — not in the approved 2026 establishment",
    annualCost: 52800,
    currency: "ZMW",
    status: "Returned",
    owner: "Grace Sinyangwe (Requester)",
    nextAction: "Add the case-volume evidence the Finance Director asked for and resubmit",
    dueDate: "2026-07-30",
    approvers: [
      { step: 1, name: "Thandiwe Banda", role: "People Director", decision: "Approved", decidedOn: "2026-07-16", sla: "2 working days" },
      { step: 2, name: "Mwila Chibesakunda", role: "Finance Business Partner, Zambia", decision: "Returned", decidedOn: "2026-07-21", sla: "3 working days" },
      { step: 3, name: "Elena Ruiz", role: "Finance Director — new position", decision: "Not started", sla: "5 working days" },
    ],
    policy: [
      { id: "p1", label: "Within establishment", outcome: "fail", detail: "All 4 approved People posts at Ndola Plant are filled." },
      { id: "p2", label: "Evidence of demand", outcome: "warn", detail: "Case volumes were quoted but not attached; Finance returned the requisition for evidence." },
      { id: "p3", label: "Grade benchmark", outcome: "pass", detail: "G5 matches the equivalent Lusaka post." },
    ],
    conflicts: ["The 2026 establishment for People is frozen until the shift-pattern review concludes on 31 August."],
    timeline: [
      { id: "t1", at: "2026-07-15T10:00:00Z", actor: "Grace Sinyangwe", event: "Requisition raised", after: "Submitted" },
      { id: "t2", at: "2026-07-21T16:30:00Z", actor: "Mwila Chibesakunda", event: "Returned for more information", reason: "Attach the case volumes for the last two quarters and the projected volume after the shift change.", before: "In review", after: "Returned" },
    ],
  },
  {
    id: "REQ-2026-045",
    jobTitle: "Depot Supervisor",
    reason: "Replacement",
    replacementFor: "Internal promotion of the previous supervisor to Logistics Lead",
    businessCase:
      "Kitwe Depot cannot run the evening shift without a supervisor on site; the post is currently covered by an acting arrangement paid at an allowance.",
    hiringManager: "Emmanuel Sakala (Depot Supervisor)",
    recruiter: "Namakau Lubinda (Talent Acquisition)",
    entityId: "ent-zm1",
    branch: "Kitwe Depot",
    department: "Logistics",
    grade: "G6",
    employmentType: "Permanent",
    headcount: 1,
    targetStartDate: "2026-09-01",
    raisedBy: "Emmanuel Sakala",
    raisedOn: "2026-07-10",
    establishment: checkEstablishment({ department: "Logistics", branch: "Kitwe Depot", headcount: 1 }),
    budgetSource: "Logistics establishment — replaces an acting-up allowance",
    annualCost: 58900,
    currency: "ZMW",
    status: "Approved",
    owner: "Namakau Lubinda (Talent Acquisition)",
    nextAction: "Close the internal window and decide whether to advertise externally",
    dueDate: "2026-08-03",
    approvers: [
      { step: 1, name: "Sanne Verhoeven", role: "Operations Director", decision: "Approved", decidedOn: "2026-07-13", sla: "2 working days" },
      { step: 2, name: "Mwila Chibesakunda", role: "Finance Business Partner, Zambia", decision: "Approved", decidedOn: "2026-07-16", sla: "3 working days" },
      { step: 3, name: "Thandiwe Banda", role: "HR operations — establishment and grading check", decision: "Approved", decidedOn: "2026-07-17", sla: "2 working days" },
    ],
    policy: [
      { id: "p1", label: "Within establishment", outcome: "pass", detail: "1 of 7 approved Logistics posts at Kitwe Depot is vacant." },
      { id: "p2", label: "Internal-first rule", outcome: "pass", detail: "Advertised internally for ten working days from 20 July, as the policy requires." },
      { id: "p3", label: "Acting-up cost", outcome: "pass", detail: "Filling the post removes an allowance of ZMW 16,000 a month." },
    ],
    conflicts: [],
    timeline: [
      { id: "t1", at: "2026-07-10T08:30:00Z", actor: "Emmanuel Sakala", event: "Requisition raised", after: "Submitted" },
      { id: "t2", at: "2026-07-17T09:15:00Z", actor: "Thandiwe Banda", event: "Approved at final step", before: "In review", after: "Approved" },
      { id: "t3", at: "2026-07-20T08:00:00Z", actor: "Namakau Lubinda", event: "Vacancy VAC-2026-019 posted internally" },
    ],
  },
  {
    id: "REQ-2026-046",
    jobTitle: "Process Engineering Graduate",
    reason: "New position",
    businessCase:
      "Two graduate posts to rebuild the process engineering pipeline after three senior leavers in 2025.",
    hiringManager: "Stefan Brandt (Production Manager)",
    recruiter: "Namakau Lubinda (Talent Acquisition)",
    entityId: "ent-zm3",
    branch: "Livingstone Works",
    department: "Manufacturing",
    grade: "G2",
    employmentType: "Fixed term",
    headcount: 2,
    targetStartDate: "2027-01-05",
    raisedBy: "Stefan Brandt",
    raisedOn: "2026-07-28",
    establishment: {
      approvedPosts: 24,
      filledPosts: 20,
      vacantPosts: 4,
      requested: 2,
      within: false,
      detail:
        "Of the 4 vacant Manufacturing posts at Livingstone Works, 3 are reserved for REQ-2026-043. Graduate posts are additional to the establishment, so this takes the team over.",
    },
    budgetSource: "Graduate scheme budget — 2027 plan, not yet authorised",
    annualCost: 71000,
    currency: "ZMW",
    status: "Draft",
    owner: "Stefan Brandt (Hiring manager)",
    nextAction: "Finish the justification and submit for approval",
    dueDate: "2026-08-11",
    approvers: [
      { step: 1, name: "Dieter Falk", role: "Works Director", decision: "Not started", sla: "2 working days" },
      { step: 2, name: "Namakau Lubinda", role: "Finance Business Partner, Southern", decision: "Not started", sla: "3 working days" },
      { step: 3, name: "Thandiwe Banda", role: "HR operations — establishment and grading check", decision: "Not started", sla: "2 working days" },
      { step: 4, name: "Elena Ruiz", role: "Finance Director — new position", decision: "Not started", sla: "5 working days" },
    ],
    policy: [
      { id: "p1", label: "Within establishment", outcome: "fail", detail: "4 of 24 approved Manufacturing posts at Livingstone Works are vacant, but 3 are reserved for REQ-2026-043 and graduate posts are additional. A Finance Director decision is needed." },
      { id: "p2", label: "Budget authorised", outcome: "warn", detail: "The 2027 graduate budget is planned but not yet authorised. Approval cannot complete before 1 October." },
    ],
    conflicts: [],
    timeline: [{ id: "t1", at: "2026-07-28T13:10:00Z", actor: "Stefan Brandt", event: "Draft created", after: "Draft" }],
  },
  {
    id: "REQ-2026-040",
    jobTitle: "Yard Logistics Coordinator",
    reason: "Replacement",
    replacementFor: "Contractor engagement ended 30 June 2026",
    businessCase: "Convert the Solwezi yard contractor arrangement into a permanent coordinator post.",
    hiringManager: "Chileshe Mumba (Finance Manager, Copperbelt)",
    recruiter: "Achieng Otieno (Talent Acquisition, EA)",
    entityId: "ent-zm2",
    branch: "Solwezi Yard",
    department: "Logistics",
    grade: "G5",
    employmentType: "Permanent",
    headcount: 1,
    targetStartDate: "2026-09-01",
    raisedBy: "Chileshe Mumba",
    raisedOn: "2026-07-02",
    establishment: checkEstablishment({ department: "Logistics", branch: "Solwezi Yard", headcount: 1 }),
    budgetSource: "Logistics opex — contractor conversion",
    annualCost: 2760000,
    currency: "ZMW",
    status: "Rejected",
    owner: "Chileshe Mumba (Finance Manager, Copperbelt)",
    nextAction: "No further action — raise a fresh requisition after the yard volume review in October",
    dueDate: "2026-07-24",
    approvers: [
      { step: 1, name: "Chileshe Mumba", role: "Finance Manager, Copperbelt", decision: "Approved", decidedOn: "2026-07-03", sla: "2 working days" },
      { step: 2, name: "Elena Ruiz", role: "Finance Director — over establishment", decision: "Rejected", decidedOn: "2026-07-24", sla: "5 working days" },
    ],
    policy: [
      { id: "p1", label: "Within establishment", outcome: "fail", detail: "All 8 approved Logistics posts at Solwezi Yard are filled." },
      { id: "p2", label: "Volume evidence", outcome: "fail", detail: "Yard throughput fell 18 per cent year on year; the case for a permanent post was not made out." },
    ],
    conflicts: [],
    timeline: [
      { id: "t1", at: "2026-07-02T06:50:00Z", actor: "Chileshe Mumba", event: "Requisition raised", after: "Submitted" },
      { id: "t2", at: "2026-07-24T12:00:00Z", actor: "Elena Ruiz", event: "Rejected", reason: "Throughput does not support a permanent post. Review again after the October volume forecast.", before: "In review", after: "Rejected" },
    ],
  },
  {
    id: "REQ-2026-039",
    jobTitle: "Maintenance Technician",
    reason: "Replacement",
    replacementFor: "Long-term absence converted to ill-health retirement",
    businessCase: "Restores the second technician on the Ndola preventive maintenance rota.",
    hiringManager: "Mutale Kabwe (Operations Manager)",
    recruiter: "Namakau Lubinda (Talent Acquisition)",
    entityId: "ent-zm1",
    branch: "Ndola Plant",
    department: "Operations",
    grade: "G5",
    employmentType: "Permanent",
    headcount: 1,
    targetStartDate: "2026-10-01",
    raisedBy: "Mutale Kabwe",
    raisedOn: "2026-06-24",
    establishment: { approvedPosts: 9, filledPosts: 8, vacantPosts: 1, requested: 1, within: true, detail: "1 of 9 approved Operations posts at Ndola Plant is vacant." },
    budgetSource: "Operations opex — like-for-like replacement",
    annualCost: 49700,
    currency: "ZMW",
    status: "Approved",
    owner: "Namakau Lubinda (Talent Acquisition)",
    nextAction: "Finalise the advertisement text and open the internal window",
    dueDate: "2026-08-05",
    approvers: [
      { step: 1, name: "Sanne Verhoeven", role: "Operations Director", decision: "Approved", decidedOn: "2026-06-25", sla: "2 working days" },
      { step: 2, name: "Mwila Chibesakunda", role: "Finance Business Partner, Zambia", decision: "Approved", decidedOn: "2026-06-29", sla: "3 working days" },
      { step: 3, name: "Thandiwe Banda", role: "HR operations — establishment and grading check", decision: "Approved", decidedOn: "2026-07-01", sla: "2 working days" },
    ],
    policy: [
      { id: "p1", label: "Within establishment", outcome: "pass", detail: "1 of 9 approved Operations posts at Ndola Plant is vacant." },
      { id: "p2", label: "Ill-health retirement completed", outcome: "pass", detail: "Occupational health process concluded 12 June 2026." },
    ],
    conflicts: [],
    timeline: [
      { id: "t1", at: "2026-06-24T09:00:00Z", actor: "Mutale Kabwe", event: "Requisition raised", after: "Submitted" },
      { id: "t2", at: "2026-07-01T10:30:00Z", actor: "Thandiwe Banda", event: "Approved at final step", before: "In review", after: "Approved" },
    ],
  },
  {
    id: "REQ-2026-037",
    jobTitle: "Field Service Engineer",
    reason: "Replacement",
    replacementFor: "Resignation, last day 12 June 2026",
    businessCase: "Restores contracted response times for the Benelux service contracts.",
    hiringManager: "Sanne Verhoeven (Operations Director)",
    recruiter: "Namakau Lubinda (Talent Acquisition)",
    entityId: "ent-zm1",
    branch: "Lusaka HQ",
    department: "Operations",
    grade: "G6",
    employmentType: "Permanent",
    headcount: 1,
    targetStartDate: "2026-09-01",
    raisedBy: "Sanne Verhoeven",
    raisedOn: "2026-06-15",
    establishment: { approvedPosts: 12, filledPosts: 11, vacantPosts: 1, requested: 1, within: true, detail: "1 of 12 approved Operations posts at Lusaka HQ is vacant." },
    budgetSource: "Operations opex — like-for-like replacement",
    annualCost: 61200,
    currency: "ZMW",
    status: "Approved",
    owner: "Namakau Lubinda (Talent Acquisition)",
    nextAction: "Confirm the offer decision for the candidate at offer stage",
    dueDate: "2026-08-01",
    approvers: [
      { step: 1, name: "Sanne Verhoeven", role: "Operations Director", decision: "Approved", decidedOn: "2026-06-16", sla: "2 working days" },
      { step: 2, name: "Mwila Chibesakunda", role: "Finance Business Partner, Zambia", decision: "Approved", decidedOn: "2026-06-18", sla: "3 working days" },
      { step: 3, name: "Thandiwe Banda", role: "HR operations — establishment and grading check", decision: "Approved", decidedOn: "2026-06-22", sla: "2 working days" },
    ],
    policy: [
      { id: "p1", label: "Within establishment", outcome: "pass", detail: "1 of 12 approved Operations posts at Lusaka HQ is vacant." },
      { id: "p2", label: "Driving licence requirement", outcome: "pass", detail: "Category B licence recorded as an essential requirement in the advertisement." },
    ],
    conflicts: [],
    timeline: [
      { id: "t1", at: "2026-06-15T08:00:00Z", actor: "Sanne Verhoeven", event: "Requisition raised", after: "Submitted" },
      { id: "t2", at: "2026-06-22T09:40:00Z", actor: "Thandiwe Banda", event: "Approved at final step", before: "In review", after: "Approved" },
      { id: "t3", at: "2026-06-29T08:00:00Z", actor: "Namakau Lubinda", event: "Vacancy VAC-2026-017 advertised externally" },
    ],
  },
  {
    id: "REQ-2026-033",
    jobTitle: "Warehouse Team Leader",
    reason: "New position",
    businessCase: "Second team leader for the Chingola warehouse night shift introduced in April 2026.",
    hiringManager: "Chileshe Mumba (Finance Manager, Copperbelt)",
    recruiter: "Achieng Otieno (Talent Acquisition, EA)",
    entityId: "ent-zm2",
    branch: "Chingola Office",
    department: "Logistics",
    grade: "G5",
    employmentType: "Permanent",
    headcount: 1,
    targetStartDate: "2026-07-01",
    raisedBy: "Chileshe Mumba",
    raisedOn: "2026-05-06",
    establishment: { approvedPosts: 6, filledPosts: 5, vacantPosts: 1, requested: 1, within: true, detail: "A new post was added to the Chingola establishment for the night shift." },
    budgetSource: "2026 growth plan — approved incremental headcount",
    annualCost: 1980000,
    currency: "ZMW",
    status: "Approved",
    owner: "Achieng Otieno (Talent Acquisition, EA)",
    nextAction: "No action — post filled, requisition closes when the hire starts",
    dueDate: "2026-08-03",
    approvers: [
      { step: 1, name: "Chileshe Mumba", role: "Finance Manager, Copperbelt", decision: "Approved", decidedOn: "2026-05-07", sla: "2 working days" },
      { step: 2, name: "Thandiwe Banda", role: "HR operations — establishment and grading check", decision: "Approved", decidedOn: "2026-05-11", sla: "2 working days" },
      { step: 3, name: "Elena Ruiz", role: "Finance Director — new position", decision: "Approved", decidedOn: "2026-05-15", sla: "5 working days" },
    ],
    policy: [
      { id: "p1", label: "Within establishment", outcome: "pass", detail: "Establishment increased by one post for the night shift, approved 15 May 2026." },
      { id: "p2", label: "Night-shift allowance budgeted", outcome: "pass", detail: "Shift allowance of ZMW 3,600 a month included in the annual cost." },
    ],
    conflicts: [],
    timeline: [
      { id: "t1", at: "2026-05-06T06:30:00Z", actor: "Chileshe Mumba", event: "Requisition raised", after: "Submitted" },
      { id: "t2", at: "2026-05-15T14:00:00Z", actor: "Elena Ruiz", event: "Approved at final step", before: "In review", after: "Approved" },
      { id: "t3", at: "2026-07-14T10:00:00Z", actor: "Achieng Otieno", event: "Offer accepted — post filled" },
    ],
  },
];

/* --------------------------------------------------------------- vacancies */

const vacancies: Vacancy[] = [
  {
    id: "VAC-2026-018",
    requisitionId: "REQ-2026-043",
    jobTitle: "Welding Technician",
    entityId: "ent-zm3",
    branch: "Livingstone Works",
    department: "Manufacturing",
    grade: "G4",
    postingStatus: "External",
    channels: ["Careers portal", "Handwerk job board", "Two agency partners"],
    openedOn: "2026-07-06",
    closingDate: "2026-08-09",
    daysOpen: 23,
    applicants: 34,
    shortlisted: 6,
    interviewsBooked: 4,
    owner: "Namakau Lubinda (Talent Acquisition)",
    nextAction: "Screen the 11 applications received since the last review",
    dueDate: "2026-08-02",
  },
  {
    id: "VAC-2026-017",
    requisitionId: "REQ-2026-037",
    jobTitle: "Field Service Engineer",
    entityId: "ent-zm1",
    branch: "Lusaka HQ",
    department: "Operations",
    grade: "G6",
    postingStatus: "External",
    channels: ["Careers portal", "LinkedIn", "Employee referral scheme"],
    openedOn: "2026-06-29",
    closingDate: "2026-08-14",
    daysOpen: 30,
    applicants: 27,
    shortlisted: 4,
    interviewsBooked: 3,
    owner: "Namakau Lubinda (Talent Acquisition)",
    nextAction: "Confirm the offer decision for Yasmin El Amrani before the offer expires",
    dueDate: "2026-08-01",
  },
  {
    id: "VAC-2026-019",
    requisitionId: "REQ-2026-045",
    jobTitle: "Depot Supervisor",
    entityId: "ent-zm1",
    branch: "Kitwe Depot",
    department: "Logistics",
    grade: "G6",
    postingStatus: "Internal",
    channels: ["Internal noticeboard", "Employee app"],
    openedOn: "2026-07-20",
    closingDate: "2026-07-31",
    daysOpen: 9,
    applicants: 5,
    shortlisted: 2,
    interviewsBooked: 2,
    owner: "Namakau Lubinda (Talent Acquisition)",
    nextAction: "Close the internal window and decide whether to advertise externally",
    dueDate: "2026-08-03",
  },
  {
    id: "VAC-2026-020",
    requisitionId: "REQ-2026-039",
    jobTitle: "Maintenance Technician",
    entityId: "ent-zm1",
    branch: "Ndola Plant",
    department: "Operations",
    grade: "G5",
    postingStatus: "Draft",
    channels: [],
    openedOn: "2026-07-27",
    closingDate: "2026-08-28",
    daysOpen: 2,
    applicants: 0,
    shortlisted: 0,
    interviewsBooked: 0,
    owner: "Namakau Lubinda (Talent Acquisition)",
    nextAction: "Finalise the advertisement text and open the internal window",
    dueDate: "2026-08-05",
  },
  {
    id: "VAC-2026-016",
    requisitionId: "REQ-2026-033",
    jobTitle: "Warehouse Team Leader",
    entityId: "ent-zm2",
    branch: "Chingola Office",
    department: "Logistics",
    grade: "G5",
    postingStatus: "Closed",
    channels: ["Careers portal", "BrighterMonday", "Employee referral scheme"],
    openedOn: "2026-05-18",
    closingDate: "2026-06-30",
    daysOpen: 43,
    applicants: 61,
    shortlisted: 7,
    interviewsBooked: 5,
    owner: "Achieng Otieno (Talent Acquisition, EA)",
    nextAction: "Complete pre-employment checks before the agreed start date",
    dueDate: "2026-08-14",
  },
];

/* -------------------------------------------------------------- candidates */

const consentCurrent = (obtainedOn: string, retainUntil: string): CandidateConsent => ({
  lawfulBasis: "Consent given at application, plus legitimate interest in defending a selection decision",
  obtainedOn,
  retainUntil,
  state: "Consent current",
  note: "Only the data needed to run a fair selection process is held. The record is deleted automatically on the retention date unless the candidate is hired.",
});

const candidates: Candidate[] = [
  {
    id: "c-1051",
    reference: "CAND-2026-1051",
    fullName: "Yasmin El Amrani",
    vacancyId: "VAC-2026-017",
    appliedOn: "2026-07-02",
    stage: "Offer",
    source: "Agency",
    sourceDetail: "Delta Technical Recruitment — introduced 2 July 2026, 15 per cent placement fee",
    status: "In review",
    owner: "Namakau Lubinda (Talent Acquisition)",
    nextAction: "Hiring manager to record the offer decision before the offer expires",
    dueDate: "2026-08-01",
    location: "Ndola, Zambia",
    rightToWork: "Verified — EU national, document check completed 21 July 2026",
    noticePeriod: "One month",
    currentRole: "Field Service Engineer at an industrial refrigeration firm",
    salaryExpectation: "ZMW 1,550,000 – 66,000",
    consent: consentCurrent("2026-07-02", "2027-01-02"),
    scorecards: [
      {
        id: "sc-1",
        stage: "Technical interview",
        interviewer: "Mutale Kabwe",
        interviewerRole: "Operations Manager",
        heldOn: "2026-07-17",
        overall: 4,
        recommendation: "Advance",
        criteria: [
          { label: "Fault diagnosis", rating: 5, note: "Worked through the pump failure scenario methodically and asked for the maintenance history first." },
          { label: "Customer handling", rating: 4, note: "Clear examples of de-escalating a contract dispute on site." },
          { label: "Safety awareness", rating: 4, note: "Named the correct isolation procedure without prompting." },
          { label: "Documentation discipline", rating: 3, note: "Admits job reports are usually written up at the end of the week rather than on the day." },
        ],
        comment: "Strong practical engineer. The documentation habit needs to be set straight in the first month.",
      },
      {
        id: "sc-2",
        stage: "Panel interview",
        interviewer: "Sanne Verhoeven",
        interviewerRole: "Operations Director",
        heldOn: "2026-07-23",
        overall: 4,
        recommendation: "Advance",
        criteria: [
          { label: "Working without supervision", rating: 5, note: "Has run a standalone territory for three years." },
          { label: "Team contribution", rating: 4, note: "Mentored two apprentices; described the handover process well." },
          { label: "Availability for call-out rota", rating: 4, note: "Willing to join the rota after the probation period." },
        ],
        comment: "Recommend an offer at the mid-point of the G6 range.",
      },
      {
        id: "sc-3",
        stage: "Panel interview",
        interviewer: "Thandiwe Banda",
        interviewerRole: "HR Operations Specialist",
        heldOn: "2026-07-23",
        overall: 3,
        recommendation: "Hold",
        criteria: [
          { label: "Values alignment", rating: 4, note: "Answers on safety culture were consistent and specific." },
          { label: "Reason for leaving", rating: 3, note: "Cites travel distance; worth confirming the commute is sustainable." },
        ],
        comment: "No concerns, but I would confirm the commute expectation in writing with the offer.",
      },
    ],
    checks: [
      { id: "bc-1", label: "Right to work", provider: "In-house document check", outcome: "Cleared", updatedOn: "2026-07-21", note: "Passport and residence permit sighted in person." },
      { id: "bc-2", label: "Employment reference — current employer", provider: "Requested directly", outcome: "In progress", updatedOn: "2026-07-27", note: "Reference requested 24 July; chased once. Not to be taken up further without the candidate's agreement." },
      { id: "bc-3", label: "Employment reference — previous employer", provider: "Requested directly", outcome: "Cleared", updatedOn: "2026-07-20", note: "Dates and role confirmed; no concerns raised." },
      { id: "bc-4", label: "Driving licence check", provider: "In-house document check", outcome: "Cleared", updatedOn: "2026-07-21", note: "Category B licence, no endorsements." },
    ],
    offer: {
      grade: "G6",
      baseSalary: 63500,
      currency: "ZMW",
      allowances: "Call-out allowance from the end of probation; company van and fuel card",
      proposedStart: "2026-09-01",
      probationMonths: 2,
      contractType: "Permanent, full time (40 hours)",
      expiresOn: "2026-08-05",
      approvedBudget: 61200,
      comparatorNote: "ZMW 57,500 above the approved requisition cost. The G6 range mid-point is ZMW 1,575,000; two employees in the same role sit at ZMW 1,525,000 and ZMW 1,620,000.",
    },
    policy: [
      { id: "p1", label: "Within the approved grade range", outcome: "pass", detail: "ZMW 1,590,000 sits at the mid-point of the G6 range (ZMW 1,400,000 – ZMW 1,750,000)." },
      { id: "p2", label: "Within requisition budget", outcome: "warn", detail: "ZMW 57,500 above the annual cost approved on REQ-2026-037. Finance Business Partner sign-off is needed." },
      { id: "p3", label: "Pay equity check", outcome: "pass", detail: "No unexplained gap against the two existing Field Service Engineers at Lusaka HQ." },
      { id: "p4", label: "Pre-employment checks complete", outcome: "warn", detail: "One employment reference is still outstanding. The offer must be made conditional on it." },
      { id: "p5", label: "Right to work verified", outcome: "pass", detail: "Verified 21 July 2026 before any offer was discussed." },
    ],
    conflicts: [
      "The agency introduction fee of 15 per cent is not included in the approved requisition cost.",
      "The proposed start date is two weeks before the approved requisition start date of 1 September; confirm the van is available.",
    ],
    timeline: [
      { id: "t1", at: "2026-07-02T10:12:00Z", actor: "Delta Technical Recruitment", event: "Application received", after: "Applied" },
      { id: "t2", at: "2026-07-08T09:00:00Z", actor: "Namakau Lubinda", event: "Screening call completed", before: "Applied", after: "Screening" },
      { id: "t3", at: "2026-07-11T15:30:00Z", actor: "Mutale Kabwe", event: "Shortlisted", before: "Screening", after: "Shortlisted" },
      { id: "t4", at: "2026-07-17T13:00:00Z", actor: "Mutale Kabwe", event: "Technical interview held", before: "Shortlisted", after: "Interview", evidence: { label: "Scorecard — technical interview", href: "#" } },
      { id: "t5", at: "2026-07-23T10:00:00Z", actor: "Panel", event: "Panel interview held", evidence: { label: "Scorecards — panel", href: "#" } },
      { id: "t6", at: "2026-07-27T16:40:00Z", actor: "Namakau Lubinda", event: "Offer prepared for decision", before: "Interview", after: "Offer" },
    ],
  },
  {
    id: "c-1044",
    reference: "CAND-2026-1044",
    fullName: "Sander Bosch",
    vacancyId: "VAC-2026-017",
    appliedOn: "2026-07-04",
    stage: "Interview",
    source: "Careers portal",
    sourceDetail: "Applied directly through the careers portal",
    status: "In review",
    owner: "Namakau Lubinda (Talent Acquisition)",
    nextAction: "Hold the second interview and record both scorecards",
    dueDate: "2026-08-04",
    location: "Lusaka, Zambia",
    rightToWork: "Verified — Zambian national, document check completed 18 July 2026",
    noticePeriod: "Two months",
    currentRole: "Maintenance Engineer at a port services company",
    salaryExpectation: "ZMW 1,450,000",
    consent: consentCurrent("2026-07-04", "2027-01-04"),
    scorecards: [
      {
        id: "sc-1",
        stage: "Technical interview",
        interviewer: "Mutale Kabwe",
        interviewerRole: "Operations Manager",
        heldOn: "2026-07-18",
        overall: 3,
        recommendation: "Advance",
        criteria: [
          { label: "Fault diagnosis", rating: 3, note: "Reached the right answer but needed prompting on the isolation sequence." },
          { label: "Customer handling", rating: 4, note: "Comfortable explaining technical findings to non-technical clients." },
          { label: "Safety awareness", rating: 4, note: "Sound understanding of permit-to-work." },
        ],
        comment: "Worth a second interview. Would need structured support in the first six months.",
      },
    ],
    checks: [
      { id: "bc-1", label: "Right to work", provider: "In-house document check", outcome: "Cleared", updatedOn: "2026-07-18", note: "Passport sighted in person." },
      { id: "bc-2", label: "Employment reference — current employer", provider: "Requested directly", outcome: "Not started", updatedOn: "2026-07-18", note: "Not to be taken up until the candidate agrees, per the policy on references." },
    ],
    policy: [
      { id: "p1", label: "Right to work verified", outcome: "pass", detail: "Verified 18 July 2026." },
      { id: "p2", label: "Two independent scorecards", outcome: "warn", detail: "Only one scorecard recorded. A second is required before a shortlist or offer decision." },
      { id: "p3", label: "Salary expectation within range", outcome: "pass", detail: "ZMW 1,450,000 sits inside the G6 range." },
    ],
    conflicts: [],
    timeline: [
      { id: "t1", at: "2026-07-04T18:22:00Z", actor: "Careers portal", event: "Application received", after: "Applied" },
      { id: "t2", at: "2026-07-09T11:00:00Z", actor: "Namakau Lubinda", event: "Screening call completed", before: "Applied", after: "Screening" },
      { id: "t3", at: "2026-07-14T09:30:00Z", actor: "Mutale Kabwe", event: "Shortlisted", before: "Screening", after: "Shortlisted" },
      { id: "t4", at: "2026-07-18T14:00:00Z", actor: "Mutale Kabwe", event: "Technical interview held", before: "Shortlisted", after: "Interview" },
    ],
  },
  {
    id: "c-1063",
    reference: "CAND-2026-1063",
    fullName: "Lukas Brenner",
    vacancyId: "VAC-2026-018",
    appliedOn: "2026-07-11",
    stage: "Shortlisted",
    source: "Careers portal",
    sourceDetail: "Applied directly through the careers portal",
    status: "In review",
    owner: "Stefan Brandt (Production Manager)",
    nextAction: "Book the practical welding assessment at Livingstone Works",
    dueDate: "2026-08-06",
    location: "Livingstone, Zambia",
    rightToWork: "Verified — Zambian national, document check completed 22 July 2026",
    noticePeriod: "Four weeks to the end of a month",
    currentRole: "Welder at a shipyard subcontractor",
    salaryExpectation: "ZMW 1,150,000",
    consent: consentCurrent("2026-07-11", "2027-01-11"),
    scorecards: [
      {
        id: "sc-1",
        stage: "Screening call",
        interviewer: "Namakau Lubinda",
        interviewerRole: "Talent Acquisition",
        heldOn: "2026-07-15",
        overall: 4,
        recommendation: "Advance",
        criteria: [
          { label: "Certification", rating: 5, note: "Holds a current EN ISO 9606-1 certificate valid to March 2027." },
          { label: "Shift availability", rating: 4, note: "Available for the two-shift pattern from September." },
        ],
        comment: "Certification checked against the register. Send to the practical assessment.",
      },
    ],
    checks: [
      { id: "bc-1", label: "Right to work", provider: "In-house document check", outcome: "Cleared", updatedOn: "2026-07-22", note: "Identity card sighted in person." },
      { id: "bc-2", label: "Welding certification check", provider: "Certification register", outcome: "Cleared", updatedOn: "2026-07-15", note: "EN ISO 9606-1 confirmed valid to March 2027." },
      { id: "bc-3", label: "Employment reference — current employer", provider: "Requested directly", outcome: "Not started", updatedOn: "2026-07-15", note: "Held until after the practical assessment, at the candidate's request." },
    ],
    policy: [
      { id: "p1", label: "Right to work verified", outcome: "pass", detail: "Verified 22 July 2026." },
      { id: "p2", label: "Trade certification current", outcome: "pass", detail: "EN ISO 9606-1 valid to March 2027." },
      { id: "p3", label: "Practical assessment held", outcome: "warn", detail: "Not yet booked. The works council agreement requires it before any offer." },
    ],
    conflicts: [],
    timeline: [
      { id: "t1", at: "2026-07-11T08:05:00Z", actor: "Careers portal", event: "Application received", after: "Applied" },
      { id: "t2", at: "2026-07-15T10:00:00Z", actor: "Namakau Lubinda", event: "Screening call completed", before: "Applied", after: "Screening" },
      { id: "t3", at: "2026-07-21T09:00:00Z", actor: "Stefan Brandt", event: "Shortlisted", before: "Screening", after: "Shortlisted" },
    ],
  },
  {
    id: "c-1067",
    reference: "CAND-2026-1067",
    fullName: "Piotr Zawadzki",
    vacancyId: "VAC-2026-018",
    appliedOn: "2026-07-19",
    stage: "Screening",
    source: "Agency",
    sourceDetail: "Nordwerk Personal — introduced 19 July 2026, 12 per cent placement fee",
    status: "Submitted",
    owner: "Namakau Lubinda (Talent Acquisition)",
    nextAction: "Complete the screening call and confirm the certification is current",
    dueDate: "2026-08-01",
    location: "Mufulira, Zambia",
    rightToWork: "Not yet verified — document check booked for 3 August 2026",
    noticePeriod: "Available immediately",
    currentRole: "Welder, currently between contracts",
    salaryExpectation: "ZMW 1,110,000",
    consent: consentCurrent("2026-07-19", "2027-01-19"),
    scorecards: [],
    checks: [
      { id: "bc-1", label: "Right to work", provider: "In-house document check", outcome: "Not started", updatedOn: "2026-07-19", note: "Appointment booked for 3 August 2026." },
      { id: "bc-2", label: "Welding certification check", provider: "Certification register", outcome: "In progress", updatedOn: "2026-07-26", note: "Certificate number supplied by the agency is being verified against the register." },
    ],
    policy: [
      { id: "p1", label: "Right to work verified", outcome: "fail", detail: "Not verified. No assessment or offer can proceed until the document check is completed." },
      { id: "p2", label: "Agency terms in place", outcome: "pass", detail: "Nordwerk Personal is on the approved supplier list at 12 per cent." },
    ],
    conflicts: [],
    timeline: [
      { id: "t1", at: "2026-07-19T13:40:00Z", actor: "Nordwerk Personal", event: "Application received", after: "Applied" },
      { id: "t2", at: "2026-07-24T09:00:00Z", actor: "Namakau Lubinda", event: "Moved to screening", before: "Applied", after: "Screening" },
    ],
  },
  {
    id: "c-1071",
    reference: "CAND-2026-1071",
    fullName: "Fatuma Abdi Hassan",
    vacancyId: "VAC-2026-017",
    appliedOn: "2026-07-25",
    stage: "Applied",
    source: "Careers portal",
    sourceDetail: "Applied directly through the careers portal",
    status: "Submitted",
    owner: "Namakau Lubinda (Talent Acquisition)",
    nextAction: "Sift the application against the essential criteria and reply either way",
    dueDate: "2026-08-01",
    location: "Lusaka, Zambia",
    rightToWork: "Declared — verification happens only if shortlisted",
    noticePeriod: "One month",
    currentRole: "Service Technician at a lift maintenance company",
    salaryExpectation: "ZMW 1,475,000",
    consent: consentCurrent("2026-07-25", "2027-01-25"),
    scorecards: [],
    checks: [
      { id: "bc-1", label: "Right to work", provider: "In-house document check", outcome: "Not started", updatedOn: "2026-07-25", note: "Deliberately not requested at application stage — only collected if the candidate is shortlisted." },
    ],
    policy: [
      { id: "p1", label: "Sift within five working days", outcome: "warn", detail: "Applied 25 July. The service standard expects a reply by 1 August." },
      { id: "p2", label: "Essential criteria met", outcome: "pass", detail: "Holds a category B licence and four years of field service experience." },
    ],
    conflicts: [],
    timeline: [{ id: "t1", at: "2026-07-25T20:10:00Z", actor: "Careers portal", event: "Application received", after: "Applied" }],
  },
  {
    id: "c-0912",
    reference: "CAND-2026-0912",
    fullName: "Rita Nyambura Gitau",
    vacancyId: "VAC-2026-016",
    appliedOn: "2026-05-22",
    stage: "Hired",
    source: "Referral",
    sourceDetail: "Referred by an employee at Chingola Office under the referral scheme",
    status: "Approved",
    owner: "Achieng Otieno (Talent Acquisition, EA)",
    nextAction: "Complete pre-employment checks and issue the contract before the start date",
    dueDate: "2026-08-14",
    location: "Chingola, Zambia",
    rightToWork: "Verified — Zambian national, document check completed 3 July 2026",
    noticePeriod: "One month",
    currentRole: "Warehouse Supervisor at a distribution company",
    salaryExpectation: "ZMW 33,000 a month",
    consent: {
      lawfulBasis: "Contract necessity — the record converts to an employment record on the start date",
      obtainedOn: "2026-05-22",
      retainUntil: "Retained as part of the employment record",
      state: "Consent current",
      note: "Once the hire starts, the recruitment record is merged into the employment record and the recruitment retention clock no longer applies.",
    },
    scorecards: [
      {
        id: "sc-1",
        stage: "Competency interview",
        interviewer: "Chileshe Mumba",
        interviewerRole: "Finance Manager, Copperbelt",
        heldOn: "2026-06-18",
        overall: 5,
        recommendation: "Advance",
        criteria: [
          { label: "Stock control", rating: 5, note: "Reduced stock discrepancies from 4 per cent to under 1 per cent in a previous role." },
          { label: "Leading a night shift", rating: 5, note: "Ran a 14-person night team for two years." },
          { label: "Safety record", rating: 4, note: "Clear on incident reporting; described a near-miss investigation in detail." },
        ],
        comment: "The strongest candidate in the process by a clear margin.",
      },
      {
        id: "sc-2",
        stage: "Operational interview",
        interviewer: "Gift Zulu",
        interviewerRole: "Yard Contractor (Logistics)",
        heldOn: "2026-06-19",
        overall: 4,
        recommendation: "Advance",
        criteria: [
          { label: "Practical warehouse knowledge", rating: 5, note: "Knew the goods-in process end to end." },
          { label: "Working with contractors", rating: 4, note: "Practical view of how to hold contractors to the schedule." },
        ],
        comment: "No reservations.",
      },
    ],
    checks: [
      { id: "bc-1", label: "Right to work", provider: "In-house document check", outcome: "Cleared", updatedOn: "2026-07-03", note: "National identity card sighted in person." },
      { id: "bc-2", label: "Employment reference — current employer", provider: "Requested directly", outcome: "Cleared", updatedOn: "2026-07-09", note: "Dates, role and reason for leaving confirmed." },
      { id: "bc-3", label: "Certificate of good conduct", provider: "Directorate of Criminal Investigations", outcome: "In progress", updatedOn: "2026-07-24", note: "Applied 22 July; typical turnaround is three weeks. The offer is conditional on the result." },
      { id: "bc-4", label: "Qualification check", provider: "Awarding institution", outcome: "Cleared", updatedOn: "2026-07-05", note: "Diploma in Supply Chain Management confirmed." },
    ],
    offer: {
      grade: "G5",
      baseSalary: 1980000,
      currency: "ZMW",
      allowances: "Night-shift allowance of ZMW 3,600 a month; medical cover from day one",
      proposedStart: "2026-09-01",
      probationMonths: 3,
      contractType: "Permanent, full time",
      expiresOn: "2026-07-18",
      approvedBudget: 1980000,
      comparatorNote: "Exactly at the approved requisition cost and at the G5 range mid-point for Zambia.",
    },
    policy: [
      { id: "p1", label: "Within the approved grade range", outcome: "pass", detail: "ZMW 395,000 a year is the G5 mid-point for Zambia." },
      { id: "p2", label: "Within requisition budget", outcome: "pass", detail: "Matches the annual cost approved on REQ-2026-033." },
      { id: "p3", label: "Pre-employment checks complete", outcome: "warn", detail: "Certificate of good conduct is still outstanding. The contract is conditional on it." },
      { id: "p4", label: "Referral fee eligibility", outcome: "pass", detail: "The referring employee qualifies for the scheme payment after the probation period." },
    ],
    conflicts: [],
    timeline: [
      { id: "t1", at: "2026-05-22T07:15:00Z", actor: "Employee referral", event: "Application received", after: "Applied" },
      { id: "t2", at: "2026-06-02T09:00:00Z", actor: "Achieng Otieno", event: "Screening call completed", before: "Applied", after: "Screening" },
      { id: "t3", at: "2026-06-10T09:00:00Z", actor: "Chileshe Mumba", event: "Shortlisted", before: "Screening", after: "Shortlisted" },
      { id: "t4", at: "2026-06-18T08:00:00Z", actor: "Chileshe Mumba", event: "Competency interview held", before: "Shortlisted", after: "Interview" },
      { id: "t5", at: "2026-07-08T12:00:00Z", actor: "Chileshe Mumba", event: "Offer authorised", before: "Interview", after: "Offer" },
      { id: "t6", at: "2026-07-14T10:00:00Z", actor: "Rita Nyambura Gitau", event: "Offer accepted", before: "Offer", after: "Hired" },
    ],
  },
  {
    id: "c-1075",
    reference: "CAND-2026-1075",
    fullName: "Mubita Nalumino",
    vacancyId: "VAC-2026-018",
    appliedOn: "2026-07-08",
    stage: "Rejected",
    source: "Careers portal",
    sourceDetail: "Applied directly through the careers portal",
    status: "Rejected",
    owner: "Namakau Lubinda (Talent Acquisition)",
    nextAction: "No further action — the record is deleted on the retention date",
    dueDate: "2027-01-08",
    location: "Kitwe, Zambia",
    rightToWork: "Not verified — the application did not reach shortlist",
    noticePeriod: "Not discussed",
    currentRole: "Fabrication Assistant",
    salaryExpectation: "Not discussed",
    consent: consentCurrent("2026-07-08", "2027-01-08"),
    scorecards: [
      {
        id: "sc-1",
        stage: "Application sift",
        interviewer: "Namakau Lubinda",
        interviewerRole: "Talent Acquisition",
        heldOn: "2026-07-12",
        overall: 2,
        recommendation: "Do not advance",
        criteria: [
          { label: "Certification", rating: 1, note: "No current welding certification; the essential criterion is not met." },
          { label: "Relevant experience", rating: 3, note: "Two years of fabrication assistance, no independent welding." },
        ],
        comment: "Rejected against the essential certification criterion. Feedback sent 12 July and the candidate was invited to reapply once certified.",
      },
    ],
    checks: [
      { id: "bc-1", label: "Right to work", provider: "In-house document check", outcome: "Not started", updatedOn: "2026-07-12", note: "Never requested — the application did not reach shortlist." },
    ],
    policy: [
      { id: "p1", label: "Essential criteria met", outcome: "fail", detail: "No current welding certification, which the advertisement listed as essential." },
      { id: "p2", label: "Reason recorded and feedback sent", outcome: "pass", detail: "Reason recorded 12 July 2026 and written feedback sent the same day." },
    ],
    conflicts: [],
    timeline: [
      { id: "t1", at: "2026-07-08T19:45:00Z", actor: "Careers portal", event: "Application received", after: "Applied" },
      { id: "t2", at: "2026-07-12T11:20:00Z", actor: "Namakau Lubinda", event: "Rejected at sift", reason: "No current welding certification, which the advertisement listed as essential.", before: "Applied", after: "Rejected" },
    ],
  },
  {
    id: "c-1079",
    reference: "CAND-2026-1079",
    fullName: "Chiara Bellini",
    vacancyId: "VAC-2026-017",
    appliedOn: "2026-07-06",
    stage: "Withdrawn",
    source: "Referral",
    sourceDetail: "Referred by an employee at Lusaka HQ under the referral scheme",
    status: "Cancelled",
    owner: "Namakau Lubinda (Talent Acquisition)",
    nextAction: "No further action — the candidate asked for the record to be deleted early",
    dueDate: "2026-08-05",
    location: "Kabwe, Zambia",
    rightToWork: "Verified — EU national, document check completed 16 July 2026",
    noticePeriod: "Three months",
    currentRole: "Service Engineer at a packaging machinery supplier",
    salaryExpectation: "ZMW 1,600,000",
    consent: {
      lawfulBasis: "Consent given at application",
      obtainedOn: "2026-07-06",
      retainUntil: "2026-08-05",
      state: "Consent withdrawn",
      note: "The candidate withdrew consent on 22 July 2026 and asked for early deletion. The record is removed on 5 August 2026; only the anonymised selection audit trail is kept.",
    },
    scorecards: [
      {
        id: "sc-1",
        stage: "Technical interview",
        interviewer: "Mutale Kabwe",
        interviewerRole: "Operations Manager",
        heldOn: "2026-07-20",
        overall: 4,
        recommendation: "Advance",
        criteria: [
          { label: "Fault diagnosis", rating: 4, note: "Structured approach; asked the right questions about the fault history." },
          { label: "Customer handling", rating: 4, note: "Good examples from a machinery supplier setting." },
        ],
        comment: "Would have progressed. Candidate withdrew two days later after accepting another offer.",
      },
    ],
    checks: [
      { id: "bc-1", label: "Right to work", provider: "In-house document check", outcome: "Cleared", updatedOn: "2026-07-16", note: "Passport sighted in person. Copy deleted when the candidate withdrew." },
    ],
    policy: [
      { id: "p1", label: "Withdrawal reason recorded", outcome: "pass", detail: "Accepted an offer elsewhere; recorded 22 July 2026." },
      { id: "p2", label: "Early deletion request", outcome: "warn", detail: "Deletion is due by 5 August 2026. Confirm the agency and interview notes are removed as well." },
    ],
    conflicts: [],
    timeline: [
      { id: "t1", at: "2026-07-06T09:30:00Z", actor: "Employee referral", event: "Application received", after: "Applied" },
      { id: "t2", at: "2026-07-13T10:00:00Z", actor: "Namakau Lubinda", event: "Shortlisted", before: "Screening", after: "Shortlisted" },
      { id: "t3", at: "2026-07-20T09:00:00Z", actor: "Mutale Kabwe", event: "Technical interview held", before: "Shortlisted", after: "Interview" },
      { id: "t4", at: "2026-07-22T14:05:00Z", actor: "Chiara Bellini", event: "Withdrew from the process", reason: "Accepted an offer elsewhere and asked for the record to be deleted early.", before: "Interview", after: "Withdrawn" },
    ],
  },
];

/* --------------------------------------------------------------- read api */

export const recruitmentApi = {
  requisitions: async () => {
    await delay();
    return requisitions;
  },
  requisition: async (id: string) => {
    await delay();
    return requisitions.find((r) => r.id === id) ?? null;
  },
  vacancies: async () => {
    await delay();
    return vacancies;
  },
  vacancy: async (id: string) => {
    await delay();
    return vacancies.find((v) => v.id === id) ?? null;
  },
  candidates: async () => {
    await delay();
    return candidates;
  },
  candidate: async (id: string) => {
    await delay();
    return candidates.find((c) => c.id === id) ?? null;
  },
  /** Candidate detail needs the vacancy and its requisition for context. */
  candidateContext: async (id: string) => {
    await delay();
    const candidate = candidates.find((c) => c.id === id) ?? null;
    const vacancy = candidate ? (vacancies.find((v) => v.id === candidate.vacancyId) ?? null) : null;
    const requisition = vacancy ? (requisitions.find((r) => r.id === vacancy.requisitionId) ?? null) : null;
    const peers = candidate ? candidates.filter((c) => c.vacancyId === candidate.vacancyId && c.id !== candidate.id) : [];
    return { candidate, vacancy, requisition, peers };
  },
  submitRequisition: async (_draft: Record<string, unknown>) => {
    await delay(600);
    return { id: "REQ-2026-047" };
  },
};

/** Vacancy lookup used by candidate lists so a row can name the role applied for. */
export const vacancyLabel = (id: string) => {
  const v = vacancies.find((x) => x.id === id);
  return v ? `${v.jobTitle} — ${v.branch}` : "Vacancy withdrawn";
};
