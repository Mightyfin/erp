/**
 * Reports and workforce analytics (HRM-008).
 * Self-contained mock data + async reader, matching the mock-service pattern.
 */
const delay = (ms = 420) => new Promise((r) => setTimeout(r, ms));

export type ReportCategory =
  | "Workforce"
  | "Time and absence"
  | "Payroll and cost"
  | "Compliance";

export interface ReportDef {
  id: string;
  name: string;
  category: ReportCategory;
  description: string;
  /** Certified reports have an agreed definition and a named data owner. */
  certified: boolean;
  owner: string;
  lastRun: string;
  /** Set when the report is on a distribution schedule. */
  schedule?: string;
  /** True when output is suppressed below a group-size threshold. */
  privacySuppression: boolean;
  /** Effective-date reporting means history stays reproducible. */
  pointInTime: boolean;
}

export interface Metric {
  id: string;
  label: string;
  value: string;
  change: string;
  direction: "up" | "down" | "flat";
  /** Whether an increase is good, bad, or neither — never imply it by colour alone. */
  sentiment: "good" | "bad" | "neutral";
  definition: string;
  source: string;
}

export const headlineMetrics: Metric[] = [
  {
    id: "m-headcount",
    label: "Headcount",
    value: "8",
    change: "+1 vs last month",
    direction: "up",
    sentiment: "neutral",
    definition: "Active employees with a current assignment on the last day of the period.",
    source: "Employee records · point-in-time",
  },
  {
    id: "m-turnover",
    label: "Turnover (12 mo. rolling)",
    value: "11.4%",
    change: "-1.2pp vs last month",
    direction: "down",
    sentiment: "good",
    definition: "Leavers in the last 12 months ÷ average headcount over the same period.",
    source: "Separation records",
  },
  {
    id: "m-absence",
    label: "Absence rate",
    value: "2.8%",
    change: "+0.4pp vs last month",
    direction: "up",
    sentiment: "bad",
    definition: "Absence days ÷ scheduled working days, excluding approved annual leave.",
    source: "Leave and attendance",
  },
  {
    id: "m-overtime",
    label: "Overtime hours",
    value: "43.5",
    change: "+6.0 vs last month",
    direction: "up",
    sentiment: "bad",
    definition: "Approved overtime hours in the period, including approved corrections.",
    source: "Attendance · approved only",
  },
];

export const reports: ReportDef[] = [
  {
    id: "RPT-HC-001",
    name: "Headcount and establishment",
    category: "Workforce",
    description: "Actual versus approved establishment by entity, branch and department.",
    certified: true,
    owner: "HR operations",
    lastRun: "2026-07-28",
    schedule: "Monthly, 1st working day",
    privacySuppression: false,
    pointInTime: true,
  },
  {
    id: "RPT-HC-002",
    name: "Turnover and retention",
    category: "Workforce",
    description: "Leavers, reasons and retention by tenure band and manager.",
    certified: true,
    owner: "HR operations",
    lastRun: "2026-07-28",
    schedule: "Monthly, 1st working day",
    privacySuppression: true,
    pointInTime: true,
  },
  {
    id: "RPT-HC-003",
    name: "Contracts expiring",
    category: "Workforce",
    description: "Fixed-term contracts ending in the next 90 days, with renewal decision status.",
    certified: false,
    owner: "HR operations",
    lastRun: "2026-07-29",
    schedule: "Weekly, Monday",
    privacySuppression: false,
    pointInTime: false,
  },
  {
    id: "RPT-TA-001",
    name: "Absence and attendance exceptions",
    category: "Time and absence",
    description: "Unresolved attendance exceptions and absence patterns by branch.",
    certified: false,
    owner: "HR operations",
    lastRun: "2026-07-29",
    privacySuppression: true,
    pointInTime: false,
  },
  {
    id: "RPT-TA-002",
    name: "Leave liability",
    category: "Time and absence",
    description: "Accrued and unused leave balances valued at current salary.",
    certified: true,
    owner: "Payroll",
    lastRun: "2026-07-27",
    schedule: "Monthly, with the pay run",
    privacySuppression: false,
    pointInTime: true,
  },
  {
    id: "RPT-PY-001",
    name: "Payroll register",
    category: "Payroll and cost",
    description: "Gross-to-net by employee for a released pay period.",
    certified: true,
    owner: "Payroll",
    lastRun: "2026-07-27",
    privacySuppression: false,
    pointInTime: true,
  },
  {
    id: "RPT-PY-002",
    name: "Workforce cost by cost centre",
    category: "Payroll and cost",
    description: "Employer cost including contributions, split by cost centre and entity.",
    certified: true,
    owner: "Payroll",
    lastRun: "2026-07-27",
    schedule: "Monthly, after pay release",
    privacySuppression: false,
    pointInTime: true,
  },
  {
    id: "RPT-CP-001",
    name: "Mandatory training compliance",
    category: "Compliance",
    description: "Completion and expiry of mandatory training and licences by role.",
    certified: false,
    owner: "HR operations",
    lastRun: "2026-07-29",
    schedule: "Weekly, Monday",
    privacySuppression: false,
    pointInTime: false,
  },
  {
    id: "RPT-CP-002",
    name: "Pay gap analysis",
    category: "Compliance",
    description: "Pay gap by protected characteristic, with small-group suppression applied.",
    certified: true,
    owner: "HR operations",
    lastRun: "2026-06-30",
    schedule: "Annually",
    privacySuppression: true,
    pointInTime: true,
  },
];

export const reportsApi = {
  metrics: async () => {
    await delay(300);
    return headlineMetrics;
  },
  list: async () => {
    await delay();
    return reports;
  },
};
