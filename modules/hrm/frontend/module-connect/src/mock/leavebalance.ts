/**
 * Leave balances are derived, never stored.
 *
 * A single "leaveBalance: 14.5" on an employee record is unfalsifiable — it
 * cannot be checked, argued with, or reproduced. What an employee actually
 * needs to see is the working: what they brought forward, what has accrued so
 * far, what they have taken, and what is already booked.
 *
 * Zambian statutory minimum is 24 days a year, accruing 2 days a month.
 * A contractor accrues nothing, because a contractor is engaged, not employed.
 */
import { employees, leaveRequests } from "./data";
import { grades } from "./configuration";

const delay = (ms = 340) => new Promise((r) => setTimeout(r, ms));

/** The date every balance is stated as at. One "today" for the whole module. */
export const LEAVE_AS_AT = "2026-07-29";
const LEAVE_YEAR_START = "2026-01-01";
const LEAVE_YEAR_END = "2026-12-31";

/** Days brought in from 2025, after the carry-over cap was applied. */
const BROUGHT_FORWARD: Record<string, number> = {
  "w-1001": 5.5,
  "w-1002": 2,
  "w-1003": 9,
  "w-1004": 0,
  "w-1005": 6,
  "w-1006": 0,
  "w-1007": 0,
  "w-1008": 12,
};

export type EntryKind =
  | "Brought forward"
  | "Accrued"
  | "Taken"
  | "Booked"
  | "Requested"
  | "Encashed";

export interface LeaveEntry {
  id: string;
  kind: EntryKind;
  /** Positive adds to the balance, negative takes from it. */
  days: number;
  when: string;
  detail: string;
  /** The request this entry came from, so the figure can be traced. */
  requestId?: string;
}

export interface LeaveBalance {
  employeeId: string;
  employeeName: string;
  policy: string;
  /** Null where the person does not accrue leave at all. */
  annualEntitlement: number | null;
  accrualPerMonth: number;
  asAt: string;
  broughtForward: number;
  accrued: number;
  taken: number;
  booked: number;
  /** Requested but not yet decided — not deducted, but worth showing. */
  requested: number;
  available: number;
  /** What the balance becomes if everything outstanding is approved. */
  projected: number;
  monthsAccrued: number;
  entries: LeaveEntry[];
  /** Set only where somebody is leaving and the balance gets paid out. */
  encashment?: {
    days: number;
    dailyRate: number;
    value: number;
    basis: string;
    lastWorkingDay: string;
    paidIn: string;
  };
  notes: string[];
}

const monthsBetween = (fromIso: string, toIso: string) => {
  const a = new Date(`${fromIso}T00:00:00`);
  const b = new Date(`${toIso}T00:00:00`);
  let m = (b.getFullYear() - a.getFullYear()) * 12 + (b.getMonth() - a.getMonth());
  if (b.getDate() < a.getDate()) m -= 1;
  return Math.max(m, 0);
};

const round1 = (n: number) => Math.round(n * 10) / 10;

/** Monthly pay used to value a day of leave, taken from the grade midpoint. */
function dailyRateFor(grade: string) {
  const g = grades.find((x) => x.grade === grade);
  if (!g) return null;
  // Grade ranges are annual. 22 is the working days in an average month.
  return g.mid / 12 / 22;
}

export function balanceFor(employeeId: string): LeaveBalance | null {
  const e = employees.find((x) => x.id === employeeId);
  if (!e) return null;

  const entries: LeaveEntry[] = [];
  const notes: string[] = [];

  // A contractor is engaged, not employed, so nothing accrues.
  if (e.employmentType === "Contractor") {
    return {
      employeeId,
      employeeName: e.fullName,
      policy: "No leave entitlement — engaged as a contractor",
      annualEntitlement: null,
      accrualPerMonth: 0,
      asAt: LEAVE_AS_AT,
      broughtForward: 0,
      accrued: 0,
      taken: 0,
      booked: 0,
      requested: 0,
      available: 0,
      projected: 0,
      monthsAccrued: 0,
      entries: [],
      notes: [
        "A contractor is engaged, not employed, so no leave accrues and none is payable on exit.",
        "If this person is working set hours under supervision, the classification itself should be reviewed.",
      ],
    };
  }

  // Someone who has not started yet accrues from their start date, not today.
  const notStarted = e.startDate > LEAVE_AS_AT;
  const accrualStart = e.startDate > LEAVE_YEAR_START ? e.startDate : LEAVE_YEAR_START;
  const accrualEnd = e.endDate && e.endDate < LEAVE_AS_AT ? e.endDate : LEAVE_AS_AT;

  const annualEntitlement = 24;
  const accrualPerMonth = annualEntitlement / 12;
  const monthsAccrued = notStarted ? 0 : monthsBetween(accrualStart, accrualEnd);
  const accrued = round1(monthsAccrued * accrualPerMonth);

  const broughtForward = BROUGHT_FORWARD[employeeId] ?? 0;
  if (broughtForward) {
    entries.push({
      id: `${employeeId}-bf`,
      kind: "Brought forward",
      days: broughtForward,
      when: LEAVE_YEAR_START,
      detail: "Carried in from 2025, after the 10-day carry-over cap.",
    });
  }
  if (accrued) {
    entries.push({
      id: `${employeeId}-acc`,
      kind: "Accrued",
      days: accrued,
      when: accrualEnd,
      detail: `${monthsAccrued} complete month${monthsAccrued === 1 ? "" : "s"} at ${accrualPerMonth} days a month.`,
    });
  }

  let taken = 0;
  let booked = 0;
  let requested = 0;

  for (const r of leaveRequests.filter((x) => x.employeeId === employeeId)) {
    // Only annual leave draws down this balance. Sick and parental are
    // separate entitlements; unpaid leave draws down nothing.
    if (r.type !== "Annual") continue;

    if (r.status === "Approved") {
      const past = r.to <= LEAVE_AS_AT;
      if (past) {
        taken += r.days;
        entries.push({
          id: `${r.id}-taken`,
          kind: "Taken",
          days: -r.days,
          when: r.to,
          detail: `${r.from} to ${r.to}, already taken.`,
          requestId: r.id,
        });
      } else {
        booked += r.days;
        entries.push({
          id: `${r.id}-booked`,
          kind: "Booked",
          days: -r.days,
          when: r.from,
          detail: `${r.from} to ${r.to}, approved and still to come.`,
          requestId: r.id,
        });
      }
    } else if (r.status === "Submitted" || r.status === "In review") {
      requested += r.days;
      entries.push({
        id: `${r.id}-req`,
        kind: "Requested",
        days: -r.days,
        when: r.from,
        detail: `${r.from} to ${r.to}, awaiting a decision. Not deducted yet.`,
        requestId: r.id,
      });
    }
  }

  const available = round1(broughtForward + accrued - taken - booked);
  const projected = round1(available - requested);

  if (notStarted) {
    notes.push(`Accrual starts on ${e.startDate}, the first day of employment.`);
  }
  if (requested > 0) {
    notes.push(
      `${requested} day${requested === 1 ? " is" : "s are"} requested but not yet decided. Approving would leave ${projected}.`,
    );
  }
  if (projected < 0) {
    notes.push(
      "Approving everything outstanding would take this balance below zero. It needs either a refusal or an agreed advance.",
    );
  }
  if (broughtForward > 0) {
    notes.push(
      `${broughtForward} carried-over day${broughtForward === 1 ? "" : "s"} must be used by ${LEAVE_YEAR_END} or ${broughtForward === 1 ? "it lapses" : "they lapse"}.`,
    );
  }

  let encashment: LeaveBalance["encashment"];
  if (e.endDate) {
    // Accrue to the last working day, not to today — they keep earning it.
    const toLastDay = monthsBetween(accrualStart, e.endDate);
    const accruedToExit = round1(toLastDay * accrualPerMonth);
    const days = round1(broughtForward + accruedToExit - taken - booked);
    const dailyRate = dailyRateFor(e.grade);
    if (dailyRate !== null) {
      encashment = {
        days,
        dailyRate,
        value: Math.round(days * dailyRate * 100) / 100,
        basis: `Grade ${e.grade} midpoint ÷ 12 months ÷ 22 working days`,
        lastWorkingDay: e.endDate,
        paidIn: "the run covering the last working day",
      };
      notes.push(
        `Accrual continues to ${e.endDate}, so the balance paid out is ${days} days, not today's ${available}.`,
      );
    }
  }

  return {
    employeeId,
    employeeName: e.fullName,
    policy: `Annual leave — ${annualEntitlement} days a year`,
    annualEntitlement,
    accrualPerMonth,
    asAt: LEAVE_AS_AT,
    broughtForward,
    accrued,
    taken,
    booked,
    requested,
    available,
    projected,
    monthsAccrued,
    entries: entries.sort((a, b) => a.when.localeCompare(b.when)),
    encashment,
    notes,
  };
}

export const leaveBalanceApi = {
  balance: async (employeeId: string) => {
    await delay();
    return balanceFor(employeeId);
  },
  all: async () => {
    await delay(420);
    return employees.map((e) => balanceFor(e.id)).filter((b): b is LeaveBalance => b !== null);
  },
};
