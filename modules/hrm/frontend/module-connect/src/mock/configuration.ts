/**
 * Configuration data (HRM-004 and the admin side of most other parents).
 *
 * Product rules encoded here:
 *  - Statutory values are configuration with an effective date, never constants.
 *    Changing a rate must not rewrite what a past payslip was calculated on.
 *  - A country pack is versioned and approved, not a checkbox.
 *  - Protected-disclosure handling is set here but can never be granted to an
 *    ordinary HR administrator.
 */
const delay = (ms = 380) => new Promise((r) => setTimeout(r, ms));

export const money = (v: number, c = "ZMW") =>
  new Intl.NumberFormat(undefined, { style: "currency", currency: c, maximumFractionDigits: 0 }).format(v);

/* ------------------------------------------------------- business setup */

export interface Grade {
  grade: string;
  family: string;
  min: number;
  mid: number;
  max: number;
  holders: number;
}

export const grades: Grade[] = [
  { grade: "G2", family: "Operative", min: 48_000, mid: 60_000, max: 72_000, holders: 0 },
  { grade: "G3", family: "Operative", min: 66_000, mid: 78_000, max: 90_000, holders: 0 },
  { grade: "G4", family: "Technical", min: 96_000, mid: 120_000, max: 144_000, holders: 1 },
  { grade: "G5", family: "Professional", min: 120_000, mid: 150_000, max: 180_000, holders: 1 },
  { grade: "G6", family: "Professional", min: 156_000, mid: 192_000, max: 228_000, holders: 2 },
  { grade: "G7", family: "Senior professional", min: 192_000, mid: 228_000, max: 264_000, holders: 1 },
  { grade: "G8", family: "Lead", min: 240_000, mid: 288_000, max: 336_000, holders: 0 },
  { grade: "G9", family: "Management", min: 300_000, mid: 360_000, max: 420_000, holders: 1 },
];

export interface Holiday {
  date: string;
  name: string;
  scope: "National" | "Site";
  note?: string;
}

export const holidays2026: Holiday[] = [
  { date: "2026-01-01", name: "New Year's Day", scope: "National" },
  { date: "2026-03-12", name: "Youth Day", scope: "National" },
  { date: "2026-04-03", name: "Good Friday", scope: "National" },
  { date: "2026-04-06", name: "Easter Monday", scope: "National" },
  { date: "2026-04-28", name: "Kenneth Kaunda Day", scope: "National" },
  { date: "2026-05-01", name: "Labour Day", scope: "National" },
  { date: "2026-05-25", name: "Africa Freedom Day", scope: "National" },
  { date: "2026-07-06", name: "Heroes' Day", scope: "National" },
  { date: "2026-07-07", name: "Unity Day", scope: "National" },
  { date: "2026-08-03", name: "Farmers' Day", scope: "National" },
  { date: "2026-08-07", name: "Plant shutdown", scope: "Site", note: "Ndola Plant only. Paid company closure." },
  { date: "2026-10-18", name: "National Prayer Day", scope: "National" },
  { date: "2026-10-24", name: "Independence Day", scope: "National" },
  { date: "2026-12-25", name: "Christmas Day", scope: "National" },
];

export interface CountryPack {
  id: string;
  country: string;
  version: string;
  status: "Active" | "Draft" | "Superseded";
  effectiveFrom: string;
  approvedBy?: string;
  covers: string[];
  source: string;
}

export const countryPacks: CountryPack[] = [
  {
    id: "ZM-2026",
    country: "Zambia",
    version: "v3",
    status: "Active",
    effectiveFrom: "2026-01-01",
    approvedBy: "Nalukui Simasiku (Payroll)",
    covers: ["PAYE bands", "NAPSA rates and ceiling", "NHIMA rates", "Statutory leave", "Public holidays", "NRC and TPIN formats"],
    source: "ZRA Practice Note 1/2026, NAPSA circular 2026/02, NHIMA statutory instrument",
  },
  {
    id: "ZM-2027",
    country: "Zambia",
    version: "v4 draft",
    status: "Draft",
    effectiveFrom: "2027-01-01",
    covers: ["PAYE bands (awaiting Budget)", "NAPSA ceiling"],
    source: "Not yet published — placeholder pending the 2027 national Budget.",
  },
  {
    id: "ZM-2025",
    country: "Zambia",
    version: "v2",
    status: "Superseded",
    effectiveFrom: "2025-01-01",
    approvedBy: "Nalukui Simasiku (Payroll)",
    covers: ["PAYE bands", "NAPSA rates", "NHIMA rates"],
    source: "Retained so 2025 payslips stay reproducible.",
  },
];

export interface Language {
  name: string;
  code: string;
  state: "Default" | "Active" | "Draft";
  coverage: number;
}

export const languages: Language[] = [
  { name: "English", code: "en-ZM", state: "Default", coverage: 100 },
  { name: "Bemba", code: "bem", state: "Active", coverage: 62 },
  { name: "Nyanja", code: "nya", state: "Draft", coverage: 18 },
];

export interface PayComponent {
  code: string;
  label: string;
  kind: "Earning" | "Deduction" | "Employer";
  basis: string;
  taxable: boolean;
  pensionable: boolean;
  effectiveFrom: string;
}

export const payComponents: PayComponent[] = [
  { code: "BASIC", label: "Basic pay", kind: "Earning", basis: "Annual salary ÷ 12", taxable: true, pensionable: true, effectiveFrom: "2026-01-01" },
  { code: "SHIFT", label: "Shift allowance", kind: "Earning", basis: "Qualifying shifts × K150", taxable: true, pensionable: true, effectiveFrom: "2026-04-01" },
  { code: "OT", label: "Overtime", kind: "Earning", basis: "Approved hours × hourly × 1.5", taxable: true, pensionable: true, effectiveFrom: "2025-07-01" },
  { code: "PAYE", label: "PAYE", kind: "Deduction", basis: "ZRA 2026 monthly bands", taxable: false, pensionable: false, effectiveFrom: "2026-01-01" },
  { code: "NAPSA", label: "NAPSA (employee)", kind: "Deduction", basis: "5% of pensionable pay, capped", taxable: false, pensionable: false, effectiveFrom: "2026-01-01" },
  { code: "NHIMA", label: "NHIMA (employee)", kind: "Deduction", basis: "1% of basic pay", taxable: false, pensionable: false, effectiveFrom: "2026-01-01" },
  { code: "NAPSA-ER", label: "NAPSA (employer)", kind: "Employer", basis: "5% of pensionable pay, capped", taxable: false, pensionable: false, effectiveFrom: "2026-01-01" },
  { code: "NHIMA-ER", label: "NHIMA (employer)", kind: "Employer", basis: "1% of basic pay", taxable: false, pensionable: false, effectiveFrom: "2026-01-01" },
];

/* ------------------------------------------------------- process design */

export interface LeavePolicy {
  id: string;
  name: string;
  entitlement: string;
  accrual: string;
  carryOver: string;
  evidence: string;
  status: "Active" | "Draft";
}

export const leavePolicies: LeavePolicy[] = [
  { id: "LP-ANN", name: "Annual leave", entitlement: "24 days a year", accrual: "2 days a month", carryOver: "Up to 10 days, expires 30 June", evidence: "None", status: "Active" },
  { id: "LP-SICK", name: "Sick leave", entitlement: "Up to 6 months (3 full pay, 3 half pay)", accrual: "Not accrued", carryOver: "Not applicable", evidence: "Medical certificate after day 2", status: "Active" },
  { id: "LP-MAT", name: "Maternity leave", entitlement: "14 weeks fully paid", accrual: "Available after 2 years' service", carryOver: "Not applicable", evidence: "Medical confirmation", status: "Active" },
  { id: "LP-PAT", name: "Paternity leave", entitlement: "5 continuous days", accrual: "Available after 12 months", carryOver: "Not applicable", evidence: "Birth record", status: "Active" },
  { id: "LP-COMP", name: "Compassionate leave", entitlement: "Up to 12 days a year", accrual: "Not accrued", carryOver: "Not applicable", evidence: "At manager's discretion", status: "Active" },
  { id: "LP-STUDY", name: "Study leave", entitlement: "Up to 10 days a year", accrual: "Not accrued", carryOver: "Not applicable", evidence: "Course confirmation", status: "Draft" },
];

export interface ShiftRule {
  id: string;
  rule: string;
  value: string;
  appliesTo: string;
}

export const shiftRules: ShiftRule[] = [
  { id: "SR-01", rule: "Standard working week", value: "48 hours, 6 days", appliesTo: "All sites" },
  { id: "SR-02", rule: "Overtime multiplier", value: "1.5× ordinary rate", appliesTo: "All sites" },
  { id: "SR-03", rule: "Sunday and holiday work", value: "2.0× ordinary rate", appliesTo: "All sites" },
  { id: "SR-04", rule: "Unpaid break", value: "30 minutes after 5 hours", appliesTo: "All sites" },
  { id: "SR-05", rule: "Grace period on clock-in", value: "7 minutes", appliesTo: "Ndola Plant, Livingstone Works" },
  { id: "SR-06", rule: "Correction window", value: "14 days from the shift", appliesTo: "All sites" },
];

export interface ApprovalRoute {
  id: string;
  what: string;
  steps: string[];
  escalation: string;
}

export const approvalRoutes: ApprovalRoute[] = [
  { id: "AR-LEAVE", what: "Leave request", steps: ["Line manager"], escalation: "HR operations after 3 working days" },
  { id: "AR-LEAVE-LONG", what: "Leave over 10 days", steps: ["Line manager", "HR operations"], escalation: "Department head after 5 working days" },
  { id: "AR-ATT", what: "Attendance correction", steps: ["Line manager", "HR operations"], escalation: "Payroll before cutoff" },
  { id: "AR-EXP", what: "Expense claim", steps: ["Line manager"], escalation: "Finance after 5 working days" },
  { id: "AR-EXP-NOREC", what: "Claim with a missing receipt", steps: ["Line manager", "Department head"], escalation: "Finance" },
  { id: "AR-PAY", what: "Pay run release", steps: ["Preparer", "Second approver (never the preparer)"], escalation: "Finance Director" },
  { id: "AR-REQ", what: "Recruitment requisition", steps: ["Department head", "Finance", "HR operations"], escalation: "Managing Director" },
];

export interface RequestCategory {
  id: string;
  category: string;
  target: string;
  owner: string;
  confidential: boolean;
}

export const requestCategories: RequestCategory[] = [
  { id: "RC-LETTER", category: "Employment letter", target: "2 working days", owner: "HR operations", confidential: false },
  { id: "RC-SALARY", category: "Salary confirmation", target: "2 working days", owner: "HR operations", confidential: true },
  { id: "RC-DATA", category: "Personal data change", target: "3 working days", owner: "HR operations", confidential: true },
  { id: "RC-PAY", category: "Payroll query", target: "3 working days", owner: "Payroll", confidential: true },
  { id: "RC-CONTRACT", category: "Contract query", target: "5 working days", owner: "HR operations", confidential: false },
  { id: "RC-OTHER", category: "Something else", target: "5 working days", owner: "HR operations", confidential: false },
];

export interface FormDef {
  id: string;
  name: string;
  fields: number;
  custom: number;
  usedBy: string;
  status: "Published" | "Draft";
}

export const forms: FormDef[] = [
  { id: "FRM-EMP", name: "Employee record", fields: 24, custom: 3, usedBy: "Add employee, profile", status: "Published" },
  { id: "FRM-LEAVE", name: "Leave request", fields: 7, custom: 1, usedBy: "Request leave", status: "Published" },
  { id: "FRM-EXP", name: "Expense line", fields: 9, custom: 2, usedBy: "Expense claim", status: "Published" },
  { id: "FRM-INC", name: "Incident report", fields: 11, custom: 0, usedBy: "Health and safety", status: "Published" },
  { id: "FRM-EXIT", name: "Exit interview", fields: 14, custom: 6, usedBy: "Offboarding", status: "Draft" },
];

export interface AutomationRule {
  id: string;
  name: string;
  trigger: string;
  action: string;
  state: "On" | "Off";
  lastRun?: string;
}

export const automations: AutomationRule[] = [
  { id: "AU-01", name: "Contract expiry alert", trigger: "60 days before an end date", action: "Notify line manager and HR", state: "On", lastRun: "2026-07-28" },
  { id: "AU-02", name: "Licence expiry alert", trigger: "90 days before a mandatory licence expires", action: "Notify holder, manager and HR", state: "On", lastRun: "2026-07-29" },
  { id: "AU-03", name: "Probation review reminder", trigger: "14 days before probation ends", action: "Create a task for the manager", state: "On", lastRun: "2026-07-20" },
  { id: "AU-04", name: "Unresolved attendance before cutoff", trigger: "2 days before payroll cutoff", action: "Notify Payroll and the manager", state: "On", lastRun: "2026-07-28" },
  { id: "AU-05", name: "Birthday greeting", trigger: "On the day", action: "Post to the team feed", state: "Off" },
];

export interface Template {
  id: string;
  name: string;
  channel: "Letter" | "Email" | "SMS" | "In-app";
  language: string;
  updated: string;
}

export const templates: Template[] = [
  { id: "TPL-EMP", name: "Employment confirmation letter", channel: "Letter", language: "English", updated: "2026-04-02" },
  { id: "TPL-SAL", name: "Salary confirmation letter", channel: "Letter", language: "English", updated: "2026-04-02" },
  { id: "TPL-LEAVE-OK", name: "Leave approved", channel: "In-app", language: "English, Bemba", updated: "2026-06-11" },
  { id: "TPL-LEAVE-RET", name: "Leave returned for information", channel: "In-app", language: "English, Bemba", updated: "2026-06-11" },
  { id: "TPL-PAYSLIP", name: "Payslip released", channel: "SMS", language: "English", updated: "2026-05-30" },
  { id: "TPL-SERVICE", name: "Service certificate", channel: "Letter", language: "English", updated: "2025-11-20" },
];

export interface SelfServiceToggle {
  id: string;
  what: string;
  who: string;
  on: boolean;
  note?: string;
}

export const selfService: SelfServiceToggle[] = [
  { id: "SS-01", what: "Update own contact details", who: "All employees", on: true },
  { id: "SS-02", what: "Update own bank details", who: "All employees", on: true, note: "Change takes effect from the next pay run and is verified against ID." },
  { id: "SS-03", what: "View own payslips", who: "All employees", on: true },
  { id: "SS-04", what: "Book leave without a manager", who: "Nobody", on: false, note: "Kept off — leave always needs a decision." },
  { id: "SS-05", what: "See team leave calendar", who: "All employees", on: true, note: "Shows dates only. Leave type is never shown." },
  { id: "SS-06", what: "Download own full record", who: "All employees", on: true },
];

/* ---------------------------------------------------- security & policy */

export interface DisclosureHandler {
  name: string;
  role: string;
  independent: boolean;
}

export const disclosureHandlers: DisclosureHandler[] = [
  { name: "Thandiwe Banda", role: "HR operations lead", independent: false },
  { name: "External — Chibesa & Co. (retained)", role: "Independent handler", independent: true },
];

export interface RetentionRule {
  id: string;
  record: string;
  keepFor: string;
  basis: string;
  thenWhat: string;
}

export const retentionRules: RetentionRule[] = [
  { id: "RT-PAY", record: "Payroll and tax records", keepFor: "10 years after the tax year", basis: "ZRA requirement", thenWhat: "Securely destroyed" },
  { id: "RT-EMP", record: "Employee file", keepFor: "6 years after employment ends", basis: "Employment Code Act", thenWhat: "Securely destroyed" },
  { id: "RT-RECRUIT", record: "Unsuccessful candidate records", keepFor: "6 months", basis: "Consent", thenWhat: "Deleted unless the candidate opted into a talent pool" },
  { id: "RT-OH", record: "Occupational health outcomes", keepFor: "Employment plus 3 years", basis: "Duty of care", thenWhat: "Securely destroyed" },
  { id: "RT-DISC", record: "Protected disclosure case files", keepFor: "7 years after closure", basis: "Evidential integrity", thenWhat: "Reviewed before disposal" },
];

/* -------------------------------------------------------------- technical */

export interface Integration {
  id: string;
  name: string;
  direction: "Inbound" | "Outbound" | "Two-way";
  state: "Connected" | "Not configured" | "Error";
  lastSync?: string;
  note: string;
}

export const integrations: Integration[] = [
  { id: "INT-IDP", name: "Identity provider (SSO)", direction: "Inbound", state: "Not configured", note: "HRM never stores passwords. Until this is connected, sign-in is demonstration only." },
  { id: "INT-BANK", name: "Bank payment file", direction: "Outbound", state: "Not configured", note: "Generates the payment instruction for the bank. Requires Payroll sign-off to enable." },
  { id: "INT-CLOCK", name: "Attendance terminals", direction: "Inbound", state: "Connected", lastSync: "2026-07-29 06:12", note: "Ndola Plant and Livingstone Works. Gate 3 reader currently faulty." },
  { id: "INT-ZRA", name: "ZRA filing", direction: "Outbound", state: "Not configured", note: "Statutory returns are prepared here and filed manually until this is enabled." },
  { id: "INT-ACC", name: "Accounting journals", direction: "Outbound", state: "Error", lastSync: "2026-07-27 18:40", note: "Cost centre CC-OPS-LUS-02 does not exist in the ledger yet." },
];

export interface NumberSeries {
  id: string;
  what: string;
  format: string;
  next: string;
}

export const numberSeries: NumberSeries[] = [
  { id: "NS-EMP", what: "Employee number", format: "EMP-####", next: "EMP-1009" },
  { id: "NS-LEAVE", what: "Leave request", format: "LV-YYYY-####", next: "LV-2026-0431" },
  { id: "NS-ATT", what: "Attendance correction", format: "AT-YYYY-####", next: "AT-2026-1192" },
  { id: "NS-CASE", what: "HR request", format: "HR-YYYY-####", next: "HR-2026-0918" },
  { id: "NS-EXP", what: "Expense claim", format: "EXP-YYYY-####", next: "EXP-2026-0312" },
];

export interface Vendor {
  id: string;
  name: string;
  service: string;
  contractTo: string;
  dataShared: string;
  review: string;
}

export const vendors: Vendor[] = [
  { id: "VEN-01", name: "Madison Life Insurance", service: "Group life and income protection", contractTo: "2027-03-31", dataShared: "Name, date of birth, salary band", review: "Annual" },
  { id: "VEN-02", name: "Prudential Zambia", service: "Private medical top-up", contractTo: "2027-01-31", dataShared: "Name, dependants, cover level", review: "Annual" },
  { id: "VEN-03", name: "Chibesa & Co.", service: "Independent protected-disclosure handling", contractTo: "2028-06-30", dataShared: "Case content only. No employee directory access.", review: "Every 2 years" },
  { id: "VEN-04", name: "Zanaco", service: "Salary payment processing", contractTo: "Rolling", dataShared: "Name, account number, net amount", review: "Annual" },
];

export const configurationApi = {
  grades: async () => { await delay(); return grades; },
  holidays: async () => { await delay(); return holidays2026; },
  countryPacks: async () => { await delay(); return countryPacks; },
  languages: async () => { await delay(); return languages; },
  payComponents: async () => { await delay(); return payComponents; },
  leavePolicies: async () => { await delay(); return leavePolicies; },
  shiftRules: async () => { await delay(); return shiftRules; },
  approvalRoutes: async () => { await delay(); return approvalRoutes; },
  requestCategories: async () => { await delay(); return requestCategories; },
  forms: async () => { await delay(); return forms; },
  automations: async () => { await delay(); return automations; },
  templates: async () => { await delay(); return templates; },
  selfService: async () => { await delay(); return selfService; },
  disclosureHandlers: async () => { await delay(); return disclosureHandlers; },
  retentionRules: async () => { await delay(); return retentionRules; },
  integrations: async () => { await delay(); return integrations; },
  numberSeries: async () => { await delay(); return numberSeries; },
  vendors: async () => { await delay(); return vendors; },
};
