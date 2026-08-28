/**
 * Lifecycle mock data: onboarding cases, offboarding (separation + clearance)
 * cases and effective-dated movements. Self-contained — this file owns its own
 * data and its own async reader so nothing else in the mock layer changes.
 *
 * No backend, no persistence. Reads have a realistic delay so screens exercise
 * their loading branch.
 */
import { employees, entities } from "./data";
import type { PolicyResult, RequestStatus, TimelineEvent } from "./types";

const delay = (ms = 420) => new Promise((r) => setTimeout(r, ms));

/** The date the mock world believes it is. */
export const TODAY = "2026-07-29";

/* ------------------------------------------------------------------ types */

export type TaskOwner = "HR" | "Manager" | "Employee" | "IT";
export type TaskState = "Not started" | "In progress" | "Done" | "Blocked";

/** Onboarding and clearance both run on the same checklist shape. */
export interface LifecycleTask {
  id: string;
  label: string;
  detail: string;
  owner: TaskOwner;
  ownerName: string;
  dueDate: string;
  state: TaskState;
  /** Present only when state is "Blocked" — says what is in the way and who must clear it. */
  blocker?: string;
}

export type ClearanceCategory =
  | "Assets returned"
  | "Access revoked"
  | "Knowledge handover"
  | "Final pay"
  | "Outstanding advances";

export interface ClearanceTask extends LifecycleTask {
  category: ClearanceCategory;
}

/** Lifecycle case states. Distinct from RequestStatus — a case is work, not a request. */
export type CaseStatus = "Draft" | "Ready" | "Active" | "Blocked" | "Completed" | "Cancelled";

export interface OnboardingCase {
  id: string;
  /** Set once the joiner exists in the directory; pre-hires may not yet. */
  employeeId?: string;
  personName: string;
  jobTitle: string;
  department: string;
  entity: string;
  branch: string;
  employmentType: string;
  hiringManager: string;
  startDate: string;
  probationStart: string;
  probationEnd: string;
  status: CaseStatus;
  owner: string;
  nextAction: string;
  dueDate: string;
  tasks: LifecycleTask[];
  timeline: TimelineEvent[];
}

export type SeparationReason =
  | "Resignation"
  | "Fixed-term expiry"
  | "End of engagement"
  | "Retirement"
  | "Mutual separation"
  | "Redundancy";

export interface OffboardingCase {
  id: string;
  employeeId?: string;
  personName: string;
  jobTitle: string;
  department: string;
  entity: string;
  branch: string;
  reason: SeparationReason;
  reasonDetail: string;
  noticeGivenOn: string;
  noticePeriod: string;
  lastWorkingDate: string;
  finalPayRun: string;
  rehireEligible: "Eligible" | "Not eligible" | "Under review";
  status: CaseStatus;
  owner: string;
  nextAction: string;
  dueDate: string;
  clearance: ClearanceTask[];
  policy: PolicyResult[];
  conflicts: string[];
  timeline: TimelineEvent[];
}

export type MovementType = "Promotion" | "Transfer" | "Secondment" | "Manager change";

export interface AssignmentSnapshot {
  jobTitle: string;
  grade: string;
  department: string;
  entity: string;
  branch: string;
  manager: string;
  positionId: string;
  costCentre: string;
}

export interface MovementImpact {
  id: string;
  area: "Reporting line" | "Payroll" | "Access" | "Position";
  summary: string;
  detail: string;
}

export interface MovementRecord {
  id: string;
  employeeId: string;
  type: MovementType;
  reason: string;
  effectiveFrom: string;
  /** Secondments and temporary transfers return on this date. */
  effectiveTo?: string;
  current: AssignmentSnapshot;
  proposed: AssignmentSnapshot;
  /** What changed, in plain words, for the list and the summary. */
  headline: string;
  status: RequestStatus;
  owner: string;
  nextAction: string;
  dueDate: string;
  raisedBy: string;
  raisedOn: string;
  impacts: MovementImpact[];
  policy: PolicyResult[];
  conflicts: string[];
  timeline: TimelineEvent[];
}

/* ---------------------------------------------------------------- helpers */

const employeeName = (id?: string) =>
  id ? employees.find((e) => e.id === id)?.fullName : undefined;

/** Directory name where the person is already an employee, otherwise the pre-hire name. */
export const displayName = (c: { employeeId?: string; personName: string }) =>
  employeeName(c.employeeId) ?? c.personName;

export const taskProgress = (tasks: { state: TaskState }[]) => {
  const done = tasks.filter((t) => t.state === "Done").length;
  return { done, total: tasks.length, label: `${done} of ${tasks.length} done` };
};

export const blockedTasks = <T extends LifecycleTask>(tasks: T[]) =>
  tasks.filter((t) => t.state === "Blocked");

export const overdueTasks = <T extends LifecycleTask>(tasks: T[], today = TODAY) =>
  tasks.filter((t) => t.state !== "Done" && t.dueDate < today);

export const taskOwners: TaskOwner[] = ["HR", "Manager", "Employee", "IT"];

export const ownerLabel: Record<TaskOwner, string> = {
  HR: "HR operations",
  Manager: "Hiring manager",
  Employee: "The employee",
  IT: "IT and facilities",
};

/* --------------------------------------------------------------- fixtures */

export const onboardings: OnboardingCase[] = [
  {
    id: "ONB-2026-0031",
    employeeId: "w-1007",
    personName: "Natasha Chirwa",
    jobTitle: "Graduate Intern, Process Engineering",
    department: "Manufacturing",
    entity: "Demo Engineering Zambia Ltd",
    branch: "Livingstone Works",
    employmentType: "Intern",
    hiringManager: "Mutale Kabwe",
    startDate: "2026-09-14",
    probationStart: "2026-09-14",
    probationEnd: "2026-12-13",
    status: "Blocked",
    owner: "Thandiwe Banda (HR operations)",
    nextAction: "Chase right-to-work evidence before the contract can be issued",
    dueDate: "2026-08-07",
    tasks: [
      {
        id: "t1",
        label: "Right-to-work evidence verified",
        detail: "Passport and study-permit copy checked against the original.",
        owner: "HR",
        ownerName: "Thandiwe Banda",
        dueDate: "2026-07-24",
        state: "Blocked",
        blocker: "Study permit expires 2026-10-31, before the internship ends. HR must confirm the renewal is lodged before the contract is issued.",
      },
      {
        id: "t2",
        label: "Signed contract returned",
        detail: "Fixed-term intern contract, 6 months, Livingstone Works.",
        owner: "Employee",
        ownerName: "Natasha Chirwa",
        dueDate: "2026-08-14",
        state: "Blocked",
        blocker: "Cannot be issued until right-to-work evidence clears.",
      },
      {
        id: "t3",
        label: "Reference check completed",
        detail: "One academic referee, one placement supervisor.",
        owner: "HR",
        ownerName: "Thandiwe Banda",
        dueDate: "2026-08-21",
        state: "In progress",
      },
      {
        id: "t4",
        label: "Payroll record created",
        detail: "Tax details, bank account and cost centre for the September run.",
        owner: "HR",
        ownerName: "Nalukui Simasiku",
        dueDate: "2026-08-28",
        state: "Not started",
      },
      {
        id: "t5",
        label: "Laptop and safety equipment issued",
        detail: "Standard engineering laptop, safety boots, hearing protection.",
        owner: "IT",
        ownerName: "Livingstone Works facilities",
        dueDate: "2026-09-11",
        state: "Not started",
      },
      {
        id: "t6",
        label: "Accounts and access provisioned",
        detail: "Directory account, plant Wi-Fi, drawing archive (read only).",
        owner: "IT",
        ownerName: "IT service desk",
        dueDate: "2026-09-11",
        state: "Not started",
      },
      {
        id: "t7",
        label: "Site safety induction booked",
        detail: "Mandatory before any access to the shop floor.",
        owner: "Manager",
        ownerName: "Mutale Kabwe",
        dueDate: "2026-09-14",
        state: "Not started",
      },
      {
        id: "t8",
        label: "First-week plan shared",
        detail: "Buddy assigned, first three objectives written down.",
        owner: "Manager",
        ownerName: "Mutale Kabwe",
        dueDate: "2026-09-11",
        state: "Not started",
      },
      {
        id: "t9",
        label: "Bank details and emergency contact submitted",
        detail: "Employee self-service; nothing is paid without them.",
        owner: "Employee",
        ownerName: "Natasha Chirwa",
        dueDate: "2026-08-28",
        state: "Not started",
      },
      {
        id: "t10",
        label: "Probation review scheduled",
        detail: "Review meeting booked for the end of the probation window.",
        owner: "Manager",
        ownerName: "Mutale Kabwe",
        dueDate: "2026-09-21",
        state: "Not started",
      },
    ],
    timeline: [
      { id: "e1", at: "2026-07-06T09:30:00Z", actor: "Thandiwe Banda", event: "Onboarding case opened from accepted offer", after: "Draft" },
      { id: "e2", at: "2026-07-08T11:05:00Z", actor: "System", event: "Checklist generated", reason: "Template: Intern — Zambia", before: "Draft", after: "Ready" },
      { id: "e3", at: "2026-07-22T15:40:00Z", actor: "Thandiwe Banda", event: "Right-to-work check raised as a blocker", reason: "Study permit expires before the internship ends", before: "Ready", after: "Blocked", evidence: { label: "Permit copy (masked)", href: "#" } },
    ],
  },
  {
    id: "ONB-2026-0034",
    personName: "Chipo Mulenga-Banda",
    jobTitle: "Maintenance Technician",
    department: "Operations",
    entity: "Demo Logistics Zambia Ltd",
    branch: "Ndola Plant",
    employmentType: "Permanent",
    hiringManager: "Mutale Kabwe",
    startDate: "2026-08-17",
    probationStart: "2026-08-17",
    probationEnd: "2026-11-16",
    status: "Active",
    owner: "Mutale Kabwe (Hiring manager)",
    nextAction: "Confirm the first-week plan and buddy assignment",
    dueDate: "2026-08-07",
    tasks: [
      {
        id: "t1",
        label: "Right-to-work evidence verified",
        detail: "Zambian passport checked against the original on 2026-07-10.",
        owner: "HR",
        ownerName: "Thandiwe Banda",
        dueDate: "2026-07-10",
        state: "Done",
      },
      {
        id: "t2",
        label: "Signed contract returned",
        detail: "Permanent contract, grade G5, Ndola Plant.",
        owner: "Employee",
        ownerName: "Chipo Mulenga-Banda",
        dueDate: "2026-07-17",
        state: "Done",
      },
      {
        id: "t3",
        label: "Reference check completed",
        detail: "Two previous employers, both returned.",
        owner: "HR",
        ownerName: "Thandiwe Banda",
        dueDate: "2026-07-24",
        state: "Done",
      },
      {
        id: "t4",
        label: "Payroll record created",
        detail: "Cost centre OPS-EIN-02, August pay run.",
        owner: "HR",
        ownerName: "Nalukui Simasiku",
        dueDate: "2026-08-05",
        state: "In progress",
      },
      {
        id: "t5",
        label: "Bank details and emergency contact submitted",
        detail: "Employee self-service.",
        owner: "Employee",
        ownerName: "Chipo Mulenga-Banda",
        dueDate: "2026-07-27",
        state: "Done",
      },
      {
        id: "t6",
        label: "Tooling and PPE issued",
        detail: "Technician tool set, boots, overalls, lockout kit.",
        owner: "IT",
        ownerName: "Ndola Plant facilities",
        dueDate: "2026-08-14",
        state: "In progress",
      },
      {
        id: "t7",
        label: "Accounts and access provisioned",
        detail: "Directory account, maintenance planning system, plant badge.",
        owner: "IT",
        ownerName: "IT service desk",
        dueDate: "2026-08-14",
        state: "Not started",
      },
      {
        id: "t8",
        label: "First-week plan shared",
        detail: "Buddy assigned, first three objectives written down.",
        owner: "Manager",
        ownerName: "Mutale Kabwe",
        dueDate: "2026-07-27",
        state: "Not started",
      },
      {
        id: "t9",
        label: "Site safety induction booked",
        detail: "Mandatory before unaccompanied plant access.",
        owner: "Manager",
        ownerName: "Mutale Kabwe",
        dueDate: "2026-08-17",
        state: "Not started",
      },
      {
        id: "t10",
        label: "Probation review scheduled",
        detail: "Review meeting booked for 2026-11-09.",
        owner: "Manager",
        ownerName: "Mutale Kabwe",
        dueDate: "2026-08-24",
        state: "Not started",
      },
    ],
    timeline: [
      { id: "e1", at: "2026-06-29T08:15:00Z", actor: "Thandiwe Banda", event: "Onboarding case opened from accepted offer", after: "Draft" },
      { id: "e2", at: "2026-07-01T09:00:00Z", actor: "System", event: "Checklist generated", reason: "Template: Permanent — Zambia", before: "Draft", after: "Ready" },
      { id: "e3", at: "2026-07-17T13:20:00Z", actor: "Chipo Mulenga-Banda", event: "Signed contract returned", before: "Ready", after: "Active", evidence: { label: "Signed contract", href: "#" } },
    ],
  },
  {
    id: "ONB-2026-0036",
    personName: "Chalo Mwansa",
    jobTitle: "Payroll Officer",
    department: "Finance",
    entity: "Demo Copperbelt Services Ltd",
    branch: "Chingola Office",
    employmentType: "Permanent",
    hiringManager: "Mutale Kabwe",
    startDate: "2026-09-01",
    probationStart: "2026-09-01",
    probationEnd: "2026-12-01",
    status: "Ready",
    owner: "Thandiwe Banda (HR operations)",
    nextAction: "Issue the contract for signature",
    dueDate: "2026-08-03",
    tasks: [
      {
        id: "t1",
        label: "Right-to-work evidence verified",
        detail: "Zambian national identity card checked on 2026-07-20.",
        owner: "HR",
        ownerName: "Thandiwe Banda",
        dueDate: "2026-07-20",
        state: "Done",
      },
      {
        id: "t2",
        label: "Signed contract returned",
        detail: "Permanent contract, grade G6, Chingola Office.",
        owner: "Employee",
        ownerName: "Chalo Mwansa",
        dueDate: "2026-08-14",
        state: "Not started",
      },
      {
        id: "t3",
        label: "Reference check completed",
        detail: "Two previous employers requested 2026-07-21.",
        owner: "HR",
        ownerName: "Thandiwe Banda",
        dueDate: "2026-08-07",
        state: "In progress",
      },
      {
        id: "t4",
        label: "Segregation-of-duties review",
        detail: "Payroll roles need a second approver named before access is granted.",
        owner: "Manager",
        ownerName: "Mutale Kabwe",
        dueDate: "2026-08-21",
        state: "Not started",
      },
      {
        id: "t5",
        label: "Payroll record created",
        detail: "Cost centre FIN-NBO-01, September pay run.",
        owner: "HR",
        ownerName: "Nalukui Simasiku",
        dueDate: "2026-08-24",
        state: "Not started",
      },
      {
        id: "t6",
        label: "Laptop issued",
        detail: "Standard finance laptop with disk encryption.",
        owner: "IT",
        ownerName: "IT service desk",
        dueDate: "2026-08-28",
        state: "Not started",
      },
      {
        id: "t7",
        label: "Accounts and access provisioned",
        detail: "Directory account, payroll system (read only until induction).",
        owner: "IT",
        ownerName: "IT service desk",
        dueDate: "2026-08-28",
        state: "Not started",
      },
      {
        id: "t8",
        label: "Bank details and emergency contact submitted",
        detail: "Employee self-service.",
        owner: "Employee",
        ownerName: "Chalo Mwansa",
        dueDate: "2026-08-21",
        state: "Not started",
      },
      {
        id: "t9",
        label: "First-week plan shared",
        detail: "Buddy assigned, first three objectives written down.",
        owner: "Manager",
        ownerName: "Mutale Kabwe",
        dueDate: "2026-08-26",
        state: "Not started",
      },
    ],
    timeline: [
      { id: "e1", at: "2026-07-15T07:45:00Z", actor: "Thandiwe Banda", event: "Onboarding case opened from accepted offer", after: "Draft" },
      { id: "e2", at: "2026-07-20T10:10:00Z", actor: "Thandiwe Banda", event: "Right-to-work evidence verified", before: "Draft", after: "Ready" },
    ],
  },
  {
    id: "ONB-2026-0038",
    personName: "Ruben Castellanos-Vega",
    jobTitle: "Process Safety Engineer",
    department: "Operations",
    entity: "Demo Logistics Zambia Ltd",
    branch: "Lusaka HQ",
    employmentType: "Permanent",
    hiringManager: "Mutale Kabwe",
    startDate: "2026-10-05",
    probationStart: "2026-10-05",
    probationEnd: "2027-01-04",
    status: "Draft",
    owner: "Thandiwe Banda (HR operations)",
    nextAction: "Confirm start date with the hiring manager, then release the checklist",
    dueDate: "2026-08-14",
    tasks: [
      {
        id: "t1",
        label: "Start date confirmed with the hiring manager",
        detail: "Candidate has asked to start a fortnight later than the offer states.",
        owner: "Manager",
        ownerName: "Mutale Kabwe",
        dueDate: "2026-08-14",
        state: "In progress",
      },
      {
        id: "t2",
        label: "Right-to-work evidence verified",
        detail: "Spanish passport; EU national, no permit required.",
        owner: "HR",
        ownerName: "Thandiwe Banda",
        dueDate: "2026-08-28",
        state: "Not started",
      },
      {
        id: "t3",
        label: "Signed contract returned",
        detail: "Permanent contract, grade G7, Lusaka HQ.",
        owner: "Employee",
        ownerName: "Ruben Castellanos-Vega",
        dueDate: "2026-09-11",
        state: "Not started",
      },
      {
        id: "t4",
        label: "Relocation support agreed",
        detail: "Temporary accommodation for the first six weeks.",
        owner: "HR",
        ownerName: "Thandiwe Banda",
        dueDate: "2026-09-04",
        state: "Not started",
      },
      {
        id: "t5",
        label: "Accounts and access provisioned",
        detail: "Directory account, incident reporting system, document library.",
        owner: "IT",
        ownerName: "IT service desk",
        dueDate: "2026-10-02",
        state: "Not started",
      },
      {
        id: "t6",
        label: "First-week plan shared",
        detail: "Buddy assigned, first three objectives written down.",
        owner: "Manager",
        ownerName: "Mutale Kabwe",
        dueDate: "2026-09-28",
        state: "Not started",
      },
    ],
    timeline: [
      { id: "e1", at: "2026-07-27T14:00:00Z", actor: "Thandiwe Banda", event: "Onboarding case opened from accepted offer", after: "Draft" },
    ],
  },
  {
    id: "ONB-2025-0119",
    employeeId: "w-1006",
    personName: "Gift Zulu",
    jobTitle: "Yard Contractor (Logistics)",
    department: "Logistics",
    entity: "Demo Copperbelt Services Ltd",
    branch: "Solwezi Yard",
    employmentType: "Contractor",
    hiringManager: "Mutale Kabwe",
    startDate: "2025-11-03",
    probationStart: "2025-11-03",
    probationEnd: "2026-01-02",
    status: "Completed",
    owner: "HR operations",
    nextAction: "Closed — no further action",
    dueDate: "2026-01-09",
    tasks: [
      {
        id: "t1",
        label: "Contractor agreement signed",
        detail: "Twelve-month engagement, Solwezi Yard.",
        owner: "Employee",
        ownerName: "Gift Zulu",
        dueDate: "2025-10-27",
        state: "Done",
      },
      {
        id: "t2",
        label: "Right-to-work evidence verified",
        detail: "Zambian national identity card checked 2025-10-24.",
        owner: "HR",
        ownerName: "Thandiwe Banda",
        dueDate: "2025-10-24",
        state: "Done",
      },
      {
        id: "t3",
        label: "Yard access and badge issued",
        detail: "Gate badge and port access sponsor letter.",
        owner: "IT",
        ownerName: "Solwezi Yard facilities",
        dueDate: "2025-11-03",
        state: "Done",
      },
      {
        id: "t4",
        label: "Site safety induction completed",
        detail: "Yard traffic, lifting and container safety.",
        owner: "Manager",
        ownerName: "Mutale Kabwe",
        dueDate: "2025-11-04",
        state: "Done",
      },
      {
        id: "t5",
        label: "Payments record created",
        detail: "Contractor payment schedule, no payroll deductions.",
        owner: "HR",
        ownerName: "Nalukui Simasiku",
        dueDate: "2025-11-07",
        state: "Done",
      },
      {
        id: "t6",
        label: "Probation review held",
        detail: "Engagement confirmed on 2026-01-08.",
        owner: "Manager",
        ownerName: "Mutale Kabwe",
        dueDate: "2026-01-09",
        state: "Done",
      },
    ],
    timeline: [
      { id: "e1", at: "2025-10-20T08:00:00Z", actor: "Thandiwe Banda", event: "Onboarding case opened", after: "Draft" },
      { id: "e2", at: "2025-11-03T06:30:00Z", actor: "System", event: "Employee started", before: "Ready", after: "Active" },
      { id: "e3", at: "2026-01-08T12:00:00Z", actor: "Mutale Kabwe", event: "Probation review completed", reason: "Engagement confirmed", before: "Active", after: "Completed" },
    ],
  },
  {
    id: "ONB-2026-0022",
    personName: "Nadia El Mansouri",
    jobTitle: "Logistics Planner",
    department: "Logistics",
    entity: "Demo Logistics Zambia Ltd",
    branch: "Kitwe Depot",
    employmentType: "Permanent",
    hiringManager: "Emmanuel Sakala",
    startDate: "2026-06-01",
    probationStart: "2026-06-01",
    probationEnd: "2026-08-31",
    status: "Cancelled",
    owner: "HR operations",
    nextAction: "Closed — offer withdrawn by the candidate",
    dueDate: "2026-05-15",
    tasks: [
      {
        id: "t1",
        label: "Right-to-work evidence verified",
        detail: "Checked 2026-05-04.",
        owner: "HR",
        ownerName: "Thandiwe Banda",
        dueDate: "2026-05-04",
        state: "Done",
      },
      {
        id: "t2",
        label: "Signed contract returned",
        detail: "Never returned — candidate accepted another role.",
        owner: "Employee",
        ownerName: "Nadia El Mansouri",
        dueDate: "2026-05-15",
        state: "Not started",
      },
      {
        id: "t3",
        label: "Accounts and access provisioned",
        detail: "Cancelled before provisioning started.",
        owner: "IT",
        ownerName: "IT service desk",
        dueDate: "2026-05-29",
        state: "Not started",
      },
    ],
    timeline: [
      { id: "e1", at: "2026-04-28T09:00:00Z", actor: "Thandiwe Banda", event: "Onboarding case opened from accepted offer", after: "Draft" },
      { id: "e2", at: "2026-05-18T16:20:00Z", actor: "Thandiwe Banda", event: "Case cancelled", reason: "Candidate withdrew — accepted another offer", before: "Ready", after: "Cancelled" },
    ],
  },
];

export const offboardings: OffboardingCase[] = [
  {
    id: "OFF-2026-0014",
    employeeId: "w-1004",
    personName: "Kondwani Mwanza",
    jobTitle: "Welding Technician",
    department: "Manufacturing",
    entity: "Demo Engineering Zambia Ltd",
    branch: "Livingstone Works",
    reason: "Fixed-term expiry",
    reasonDetail: "Fixed-term contract ends and will not be renewed; the position closes with the Livingstone line change.",
    noticeGivenOn: "2026-06-30",
    noticePeriod: "30 days (statutory minimum, Zambia)",
    lastWorkingDate: "2026-08-31",
    finalPayRun: "September 2026",
    rehireEligible: "Eligible",
    status: "Active",
    owner: "Thandiwe Banda (HR operations)",
    nextAction: "Clear the two outstanding blockers before the final pay cut-off",
    dueDate: "2026-08-24",
    clearance: [
      {
        id: "c1",
        category: "Assets returned",
        label: "Welding set and calibrated tooling returned",
        detail: "Serial WLD-2214 and torque set TQ-118, both issued 2024-02-01.",
        owner: "Employee",
        ownerName: "Kondwani Mwanza",
        dueDate: "2026-08-28",
        state: "Not started",
      },
      {
        id: "c2",
        category: "Assets returned",
        label: "Laptop and plant badge returned",
        detail: "Asset LT-4471; badge deactivates automatically on the last working date.",
        owner: "IT",
        ownerName: "Livingstone Works facilities",
        dueDate: "2026-08-31",
        state: "Blocked",
        blocker: "Laptop is with the workshop for a screen repair; IT must recall it from the supplier before 24 August.",
      },
      {
        id: "c3",
        category: "Access revoked",
        label: "Directory and plant systems access revoked",
        detail: "Scheduled for 18:00 CET on the last working date.",
        owner: "IT",
        ownerName: "IT service desk",
        dueDate: "2026-08-31",
        state: "Not started",
      },
      {
        id: "c4",
        category: "Access revoked",
        label: "Shared mailbox and approval rights reassigned",
        detail: "Line-side quality approvals must move to a named replacement.",
        owner: "Manager",
        ownerName: "Mutale Kabwe",
        dueDate: "2026-08-21",
        state: "In progress",
      },
      {
        id: "c5",
        category: "Knowledge handover",
        label: "Handover note accepted by the manager",
        detail: "Weld procedure records, jig set-up notes and open non-conformances.",
        owner: "Manager",
        ownerName: "Mutale Kabwe",
        dueDate: "2026-08-21",
        state: "In progress",
      },
      {
        id: "c6",
        category: "Knowledge handover",
        label: "Certification records copied to the employee",
        detail: "Welding certifications the employee is entitled to take with them.",
        owner: "HR",
        ownerName: "Thandiwe Banda",
        dueDate: "2026-08-28",
        state: "Not started",
      },
      {
        id: "c7",
        category: "Outstanding advances",
        label: "Training advance recovered",
        detail: "K640 remaining on the 2025 certification advance.",
        owner: "HR",
        ownerName: "Nalukui Simasiku",
        dueDate: "2026-08-24",
        state: "Blocked",
        blocker: "Recovery exceeds the statutory deduction limit for a single pay period. Payroll needs an authorised repayment agreement or a write-off decision.",
      },
      {
        id: "c8",
        category: "Final pay",
        label: "Final settlement calculated",
        detail: "Untaken leave (3.5 days), notice pay and severance where due.",
        owner: "HR",
        ownerName: "Nalukui Simasiku",
        dueDate: "2026-09-04",
        state: "Not started",
      },
      {
        id: "c9",
        category: "Final pay",
        label: "Separation documents issued",
        detail: "Service certificate and tax year-end statement.",
        owner: "HR",
        ownerName: "Thandiwe Banda",
        dueDate: "2026-09-11",
        state: "Not started",
      },
    ],
    policy: [
      { id: "p1", label: "Notice period observed", outcome: "pass", detail: "Two months given on 30 June for a 31 August end date." },
      { id: "p2", label: "Assets fully returned", outcome: "fail", detail: "Laptop LT-4471 is still with the repair supplier." },
      { id: "p3", label: "Advances cleared", outcome: "fail", detail: "K640 training advance outstanding and above the single-period deduction limit." },
      { id: "p4", label: "Untaken leave settled", outcome: "warn", detail: "3.5 days to pay; confirm before the September run closes." },
      { id: "p5", label: "Knowledge handover accepted", outcome: "warn", detail: "Handover note drafted, not yet accepted by the manager." },
    ],
    conflicts: [
      "Final pay cut-off is 4 September; two blockers must clear before then or the settlement slips a month.",
      "The employee has an approved attendance correction (AT-2026-1180) still to be included in a pay run.",
    ],
    timeline: [
      { id: "e1", at: "2026-06-30T09:00:00Z", actor: "Mutale Kabwe", event: "Non-renewal confirmed to the employee", after: "Draft" },
      { id: "e2", at: "2026-07-01T08:30:00Z", actor: "Thandiwe Banda", event: "Separation case opened", reason: "Fixed-term expiry", before: "Draft", after: "Ready" },
      { id: "e3", at: "2026-07-14T10:15:00Z", actor: "System", event: "Clearance checklist generated", reason: "Template: Fixed-term expiry — Zambia", before: "Ready", after: "Active" },
      { id: "e4", at: "2026-07-27T11:45:00Z", actor: "Nalukui Simasiku", event: "Advance recovery raised as a blocker", reason: "Above statutory single-period deduction limit" },
    ],
  },
  {
    id: "OFF-2026-0016",
    employeeId: "w-1003",
    personName: "Nalukui Simasiku",
    jobTitle: "Payroll Analyst",
    department: "Finance",
    entity: "Demo Copperbelt Services Ltd",
    branch: "Chingola Office",
    reason: "Resignation",
    reasonDetail: "Resigned to take up full-time postgraduate study from October.",
    noticeGivenOn: "2026-07-21",
    noticePeriod: "One month (contractual)",
    lastWorkingDate: "2026-08-31",
    finalPayRun: "September 2026",
    rehireEligible: "Eligible",
    status: "Ready",
    owner: "Thandiwe Banda (HR operations)",
    nextAction: "Name a payroll successor before access is revoked",
    dueDate: "2026-08-07",
    clearance: [
      {
        id: "c1",
        category: "Access revoked",
        label: "Payroll system access transferred",
        detail: "Sole approver for the Copperbelt pay run; a successor must be named first.",
        owner: "Manager",
        ownerName: "Mutale Kabwe",
        dueDate: "2026-08-07",
        state: "In progress",
      },
      {
        id: "c2",
        category: "Access revoked",
        label: "Directory and email access revoked",
        detail: "Scheduled for 17:00 EAT on the last working date.",
        owner: "IT",
        ownerName: "IT service desk",
        dueDate: "2026-08-31",
        state: "Not started",
      },
      {
        id: "c3",
        category: "Assets returned",
        label: "Laptop and access card returned",
        detail: "Asset LT-3390 and Chingola Office card.",
        owner: "Employee",
        ownerName: "Nalukui Simasiku",
        dueDate: "2026-08-31",
        state: "Not started",
      },
      {
        id: "c4",
        category: "Knowledge handover",
        label: "Pay run runbook handed over",
        detail: "Monthly close steps, statutory filings and the year-end calendar.",
        owner: "Employee",
        ownerName: "Nalukui Simasiku",
        dueDate: "2026-08-21",
        state: "In progress",
      },
      {
        id: "c5",
        category: "Outstanding advances",
        label: "No outstanding advances confirmed",
        detail: "Checked against the ledger on 2026-07-24.",
        owner: "HR",
        ownerName: "Thandiwe Banda",
        dueDate: "2026-07-24",
        state: "Done",
      },
      {
        id: "c6",
        category: "Final pay",
        label: "Final settlement calculated",
        detail: "Untaken leave (21 days) and pro-rata salary to the last working date.",
        owner: "HR",
        ownerName: "Thandiwe Banda",
        dueDate: "2026-09-04",
        state: "Not started",
      },
    ],
    policy: [
      { id: "p1", label: "Notice period observed", outcome: "pass", detail: "One month given on 21 July for a 31 August end date." },
      { id: "p2", label: "Segregation of duties", outcome: "warn", detail: "Sole payroll approver for Zambia; a successor must be named before access is revoked." },
      { id: "p3", label: "Assets fully returned", outcome: "warn", detail: "Two items outstanding, both due on the last working date." },
      { id: "p4", label: "Advances cleared", outcome: "pass", detail: "Nothing outstanding on the ledger." },
      { id: "p5", label: "Untaken leave settled", outcome: "warn", detail: "21 days to pay; the largest single item in the settlement." },
    ],
    conflicts: [
      "No named successor for the Copperbelt pay run approval — revoking access on 31 August would leave the September run unapprovable.",
    ],
    timeline: [
      { id: "e1", at: "2026-07-21T07:10:00Z", actor: "Nalukui Simasiku", event: "Resignation submitted", after: "Draft" },
      { id: "e2", at: "2026-07-22T09:00:00Z", actor: "Mutale Kabwe", event: "Resignation acknowledged", reason: "Leaving for postgraduate study" },
      { id: "e3", at: "2026-07-23T08:40:00Z", actor: "Thandiwe Banda", event: "Clearance checklist generated", reason: "Template: Resignation — Zambia", before: "Draft", after: "Ready" },
    ],
  },
  {
    id: "OFF-2026-0009",
    employeeId: "w-1006",
    personName: "Gift Zulu",
    jobTitle: "Yard Contractor (Logistics)",
    department: "Logistics",
    entity: "Demo Copperbelt Services Ltd",
    branch: "Solwezi Yard",
    reason: "End of engagement",
    reasonDetail: "Twelve-month contractor engagement ends; no extension requested by the yard.",
    noticeGivenOn: "2026-07-03",
    noticePeriod: "Thirty days (contractual)",
    lastWorkingDate: "2026-08-02",
    finalPayRun: "August 2026",
    rehireEligible: "Eligible",
    status: "Blocked",
    owner: "Mutale Kabwe (Manager)",
    nextAction: "Recover the yard equipment and cancel the port sponsorship",
    dueDate: "2026-08-02",
    clearance: [
      {
        id: "c1",
        category: "Assets returned",
        label: "Yard handset and lifting gear returned",
        detail: "Radio RD-882 and two lifting slings issued at the start of the engagement.",
        owner: "Employee",
        ownerName: "Gift Zulu",
        dueDate: "2026-07-27",
        state: "Blocked",
        blocker: "Equipment is on a vessel that returns to Solwezi on 5 August, after the last working date.",
      },
      {
        id: "c2",
        category: "Access revoked",
        label: "Port authority sponsorship withdrawn",
        detail: "External access sponsored by Demo Organization; withdrawal must be filed with the port authority.",
        owner: "HR",
        ownerName: "Thandiwe Banda",
        dueDate: "2026-07-26",
        state: "Blocked",
        blocker: "Withdrawal filing is two days overdue. External port access stays live until the authority confirms.",
      },
      {
        id: "c3",
        category: "Access revoked",
        label: "Yard badge deactivated",
        detail: "Automatic at 18:00 EAT on the last working date.",
        owner: "IT",
        ownerName: "Solwezi Yard facilities",
        dueDate: "2026-08-02",
        state: "Not started",
      },
      {
        id: "c4",
        category: "Knowledge handover",
        label: "Open container movements handed over",
        detail: "Eleven movements still open against the August schedule.",
        owner: "Manager",
        ownerName: "Mutale Kabwe",
        dueDate: "2026-07-31",
        state: "In progress",
      },
      {
        id: "c5",
        category: "Outstanding advances",
        label: "Fuel float reconciled",
        detail: "ZMW 3,700 float issued for port runs.",
        owner: "HR",
        ownerName: "Nalukui Simasiku",
        dueDate: "2026-07-31",
        state: "In progress",
      },
      {
        id: "c6",
        category: "Final pay",
        label: "Final contractor invoice settled",
        detail: "Includes the approved attendance correction AT-2026-1174.",
        owner: "HR",
        ownerName: "Nalukui Simasiku",
        dueDate: "2026-08-05",
        state: "Not started",
      },
    ],
    policy: [
      { id: "p1", label: "Notice period observed", outcome: "pass", detail: "Thirty days given on 3 July for a 2 August end date." },
      { id: "p2", label: "Assets fully returned", outcome: "fail", detail: "Radio and lifting gear return after the last working date." },
      { id: "p3", label: "External access withdrawn", outcome: "fail", detail: "Port authority sponsorship withdrawal is two days overdue." },
      { id: "p4", label: "Advances cleared", outcome: "warn", detail: "Fuel float of ZMW 3,700 not yet reconciled." },
      { id: "p5", label: "Untaken leave settled", outcome: "pass", detail: "Contractor engagement; no leave entitlement to settle." },
    ],
    conflicts: [
      "External port access remains live after the last working date until the authority confirms withdrawal.",
      "Eleven open container movements are still assigned to this person for August.",
    ],
    timeline: [
      { id: "e1", at: "2026-07-03T06:00:00Z", actor: "Mutale Kabwe", event: "Non-extension confirmed", after: "Draft" },
      { id: "e2", at: "2026-07-04T08:20:00Z", actor: "Thandiwe Banda", event: "Clearance checklist generated", reason: "Template: Contractor end of engagement — Zambia", before: "Draft", after: "Active" },
      { id: "e3", at: "2026-07-28T13:05:00Z", actor: "System", event: "Case blocked", reason: "Two clearance items overdue or unachievable before the last working date", before: "Active", after: "Blocked" },
    ],
  },
  {
    id: "OFF-2026-0018",
    employeeId: "w-1008",
    personName: "Emmanuel Sakala",
    jobTitle: "Depot Supervisor",
    department: "Logistics",
    entity: "Demo Logistics Zambia Ltd",
    branch: "Kitwe Depot",
    reason: "Retirement",
    reasonDetail: "Planned retirement after fifteen years of service; phased handover agreed with the depot.",
    noticeGivenOn: "2026-07-15",
    noticePeriod: "Six months (agreed)",
    lastWorkingDate: "2027-01-15",
    finalPayRun: "January 2027",
    rehireEligible: "Eligible",
    status: "Draft",
    owner: "Thandiwe Banda (HR operations)",
    nextAction: "Agree the phased handover plan with the depot before releasing the checklist",
    dueDate: "2026-09-01",
    clearance: [
      {
        id: "c1",
        category: "Knowledge handover",
        label: "Depot handover plan agreed",
        detail: "Six-month phased handover to a named successor.",
        owner: "Manager",
        ownerName: "Mutale Kabwe",
        dueDate: "2026-09-01",
        state: "In progress",
      },
      {
        id: "c2",
        category: "Final pay",
        label: "Pension commencement confirmed with the provider",
        detail: "Scheme notification must be lodged three months before the last working date.",
        owner: "HR",
        ownerName: "Thandiwe Banda",
        dueDate: "2026-10-15",
        state: "Not started",
      },
      {
        id: "c3",
        category: "Assets returned",
        label: "Depot vehicle and keys returned",
        detail: "Van GRN-114 and depot key set.",
        owner: "Employee",
        ownerName: "Emmanuel Sakala",
        dueDate: "2027-01-15",
        state: "Not started",
      },
      {
        id: "c4",
        category: "Access revoked",
        label: "Depot systems access revoked",
        detail: "Scheduled for the last working date.",
        owner: "IT",
        ownerName: "IT service desk",
        dueDate: "2027-01-15",
        state: "Not started",
      },
      {
        id: "c5",
        category: "Outstanding advances",
        label: "No outstanding advances confirmed",
        detail: "Checked against the ledger on 2026-07-16.",
        owner: "HR",
        ownerName: "Thandiwe Banda",
        dueDate: "2026-07-16",
        state: "Done",
      },
    ],
    policy: [
      { id: "p1", label: "Notice period observed", outcome: "pass", detail: "Six months agreed, well beyond the contractual minimum." },
      { id: "p2", label: "Succession named", outcome: "warn", detail: "No successor named for the depot supervisor position yet." },
      { id: "p3", label: "Untaken leave settled", outcome: "warn", detail: "27.5 days on the balance; agree how much is taken before the last working date." },
      { id: "p4", label: "Advances cleared", outcome: "pass", detail: "Nothing outstanding on the ledger." },
    ],
    conflicts: [
      "27.5 days of untaken leave against a six-month notice period — plan the drawdown or the final settlement will be large.",
    ],
    timeline: [
      { id: "e1", at: "2026-07-15T10:00:00Z", actor: "Emmanuel Sakala", event: "Retirement intention confirmed in writing", after: "Draft" },
    ],
  },
  {
    id: "OFF-2026-0007",
    personName: "Grace Nyirenda",
    jobTitle: "Depot Planner",
    department: "Logistics",
    entity: "Demo Logistics Zambia Ltd",
    branch: "Kitwe Depot",
    reason: "Resignation",
    reasonDetail: "Resigned to join a customer in the same sector; non-solicitation reminder issued.",
    noticeGivenOn: "2026-03-31",
    noticePeriod: "One month (contractual)",
    lastWorkingDate: "2026-04-30",
    finalPayRun: "May 2026",
    rehireEligible: "Eligible",
    status: "Completed",
    owner: "HR operations",
    nextAction: "Closed — final settlement paid in the May 2026 run",
    dueDate: "2026-05-27",
    clearance: [
      { id: "c1", category: "Assets returned", label: "Laptop and depot badge returned", detail: "Both received 2026-04-30.", owner: "Employee", ownerName: "Grace Nyirenda", dueDate: "2026-04-30", state: "Done" },
      { id: "c2", category: "Access revoked", label: "All systems access revoked", detail: "Completed 2026-04-30 at 18:00 CET.", owner: "IT", ownerName: "IT service desk", dueDate: "2026-04-30", state: "Done" },
      { id: "c3", category: "Knowledge handover", label: "Planning handover accepted", detail: "Accepted by Emmanuel Sakala 2026-04-28.", owner: "Manager", ownerName: "Emmanuel Sakala", dueDate: "2026-04-28", state: "Done" },
      { id: "c4", category: "Outstanding advances", label: "Travel advance recovered", detail: "K180 recovered in the May run.", owner: "HR", ownerName: "Nalukui Simasiku", dueDate: "2026-05-20", state: "Done" },
      { id: "c5", category: "Final pay", label: "Final settlement paid", detail: "Untaken leave (6 days) and pro-rata salary.", owner: "HR", ownerName: "Nalukui Simasiku", dueDate: "2026-05-27", state: "Done" },
    ],
    policy: [
      { id: "p1", label: "Notice period observed", outcome: "pass", detail: "One month given and worked in full." },
      { id: "p2", label: "Assets fully returned", outcome: "pass", detail: "All items received on the last working date." },
      { id: "p3", label: "Advances cleared", outcome: "pass", detail: "K180 recovered in the May run." },
      { id: "p4", label: "Untaken leave settled", outcome: "pass", detail: "6 days paid in the final settlement." },
    ],
    conflicts: [],
    timeline: [
      { id: "e1", at: "2026-03-31T09:00:00Z", actor: "Grace Nyirenda", event: "Resignation submitted", after: "Draft" },
      { id: "e2", at: "2026-04-30T16:00:00Z", actor: "Thandiwe Banda", event: "Clearance completed", before: "Active", after: "Completed" },
      { id: "e3", at: "2026-05-27T09:00:00Z", actor: "Nalukui Simasiku", event: "Final settlement paid", evidence: { label: "Settlement statement", href: "#" } },
    ],
  },
  {
    id: "OFF-2026-0012",
    employeeId: "w-1005",
    personName: "Thandiwe Banda",
    jobTitle: "HR Operations Specialist",
    department: "People",
    entity: "Demo Logistics Zambia Ltd",
    branch: "Ndola Plant",
    reason: "Resignation",
    reasonDetail: "Resignation withdrawn on 2026-06-12 after a revised part-time arrangement was agreed.",
    noticeGivenOn: "2026-06-02",
    noticePeriod: "One month (contractual)",
    lastWorkingDate: "2026-07-02",
    finalPayRun: "Not applicable",
    rehireEligible: "Eligible",
    status: "Cancelled",
    owner: "HR operations",
    nextAction: "Closed — resignation withdrawn, employment continues unchanged",
    dueDate: "2026-06-12",
    clearance: [
      { id: "c1", category: "Assets returned", label: "Laptop return scheduled", detail: "Cancelled — employment continues.", owner: "Employee", ownerName: "Thandiwe Banda", dueDate: "2026-07-02", state: "Not started" },
      { id: "c2", category: "Access revoked", label: "Access revocation scheduled", detail: "Cancelled before it ran; no access was lost.", owner: "IT", ownerName: "IT service desk", dueDate: "2026-07-02", state: "Not started" },
      { id: "c3", category: "Final pay", label: "Final settlement calculation", detail: "Cancelled — no settlement due.", owner: "HR", ownerName: "Nalukui Simasiku", dueDate: "2026-07-24", state: "Not started" },
    ],
    policy: [
      { id: "p1", label: "Withdrawal accepted in writing", outcome: "pass", detail: "Accepted by the manager on 2026-06-12." },
      { id: "p2", label: "Access left intact", outcome: "pass", detail: "Revocation cancelled before the scheduled run." },
    ],
    conflicts: [],
    timeline: [
      { id: "e1", at: "2026-06-02T08:00:00Z", actor: "Thandiwe Banda", event: "Resignation submitted", after: "Draft" },
      { id: "e2", at: "2026-06-12T14:30:00Z", actor: "Mutale Kabwe", event: "Resignation withdrawn and accepted", reason: "Revised part-time arrangement agreed", before: "Ready", after: "Cancelled" },
    ],
  },
];

export const movements: MovementRecord[] = [
  {
    id: "MOV-2026-0051",
    employeeId: "w-1001",
    type: "Promotion",
    reason: "Took on planning accountability for the Ndola line during the 2026 rebuild.",
    effectiveFrom: "2026-09-01",
    current: {
      jobTitle: "Senior Maintenance Planning Coordinator",
      grade: "G7",
      department: "Operations",
      entity: "Demo Logistics Zambia Ltd",
      branch: "Lusaka HQ",
      manager: "Mutale Kabwe",
      positionId: "POS-OPS-0142",
      costCentre: "OPS-ROT-01",
    },
    proposed: {
      jobTitle: "Maintenance Planning Lead",
      grade: "G8",
      department: "Operations",
      entity: "Demo Logistics Zambia Ltd",
      branch: "Lusaka HQ",
      manager: "Mutale Kabwe",
      positionId: "POS-OPS-0207",
      costCentre: "OPS-ROT-01",
    },
    headline: "G7 → G8, Maintenance Planning Lead",
    status: "Approved",
    owner: "Payroll",
    nextAction: "Apply on the effective date in the September pay run",
    dueDate: "2026-09-01",
    raisedBy: "Mutale Kabwe",
    raisedOn: "2026-07-02",
    impacts: [
      { id: "i1", area: "Reporting line", summary: "Unchanged", detail: "Continues to report to Mutale Kabwe. Two planners move under this position on the effective date." },
      { id: "i2", area: "Payroll", summary: "Grade change from the September run", detail: "Salary band moves to G8. Pro-rating is not needed — the effective date is the first of the month." },
      { id: "i3", area: "Access", summary: "Two additions", detail: "Planning approvals up to K25,000 and access to the maintenance budget dashboard." },
      { id: "i4", area: "Position", summary: "POS-OPS-0142 → POS-OPS-0207", detail: "The old position closes on 31 August; the new position is already on the approved establishment." },
    ],
    policy: [
      { id: "p1", label: "Position on establishment", outcome: "pass", detail: "POS-OPS-0207 approved in the 2026 plan." },
      { id: "p2", label: "Grade step within policy", outcome: "pass", detail: "Single-grade step; no exception needed." },
      { id: "p3", label: "Effective date alignment", outcome: "pass", detail: "First of the month — aligns with the pay period." },
    ],
    conflicts: [],
    timeline: [
      { id: "e1", at: "2026-07-02T09:20:00Z", actor: "Mutale Kabwe", event: "Movement raised", after: "Submitted" },
      { id: "e2", at: "2026-07-06T11:00:00Z", actor: "Thandiwe Banda", event: "Establishment and grade checked", before: "Submitted", after: "In review" },
      { id: "e3", at: "2026-07-09T15:30:00Z", actor: "Mutale Kabwe", event: "Approved as a pending future change", reason: "Line rebuild accountability", before: "In review", after: "Approved" },
    ],
  },
  {
    id: "MOV-2026-0047",
    employeeId: "w-1005",
    type: "Transfer",
    reason: "HR operations consolidating into Lusaka HQ; the employee asked to move with the team.",
    effectiveFrom: "2026-08-01",
    current: {
      jobTitle: "HR Operations Specialist",
      grade: "G5",
      department: "People",
      entity: "Demo Logistics Zambia Ltd",
      branch: "Ndola Plant",
      manager: "Mutale Kabwe",
      positionId: "POS-PPL-0031",
      costCentre: "PPL-EIN-01",
    },
    proposed: {
      jobTitle: "HR Operations Specialist",
      grade: "G5",
      department: "People",
      entity: "Demo Logistics Zambia Ltd",
      branch: "Lusaka HQ",
      manager: "Mutale Kabwe",
      positionId: "POS-PPL-0031",
      costCentre: "PPL-ROT-01",
    },
    headline: "Ndola Plant → Lusaka HQ",
    status: "In review",
    owner: "Thandiwe Banda (HR operations)",
    nextAction: "Confirm the commuting allowance change with payroll",
    dueDate: "2026-07-30",
    raisedBy: "Thandiwe Banda",
    raisedOn: "2026-07-11",
    impacts: [
      { id: "i1", area: "Reporting line", summary: "Unchanged", detail: "Continues to report to Mutale Kabwe." },
      { id: "i2", area: "Payroll", summary: "Commuting allowance recalculates", detail: "Home-to-office distance changes; the allowance is recalculated from the effective date, mid-period." },
      { id: "i3", area: "Access", summary: "Badge scope changes", detail: "Lusaka HQ badge added, Ndola Plant badge retained for two months during handover." },
      { id: "i4", area: "Position", summary: "Same position, new cost centre", detail: "POS-PPL-0031 moves from PPL-EIN-01 to PPL-ROT-01." },
    ],
    policy: [
      { id: "p1", label: "Position on establishment", outcome: "pass", detail: "Position unchanged; only the cost centre moves." },
      { id: "p2", label: "Effective date alignment", outcome: "pass", detail: "First of the month — aligns with the pay period." },
      { id: "p3", label: "Allowance recalculation", outcome: "warn", detail: "Commuting allowance must be confirmed before the August run closes on 18 August." },
    ],
    conflicts: ["Part-time working pattern (0.6 FTE) is unchanged but must be restated on the new cost centre."],
    timeline: [
      { id: "e1", at: "2026-07-11T10:00:00Z", actor: "Thandiwe Banda", event: "Movement raised", after: "Submitted" },
      { id: "e2", at: "2026-07-15T08:45:00Z", actor: "System", event: "Allowance impact detected", reason: "Home-to-office distance changed", before: "Submitted", after: "In review" },
    ],
  },
  {
    id: "MOV-2026-0044",
    employeeId: "w-1003",
    type: "Secondment",
    reason: "Six-month secondment to cover the Lusaka payroll close during a system migration.",
    effectiveFrom: "2026-10-01",
    effectiveTo: "2027-03-31",
    current: {
      jobTitle: "Payroll Analyst",
      grade: "G6",
      department: "Finance",
      entity: "Demo Copperbelt Services Ltd",
      branch: "Chingola Office",
      manager: "Mutale Kabwe",
      positionId: "POS-FIN-0088",
      costCentre: "FIN-NBO-01",
    },
    proposed: {
      jobTitle: "Payroll Analyst (seconded)",
      grade: "G6",
      department: "Finance",
      entity: "Demo Logistics Zambia Ltd",
      branch: "Lusaka HQ",
      manager: "Mutale Kabwe",
      positionId: "POS-FIN-0088",
      costCentre: "FIN-ROT-02",
    },
    headline: "Chingola Office → Lusaka HQ, six months",
    status: "Submitted",
    owner: "Thandiwe Banda (HR operations)",
    nextAction: "Check work permit lead time before approving",
    dueDate: "2026-08-05",
    raisedBy: "Mutale Kabwe",
    raisedOn: "2026-07-18",
    impacts: [
      { id: "i1", area: "Reporting line", summary: "Dotted line to the host entity", detail: "Home manager stays Mutale Kabwe; day-to-day direction comes from the Lusaka payroll lead." },
      { id: "i2", area: "Payroll", summary: "Paid from the home entity, recharged to the host", detail: "Stays on the Zambian payroll; the host entity is recharged monthly for the secondment period." },
      { id: "i3", area: "Access", summary: "Host systems added for the period", detail: "Lusaka payroll access granted for the secondment window and expires automatically on 31 March 2027." },
      { id: "i4", area: "Position", summary: "Home position held open", detail: "POS-FIN-0088 is reserved for the return date; it cannot be filled permanently." },
    ],
    policy: [
      { id: "p1", label: "Site clearance", outcome: "warn", detail: "Site induction and safety clearance take 3–4 weeks; only 5 weeks remain before the effective date." },
      { id: "p2", label: "Home position reserved", outcome: "pass", detail: "Return date recorded; the position cannot be filled permanently." },
      { id: "p3", label: "Cross-entity recharge agreed", outcome: "pass", detail: "Recharge rate confirmed with both finance teams." },
    ],
    conflicts: [
      "This employee has a separation case in progress (OFF-2026-0016) with a last working date of 31 August 2026 — the secondment cannot proceed unless the resignation is withdrawn.",
    ],
    timeline: [
      { id: "e1", at: "2026-07-18T12:30:00Z", actor: "Mutale Kabwe", event: "Movement raised", after: "Submitted" },
      { id: "e2", at: "2026-07-19T07:15:00Z", actor: "System", event: "Conflict detected with an open separation case", reason: "OFF-2026-0016 last working date 2026-08-31" },
    ],
  },
  {
    id: "MOV-2026-0040",
    employeeId: "w-1008",
    type: "Manager change",
    reason: "Depot supervision realigned to the Logistics lead ahead of the phased retirement handover.",
    effectiveFrom: "2026-08-15",
    current: {
      jobTitle: "Depot Supervisor",
      grade: "G6",
      department: "Logistics",
      entity: "Demo Logistics Zambia Ltd",
      branch: "Kitwe Depot",
      manager: "Mutale Kabwe",
      positionId: "POS-LOG-0019",
      costCentre: "LOG-GRN-01",
    },
    proposed: {
      jobTitle: "Depot Supervisor",
      grade: "G6",
      department: "Logistics",
      entity: "Demo Logistics Zambia Ltd",
      branch: "Kitwe Depot",
      manager: "Chanda Mwansa-Chileshe",
      positionId: "POS-LOG-0019",
      costCentre: "LOG-GRN-01",
    },
    headline: "Manager: Mutale Kabwe → Chanda Mwansa-Chileshe",
    status: "Approved",
    owner: "HR operations",
    nextAction: "Apply on the effective date and re-point open approvals",
    dueDate: "2026-08-15",
    raisedBy: "Mutale Kabwe",
    raisedOn: "2026-06-24",
    impacts: [
      { id: "i1", area: "Reporting line", summary: "New manager from 15 August", detail: "Open approvals raised before the effective date stay with Mutale Kabwe; anything raised after moves to the new manager." },
      { id: "i2", area: "Payroll", summary: "No change", detail: "Grade, cost centre and pay elements are all unchanged." },
      { id: "i3", area: "Access", summary: "Approval routing changes", detail: "Leave and attendance approvals for the depot re-point to the new manager on the effective date." },
      { id: "i4", area: "Position", summary: "Unchanged", detail: "POS-LOG-0019 stays in place; only the reporting relationship changes." },
    ],
    policy: [
      { id: "p1", label: "Manager has capacity", outcome: "pass", detail: "New manager has 6 direct reports; the policy limit is 10." },
      { id: "p2", label: "No conflict of interest declared", outcome: "pass", detail: "Checked against the declared-interests register." },
      { id: "p3", label: "Effective date alignment", outcome: "warn", detail: "Mid-month effective date; approvals in flight on 15 August need re-pointing by hand." },
    ],
    conflicts: [],
    timeline: [
      { id: "e1", at: "2026-06-24T09:00:00Z", actor: "Mutale Kabwe", event: "Movement raised", after: "Submitted" },
      { id: "e2", at: "2026-06-27T10:30:00Z", actor: "Thandiwe Banda", event: "Span-of-control check passed", before: "Submitted", after: "In review" },
      { id: "e3", at: "2026-07-01T08:00:00Z", actor: "Thandiwe Banda", event: "Approved as a pending future change", before: "In review", after: "Approved" },
    ],
  },
  {
    id: "MOV-2026-0038",
    employeeId: "w-1004",
    type: "Transfer",
    reason: "Requested move to the Ndola Plant fabrication cell after the Livingstone line change.",
    effectiveFrom: "2026-09-01",
    current: {
      jobTitle: "Welding Technician",
      grade: "G4",
      department: "Manufacturing",
      entity: "Demo Engineering Zambia Ltd",
      branch: "Livingstone Works",
      manager: "Mutale Kabwe",
      positionId: "POS-MFG-0064",
      costCentre: "MFG-HAM-01",
    },
    proposed: {
      jobTitle: "Welding Technician",
      grade: "G4",
      department: "Manufacturing",
      entity: "Demo Logistics Zambia Ltd",
      branch: "Ndola Plant",
      manager: "Mutale Kabwe",
      positionId: "POS-MFG-0071",
      costCentre: "MFG-EIN-01",
    },
    headline: "Livingstone Works → Ndola Plant (cross-entity)",
    status: "Returned",
    owner: "Mutale Kabwe (Manager)",
    nextAction: "Confirm the receiving position is funded, then resubmit",
    dueDate: "2026-08-07",
    raisedBy: "Mutale Kabwe",
    raisedOn: "2026-06-15",
    impacts: [
      { id: "i1", area: "Reporting line", summary: "Unchanged", detail: "Continues to report to Mutale Kabwe." },
      { id: "i2", area: "Payroll", summary: "New employing entity", detail: "Cross-entity move: a new employment contract, new tax treatment and a new payroll record are required." },
      { id: "i3", area: "Access", summary: "Site access swaps", detail: "Livingstone Works badge closes; Ndola Plant badge opens on the effective date." },
      { id: "i4", area: "Position", summary: "POS-MFG-0064 → POS-MFG-0071", detail: "The receiving position is not yet funded in the 2026 establishment." },
    ],
    policy: [
      { id: "p1", label: "Position on establishment", outcome: "fail", detail: "POS-MFG-0071 is not funded in the 2026 plan." },
      { id: "p2", label: "Cross-entity transfer", outcome: "warn", detail: "Requires a new contract and continuous-service confirmation." },
      { id: "p3", label: "Effective date alignment", outcome: "pass", detail: "First of the month — aligns with the pay period." },
    ],
    conflicts: [
      "The employee has an open separation case (OFF-2026-0014) with a last working date of 31 August 2026. Only one of the two can proceed.",
    ],
    timeline: [
      { id: "e1", at: "2026-06-15T13:00:00Z", actor: "Mutale Kabwe", event: "Movement raised", after: "Submitted" },
      { id: "e2", at: "2026-06-19T09:30:00Z", actor: "Thandiwe Banda", event: "Returned for information", reason: "Receiving position is not funded in the 2026 establishment", before: "In review", after: "Returned" },
    ],
  },
  {
    id: "MOV-2026-0031",
    employeeId: "w-1006",
    type: "Secondment",
    reason: "Proposed three-month secondment to the Chingola Office to cover a planning gap.",
    effectiveFrom: "2026-08-01",
    effectiveTo: "2026-10-31",
    current: {
      jobTitle: "Yard Contractor (Logistics)",
      grade: "N/A",
      department: "Logistics",
      entity: "Demo Copperbelt Services Ltd",
      branch: "Solwezi Yard",
      manager: "Mutale Kabwe",
      positionId: "POS-LOG-0044",
      costCentre: "LOG-MBA-01",
    },
    proposed: {
      jobTitle: "Logistics Planner (seconded)",
      grade: "N/A",
      department: "Logistics",
      entity: "Demo Copperbelt Services Ltd",
      branch: "Chingola Office",
      manager: "Mutale Kabwe",
      positionId: "POS-LOG-0044",
      costCentre: "LOG-NBO-01",
    },
    headline: "Solwezi Yard → Chingola Office, three months",
    status: "Rejected",
    owner: "HR operations",
    nextAction: "Closed — raise a new engagement if the cover is still needed",
    dueDate: "2026-07-10",
    raisedBy: "Mutale Kabwe",
    raisedOn: "2026-06-29",
    impacts: [
      { id: "i1", area: "Reporting line", summary: "Would have moved to the Chingola planning lead", detail: "Not applied — the movement was rejected." },
      { id: "i2", area: "Payroll", summary: "Contractor rate would have changed", detail: "Not applied — the movement was rejected." },
      { id: "i3", area: "Access", summary: "Chingola Office access would have been added", detail: "Not applied — the movement was rejected." },
      { id: "i4", area: "Position", summary: "No planner position available", detail: "The contractor engagement does not carry a planner position." },
    ],
    policy: [
      { id: "p1", label: "Engagement runs past the effective date", outcome: "fail", detail: "The contractor engagement ends 2 August 2026, one day into the secondment." },
      { id: "p2", label: "Position on establishment", outcome: "fail", detail: "No planner position exists for a contractor engagement." },
    ],
    conflicts: ["The end of engagement (OFF-2026-0009) falls inside the proposed secondment window."],
    timeline: [
      { id: "e1", at: "2026-06-29T11:00:00Z", actor: "Mutale Kabwe", event: "Movement raised", after: "Submitted" },
      { id: "e2", at: "2026-07-08T14:20:00Z", actor: "Thandiwe Banda", event: "Rejected", reason: "Engagement ends inside the proposed secondment window", before: "In review", after: "Rejected" },
    ],
  },
];

/**
 * The employee's assignment as it stands today — the read-only baseline a
 * movement is proposed against. Taken from an existing movement where one
 * exists, otherwise built from the directory record.
 */
export const currentAssignment = (employeeId: string): AssignmentSnapshot | null => {
  const e = employees.find((x) => x.id === employeeId);
  if (!e) return null;
  const known = movements.find((m) => m.employeeId === employeeId);
  if (known) return known.current;
  const entity = entities.find((x) => x.id === e.entityId)?.name ?? "Unknown organisation";
  const manager = employees.find((x) => x.id === e.managerId)?.fullName ?? "Not assigned";
  const dept = e.department.slice(0, 3).toUpperCase();
  const site = e.branch.slice(0, 3).toUpperCase();
  return {
    jobTitle: e.jobTitle,
    grade: e.grade,
    department: e.department,
    entity,
    branch: e.branch,
    manager,
    positionId: `POS-${dept}-${e.employeeNo.replace("EMP-", "0")}`,
    costCentre: `${dept}-${site}-01`,
  };
};

/** Movements already recorded against an employee — history is never overwritten. */
export const movementsFor = (employeeId: string) => movements.filter((m) => m.employeeId === employeeId);

/* ------------------------------------------------------------------- api */

export const lifecycleApi = {
  onboardings: async () => {
    await delay();
    return onboardings;
  },
  onboarding: async (id: string) => {
    await delay();
    return onboardings.find((o) => o.id === id) ?? null;
  },
  offboardings: async () => {
    await delay();
    return offboardings;
  },
  offboarding: async (id: string) => {
    await delay();
    return offboardings.find((o) => o.id === id) ?? null;
  },
  movements: async () => {
    await delay();
    return movements;
  },
  movement: async (id: string) => {
    await delay();
    return movements.find((m) => m.id === id) ?? null;
  },
  /** Movements are never applied immediately — submitting records a pending future change. */
  submitMovement: async (_payload: unknown) => {
    await delay(700);
    return { id: `MOV-2026-0${Math.floor(Math.random() * 89 + 10)}`, ok: true as const };
  },
};
