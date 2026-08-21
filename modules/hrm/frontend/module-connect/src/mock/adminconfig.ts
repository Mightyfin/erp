/**
 * Administration and security configuration mock data.
 *
 * Two administrator surfaces are served from here:
 *  - organisation setup (legal entities, work locations, departments and cost
 *    centres) where every change is effective-dated, so amending the structure
 *    never rewrites what was true in the past; and
 *  - roles, permissions, data scope, segregation of duties and sensitive field
 *    masking.
 *
 * Self-contained, but every entity, branch and person below points at the
 * shared records in `data.ts` so the whole app tells one consistent story.
 */
import { employees, entities } from "./data";
import type { Role, TimelineEvent } from "./types";

const delay = (ms = 420) => new Promise((r) => setTimeout(r, ms));

/** The date the prototype treats as "today" when deciding what is in force. */
export const todayIso = "2026-07-29";

export const employeeName = (id: string) =>
  employees.find((e) => e.id === id)?.fullName ?? "Unknown employee";

export const entityName = (entityId: string) =>
  entities.find((e) => e.id === entityId)?.name ?? "Unknown entity";

export const shortEntityName = (entityId: string) =>
  entityName(entityId).split(" ").slice(0, 2).join(" ");

/* ------------------------------------------------------------------------- */
/* Organisation structure                                                     */
/* ------------------------------------------------------------------------- */

/**
 * Derived, never stored. A unit is in force between its effective dates; the
 * record itself is never edited in place and never deleted, so history stays
 * readable at any past date.
 */
export type UnitState = "Active" | "Closing" | "Closed" | "Not yet in force";

export interface EffectiveDated {
  effectiveFrom: string;
  effectiveTo?: string;
}

export function unitState(unit: EffectiveDated, asAt: string): UnitState {
  if (asAt < unit.effectiveFrom) return "Not yet in force";
  if (unit.effectiveTo && asAt > unit.effectiveTo) return "Closed";
  if (unit.effectiveTo) return "Closing";
  return "Active";
}

export const unitStateExplanation: Record<UnitState, string> = {
  Active: "In force at the selected date, with no closure recorded.",
  Closing: "In force now, but a closure date is already recorded against it.",
  Closed: "Not in force at the selected date. History before the closure date is unchanged.",
  "Not yet in force":
    "Created with a future effective date. It does not exist at the selected date.",
};

export interface LegalEntityConfig extends EffectiveDated {
  id: string;
  entityId: string;
  registeredName: string;
  country: string;
  legalIdLabel: string;
  legalId: string;
  currency: string;
  payrollCountryPack: string;
  registeredAddress: string;
  employees: number;
  branches: number;
  note?: string;
}

export const legalEntityConfigs: LegalEntityConfig[] = [
  {
    id: "cfg-ent-zm1",
    entityId: "ent-zm1",
    registeredName: "New World Cargo Logistics Zambia Ltd",
    country: "Zambia",
    legalIdLabel: "PACRA / TPIN",
    legalId: "PACRA 120190001234 · TPIN 1001234567",
    currency: "ZMW",
    payrollCountryPack: "Zambia country pack 2026.07",
    registeredAddress: "Plot 7231, Great East Road, Lusaka",
    effectiveFrom: "2009-04-01",
    employees: 4,
    branches: 3,
    note: "Group parent. Employs the Zambian establishment and carries the group cost centres.",
  },
  {
    id: "cfg-ent-zm2",
    entityId: "ent-zm2",
    registeredName: "New World Cargo Copperbelt Services Ltd",
    country: "Zambia",
    legalIdLabel: "PACRA / TPIN",
    legalId: "PACRA 120150004417 · TPIN 1002298431",
    currency: "ZMW",
    payrollCountryPack: "Zambia country pack 2026.06",
    registeredAddress: "Plot 15, Kabundi Road, Chingola",
    effectiveFrom: "2018-02-12",
    employees: 2,
    branches: 2,
    note: "Payroll runs on the Zambian calendar; NAPSA and NHIMA rates are held in the country pack, not here.",
  },
  {
    id: "cfg-ent-zm3",
    entityId: "ent-zm3",
    registeredName: "New World Cargo Engineering Zambia Ltd",
    country: "Zambia",
    legalIdLabel: "PACRA / TPIN",
    legalId: "PACRA 120210007782 · TPIN 1003844126",
    currency: "ZMW",
    payrollCountryPack: "Zambia country pack 2026.07",
    registeredAddress: "Plot 33, Mosi-oa-Tunya Road, Livingstone",
    effectiveFrom: "2015-10-01",
    employees: 2,
    branches: 1,
    note: "A works council agreement applies. Structural changes here need consultation before the effective date.",
  },
];

export type LocationKind = "Head office" | "Plant" | "Depot" | "Yard" | "Office" | "Works";

export interface WorkLocation extends EffectiveDated {
  id: string;
  entityId: string;
  /** Matches the branch names held on the entity record in `data.ts`. */
  name: string;
  code: string;
  kind: LocationKind;
  address: string;
  timeZone: string;
  employees: number;
  positions: number;
  note?: string;
}

export const workLocations: WorkLocation[] = [
  {
    id: "loc-zm-lsk",
    entityId: "ent-zm1",
    name: "Lusaka HQ",
    code: "ZM-LSK",
    kind: "Head office",
    address: "Plot 7231, Great East Road, Lusaka",
    timeZone: "Africa/Lusaka",
    effectiveFrom: "2009-04-01",
    employees: 2,
    positions: 4,
  },
  {
    id: "loc-zm-nla",
    entityId: "ent-zm1",
    name: "Ndola Plant",
    code: "ZM-NLA",
    kind: "Plant",
    address: "Plot 412, President Avenue, Ndola",
    timeZone: "Africa/Lusaka",
    effectiveFrom: "2013-06-03",
    employees: 1,
    positions: 3,
  },
  {
    id: "loc-zm-kit",
    entityId: "ent-zm1",
    name: "Kitwe Depot",
    code: "ZM-KIT",
    kind: "Depot",
    address: "Plot 88, Kalulushi Road, Kitwe",
    timeZone: "Africa/Lusaka",
    effectiveFrom: "2016-01-04",
    effectiveTo: "2026-12-31",
    employees: 1,
    positions: 2,
    note: "Closure agreed with the works council. The one employee based here transfers to Lusaka HQ on 1 January 2027.",
  },
  {
    id: "loc-zm-chg",
    entityId: "ent-zm2",
    name: "Chingola Office",
    code: "ZM-CHG",
    kind: "Office",
    address: "Plot 15, Kabundi Road, Chingola",
    timeZone: "Africa/Lusaka",
    effectiveFrom: "2018-02-12",
    employees: 1,
    positions: 3,
  },
  {
    id: "loc-zm-slw",
    entityId: "ent-zm2",
    name: "Solwezi Yard",
    code: "ZM-SLW",
    kind: "Yard",
    address: "Plot 6, Kyawama Industrial Area, Solwezi",
    timeZone: "Africa/Lusaka",
    effectiveFrom: "2021-09-01",
    employees: 1,
    positions: 2,
  },
  {
    id: "loc-zm-liv",
    entityId: "ent-zm3",
    name: "Livingstone Works",
    code: "ZM-LIV",
    kind: "Works",
    address: "Plot 33, Mosi-oa-Tunya Road, Livingstone",
    timeZone: "Africa/Lusaka",
    effectiveFrom: "2015-10-01",
    employees: 2,
    positions: 4,
  },
  {
    id: "loc-zm-kab",
    entityId: "ent-zm1",
    name: "Kabwe Service Hub",
    code: "ZM-KAB",
    kind: "Office",
    address: "Plot 210, Buntungwa Street, Kabwe",
    timeZone: "Africa/Lusaka",
    effectiveFrom: "2026-10-01",
    employees: 0,
    positions: 0,
    note: "Created ahead of opening. It is deliberately not in force yet, so nothing can be assigned to it before 1 October 2026.",
  },
];

export interface OrgUnitConfig extends EffectiveDated {
  id: string;
  name: string;
  code: string;
  costCentre: string;
  entityId: string;
  branch: string;
  parent?: string;
  employees: number;
  positions: number;
  /** What still points at this unit. Empty means it is safe to remove outright. */
  references: string[];
  note?: string;
}

export const orgUnitConfigs: OrgUnitConfig[] = [
  {
    id: "unit-zm-ops",
    name: "Operations",
    code: "ZM-OPS",
    costCentre: "CC-1100",
    entityId: "ent-zm1",
    branch: "Lusaka HQ",
    effectiveFrom: "2009-04-01",
    employees: 2,
    positions: 3,
    references: ["2 employees", "3 positions", "Leave approval route ZM-01"],
  },
  {
    id: "unit-zm-ppl",
    name: "People",
    code: "ZM-PPL",
    costCentre: "CC-1400",
    entityId: "ent-zm1",
    branch: "Lusaka HQ",
    effectiveFrom: "2011-02-01",
    employees: 0,
    positions: 1,
    references: ["1 position", "Parent of Reward", "Case category routing"],
  },
  {
    id: "unit-zm-rew",
    name: "Reward",
    code: "ZM-PPL-REW",
    costCentre: "CC-1410",
    entityId: "ent-zm1",
    branch: "Lusaka HQ",
    parent: "People",
    effectiveFrom: "2022-04-01",
    employees: 0,
    positions: 1,
    references: ["1 vacant position (Reward Analyst, POS-LUS-0142)"],
    note: "No employees, but a vacant position is still attached. Deleting the unit would orphan the establishment record.",
  },
  {
    id: "unit-zm-hro",
    name: "HR Operations",
    code: "ZM-HRO",
    costCentre: "CC-1420",
    entityId: "ent-zm1",
    branch: "Ndola Plant",
    effectiveFrom: "2019-01-07",
    employees: 1,
    positions: 1,
    references: ["1 employee", "1 position", "Request SLA set HR-STD"],
  },
  {
    id: "unit-zm-eng",
    name: "Plant Engineering",
    code: "ZM-ENG",
    costCentre: "CC-1200",
    entityId: "ent-zm1",
    branch: "Ndola Plant",
    effectiveFrom: "2013-06-03",
    employees: 0,
    positions: 1,
    references: ["1 vacant position (Electrical Safety Engineer)", "ZS 385 electrical safety licence register"],
  },
  {
    id: "unit-zm-log",
    name: "Logistics",
    code: "ZM-LOG",
    costCentre: "CC-1300",
    entityId: "ent-zm1",
    branch: "Kitwe Depot",
    effectiveFrom: "2016-01-04",
    effectiveTo: "2026-12-31",
    employees: 1,
    positions: 2,
    references: ["1 employee", "2 positions"],
    note: "Closes with the depot. The employee and both positions must be moved before 31 December 2026.",
  },
  {
    id: "unit-zm-fin",
    name: "Finance",
    code: "ZM-FIN",
    costCentre: "CC-2100",
    entityId: "ent-zm2",
    branch: "Chingola Office",
    effectiveFrom: "2018-02-12",
    employees: 0,
    positions: 1,
    references: ["1 vacant position (Finance Manager)", "Parent of Payroll"],
  },
  {
    id: "unit-zm-pay",
    name: "Payroll",
    code: "ZM-FIN-PAY",
    costCentre: "CC-2110",
    entityId: "ent-zm2",
    branch: "Chingola Office",
    parent: "Finance",
    effectiveFrom: "2019-04-01",
    employees: 1,
    positions: 1,
    references: ["1 employee", "1 position", "Pay group ZM-MONTHLY"],
  },
  {
    id: "unit-zm-logcb",
    name: "Logistics — Copperbelt",
    code: "ZM-LOG-CB",
    costCentre: "CC-2300",
    entityId: "ent-zm2",
    branch: "Solwezi Yard",
    effectiveFrom: "2021-09-01",
    employees: 1,
    positions: 2,
    references: ["1 employee", "2 positions", "Contractor engagement register"],
  },
  {
    id: "unit-zm-mfg",
    name: "Manufacturing",
    code: "ZM-MFG",
    costCentre: "CC-3100",
    entityId: "ent-zm3",
    branch: "Livingstone Works",
    effectiveFrom: "2015-10-01",
    employees: 2,
    positions: 3,
    references: ["2 employees", "3 positions", "Shift rule set ZM-2SHIFT"],
  },
  {
    id: "unit-zm-qa",
    name: "Quality Assurance",
    code: "ZM-QA",
    costCentre: "CC-3200",
    entityId: "ent-zm3",
    branch: "Livingstone Works",
    effectiveFrom: "2026-06-15",
    employees: 0,
    positions: 0,
    references: [],
    note: "Created in June 2026 and never used. Nothing references it, so it can be removed outright.",
  },
  {
    id: "unit-grp-svc",
    name: "Group Services",
    code: "GRP-SVC",
    costCentre: "CC-9000",
    entityId: "ent-zm1",
    branch: "Lusaka HQ",
    effectiveFrom: "2012-01-01",
    employees: 0,
    positions: 0,
    references: ["4 approval routes", "Payroll cost allocation ZM-ALLOC-02"],
    note: "No people attached, but configuration elsewhere still posts to this cost centre.",
  },
];

/** A unit can only be removed outright when nothing at all points at it. */
export const canDeleteUnit = (unit: OrgUnitConfig) => unit.references.length === 0;

export type ChangeState = "Scheduled" | "Awaiting consultation" | "Draft";

export interface ScheduledChange {
  id: string;
  scope: "Legal entity" | "Work location" | "Department" | "Cost centre";
  unit: string;
  change: string;
  effectiveFrom: string;
  requestedBy: string;
  requestedOn: string;
  state: ChangeState;
  employeesAffected: number;
  impact: string;
}

export const scheduledChanges: ScheduledChange[] = [
  {
    id: "chg-001",
    scope: "Work location",
    unit: "Kitwe Depot (ZM-KIT)",
    change: "Close location and transfer the depot establishment to Lusaka HQ",
    effectiveFrom: "2026-12-31",
    requestedBy: "Mutale Kabwe",
    requestedOn: "2026-05-18",
    state: "Scheduled",
    employeesAffected: 1,
    impact:
      "Attendance, leave and pay recorded against Kitwe up to 31 December 2026 stay attached to Kitwe. Nothing before that date is rewritten.",
  },
  {
    id: "chg-002",
    scope: "Work location",
    unit: "Kabwe Service Hub (ZM-KAB)",
    change: "Open new work location under New World Cargo Logistics Zambia Ltd",
    effectiveFrom: "2026-10-01",
    requestedBy: "Thandiwe Banda",
    requestedOn: "2026-07-02",
    state: "Scheduled",
    employeesAffected: 0,
    impact:
      "The location exists in configuration but cannot be selected on an employee record, position or requisition until 1 October 2026.",
  },
  {
    id: "chg-003",
    scope: "Department",
    unit: "Reward (ZM-PPL-REW)",
    change: "Merge into People and supersede the separate unit",
    effectiveFrom: "2026-09-01",
    requestedBy: "Thandiwe Banda",
    requestedOn: "2026-07-21",
    state: "Draft",
    employeesAffected: 0,
    impact:
      "The vacant Reward Analyst position moves to People on the effective date. Reward is superseded rather than deleted, so historical reporting still resolves the unit.",
  },
  {
    id: "chg-004",
    scope: "Cost centre",
    unit: "Manufacturing (CC-3100)",
    change: "Re-code cost centre CC-3100 to CC-3105 for the new chart of accounts",
    effectiveFrom: "2027-01-01",
    requestedBy: "Nalukui Simasiku",
    requestedOn: "2026-06-30",
    state: "Awaiting consultation",
    employeesAffected: 2,
    impact:
      "Pay journals posted before 1 January 2027 keep CC-3100. Only postings on or after the effective date use the new code.",
  },
  {
    id: "chg-005",
    scope: "Legal entity",
    unit: "New World Cargo Engineering Zambia Ltd",
    change: "Update registered address to Plot 33, Mosi-oa-Tunya Road, Livingstone",
    effectiveFrom: "2026-08-01",
    requestedBy: "Thandiwe Banda",
    requestedOn: "2026-07-14",
    state: "Scheduled",
    employeesAffected: 2,
    impact:
      "Contracts and letters generated before 1 August 2026 keep the previous registered address exactly as issued.",
  },
];

export const structureAudit: TimelineEvent[] = [
  {
    id: "sa-1",
    at: "2026-07-21T09:12:00Z",
    actor: "Thandiwe Banda",
    event: "Drafted merge of Reward into People",
    reason: "Reward has had no employees since the analyst left in March 2026.",
    before: "Reward — separate unit",
    after: "Reward — merge drafted, effective 1 September 2026",
  },
  {
    id: "sa-2",
    at: "2026-07-14T14:40:00Z",
    actor: "Thandiwe Banda",
    event: "Scheduled registered address change for New World Cargo Engineering Zambia Ltd",
    reason: "Lease moved to the Billstraße site.",
    before: "Süderstraße 77, 20097 Livingstone",
    after: "Plot 33, Mosi-oa-Tunya Road, Livingstone",
    evidence: { label: "Lease agreement ZM-2026-0114", href: "#" },
  },
  {
    id: "sa-3",
    at: "2026-06-30T11:05:00Z",
    actor: "Nalukui Simasiku",
    event: "Raised cost centre re-code for Manufacturing",
    reason: "Group chart of accounts is renumbered from the 2027 financial year.",
    before: "CC-3100",
    after: "CC-3105 from 1 January 2027",
  },
  {
    id: "sa-4",
    at: "2026-06-15T08:30:00Z",
    actor: "Thandiwe Banda",
    event: "Created department Quality Assurance",
    reason: "Anticipated quality function at Livingstone Works. Not yet staffed.",
    after: "Quality Assurance (ZM-QA, CC-3200)",
  },
  {
    id: "sa-5",
    at: "2026-05-18T16:22:00Z",
    actor: "Mutale Kabwe",
    event: "Scheduled closure of Kitwe Depot",
    reason: "Depot consolidation agreed with the works council on 12 May 2026.",
    before: "Open, no closure date",
    after: "Closes 31 December 2026",
    evidence: { label: "Works council minute ZM-WC-2026-05", href: "#" },
  },
  {
    id: "sa-6",
    at: "2026-04-02T10:15:00Z",
    actor: "Mutale Kabwe",
    event: "Moved Logistics cost centre from CC-1310 to CC-1300",
    reason: "Depot and transport budgets combined.",
    before: "CC-1310",
    after: "CC-1300",
  },
];

/* ------------------------------------------------------------------------- */
/* Roles, permissions, scope and masking                                      */
/* ------------------------------------------------------------------------- */

export type DataScope =
  "Own record" | "Reporting line" | "Team" | "Department" | "Branch" | "Entity";

export const dataScopes: DataScope[] = [
  "Own record",
  "Reporting line",
  "Team",
  "Department",
  "Branch",
  "Entity",
];

export const dataScopeMeaning: Record<DataScope, string> = {
  "Own record": "Only the signed-in employee's own record.",
  "Reporting line": "Everyone who reports to the holder, directly or indirectly.",
  Team: "The holder's immediate team, including peers.",
  Department: "Every employee in the holder's department, across locations.",
  Branch: "Every employee based at the holder's work location.",
  Entity: "Every employee in the legal entities the holder is assigned to.",
};

export interface RoleDef {
  id: Role;
  label: string;
  purpose: string;
  holderIds: string[];
  scope: DataScope;
  scopeNote: string;
  /** Administrative roles are the ones that can change how the system behaves. */
  administrative: boolean;
}

export const roleDefs: RoleDef[] = [
  {
    id: "employee",
    label: "Employee",
    purpose: "Everyone with a contract. Sees their own record and raises their own requests.",
    holderIds: ["w-1001", "w-1002", "w-1003", "w-1004", "w-1005", "w-1006", "w-1007", "w-1008"],
    scope: "Own record",
    scopeNote:
      "Granted automatically on hire and removed on the leaving date. It cannot be widened.",
    administrative: false,
  },
  {
    id: "manager",
    label: "Manager",
    purpose:
      "Approves leave, attendance corrections and requests for the people who report to them.",
    holderIds: ["w-1002", "w-1008"],
    scope: "Reporting line",
    scopeNote:
      "Follows the reporting line as it stands on the day, so a transfer moves access with the employee.",
    administrative: false,
  },
  {
    id: "hr_ops",
    label: "HR operations",
    purpose: "Maintains employee records, handles cases and keeps employment data correct.",
    holderIds: ["w-1005"],
    scope: "Entity",
    scopeNote:
      "Assigned to New World Cargo Logistics Zambia Ltd only. A second holder is needed for the Zambian entity.",
    administrative: false,
  },
  {
    id: "payroll",
    label: "Payroll",
    purpose: "Prepares and runs pay, and reconciles the results before payment is authorised.",
    holderIds: ["w-1003"],
    scope: "Entity",
    scopeNote:
      "Assigned to all three entities. Sees pay data for those entities and nothing outside them.",
    administrative: false,
  },
  {
    id: "hr_admin",
    label: "HR admin",
    purpose: "Configures the system: structure, policies, routing, roles and permissions.",
    holderIds: ["w-1003", "w-1005"],
    scope: "Entity",
    scopeNote:
      "Configuration is group-wide, so the entity scope only limits which employee records are visible.",
    administrative: true,
  },
];

export const roleLabel = (id: Role) => roleDefs.find((r) => r.id === id)?.label ?? id;

export interface Capability {
  id: string;
  label: string;
  description: string;
  group: "Employee records" | "Requests and approvals" | "Pay" | "Administration" | "Data";
  /** Losing every holder of an administrative capability locks the system. */
  administrative?: boolean;
  /** Granted to named individuals only — never through a role. */
  namedOnly?: boolean;
  grants: Record<Role, boolean>;
  note?: string;
}

export const capabilities: Capability[] = [
  {
    id: "cap-view-employee",
    label: "View employee record",
    description: "Open an employee's profile within the role's data scope.",
    group: "Employee records",
    grants: { employee: true, manager: true, hr_ops: true, payroll: true, hr_admin: true },
    note: "For the Employee role this resolves to their own record only.",
  },
  {
    id: "cap-edit-employee",
    label: "Edit employee record",
    description:
      "Change employment data such as job title, department, location or contract terms.",
    group: "Employee records",
    grants: { employee: false, manager: false, hr_ops: true, payroll: false, hr_admin: true },
    note: "Managers propose changes; the change is only recorded once HR operations accepts it.",
  },
  {
    id: "cap-approve-leave",
    label: "Approve leave",
    description: "Decide leave and attendance requests raised inside the role's data scope.",
    group: "Requests and approvals",
    grants: { employee: false, manager: true, hr_ops: true, payroll: false, hr_admin: false },
    note: "Nobody can approve their own request, whatever the role says.",
  },
  {
    id: "cap-run-payroll",
    label: "Run payroll",
    description: "Prepare, calculate and submit a pay run for authorisation.",
    group: "Pay",
    grants: { employee: false, manager: false, hr_ops: false, payroll: true, hr_admin: false },
  },
  {
    id: "cap-view-payslips",
    label: "View payslips",
    description: "Open issued payslips and the calculation behind each line.",
    group: "Pay",
    grants: { employee: true, manager: false, hr_ops: false, payroll: true, hr_admin: false },
    note: "Employees see their own payslips. Managers never see a payslip through the Manager role.",
  },
  {
    id: "cap-manage-config",
    label: "Manage configuration",
    description:
      "Change organisation structure, policies, approval routing, roles and permissions.",
    group: "Administration",
    administrative: true,
    grants: { employee: false, manager: false, hr_ops: false, payroll: false, hr_admin: true },
  },
  {
    id: "cap-protected-disclosures",
    label: "Handle protected disclosures",
    description:
      "Read, triage and progress speak-up reports, including the identity of a reporter who has not stayed anonymous.",
    group: "Administration",
    namedOnly: true,
    grants: { employee: false, manager: false, hr_ops: false, payroll: false, hr_admin: false },
    note: "Granted to named handlers only. It is not attachable to any role, including HR admin.",
  },
  {
    id: "cap-export",
    label: "Export data",
    description: "Download employee, pay or case data out of the system as a file.",
    group: "Data",
    grants: { employee: false, manager: false, hr_ops: true, payroll: true, hr_admin: true },
    note: "Every export is recorded with the exporter, the filter used and the row count.",
  },
  {
    id: "cap-sensitive-fields",
    label: "View sensitive fields",
    description:
      "Reveal masked values such as bank details or national identifiers, one record at a time.",
    group: "Data",
    grants: { employee: false, manager: false, hr_ops: true, payroll: true, hr_admin: false },
    note: "Which fields actually unmask still depends on the masking rules below.",
  },
];

export type PermissionMatrix = Record<string, Record<Role, boolean>>;

export const permissionMatrix: PermissionMatrix = Object.fromEntries(
  capabilities.map((c) => [c.id, { ...c.grants }]),
);

export interface SodRule {
  id: string;
  title: string;
  /** The two capabilities that must not sit with the same person. */
  pair: [string, string];
  risk: string;
  control: string;
  /** People who currently hold both sides of the conflict. */
  holders: { employeeId: string; roles: Role[] }[];
  mitigated: boolean;
  mitigation?: string;
}

export const sodRules: SodRule[] = [
  {
    id: "sod-1",
    title: "Prepares pay and authorises the configuration that drives it",
    pair: ["cap-run-payroll", "cap-manage-config"],
    risk: "The same person can create a pay element, apply it to a pay run and submit that run. A fictitious payment or an inflated rate would pass through with no second pair of eyes.",
    control:
      "Payroll and HR admin should sit with different people. If the entity is too small for that, a second approver must authorise every pay run before payment.",
    holders: [{ employeeId: "w-1003", roles: ["payroll", "hr_admin"] }],
    mitigated: false,
  },
  {
    id: "sod-2",
    title: "Approves requests and edits the record the request is measured against",
    pair: ["cap-approve-leave", "cap-edit-employee"],
    risk: "A holder can approve leave beyond entitlement and then adjust the balance on the employee record so the overspend never surfaces.",
    control:
      "Balance adjustments made by an approver are reported weekly to the entity HR lead and are visible on the employee's timeline.",
    holders: [{ employeeId: "w-1005", roles: ["hr_ops", "hr_admin"] }],
    mitigated: true,
    mitigation:
      "Accepted for New World Cargo Logistics Zambia Ltd until a second HR operations holder is appointed. Reviewed on 30 June 2026, next review 31 December 2026.",
  },
  {
    id: "sod-3",
    title: "Reveals sensitive fields and exports data",
    pair: ["cap-sensitive-fields", "cap-export"],
    risk: "Unmasking bank details and exporting in the same session allows a full set of payment data to leave the system in one step.",
    control:
      "Exports containing an unmasked sensitive field require a stated purpose and are reported to the data protection contact.",
    holders: [
      { employeeId: "w-1003", roles: ["payroll", "hr_admin"] },
      { employeeId: "w-1005", roles: ["hr_ops", "hr_admin"] },
    ],
    mitigated: true,
    mitigation: "Purpose prompt is enabled on export, and unmasked exports are capped at 50 rows.",
  },
];

export interface MaskedField {
  id: string;
  label: string;
  category: string;
  /** Roles that can reveal the value. An empty list means no role can. */
  visibleTo: Role[];
  namedHandlersOnly?: boolean;
  rule: string;
  onReveal: string;
}

export const maskedFields: MaskedField[] = [
  {
    id: "mask-bank",
    label: "Bank account details",
    category: "Payment data",
    visibleTo: ["payroll"],
    rule: "Shown to Payroll in full. Everyone else sees the last four characters. Employees see their own account in full.",
    onReveal: "Reveal is recorded on the employee's timeline with the reason given.",
  },
  {
    id: "mask-nid",
    label: "National identifier",
    category: "Identity data",
    visibleTo: ["hr_ops", "payroll"],
    rule: "Needed for statutory reporting and right-to-work checks. Masked for managers at all times.",
    onReveal: "Reveal is recorded, and repeated reveals across many records raise an alert.",
  },
  {
    id: "mask-medical",
    label: "Medical and fitness-to-work data",
    category: "Health data",
    visibleTo: [],
    rule: "Held by occupational health, not in the employee record. HRM stores only the outcome — fit, fit with adjustments, or not fit — and never the underlying condition.",
    onReveal:
      "No role can unmask this. Access is requested from occupational health outside the system.",
  },
  {
    id: "mask-discipline",
    label: "Disciplinary records",
    category: "Case data",
    visibleTo: ["hr_ops"],
    rule: "Visible to the named case handler and HR operations for the entity. A manager sees that a case exists and its status, never the content.",
    onReveal: "Every open of a disciplinary file is recorded, including by HR operations.",
  },
  {
    id: "mask-disclosure",
    label: "Protected disclosures",
    category: "Speak-up",
    visibleTo: [],
    namedHandlersOnly: true,
    rule: "Restricted to the named handlers below. Not visible to HR admin, and it cannot be granted through a role.",
    onReveal: "Handler access is recorded and reported to the audit committee each quarter.",
  },
  {
    id: "mask-pay",
    label: "Salary and pay elements",
    category: "Pay data",
    visibleTo: ["payroll", "hr_admin"],
    rule: "Managers see the pay range for a position, never an individual's salary. Employees see their own pay in full.",
    onReveal: "Reveal is recorded against the pay record.",
  },
];

export interface DisclosureHandler {
  id: string;
  name: string;
  title: string;
  appointedOn: string;
  employeeId?: string;
  independent: boolean;
  note: string;
}

export const disclosureHandlers: DisclosureHandler[] = [
  {
    id: "dh-1",
    name: "Beatrice Tembo",
    title: "Non-executive director, appointed disclosure handler",
    appointedOn: "2024-11-01",
    independent: true,
    note: "Deliberately outside the HR line. She has no other role in HRM and cannot see employee records, pay or cases.",
  },
  {
    id: "dh-2",
    name: "Thandiwe Banda",
    title: "HR Operations Specialist, second handler",
    appointedOn: "2025-03-17",
    employeeId: "w-1005",
    independent: false,
    note: "Named individually, not through her HR admin role. Removing her name removes the access even though the role stays.",
  },
];

export const permissionAudit: TimelineEvent[] = [
  {
    id: "pa-1",
    at: "2026-07-24T13:05:00Z",
    actor: "Thandiwe Banda",
    event: "Narrowed the data scope of the Manager role",
    reason: "Managers were seeing peers of their reports at the same branch.",
    before: "Branch",
    after: "Reporting line",
  },
  {
    id: "pa-2",
    at: "2026-07-09T10:41:00Z",
    actor: "Nalukui Simasiku",
    event: "Granted HR admin to a second holder",
    reason: "Single-holder risk raised at the June controls review.",
    before: "1 holder",
    after: "2 holders",
    evidence: { label: "Controls review note CR-2026-06", href: "#" },
  },
  {
    id: "pa-3",
    at: "2026-06-30T15:20:00Z",
    actor: "Thandiwe Banda",
    event: "Accepted segregation-of-duties conflict with a compensating control",
    reason: "No second HR operations holder available for the Zambian entity.",
    before: "Open conflict",
    after: "Mitigated until 31 December 2026",
  },
  {
    id: "pa-4",
    at: "2026-05-12T08:55:00Z",
    actor: "Beatrice Tembo",
    event: "Confirmed protected disclosure handlers for the year",
    reason: "Annual reappointment required by the speak-up policy.",
    after: "2 named handlers",
  },
  {
    id: "pa-5",
    at: "2026-04-18T09:30:00Z",
    actor: "Thandiwe Banda",
    event: "Removed View payslips from the Manager role",
    reason: "Managers do not need individual pay to run a team.",
    before: "Allowed",
    after: "Not allowed",
  },
];

/* ------------------------------------------------------------------------- */
/* Reader                                                                     */
/* ------------------------------------------------------------------------- */

export const adminConfigApi = {
  legalEntities: async () => {
    await delay();
    return legalEntityConfigs;
  },
  workLocations: async () => {
    await delay();
    return workLocations;
  },
  orgUnits: async () => {
    await delay();
    return orgUnitConfigs;
  },
  scheduledChanges: async () => {
    await delay();
    return scheduledChanges;
  },
  structureAudit: async () => {
    await delay();
    return structureAudit;
  },
  organisation: async () => {
    await delay();
    return {
      entities: legalEntityConfigs,
      locations: workLocations,
      units: orgUnitConfigs,
      scheduled: scheduledChanges,
      audit: structureAudit,
    };
  },
  roles: async () => {
    await delay();
    return roleDefs;
  },
  capabilities: async () => {
    await delay();
    return capabilities;
  },
  permissionMatrix: async () => {
    await delay();
    return permissionMatrix;
  },
  security: async () => {
    await delay();
    return {
      roles: roleDefs,
      capabilities,
      matrix: permissionMatrix,
      sod: sodRules,
      masking: maskedFields,
      handlers: disclosureHandlers,
      audit: permissionAudit,
    };
  },
};
