import type { RequestStatus, TimelineEvent } from "./types";

/**
 * Talent mock data: goals, performance reviews and learning.
 * Self-contained — nothing here is shared with the core mock service.
 * "Today" for this dataset is 29 July 2026.
 */

export const TODAY = "2026-07-29";
export const ME = "w-1001";
export const MY_MANAGER = "w-1002";

/* ------------------------------------------------------------------ goals */

export type GoalStatus = "Draft" | "Active" | "Achieved" | "Missed" | "Cancelled";

export interface Goal {
  id: string;
  scope: "mine" | "team";
  employeeId: string;
  cycle: string;
  title: string;
  /** The KPI the goal is measured on — never just a description. */
  measure: string;
  /** Percentage of the cycle scorecard. Weightings in a cycle sum to 100%. */
  weighting: number;
  target: string;
  current: string;
  progress: number;
  status: GoalStatus;
  owner: string;
  nextAction: string;
  dueDate: string;
  lastUpdated: string;
  /** Goal cascading: the organisational goal this one rolls up to. */
  alignedTo?: { id: string; title: string };
  note?: string;
}

const goals: Goal[] = [
  {
    id: "gl-2601",
    scope: "mine",
    employeeId: "w-1001",
    cycle: "2026 performance cycle",
    title: "Cut unplanned downtime on the Lusaka packaging line",
    measure: "Unplanned downtime hours per month (CMMS availability report)",
    weighting: 30,
    target: "42 hours or fewer per month",
    current: "51 hours per month",
    progress: 64,
    status: "Active",
    owner: "Chanda Mwansa-Chileshe",
    nextAction: "Upload the June and July downtime extract as evidence",
    dueDate: "2026-12-31",
    lastUpdated: "2026-07-21",
    alignedTo: { id: "gl-org-04", title: "Operations: raise plant availability to 96% by year end" },
  },
  {
    id: "gl-2602",
    scope: "mine",
    employeeId: "w-1001",
    cycle: "2026 performance cycle",
    title: "Lift preventive maintenance compliance above 90%",
    measure: "Preventive work orders closed within the scheduled window (%)",
    weighting: 25,
    target: "90%",
    current: "84%",
    progress: 78,
    status: "Active",
    owner: "Chanda Mwansa-Chileshe",
    nextAction: "Agree the revised weekly schedule with Mutale Kabwe",
    dueDate: "2026-10-31",
    lastUpdated: "2026-07-14",
    alignedTo: { id: "gl-org-04", title: "Operations: raise plant availability to 96% by year end" },
  },
  {
    id: "gl-2603",
    scope: "mine",
    employeeId: "w-1001",
    cycle: "2026 performance cycle",
    title: "Qualify three more planners on the new CMMS",
    measure: "Planners signed off at competency level 3 by the systems lead",
    weighting: 20,
    target: "3 planners",
    current: "2 planners",
    progress: 67,
    status: "Active",
    owner: "Chanda Mwansa-Chileshe",
    nextAction: "Book the third planner onto the September sign-off session",
    dueDate: "2026-11-30",
    lastUpdated: "2026-07-09",
    alignedTo: { id: "gl-org-11", title: "People: every critical role has a qualified deputy" },
  },
  {
    id: "gl-2604",
    scope: "mine",
    employeeId: "w-1001",
    cycle: "2026 performance cycle",
    title: "Clear the critical spare-parts stock-out backlog",
    measure: "Critical spare lines sitting below their reorder point",
    weighting: 15,
    target: "0 lines",
    current: "0 lines",
    progress: 100,
    status: "Achieved",
    owner: "Chanda Mwansa-Chileshe",
    nextAction: "No action — recognised at the June operations review",
    dueDate: "2026-06-30",
    lastUpdated: "2026-06-27",
    alignedTo: { id: "gl-org-04", title: "Operations: raise plant availability to 96% by year end" },
  },
  {
    id: "gl-2605",
    scope: "mine",
    employeeId: "w-1001",
    cycle: "2026 performance cycle",
    title: "Publish a shutdown readiness playbook for the Ndola plant",
    measure: "Playbook approved by the plant manager and issued to all planners",
    weighting: 10,
    target: "1 approved playbook",
    current: "Draft v3 in review",
    progress: 45,
    status: "Active",
    owner: "Chanda Mwansa-Chileshe",
    nextAction: "Send draft v3 to the plant manager for comment",
    dueDate: "2026-09-30",
    lastUpdated: "2026-07-24",
    alignedTo: { id: "gl-org-07", title: "Operations: standardise shutdown practice across all sites" },
  },
  {
    id: "gl-2606",
    scope: "mine",
    employeeId: "w-1001",
    cycle: "2026 performance cycle",
    title: "Deliver the Kitwe depot relocation plan",
    measure: "Relocation plan signed off by the depot steering group",
    weighting: 0,
    target: "Plan signed off",
    current: "Withdrawn",
    progress: 0,
    status: "Cancelled",
    owner: "Chanda Mwansa-Chileshe",
    nextAction: "No action — the relocation was deferred to 2027",
    dueDate: "2026-05-12",
    lastUpdated: "2026-05-12",
    note: "Reweighted from 15% to 0% on 12 May 2026 when the relocation was deferred. The remaining five goals carry the full 100%.",
  },

  {
    id: "gl-2611",
    scope: "team",
    employeeId: "w-1004",
    cycle: "2026 performance cycle",
    title: "Re-qualify to ISO 9606-1 in the 6G position",
    measure: "Coupon test passed at first attempt and certificate filed",
    weighting: 40,
    target: "Pass",
    current: "Test booked for 14 August",
    progress: 30,
    status: "Active",
    owner: "Kondwani Mwanza",
    nextAction: "Attend the 6G coupon test at the Livingstone works",
    dueDate: "2026-08-31",
    lastUpdated: "2026-07-18",
    alignedTo: { id: "gl-org-02", title: "Manufacturing: zero weld rework on pressure vessels" },
  },
  {
    id: "gl-2612",
    scope: "team",
    employeeId: "w-1004",
    cycle: "2026 performance cycle",
    title: "Bring the weld rework rate under 1.5%",
    measure: "Rework as a percentage of welds inspected (QA sample)",
    weighting: 35,
    target: "1.5% or lower",
    current: "1.9%",
    progress: 62,
    status: "Active",
    owner: "Kondwani Mwanza",
    nextAction: "Review the July QA sample with the welding supervisor",
    dueDate: "2026-12-31",
    lastUpdated: "2026-07-16",
    alignedTo: { id: "gl-org-02", title: "Manufacturing: zero weld rework on pressure vessels" },
  },
  {
    id: "gl-2613",
    scope: "team",
    employeeId: "w-1004",
    cycle: "2026 performance cycle",
    title: "Run the quarterly toolbox-talk rota",
    measure: "Toolbox talks delivered per quarter",
    weighting: 25,
    target: "6 talks",
    current: "6 talks",
    progress: 100,
    status: "Achieved",
    owner: "Kondwani Mwanza",
    nextAction: "No action — closed at the June safety review",
    dueDate: "2026-06-30",
    lastUpdated: "2026-06-30",
    alignedTo: { id: "gl-org-09", title: "Safety: every team hears a safety message every fortnight" },
  },

  {
    id: "gl-2621",
    scope: "team",
    employeeId: "w-1008",
    cycle: "2026 performance cycle",
    title: "Lift depot on-time dispatch to 97%",
    measure: "Consignments dispatched within the promised window (%)",
    weighting: 45,
    target: "97%",
    current: "94.2%",
    progress: 71,
    status: "Active",
    owner: "Emmanuel Sakala",
    nextAction: "Agree the revised second-shift loading plan",
    dueDate: "2026-12-31",
    lastUpdated: "2026-07-23",
    alignedTo: { id: "gl-org-05", title: "Logistics: 97% on-time dispatch across all depots" },
  },
  {
    id: "gl-2622",
    scope: "team",
    employeeId: "w-1008",
    cycle: "2026 performance cycle",
    title: "Close every open forklift licence renewal at the depot",
    measure: "Depot operators holding an in-date counterbalance licence",
    weighting: 30,
    target: "0 expired licences",
    current: "1 expired licence",
    progress: 80,
    status: "Active",
    owner: "Emmanuel Sakala",
    nextAction: "Book the remaining operator onto the RTITB re-test",
    dueDate: "2026-08-15",
    lastUpdated: "2026-07-27",
    alignedTo: { id: "gl-org-09", title: "Safety: every team hears a safety message every fortnight" },
    note: "An expired licence removes fitness to operate. Track the renewal in Learning.",
  },
  {
    id: "gl-2623",
    scope: "team",
    employeeId: "w-1008",
    cycle: "2026 performance cycle",
    title: "Halve depot overtime hours",
    measure: "Overtime hours per quarter (time and attendance export)",
    weighting: 25,
    target: "180 hours or fewer per quarter",
    current: "268 hours per quarter",
    progress: 22,
    status: "Missed",
    owner: "Emmanuel Sakala",
    nextAction: "Agree a recovery plan at the August one-to-one",
    dueDate: "2026-06-30",
    lastUpdated: "2026-07-02",
    alignedTo: { id: "gl-org-05", title: "Logistics: 97% on-time dispatch across all depots" },
  },

  {
    id: "gl-2631",
    scope: "team",
    employeeId: "w-1005",
    cycle: "2026 performance cycle",
    title: "Bring HR case first-response time under 8 working hours",
    measure: "Median hours from case raised to first substantive reply",
    weighting: 50,
    target: "8 working hours or fewer",
    current: "8.6 working hours",
    progress: 88,
    status: "Active",
    owner: "Thandiwe Banda",
    nextAction: "Publish the revised triage rota for August",
    dueDate: "2026-09-30",
    lastUpdated: "2026-07-25",
    alignedTo: { id: "gl-org-12", title: "People: every employee gets a substantive answer the same working day" },
  },
  {
    id: "gl-2632",
    scope: "team",
    employeeId: "w-1005",
    cycle: "2026 performance cycle",
    title: "Complete the employee data quality clean-up",
    measure: "Records with no outstanding data quality exception (%)",
    weighting: 30,
    target: "99%",
    current: "91%",
    progress: 55,
    status: "Active",
    owner: "Thandiwe Banda",
    nextAction: "Clear the 42 remaining bank detail mismatches",
    dueDate: "2026-10-31",
    lastUpdated: "2026-07-20",
    alignedTo: { id: "gl-org-12", title: "People: every employee gets a substantive answer the same working day" },
  },
  {
    id: "gl-2633",
    scope: "team",
    employeeId: "w-1005",
    cycle: "2026 performance cycle",
    title: "Draft the 2027 leave policy refresh",
    measure: "Draft policy circulated to the works council",
    weighting: 20,
    target: "1 circulated draft",
    current: "Not started",
    progress: 0,
    status: "Draft",
    owner: "Thandiwe Banda",
    nextAction: "Agree the scope of the refresh with HR leadership",
    dueDate: "2026-11-28",
    lastUpdated: "2026-07-06",
  },
];

/* ---------------------------------------------------------------- reviews */

export type ReviewStage =
  | "Self-assessment"
  | "Manager review"
  | "Calibration"
  | "Acknowledgement"
  | "Closed";

/** 1–5 scale. A rating is never shown as a number or a colour alone. */
export const ratingScale: Record<number, string> = {
  1: "Not met",
  2: "Partially met",
  3: "Met expectations",
  4: "Exceeded expectations",
  5: "Outstanding",
};

export const ratingLabel = (score: number | null) =>
  score === null ? "Not yet rated" : `${score} of 5 — ${ratingScale[score] ?? "Not rated"}`;

export interface CompetencyRating {
  id: string;
  competency: string;
  descriptor: string;
  selfScore: number | null;
  selfComment: string;
  managerScore: number | null;
  managerComment: string;
}

export interface EvidenceLink {
  id: string;
  label: string;
  source: string;
  addedOn: string;
  href: string;
}

export interface Review {
  id: string;
  cycle: string;
  employeeId: string;
  reviewerId: string;
  stage: ReviewStage;
  status: RequestStatus;
  owner: string;
  nextAction: string;
  dueDate: string;
  scope: "mine" | "team";
  /** Ratings are sensitive: a general list view never prints another employee's score. */
  visibleToMe: boolean;
  overallScore: number | null;
  selfOverallScore: number | null;
  managerSummary: string;
  selfSummary: string;
  competencies: CompetencyRating[];
  evidence: EvidenceLink[];
  acknowledgedOn?: string;
  appeal: {
    open: boolean;
    deadline: string;
    routeTo: string;
    howItWorks: string[];
    raisedOn?: string;
    outcome?: string;
  };
  timeline: TimelineEvent[];
}

const reviews: Review[] = [
  {
    id: "rv-2041",
    cycle: "Mid-year 2026",
    employeeId: "w-1001",
    reviewerId: "w-1002",
    stage: "Acknowledgement",
    status: "In review",
    owner: "Chanda Mwansa-Chileshe",
    nextAction: "Acknowledge the review, or request reconsideration",
    dueDate: "2026-08-12",
    scope: "mine",
    visibleToMe: true,
    overallScore: 4,
    selfOverallScore: 4,
    selfSummary:
      "A strong half-year on planning discipline. The packaging line is still short of the downtime target, but preventive compliance has moved from 71% to 84% and the second planner is now signed off on the CMMS. I would like more time on the shutdown playbook in the second half.",
    managerSummary:
      "Chanda has held the planning function together through a difficult first half. Preventive compliance is the standout result and the spare-parts backlog is fully cleared. The downtime target is behind plan, but the causes sit largely with the packaging line's ageing sealing unit rather than with planning. For the second half I want to see the shutdown playbook finished and the CMMS reporting used more consistently in the weekly review — that is the one area where my rating sits below Chanda's own.",
    competencies: [
      {
        id: "cp-01",
        competency: "Planning and scheduling discipline",
        descriptor: "Builds realistic schedules, protects the frozen window and replans transparently.",
        selfScore: 4,
        selfComment:
          "Held the frozen window in 21 of 26 weeks, and every break was agreed with operations beforehand.",
        managerScore: 4,
        managerComment:
          "Agreed. The frozen-window discipline is now the benchmark other sites are being pointed at.",
      },
      {
        id: "cp-02",
        competency: "Safety leadership",
        descriptor: "Designs work so it can be done safely and challenges unsafe shortcuts.",
        selfScore: 5,
        selfComment:
          "Stopped the March sealing-unit job when the isolation certificate was incomplete, and rewrote the permit template afterwards.",
        managerScore: 4,
        managerComment:
          "The March stop was exactly right. I have rated this a 4 rather than a 5 because the rewritten permit template has not yet been rolled out beyond Lusaka.",
      },
      {
        id: "cp-03",
        competency: "Stakeholder communication",
        descriptor: "Keeps operations, maintenance and supply chain aligned on what happens when.",
        selfScore: 3,
        selfComment: "Consistent weekly notes, though I was slower than I would like when plans changed mid-week.",
        managerScore: 4,
        managerComment:
          "I would rate this higher than Chanda does. Operations specifically asked for the weekly note to be extended to the Ndola plant.",
      },
      {
        id: "cp-04",
        competency: "Data and systems (CMMS)",
        descriptor: "Uses the maintenance system as the single source of truth for planning decisions.",
        selfScore: 4,
        selfComment:
          "Rebuilt the availability report and used it in every monthly review. Two planners are now signed off at level 3.",
        managerScore: 3,
        managerComment:
          "The rebuilt report is good work. My rating reflects that weekly decisions are still being made from a side spreadsheet rather than from the system itself.",
      },
      {
        id: "cp-05",
        competency: "Coaching and knowledge transfer",
        descriptor: "Grows capability in others rather than absorbing the work personally.",
        selfScore: 3,
        selfComment: "Two planners qualified; the third slipped because of the shutdown workload.",
        managerScore: 3,
        managerComment: "Fair self-assessment. The third sign-off should land before the year-end review.",
      },
    ],
    evidence: [
      {
        id: "ev-01",
        label: "CMMS availability report — January to June 2026",
        source: "Maintenance systems",
        addedOn: "2026-07-06",
        href: "#evidence-cmms-availability",
      },
      {
        id: "ev-02",
        label: "Shutdown readiness playbook, draft v3",
        source: "Document library",
        addedOn: "2026-07-24",
        href: "#evidence-shutdown-playbook",
      },
      {
        id: "ev-03",
        label: "Peer feedback — 3 responses, summarised",
        source: "Feedback module",
        addedOn: "2026-07-02",
        href: "#evidence-peer-feedback",
      },
      {
        id: "ev-04",
        label: "Permit-to-work template, revision B",
        source: "Health and safety",
        addedOn: "2026-04-11",
        href: "#evidence-permit-template",
      },
    ],
    appeal: {
      open: true,
      deadline: "2026-08-26",
      routeTo: "Thandiwe Banda, HR Operations — not your reviewer",
      howItWorks: [
        "Tell us which competency or rating you disagree with, and why.",
        "HR Operations acknowledges within 2 working days and tells you who will look at it.",
        "A reviewer outside your reporting line re-reads the evidence with you and your manager.",
        "You get a written outcome within 15 working days. The original rating stays visible alongside any change.",
      ],
    },
    timeline: [
      {
        id: "tl-01",
        at: "2026-06-15T08:30:00Z",
        actor: "System",
        event: "Mid-year 2026 cycle opened",
        after: "Self-assessment",
      },
      {
        id: "tl-02",
        at: "2026-07-06T15:12:00Z",
        actor: "Chanda Mwansa-Chileshe",
        event: "Self-assessment submitted",
        before: "Self-assessment",
        after: "Manager review",
        evidence: { label: "CMMS availability report", href: "#evidence-cmms-availability" },
      },
      {
        id: "tl-03",
        at: "2026-07-17T10:40:00Z",
        actor: "Mutale Kabwe",
        event: "Manager review completed",
        before: "Manager review",
        after: "Calibration",
      },
      {
        id: "tl-04",
        at: "2026-07-24T13:05:00Z",
        actor: "Calibration panel, Operations",
        event: "Rating confirmed without change at calibration",
        reason: "Consistent with two comparable planning roles at grade G7.",
        before: "Calibration",
        after: "Acknowledgement",
      },
      {
        id: "tl-05",
        at: "2026-07-24T13:07:00Z",
        actor: "System",
        event: "Released to the employee for acknowledgement",
        reason: "Acknowledgement is due by 12 August 2026.",
      },
    ],
  },
  {
    id: "rv-2042",
    cycle: "Year-end 2025",
    employeeId: "w-1001",
    reviewerId: "w-1002",
    stage: "Closed",
    status: "Approved",
    owner: "Closed — no owner",
    nextAction: "No action — closed and acknowledged",
    dueDate: "2026-01-30",
    scope: "mine",
    visibleToMe: true,
    overallScore: 3,
    selfOverallScore: 4,
    selfSummary:
      "First full year owning the planning schedule. Preventive compliance moved in the right direction and I picked up the Ndola site alongside Lusaka.",
    managerSummary:
      "A solid year in a role that grew considerably mid-way through. Rated at expectations, with the clear expectation that preventive compliance passes 90% during 2026. Chanda raised a reconsideration on the communication rating; it was reviewed and the original rating stood, with the reasoning recorded below.",
    competencies: [
      {
        id: "cp-11",
        competency: "Planning and scheduling discipline",
        descriptor: "Builds realistic schedules, protects the frozen window and replans transparently.",
        selfScore: 4,
        selfComment: "Took on Ndola in July without losing the Lusaka schedule.",
        managerScore: 3,
        managerComment: "Steady, with some slippage in the fourth quarter when the second site was added.",
      },
      {
        id: "cp-12",
        competency: "Safety leadership",
        descriptor: "Designs work so it can be done safely and challenges unsafe shortcuts.",
        selfScore: 4,
        selfComment: "No lost-time incidents on planned work all year.",
        managerScore: 4,
        managerComment: "Agreed — consistently strong.",
      },
      {
        id: "cp-13",
        competency: "Stakeholder communication",
        descriptor: "Keeps operations, maintenance and supply chain aligned on what happens when.",
        selfScore: 4,
        selfComment: "Weekly note introduced in March and never missed.",
        managerScore: 3,
        managerComment:
          "Reconsidered in February 2026 at Chanda's request. The rating stood: the weekly note was excellent, but mid-week changes were not always communicated to the Ndola shift leads.",
      },
      {
        id: "cp-14",
        competency: "Data and systems (CMMS)",
        descriptor: "Uses the maintenance system as the single source of truth for planning decisions.",
        selfScore: 3,
        selfComment: "Learned the new system during the year and started rebuilding the reports.",
        managerScore: 3,
        managerComment: "Agreed.",
      },
    ],
    evidence: [
      {
        id: "ev-11",
        label: "2025 preventive compliance summary",
        source: "Maintenance systems",
        addedOn: "2026-01-12",
        href: "#evidence-2025-compliance",
      },
      {
        id: "ev-12",
        label: "Reconsideration outcome letter, 27 February 2026",
        source: "HR Operations",
        addedOn: "2026-02-27",
        href: "#evidence-reconsideration-2025",
      },
    ],
    acknowledgedOn: "2026-03-04",
    appeal: {
      open: false,
      deadline: "2026-02-13",
      routeTo: "Thandiwe Banda, HR Operations — not your reviewer",
      howItWorks: [
        "The reconsideration window for a closed review has passed.",
        "You can still raise a new HR case if something material comes to light.",
      ],
      raisedOn: "2026-02-06",
      outcome: "Reviewed by HR Operations on 27 February 2026. Original rating stood; reasoning recorded on the record.",
    },
    timeline: [
      {
        id: "tl-11",
        at: "2026-01-09T09:00:00Z",
        actor: "Chanda Mwansa-Chileshe",
        event: "Self-assessment submitted",
        before: "Self-assessment",
        after: "Manager review",
      },
      {
        id: "tl-12",
        at: "2026-01-22T11:30:00Z",
        actor: "Mutale Kabwe",
        event: "Manager review completed",
        before: "Manager review",
        after: "Calibration",
      },
      {
        id: "tl-13",
        at: "2026-02-06T16:20:00Z",
        actor: "Chanda Mwansa-Chileshe",
        event: "Reconsideration requested",
        reason: "Disagreed with the stakeholder communication rating.",
        before: "Acknowledgement",
        after: "Reconsideration",
      },
      {
        id: "tl-14",
        at: "2026-02-27T10:05:00Z",
        actor: "Thandiwe Banda",
        event: "Reconsideration outcome issued — original rating stood",
        reason: "Evidence re-read with an independent reviewer; the reasoning is recorded on the review.",
        evidence: { label: "Outcome letter", href: "#evidence-reconsideration-2025" },
      },
      {
        id: "tl-15",
        at: "2026-03-04T08:45:00Z",
        actor: "Chanda Mwansa-Chileshe",
        event: "Review acknowledged",
        before: "Acknowledgement",
        after: "Closed",
      },
    ],
  },
  {
    id: "rv-2043",
    cycle: "Mid-year 2026",
    employeeId: "w-1004",
    reviewerId: "w-1002",
    stage: "Self-assessment",
    status: "Draft",
    owner: "Kondwani Mwanza",
    nextAction: "Employee to submit the self-assessment",
    dueDate: "2026-08-07",
    scope: "team",
    visibleToMe: false,
    overallScore: null,
    selfOverallScore: null,
    selfSummary: "",
    managerSummary: "",
    competencies: [],
    evidence: [],
    appeal: {
      open: false,
      deadline: "2026-09-04",
      routeTo: "Thandiwe Banda, HR Operations — not your reviewer",
      howItWorks: ["A reconsideration can only be raised once a rating has been released."],
    },
    timeline: [],
  },
  {
    id: "rv-2044",
    cycle: "Mid-year 2026",
    employeeId: "w-1008",
    reviewerId: "w-1002",
    stage: "Calibration",
    status: "In review",
    owner: "Calibration panel, Logistics",
    nextAction: "Panel to confirm or adjust the proposed rating",
    dueDate: "2026-08-21",
    scope: "team",
    visibleToMe: false,
    overallScore: null,
    selfOverallScore: null,
    selfSummary: "",
    managerSummary: "",
    competencies: [],
    evidence: [],
    appeal: {
      open: false,
      deadline: "2026-09-18",
      routeTo: "Thandiwe Banda, HR Operations — not your reviewer",
      howItWorks: ["A reconsideration can only be raised once a rating has been released."],
    },
    timeline: [],
  },
  {
    id: "rv-2045",
    cycle: "Mid-year 2026",
    employeeId: "w-1005",
    reviewerId: "w-1002",
    stage: "Acknowledgement",
    status: "Submitted",
    owner: "Thandiwe Banda",
    nextAction: "Employee to acknowledge, or request reconsideration",
    dueDate: "2026-08-18",
    scope: "team",
    visibleToMe: false,
    overallScore: null,
    selfOverallScore: null,
    selfSummary: "",
    managerSummary: "",
    competencies: [],
    evidence: [],
    appeal: {
      open: true,
      deadline: "2026-09-01",
      routeTo: "Thandiwe Banda, HR Operations — not your reviewer",
      howItWorks: ["A reconsideration can only be raised once a rating has been released."],
    },
    timeline: [],
  },
  {
    id: "rv-2046",
    cycle: "Mid-year 2026",
    employeeId: "w-1007",
    reviewerId: "w-1002",
    stage: "Manager review",
    status: "Returned",
    owner: "Mutale Kabwe",
    nextAction: "Add placement evidence before the rating can be proposed",
    dueDate: "2026-08-05",
    scope: "team",
    visibleToMe: false,
    overallScore: null,
    selfOverallScore: null,
    selfSummary: "",
    managerSummary: "",
    competencies: [],
    evidence: [],
    appeal: {
      open: false,
      deadline: "2026-09-02",
      routeTo: "Thandiwe Banda, HR Operations — not your reviewer",
      howItWorks: ["A reconsideration can only be raised once a rating has been released."],
    },
    timeline: [],
  },
  {
    id: "rv-2047",
    cycle: "Mid-year 2026",
    employeeId: "w-1003",
    reviewerId: "w-1002",
    stage: "Closed",
    status: "Approved",
    owner: "Closed — no owner",
    nextAction: "No action — closed and acknowledged",
    dueDate: "2026-07-17",
    scope: "team",
    visibleToMe: false,
    overallScore: null,
    selfOverallScore: null,
    selfSummary: "",
    managerSummary: "",
    competencies: [],
    evidence: [],
    appeal: {
      open: false,
      deadline: "2026-07-31",
      routeTo: "Thandiwe Banda, HR Operations — not your reviewer",
      howItWorks: ["The reconsideration window closes 14 days after acknowledgement."],
    },
    timeline: [],
  },
];

/* --------------------------------------------------------------- learning */

export type DeliveryMode =
  | "E-learning"
  | "Classroom"
  | "Virtual classroom"
  | "On-the-job"
  | "Blended";

export interface Course {
  id: string;
  title: string;
  provider: string;
  mode: DeliveryMode;
  durationHours: number;
  mandatory: boolean;
  cpdPoints: number;
  category: "Health and safety" | "Licences" | "Technical" | "Leadership" | "Compliance";
  /** Months before a certificate from this course must be renewed. */
  renewalMonths?: number;
  description: string;
  nextCohort: string;
}

const courses: Course[] = [
  {
    id: "cr-101",
    title: "Working at Height — refresher",
    provider: "Meridian Safety Academy",
    mode: "Classroom",
    durationHours: 4,
    mandatory: true,
    cpdPoints: 4,
    category: "Health and safety",
    renewalMonths: 24,
    description: "Ladder, tower and mobile platform work, with rescue planning for the Lusaka gantry.",
    nextCohort: "2026-09-03",
  },
  {
    id: "cr-102",
    title: "Lock-out / tag-out — Authorised Person",
    provider: "TÜV Rheinland Academy",
    mode: "Blended",
    durationHours: 8,
    mandatory: true,
    cpdPoints: 8,
    category: "Licences",
    renewalMonths: 36,
    description: "Authorisation to write and sign isolation certificates for mechanical and electrical work.",
    nextCohort: "2026-08-19",
  },
  {
    id: "cr-103",
    title: "Counterbalance forklift — licence renewal",
    provider: "RTITB accredited centre, Lusaka",
    mode: "On-the-job",
    durationHours: 6,
    mandatory: true,
    cpdPoints: 6,
    category: "Licences",
    renewalMonths: 36,
    description: "Practical re-test and theory refresh for counterbalance truck operators.",
    nextCohort: "2026-08-11",
  },
  {
    id: "cr-104",
    title: "First aid at work (first aid at work)",
    provider: "Het Oranje Kruis",
    mode: "Classroom",
    durationHours: 16,
    mandatory: true,
    cpdPoints: 12,
    category: "Licences",
    renewalMonths: 12,
    description: "Zambian statutory first-aid certification, including AED and workplace incident handling.",
    nextCohort: "2026-08-17",
  },
  {
    id: "cr-105",
    title: "Reliability-centred maintenance foundations",
    provider: "SMRP Zambia",
    mode: "Virtual classroom",
    durationHours: 12,
    mandatory: false,
    cpdPoints: 12,
    category: "Technical",
    description: "Failure modes, criticality analysis and building a defensible preventive maintenance plan.",
    nextCohort: "2026-09-08",
  },
  {
    id: "cr-106",
    title: "CMMS advanced planning and scheduling",
    provider: "Meridian Digital Skills",
    mode: "E-learning",
    durationHours: 5,
    mandatory: false,
    cpdPoints: 5,
    category: "Technical",
    description: "Capacity levelling, frozen-window discipline and building availability reports in the CMMS.",
    nextCohort: "Available on demand",
  },
  {
    id: "cr-107",
    title: "Having difficult conversations",
    provider: "Lusaka Leadership Lab",
    mode: "Virtual classroom",
    durationHours: 3,
    mandatory: false,
    cpdPoints: 3,
    category: "Leadership",
    description: "Structuring performance and conduct conversations so they stay factual and specific.",
    nextCohort: "2026-10-02",
  },
  {
    id: "cr-108",
    title: "Data protection and the GDPR for people managers",
    provider: "Meridian Compliance",
    mode: "E-learning",
    durationHours: 2,
    mandatory: true,
    cpdPoints: 2,
    category: "Compliance",
    renewalMonths: 12,
    description: "Lawful basis, retention and what to do in the first hour of a suspected data breach.",
    nextCohort: "Available on demand",
  },
  {
    id: "cr-109",
    title: "Inclusive recruitment and fair assessment",
    provider: "Meridian People Academy",
    mode: "Virtual classroom",
    durationHours: 4,
    mandatory: false,
    cpdPoints: 4,
    category: "Leadership",
    description: "Structured interviewing, scoring rubrics and recognising assessment bias in panels.",
    nextCohort: "2026-09-24",
  },
];

export type EnrolmentStatus = "Enrolled" | "In progress" | "Completed" | "Expired";

export interface Enrolment {
  id: string;
  courseId: string;
  employeeId: string;
  status: EnrolmentStatus;
  progress: number;
  enrolledOn: string;
  completedOn?: string;
  certificateRef?: string;
  certificateExpiry?: string;
  recertificationDue?: string;
  owner: string;
  nextAction: string;
  dueDate: string;
  /** How a lapsed certificate changes what this employee may be asked to do. */
  fitnessImpact?: string;
}

const enrolments: Enrolment[] = [
  {
    id: "en-501",
    courseId: "cr-104",
    employeeId: "w-1001",
    status: "Completed",
    progress: 100,
    enrolledOn: "2025-07-30",
    completedOn: "2025-08-14",
    certificateRef: "first aid at work-2025-0442",
    certificateExpiry: "2026-08-31",
    recertificationDue: "2026-08-17",
    owner: "Chanda Mwansa-Chileshe",
    nextAction: "Book the 17 August refresher cohort before the certificate lapses",
    dueDate: "2026-08-17",
    fitnessImpact:
      "When this lapses you are removed from the Lusaka HQ first-aider roster, and lone-working cover cannot be assigned to you.",
  },
  {
    id: "en-502",
    courseId: "cr-102",
    employeeId: "w-1001",
    status: "Expired",
    progress: 100,
    enrolledOn: "2023-05-02",
    completedOn: "2023-05-31",
    certificateRef: "LOTO-2023-1187",
    certificateExpiry: "2026-05-31",
    recertificationDue: "2026-05-17",
    owner: "Chanda Mwansa-Chileshe",
    nextAction: "Book the 19 August authorisation course — Mutale Kabwe must approve the release",
    dueDate: "2026-08-19",
    fitnessImpact:
      "Expired. You cannot write or sign isolation certificates until you are re-authorised; planned jobs needing an isolation must be handed to another authorised person.",
  },
  {
    id: "en-503",
    courseId: "cr-101",
    employeeId: "w-1001",
    status: "Completed",
    progress: 100,
    enrolledOn: "2025-11-04",
    completedOn: "2025-11-20",
    certificateRef: "WAH-2025-3310",
    certificateExpiry: "2027-11-20",
    recertificationDue: "2027-10-20",
    owner: "Chanda Mwansa-Chileshe",
    nextAction: "No action — the next refresher is due 20 October 2027",
    dueDate: "2027-10-20",
  },
  {
    id: "en-504",
    courseId: "cr-106",
    employeeId: "w-1001",
    status: "In progress",
    progress: 60,
    enrolledOn: "2026-06-22",
    owner: "Chanda Mwansa-Chileshe",
    nextAction: "Complete modules 4 and 5, then the scheduling assessment",
    dueDate: "2026-08-21",
  },
  {
    id: "en-505",
    courseId: "cr-105",
    employeeId: "w-1001",
    status: "Enrolled",
    progress: 0,
    enrolledOn: "2026-07-15",
    owner: "Chanda Mwansa-Chileshe",
    nextAction: "Attend the opening session on 8 September 2026",
    dueDate: "2026-09-08",
  },
  {
    id: "en-506",
    courseId: "cr-108",
    employeeId: "w-1001",
    status: "Completed",
    progress: 100,
    enrolledOn: "2026-01-28",
    completedOn: "2026-02-03",
    certificateRef: "GDPR-2026-0918",
    certificateExpiry: "2027-02-03",
    recertificationDue: "2027-01-06",
    owner: "Chanda Mwansa-Chileshe",
    nextAction: "No action — annual refresh due 6 January 2027",
    dueDate: "2027-01-06",
  },
  {
    id: "en-507",
    courseId: "cr-107",
    employeeId: "w-1001",
    status: "Enrolled",
    progress: 0,
    enrolledOn: "2026-07-27",
    owner: "Chanda Mwansa-Chileshe",
    nextAction: "Attend the session on 2 October 2026",
    dueDate: "2026-10-02",
  },
];

/* -------------------------------------------------------------- helpers */

export type CertificateState = "Valid" | "Expiring" | "Expired" | "No certificate";

/** Certificates inside 90 days of expiry are flagged; the label is always text. */
export function certificateState(expiry: string | undefined, today = TODAY): CertificateState {
  if (!expiry) return "No certificate";
  const days = Math.round((Date.parse(expiry) - Date.parse(today)) / 86_400_000);
  if (days < 0) return "Expired";
  if (days <= 90) return "Expiring";
  return "Valid";
}

export function daysUntil(date: string, today = TODAY) {
  return Math.round((Date.parse(date) - Date.parse(today)) / 86_400_000);
}

/* ------------------------------------------------------------------ api */

const delay = (ms = 420) => new Promise((r) => setTimeout(r, ms));

export const talentApi = {
  goals: async () => {
    await delay();
    return goals;
  },
  reviews: async () => {
    await delay();
    return reviews;
  },
  review: async (id: string) => {
    await delay();
    return reviews.find((r) => r.id === id) ?? null;
  },
  courses: async () => {
    await delay();
    return courses;
  },
  enrolments: async () => {
    await delay();
    return enrolments;
  },
  learning: async () => {
    await delay();
    return { courses, enrolments };
  },
};
