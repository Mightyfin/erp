/**
 * Remaining capability data: assets, journeys, mobility, alumni, offers,
 * referrals, feedback, PIPs, succession, skills, TOIL, utilisation, unions,
 * emergency roll-call and saved reports.
 *
 * Kept deliberately light — short labels, no long policy prose. The UI reads as
 * a working tool, not a compliance manual.
 */
const delay = (ms = 400) => new Promise((r) => setTimeout(r, ms));

export const money = (v: number, c: string) =>
  new Intl.NumberFormat(undefined, { style: "currency", currency: c, maximumFractionDigits: 0 }).format(v);

/* ---------------------------------------------------------------- assets */

export interface Asset {
  id: string;
  item: string;
  kind: "Laptop" | "Phone" | "Access card" | "Vehicle" | "PPE" | "Software";
  serial: string;
  holder: string;
  issued: string;
  condition: "New" | "Good" | "Fair" | "Damaged";
  state: "Assigned" | "Return due" | "Returned" | "Lost";
  dueBack?: string;
}

export const assets: Asset[] = [
  { id: "AST-0114", item: "ThinkPad T14", kind: "Laptop", serial: "PF-3K92LM", holder: "Chanda Mwansa-Chileshe", issued: "2024-02-01", condition: "Good", state: "Assigned" },
  { id: "AST-0088", item: "Site access card", kind: "Access card", serial: "AC-77120", holder: "Kondwani Mwanza", issued: "2024-02-01", condition: "Good", state: "Return due", dueBack: "2026-08-31" },
  { id: "AST-0092", item: "Forklift key fob", kind: "Vehicle", serial: "FK-2201", holder: "Emmanuel Sakala", issued: "2023-08-14", condition: "Fair", state: "Assigned" },
  { id: "AST-0131", item: "iPhone 15", kind: "Phone", serial: "IM-8841203", holder: "Mutale Kabwe", issued: "2025-03-11", condition: "Good", state: "Assigned" },
  { id: "AST-0075", item: "Cut-resistant gloves (pair)", kind: "PPE", serial: "—", holder: "Kondwani Mwanza", issued: "2026-07-18", condition: "New", state: "Assigned" },
  { id: "AST-0061", item: "Yard radio", kind: "Phone", serial: "RD-4410", holder: "Gift Zulu", issued: "2025-11-03", condition: "Damaged", state: "Lost", dueBack: "2026-08-02" },
];

/* -------------------------------------------------------------- journeys */

export interface Journey {
  id: string;
  name: string;
  trigger: string;
  employee: string;
  steps: { label: string; owner: string; done: boolean }[];
  due: string;
}

export const journeys: Journey[] = [
  {
    id: "JRN-0021",
    name: "First-time manager",
    trigger: "Promotion to a role with reports",
    employee: "Thandiwe Banda",
    due: "2026-09-30",
    steps: [
      { label: "Manager essentials course", owner: "Employee", done: true },
      { label: "1:1 with HR on the basics", owner: "HR", done: true },
      { label: "Set first team goals", owner: "Employee", done: false },
      { label: "30-day check-in", owner: "Manager", done: false },
    ],
  },
  {
    id: "JRN-0018",
    name: "Return from long absence",
    trigger: "Return-to-work after 4+ weeks",
    employee: "Emmanuel Sakala",
    due: "2026-08-14",
    steps: [
      { label: "Return-to-work conversation", owner: "Manager", done: true },
      { label: "Confirm adjustments", owner: "HR", done: true },
      { label: "Phased hours agreed", owner: "Manager", done: false },
      { label: "Two-week review", owner: "Manager", done: false },
    ],
  },
  {
    id: "JRN-0024",
    name: "New parent",
    trigger: "Parental leave booked",
    employee: "Chanda Mwansa-Chileshe",
    due: "2026-10-01",
    steps: [
      { label: "Leave and pay explained", owner: "HR", done: true },
      { label: "Handover plan", owner: "Employee", done: false },
      { label: "Keep-in-touch preference", owner: "Employee", done: false },
      { label: "Return date confirmed", owner: "HR", done: false },
    ],
  },
];

/* -------------------------------------------------------------- mobility */

export interface Assignment {
  id: string;
  employee: string;
  type: "Short-term" | "Long-term" | "Permanent transfer" | "Commuter";
  homeEntity: string;
  hostEntity: string;
  from: string;
  to?: string;
  status: "Proposed" | "Approved" | "Active" | "Completed";
  permit: "Not required" | "Applied" | "Granted" | "Expiring";
  permitNote?: string;
  allowances: { label: string; amount: number; currency: string }[];
  payrollNote: string;
}

export const assignments: Assignment[] = [
  {
    id: "ASG-0007",
    employee: "Nalukui Simasiku",
    type: "Short-term",
    homeEntity: "Demo Copperbelt Services Ltd",
    hostEntity: "Demo Logistics Zambia Ltd",
    from: "2026-10-01",
    to: "2027-01-31",
    status: "Approved",
    permit: "Not required",
    permitNote: "Internal transfer between Zambian entities — no permit needed. Site induction booked for 28 Sep.",
    allowances: [
      { label: "Housing", amount: 9_500, currency: "ZMW" },
      { label: "Cost-of-living", amount: 2_400, currency: "ZMW" },
      { label: "One-off relocation", amount: 14_000, currency: "ZMW" },
    ],
    payrollNote: "Stays on the Copperbelt payroll. Host allowances paid by the Lusaka entity and recharged.",
  },
  {
    id: "ASG-0004",
    employee: "Natasha Chirwa",
    type: "Commuter",
    homeEntity: "Demo Engineering Zambia Ltd",
    hostEntity: "Demo Logistics Zambia Ltd",
    from: "2026-09-14",
    status: "Proposed",
    permit: "Not required",
    allowances: [{ label: "Travel", amount: 3_200, currency: "ZMW" }],
    payrollNote: "Single payroll. Travel reimbursed as expenses, not an allowance.",
  },
];

/* ---------------------------------------------------------------- alumni */

export interface Alumnus {
  id: string;
  name: string;
  lastRole: string;
  left: string;
  reason: "Resignation" | "Contract ended" | "Retirement" | "Redundancy";
  rehireEligible: "Yes" | "With review" | "No";
  rehireNote?: string;
  keepInTouch: boolean;
  serviceYears: number;
}

export const alumni: Alumnus[] = [
  { id: "ALM-0031", name: "Grace Nyirenda", lastRole: "Depot Planner", left: "2026-04-30", reason: "Resignation", rehireEligible: "Yes", keepInTouch: true, serviceYears: 6 },
  { id: "ALM-0028", name: "Brian Mulenga", lastRole: "Yard Operative", left: "2025-12-19", reason: "Contract ended", rehireEligible: "Yes", keepInTouch: true, serviceYears: 2 },
  { id: "ALM-0022", name: "Beatrice Tembo", lastRole: "Maintenance Lead", left: "2025-06-30", reason: "Retirement", rehireEligible: "With review", rehireNote: "Open to seasonal cover. Check pension implications first.", keepInTouch: true, serviceYears: 21 },
  { id: "ALM-0019", name: "Former employee", lastRole: "Warehouse Operative", left: "2025-03-14", reason: "Resignation", rehireEligible: "No", rehireNote: "Flagged at exit. HR review required before any approach.", keepInTouch: false, serviceYears: 1 },
];

/* ---------------------------------------------------------------- offers */

export interface Offer {
  id: string;
  candidate: string;
  role: string;
  entity: string;
  salary: number;
  currency: string;
  startDate: string;
  status: "Draft" | "Awaiting approval" | "Sent" | "Accepted" | "Declined" | "Withdrawn";
  approver: string;
  expires: string;
  vsBand: string;
}

export const offers: Offer[] = [
  { id: "OFR-0044", candidate: "Mwaka Lungu", role: "Maintenance Planner", entity: "Demo Logistics Zambia Ltd", salary: 198_000, currency: "ZMW", startDate: "2026-10-01", status: "Sent", approver: "Mutale Kabwe", expires: "2026-08-15", vsBand: "Within G6 band, 3% above midpoint" },
  { id: "OFR-0041", candidate: "Chembo Katongo", role: "Yard Supervisor", entity: "Demo Copperbelt Services Ltd", salary: 186_000, currency: "ZMW", startDate: "2026-09-15", status: "Awaiting approval", approver: "Mutale Kabwe", expires: "2026-08-20", vsBand: "Above band maximum — needs written justification" },
  { id: "OFR-0038", candidate: "Lubona Mubita", role: "Welding Technician", entity: "Demo Engineering Zambia Ltd", salary: 114_000, currency: "ZMW", startDate: "2026-09-01", status: "Accepted", approver: "Mutale Kabwe", expires: "2026-08-01", vsBand: "Within G4 band" },
];

export interface Referral {
  id: string;
  referrer: string;
  candidate: string;
  role: string;
  stage: string;
  submitted: string;
  reward: number;
  currency: string;
  rewardState: "Not yet due" | "Due on 3-month mark" | "Paid" | "Not payable";
  conflictDeclared: boolean;
}

export const referrals: Referral[] = [
  { id: "REF-0019", referrer: "Chanda Mwansa-Chileshe", candidate: "Mwaka Lungu", role: "Maintenance Planner", stage: "Offer sent", submitted: "2026-06-02", reward: 5_000, currency: "ZMW", rewardState: "Not yet due", conflictDeclared: false },
  { id: "REF-0016", referrer: "Gift Zulu", candidate: "Chembo Katongo", role: "Yard Supervisor", stage: "Offer approval", submitted: "2026-05-18", reward: 5_000, currency: "ZMW", rewardState: "Not yet due", conflictDeclared: true },
  { id: "REF-0011", referrer: "Thandiwe Banda", candidate: "Lubona Mubita", role: "Welding Technician", stage: "Hired", submitted: "2026-03-04", reward: 5_000, currency: "ZMW", rewardState: "Due on 3-month mark", conflictDeclared: false },
];

/* -------------------------------------------------------------- feedback */

export interface Feedback {
  id: string;
  from: string;
  to: string;
  kind: "Praise" | "Suggestion" | "Check-in note";
  note: string;
  when: string;
  visibility: "Shared with recipient" | "Recipient and manager";
}

export const feedback: Feedback[] = [
  { id: "FB-0091", from: "Mutale Kabwe", to: "Chanda Mwansa-Chileshe", kind: "Praise", note: "Handled the Gate 3 outage calmly and kept the shift moving.", when: "2026-07-25", visibility: "Recipient and manager" },
  { id: "FB-0088", from: "Thandiwe Banda", to: "Chanda Mwansa-Chileshe", kind: "Suggestion", note: "The planning handover note could be shorter — a bullet list would land better with the night shift.", when: "2026-07-19", visibility: "Shared with recipient" },
  { id: "FB-0084", from: "Chanda Mwansa-Chileshe", to: "Mutale Kabwe", kind: "Check-in note", note: "Agreed to revisit the retrofit timeline once the Livingstone review is done.", when: "2026-07-14", visibility: "Recipient and manager" },
];

export interface Pip {
  id: string;
  employee: string;
  opened: string;
  reviewDate: string;
  status: "Active" | "Met" | "Extended" | "Not met";
  focus: string;
  support: string[];
  milestones: { label: string; due: string; done: boolean }[];
}

export const pips: Pip[] = [
  {
    id: "PIP-0007",
    employee: "Employee E · Kitwe Depot",
    opened: "2026-06-15",
    reviewDate: "2026-09-15",
    status: "Active",
    focus: "Consistency of stock counts and shift log completion.",
    support: ["Weekly 1:1 with the depot supervisor", "Refresher on the stock system", "Reduced overtime while on plan"],
    milestones: [
      { label: "Stock system refresher completed", due: "2026-07-04", done: true },
      { label: "Four consecutive accurate counts", due: "2026-08-08", done: true },
      { label: "Shift log complete for a full month", due: "2026-09-05", done: false },
    ],
  },
];

/* ------------------------------------------------------------ succession */

export interface CriticalRole {
  id: string;
  role: string;
  incumbent: string;
  risk: "Low" | "Medium" | "High";
  reason: string;
  successors: { name: string; readiness: "Ready now" | "1–2 years" | "Longer term" }[];
}

export const criticalRoles: CriticalRole[] = [
  {
    id: "CR-001",
    role: "Operations Manager, Lusaka",
    incumbent: "Mutale Kabwe",
    risk: "High",
    reason: "Single point of approval for leave, attendance and expenses across three branches.",
    successors: [
      { name: "Chanda Mwansa-Chileshe", readiness: "1–2 years" },
      { name: "Thandiwe Banda", readiness: "Longer term" },
    ],
  },
  {
    id: "CR-002",
    role: "Payroll Analyst",
    incumbent: "Nalukui Simasiku",
    risk: "High",
    reason: "Only person who runs payroll. Also going on assignment in October.",
    successors: [{ name: "Thandiwe Banda", readiness: "1–2 years" }],
  },
  {
    id: "CR-003",
    role: "Site Electrical Safety Officer",
    incumbent: "Vacant",
    risk: "High",
    reason: "Statutory duty currently covered by a contractor.",
    successors: [],
  },
  {
    id: "CR-004",
    role: "Depot Supervisor, Kitwe",
    incumbent: "Emmanuel Sakala",
    risk: "Medium",
    reason: "Retiring January 2027. Handover started.",
    successors: [{ name: "Grace Nyirenda (alumna)", readiness: "Ready now" }],
  },
];

export interface Skill {
  name: string;
  category: string;
  held: number;
  needed: number;
  scarce: boolean;
}

export const skills: Skill[] = [
  { name: "ZS 385 electrical safety electrical safety", category: "Statutory", held: 0, needed: 1, scarce: true },
  { name: "Counterbalance forklift", category: "Operational", held: 1, needed: 2, scarce: true },
  { name: "EN ISO 9606-1 welding", category: "Operational", held: 1, needed: 1, scarce: false },
  { name: "Payroll statutory filing", category: "Finance", held: 1, needed: 2, scarce: true },
  { name: "Maintenance planning", category: "Operational", held: 2, needed: 2, scarce: false },
  { name: "First aid at work", category: "Safety", held: 2, needed: 3, scarce: false },
];

export interface Opportunity {
  id: string;
  title: string;
  kind: "Internal role" | "Project" | "Mentoring" | "Secondment";
  branch: string;
  commitment: string;
  skills: string[];
  closes: string;
}

export const opportunities: Opportunity[] = [
  { id: "OPP-0012", title: "Payroll cover during October assignment", kind: "Secondment", branch: "Chingola Office", commitment: "4 months, full time", skills: ["Payroll statutory filing"], closes: "2026-08-22" },
  { id: "OPP-0009", title: "Zuidhaven retrofit — planning support", kind: "Project", branch: "Lusaka HQ", commitment: "1 day a week, 3 months", skills: ["Maintenance planning"], closes: "2026-08-15" },
  { id: "OPP-0007", title: "Mentor a first-time manager", kind: "Mentoring", branch: "Any", commitment: "1 hour a month", skills: ["People management"], closes: "2026-09-01" },
];

/* ------------------------------------------------------------------ TOIL */

export interface ToilEntry {
  id: string;
  employee: string;
  earned: string;
  hours: number;
  source: string;
  expires: string;
  state: "Available" | "Booked" | "Expired" | "Paid out";
}

export const toil: ToilEntry[] = [
  { id: "TOIL-0031", employee: "Chanda Mwansa-Chileshe", earned: "2026-07-19", hours: 3.5, source: "Weekend callout", expires: "2027-01-19", state: "Available" },
  { id: "TOIL-0028", employee: "Chanda Mwansa-Chileshe", earned: "2026-05-02", hours: 2, source: "Public holiday cover", expires: "2026-11-02", state: "Booked" },
  { id: "TOIL-0021", employee: "Kondwani Mwanza", earned: "2026-01-14", hours: 4, source: "Shutdown overtime", expires: "2026-07-14", state: "Expired" },
  { id: "TOIL-0034", employee: "Emmanuel Sakala", earned: "2026-07-26", hours: 6, source: "Weekend stock count", expires: "2027-01-26", state: "Available" },
];

export interface FatigueRule {
  id: string;
  rule: string;
  limit: string;
  breaches: number;
  note: string;
}

export const fatigueRules: FatigueRule[] = [
  { id: "FTG-01", rule: "Rest between shifts", limit: "At least 11 hours", breaches: 0, note: "Checked when a roster is published." },
  { id: "FTG-02", rule: "Consecutive days worked", limit: "No more than 6", breaches: 1, note: "One breach flagged at Solwezi Yard in July." },
  { id: "FTG-03", rule: "Weekly hours average", limit: "48 over 17 weeks", breaches: 0, note: "Currently averaging 39.4." },
  { id: "FTG-04", rule: "Night shifts in a row", limit: "No more than 4", breaches: 0, note: "Not currently used at any branch." },
];

/* ----------------------------------------------------------- utilisation */

export interface UtilisationRow {
  employee: string;
  capacity: number;
  billable: number;
  nonBillable: number;
  absence: number;
}

export const utilisation: UtilisationRow[] = [
  { employee: "Chanda Mwansa-Chileshe", capacity: 160, billable: 62, nonBillable: 88, absence: 10 },
  { employee: "Kondwani Mwanza", capacity: 160, billable: 128, nonBillable: 24, absence: 8 },
  { employee: "Emmanuel Sakala", capacity: 160, billable: 0, nonBillable: 96, absence: 64 },
  { employee: "Thandiwe Banda", capacity: 96, billable: 0, nonBillable: 96, absence: 0 },
];

export interface HandoffLine {
  id: string;
  destination: "Payroll" | "Accounting";
  what: string;
  period: string;
  amount?: number;
  currency?: string;
  hours?: number;
  state: "Ready" | "Sent" | "Held";
  note?: string;
}

export const handoff: HandoffLine[] = [
  { id: "HO-0041", destination: "Payroll", what: "Approved overtime hours", period: "July 2026", hours: 43.5, state: "Sent", note: "Included in the July run." },
  { id: "HO-0042", destination: "Accounting", what: "Billable time — Zuidhaven retrofit", period: "July 2026", amount: 96_500, currency: "ZMW", state: "Ready" },
  { id: "HO-0043", destination: "Payroll", what: "TOIL taken instead of pay", period: "July 2026", hours: 2, state: "Sent" },
  { id: "HO-0044", destination: "Accounting", what: "Non-billable planning time", period: "July 2026", amount: 148_000, currency: "ZMW", state: "Held", note: "Waiting on the cost-centre move to CC-OPS-LUS-02." },
];

/* ----------------------------------------------------------------- union */

export interface Agreement {
  id: string;
  union: string;
  covers: string;
  members: number;
  from: string;
  to: string;
  status: "Active" | "In negotiation" | "Expired";
  keyTerms: string[];
}

export const agreements: Agreement[] = [
  {
    id: "CBA-2024-ZM",
    union: "NUMAW",
    covers: "Production and maintenance roles, Zambia",
    members: 2,
    from: "2024-01-01",
    to: "2026-12-31",
    status: "Active",
    keyTerms: ["Annual uplift linked to sector agreement", "Overtime at 1.5× after 40 hours", "Shift allowance floor of K30 per qualifying shift"],
  },
  {
    id: "CBA-2027-ZM",
    union: "NUMAW",
    covers: "Production and maintenance roles, Zambia",
    members: 2,
    from: "2027-01-01",
    to: "2029-12-31",
    status: "In negotiation",
    keyTerms: ["Opening position exchanged 12 Jul", "Next session 4 Sep"],
  },
];

export interface RollCall {
  id: string;
  event: string;
  started: string;
  branch: string;
  expected: number;
  safe: number;
  noResponse: number;
  status: "Active" | "Closed";
  outstanding: string[];
}

export const rollCalls: RollCall[] = [
  { id: "RC-2026-003", event: "Port access incident — practice roll call", started: "2026-07-18 09:14", branch: "Solwezi Yard", expected: 1, safe: 1, noResponse: 0, status: "Closed", outstanding: [] },
  { id: "RC-2026-002", event: "Fire alarm — Livingstone Works", started: "2026-06-02 14:31", branch: "Livingstone Works", expected: 2, safe: 1, noResponse: 1, status: "Closed", outstanding: ["Resolved on site — one person was on approved leave and not present."] },
];

/* --------------------------------------------------------------- reports */

export interface SavedReport {
  id: string;
  name: string;
  base: string;
  fields: string[];
  filters: string[];
  owner: string;
  shared: string;
  lastRun: string;
}

export const savedReports: SavedReport[] = [
  { id: "MY-0012", name: "Contracts ending next 90 days", base: "Employees", fields: ["Name", "Entity", "Employment type", "End date", "Manager"], filters: ["Employment type is Fixed term", "End date within 90 days"], owner: "Thandiwe Banda", shared: "HR operations", lastRun: "2026-07-29" },
  { id: "MY-0009", name: "Open attendance exceptions by branch", base: "Attendance", fields: ["Reference", "Employee", "Branch", "Date", "Status"], filters: ["Status is not Approved"], owner: "Thandiwe Banda", shared: "Private", lastRun: "2026-07-28" },
  { id: "MY-0004", name: "Licence expiry watchlist", base: "Positions", fields: ["Position", "Incumbent", "Licence", "Expires"], filters: ["Mandatory licence is set", "Expires within 120 days"], owner: "Mutale Kabwe", shared: "Managers", lastRun: "2026-07-27" },
];

export const extrasApi = {
  assets: async () => { await delay(); return assets; },
  journeys: async () => { await delay(); return journeys; },
  assignments: async () => { await delay(); return assignments; },
  alumni: async () => { await delay(); return alumni; },
  offers: async () => { await delay(); return offers; },
  referrals: async () => { await delay(); return referrals; },
  feedback: async () => { await delay(); return feedback; },
  pips: async () => { await delay(); return pips; },
  criticalRoles: async () => { await delay(); return criticalRoles; },
  skills: async () => { await delay(); return skills; },
  opportunities: async () => { await delay(); return opportunities; },
  toil: async () => { await delay(); return toil; },
  fatigueRules: async () => { await delay(300); return fatigueRules; },
  utilisation: async () => { await delay(); return utilisation; },
  handoff: async () => { await delay(); return handoff; },
  agreements: async () => { await delay(); return agreements; },
  rollCalls: async () => { await delay(); return rollCalls; },
  savedReports: async () => { await delay(); return savedReports; },
};
