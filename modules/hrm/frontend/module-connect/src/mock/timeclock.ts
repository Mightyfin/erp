/**
 * Clocking, rosters and shift-change requests (UI-TIM-001, UI-TIM-005).
 * Self-contained mock data + async reader, matching the mock-service pattern.
 *
 * Everything here is from the point of view of Chanda Mwansa-Chileshe
 * (w-1001, Lusaka HQ), whose manager is Mutale Kabwe (w-1002).
 */
import type { TimelineEvent } from "./types";

const delay = (ms = 420) => new Promise((r) => setTimeout(r, ms));

/* ------------------------------------------------------------------ today */

export type ClockState = "out" | "in" | "break";

export type PunchKind = "in" | "break-start" | "break-end" | "out";

/** A punch is a timeline event plus how it was captured and whether it has reached the server. */
export interface PunchEvent extends TimelineEvent {
  kind: PunchKind;
  /** Local time the employee actually punched, preserved even when the punch is queued. */
  localTime: string;
  queued?: boolean;
}

export interface ScheduledShift {
  name: string;
  start: string;
  end: string;
  location: string;
  breakMinutes: number;
  breakDueBy: string;
  paidHours: number;
}

/** How this punch is captured, shown to the employee rather than hidden. */
export interface CaptureSource {
  label: string;
  channel: string;
  device: string;
  network: string;
  locationRecorded: boolean;
  locationDetail: string;
  consentNote: string;
}

export interface TodayRecord {
  date: string;
  dayLabel: string;
  state: ClockState;
  /** Local clock-in time, e.g. "07:02". Null when the employee has not clocked in yet. */
  clockedInAt: string | null;
  /** Worked minutes already banked when the screen loaded. */
  workedMinutesAtLoad: number;
  /** Break minutes already taken today. */
  breakMinutesTaken: number;
  /** Mock "now" as minutes past midnight, so elapsed time is deterministic. */
  nowMinutesAtLoad: number;
  shift: ScheduledShift;
  source: CaptureSource;
  punches: PunchEvent[];
  payrollCutoff: string;
  manager: string;
}

const shiftToday: ScheduledShift = {
  name: "Early shift (E1)",
  start: "07:00",
  end: "15:30",
  location: "Lusaka HQ — Maintenance planning office",
  breakMinutes: 30,
  breakDueBy: "12:30",
  paidHours: 8,
};

const captureSource: CaptureSource = {
  label: "Web · Lusaka HQ (on-site network)",
  channel: "Web",
  device: "This browser, signed in as Chanda Mwansa-Chileshe",
  network: "Lusaka HQ (on-site network)",
  locationRecorded: false,
  locationDetail:
    "Precise location is not recorded for this punch. Your organisation identifies the site from the on-site network only.",
  consentNote:
    "Location is recorded only where it is lawful and only when you have been told first. Nothing about your location is captured off-site or outside your shift.",
};

const todayRecord: TodayRecord = {
  date: "2026-07-29",
  dayLabel: "Wednesday 29 July 2026",
  state: "in",
  clockedInAt: "07:02",
  workedMinutesAtLoad: 313,
  breakMinutesTaken: 0,
  nowMinutesAtLoad: 12 * 60 + 15,
  shift: shiftToday,
  source: captureSource,
  payrollCutoff: "17:00 on 5 August 2026",
  manager: "Mutale Kabwe (Operations Manager)",
  punches: [
    {
      id: "p-1",
      kind: "in",
      at: "2026-07-29T07:02:00+02:00",
      localTime: "07:02",
      actor: "Chanda Mwansa-Chileshe",
      event: "Clocked in",
      after: "Web · Lusaka HQ (on-site network)",
    },
  ],
};

/* ------------------------------------------------------- last seven days */

export interface DayException {
  kind: "Missing punch" | "Late arrival" | "Early departure";
  detail: string;
  /** Said plainly: what happens if nobody fixes this before payroll closes. */
  consequence: string;
  /** Set when a correction has already been raised for the day. */
  correctionRef?: string;
  correctionStatus?: string;
  correctionOwner?: string;
  correctionDue?: string;
}

export interface DaySummary {
  id: string;
  date: string;
  dayLabel: string;
  shift: string;
  recorded: string;
  scheduledHours: number;
  /** Null when a punch is missing and the actual hours cannot be worked out. */
  actualHours: number | null;
  status: "Clean" | "Rest day" | "Exception" | "Resolved";
  exception?: DayException;
  resolvedNote?: string;
}

const recentDays: DaySummary[] = [
  {
    id: "2026-07-28",
    date: "2026-07-28",
    dayLabel: "Tue 28 Jul",
    shift: "Early shift (E1)",
    recorded: "06:58 – 15:34",
    scheduledHours: 8,
    actualHours: 8.1,
    status: "Clean",
  },
  {
    id: "2026-07-27",
    date: "2026-07-27",
    dayLabel: "Mon 27 Jul",
    shift: "Early shift (E1)",
    recorded: "07:24 – 15:31",
    scheduledHours: 8,
    actualHours: 7.6,
    status: "Exception",
    exception: {
      kind: "Late arrival",
      detail:
        "Clocked in 24 minutes after the shift start of 07:00. No correction has been raised.",
      consequence:
        "If this is not corrected before the payroll cutoff, the day is paid on recorded hours only — 0.4 hours short — and any later fix moves to the September pay run.",
    },
  },
  {
    id: "2026-07-26",
    date: "2026-07-26",
    dayLabel: "Sun 26 Jul",
    shift: "Rest day",
    recorded: "—",
    scheduledHours: 0,
    actualHours: 0,
    status: "Rest day",
  },
  {
    id: "2026-07-25",
    date: "2026-07-25",
    dayLabel: "Sat 25 Jul",
    shift: "Rest day",
    recorded: "—",
    scheduledHours: 0,
    actualHours: 0,
    status: "Rest day",
  },
  {
    id: "2026-07-24",
    date: "2026-07-24",
    dayLabel: "Fri 24 Jul",
    shift: "Late shift (L1)",
    recorded: "12:57 – —",
    scheduledHours: 8,
    actualHours: null,
    status: "Exception",
    exception: {
      kind: "Missing punch",
      detail:
        "No clock-out recorded. The badge reader at the Lusaka HQ side entrance was offline from 20:40.",
      consequence:
        "A day without a clock-out is paid as zero hours if it is still open at the payroll cutoff. Your correction must be decided before then.",
      correctionRef: "AT-2026-1191",
      correctionStatus: "In review",
      correctionOwner: "Mutale Kabwe (Manager)",
      correctionDue: "2026-08-03",
    },
  },
  {
    id: "2026-07-23",
    date: "2026-07-23",
    dayLabel: "Thu 23 Jul",
    shift: "Early shift (E1)",
    recorded: "06:56 – 15:30",
    scheduledHours: 8,
    actualHours: 8,
    status: "Clean",
  },
  {
    id: "2026-07-22",
    date: "2026-07-22",
    dayLabel: "Wed 22 Jul",
    shift: "Early shift (E1)",
    recorded: "07:00 – 15:15",
    scheduledHours: 8,
    actualHours: 7.75,
    status: "Resolved",
    resolvedNote:
      "Early departure authorised in advance by Mutale Kabwe for a plant handover. Paid in full — no action needed.",
  },
];

/* --------------------------------------------------------- my 14-day plan */

export type ShiftKind =
  "Normal shift" | "On-call" | "Rest day" | "Public holiday" | "Company closure";

export interface RosterDay {
  id: string;
  date: string;
  dayLabel: string;
  isToday: boolean;
  kind: ShiftKind;
  shiftName: string;
  start: string | null;
  end: string | null;
  location: string;
  /** Set when the employee is on approved leave for this day. */
  leave?: { type: string; reference: string; status: string; coveredBy: string };
  note?: string;
  /** False for rest days, closures and approved leave — nothing to swap. */
  changeable: boolean;
}

const rosterDays: RosterDay[] = [
  {
    id: "2026-07-29",
    date: "2026-07-29",
    dayLabel: "Wed 29 Jul",
    isToday: true,
    kind: "Normal shift",
    shiftName: "Early shift (E1)",
    start: "07:00",
    end: "15:30",
    location: "Lusaka HQ",
    note: "You are clocked in for this shift.",
    changeable: true,
  },
  {
    id: "2026-07-30",
    date: "2026-07-30",
    dayLabel: "Thu 30 Jul",
    isToday: false,
    kind: "Normal shift",
    shiftName: "Early shift (E1)",
    start: "07:00",
    end: "15:30",
    location: "Lusaka HQ",
    changeable: true,
  },
  {
    id: "2026-07-31",
    date: "2026-07-31",
    dayLabel: "Fri 31 Jul",
    isToday: false,
    kind: "Normal shift",
    shiftName: "Late shift (L1)",
    start: "13:00",
    end: "21:30",
    location: "Lusaka HQ",
    note: "Pattern rotates to lates this week. Evening allowance applies after 18:00.",
    changeable: true,
  },
  {
    id: "2026-08-01",
    date: "2026-08-01",
    dayLabel: "Sat 1 Aug",
    isToday: false,
    kind: "Rest day",
    shiftName: "Rest day",
    start: null,
    end: null,
    location: "—",
    note: "Weekly rest. You cannot be rostered without 11 hours' notice and your agreement.",
    changeable: false,
  },
  {
    id: "2026-08-02",
    date: "2026-08-02",
    dayLabel: "Sun 2 Aug",
    isToday: false,
    kind: "On-call",
    shiftName: "On-call (standby)",
    start: "08:00",
    end: "20:00",
    location: "Remote — call-out to Ndola Plant",
    note: "Standby allowance paid for the window. Call-out time is paid separately from the moment you are called.",
    changeable: true,
  },
  {
    id: "2026-08-03",
    date: "2026-08-03",
    dayLabel: "Mon 3 Aug",
    isToday: false,
    kind: "Normal shift",
    shiftName: "Early shift (E1)",
    start: "07:00",
    end: "15:30",
    location: "Ndola Plant",
    note: "Off-site: planned maintenance window. Travel time from Lusaka HQ is paid.",
    changeable: true,
  },
  {
    id: "2026-08-04",
    date: "2026-08-04",
    dayLabel: "Tue 4 Aug",
    isToday: false,
    kind: "Normal shift",
    shiftName: "Early shift (E1)",
    start: "07:00",
    end: "15:30",
    location: "Lusaka HQ",
    changeable: true,
  },
  {
    id: "2026-08-05",
    date: "2026-08-05",
    dayLabel: "Wed 5 Aug",
    isToday: false,
    kind: "Normal shift",
    shiftName: "Early shift (E1)",
    start: "07:00",
    end: "15:30",
    location: "Lusaka HQ",
    note: "Payroll cutoff is 17:00 today. Anything still open after that is paid on recorded hours.",
    changeable: true,
  },
  {
    id: "2026-08-06",
    date: "2026-08-06",
    dayLabel: "Thu 6 Aug",
    isToday: false,
    kind: "Normal shift",
    shiftName: "Early shift (E1)",
    start: "07:00",
    end: "15:30",
    location: "Lusaka HQ",
    leave: {
      type: "Annual leave",
      reference: "LV-2026-0355",
      status: "Approved",
      coveredBy: "Thandiwe Banda",
    },
    changeable: false,
  },
  {
    id: "2026-08-07",
    date: "2026-08-07",
    dayLabel: "Fri 7 Aug",
    isToday: false,
    kind: "Company closure",
    shiftName: "Collective day off — Lusaka HQ summer closure",
    start: null,
    end: null,
    location: "Lusaka HQ (site closed)",
    note: "Paid closure day recognised in the collective labour agreement. It does not come off your leave balance.",
    changeable: false,
  },
  {
    id: "2026-08-08",
    date: "2026-08-08",
    dayLabel: "Sat 8 Aug",
    isToday: false,
    kind: "Rest day",
    shiftName: "Rest day",
    start: null,
    end: null,
    location: "—",
    changeable: false,
  },
  {
    id: "2026-08-09",
    date: "2026-08-09",
    dayLabel: "Sun 9 Aug",
    isToday: false,
    kind: "Rest day",
    shiftName: "Rest day",
    start: null,
    end: null,
    location: "—",
    changeable: false,
  },
  {
    id: "2026-08-10",
    date: "2026-08-10",
    dayLabel: "Mon 10 Aug",
    isToday: false,
    kind: "Normal shift",
    shiftName: "Early shift (E1)",
    start: "07:00",
    end: "15:30",
    location: "Lusaka HQ",
    note: "Leave request LV-2026-0412 (10–21 Aug) is still in review, so this shift still counts as yours.",
    changeable: true,
  },
  {
    id: "2026-08-11",
    date: "2026-08-11",
    dayLabel: "Tue 11 Aug",
    isToday: false,
    kind: "Normal shift",
    shiftName: "Early shift (E1)",
    start: "07:00",
    end: "15:30",
    location: "Lusaka HQ",
    note: "Leave request LV-2026-0412 (10–21 Aug) is still in review, so this shift still counts as yours.",
    changeable: true,
  },
];

/**
 * No public holiday falls inside this 14-day window, so none is shown. Stated
 * explicitly rather than left as an absence the employee has to infer.
 */
export const holidayNote =
  "No public holiday falls in the next 14 days at Lusaka HQ. The next one is Independence Day, Saturday 24 October 2026.";

/* --------------------------------------------------------- shift changes */

export interface SwapCandidate {
  employeeId: string;
  eligible: boolean;
  note: string;
}

const swapCandidates: SwapCandidate[] = [
  {
    employeeId: "w-1005",
    eligible: true,
    note: "Same cover group, holds the same maintenance planning qualification.",
  },
  {
    employeeId: "w-1002",
    eligible: true,
    note: "Your manager. He can cover the shift but cannot approve his own swap.",
  },
  { employeeId: "w-1008", eligible: false, note: "On approved leave across this period." },
  {
    employeeId: "w-1004",
    eligible: false,
    note: "Livingstone Works — cross-entity swaps need HR operations approval first.",
  },
  { employeeId: "w-1006", eligible: false, note: "Contractor engagement — not in the swap pool." },
];

/* -------------------------------------------------------- team coverage */

export interface CoverageAssignment {
  employeeId: string;
  shift: string;
}

export interface CoverageDay {
  id: string;
  date: string;
  dayLabel: string;
  required: number;
  assignments: CoverageAssignment[];
  /** Set when the day is short of the required headcount. */
  gapNote?: string;
  openShiftId?: string;
}

const coverageDays: CoverageDay[] = [
  {
    id: "2026-07-29",
    date: "2026-07-29",
    dayLabel: "Wed 29 Jul",
    required: 3,
    assignments: [
      { employeeId: "w-1001", shift: "Early 07:00–15:30" },
      { employeeId: "w-1002", shift: "Day 08:30–17:00" },
      { employeeId: "w-1005", shift: "Late 13:00–21:30" },
    ],
  },
  {
    id: "2026-07-30",
    date: "2026-07-30",
    dayLabel: "Thu 30 Jul",
    required: 3,
    assignments: [
      { employeeId: "w-1001", shift: "Early 07:00–15:30" },
      { employeeId: "w-1002", shift: "Day 08:30–17:00" },
      { employeeId: "w-1005", shift: "Late 13:00–21:30" },
    ],
  },
  {
    id: "2026-07-31",
    date: "2026-07-31",
    dayLabel: "Fri 31 Jul",
    required: 3,
    assignments: [
      { employeeId: "w-1001", shift: "Late 13:00–21:30" },
      { employeeId: "w-1002", shift: "Day 08:30–17:00" },
    ],
    gapNote: "Short by 1. Emmanuel Sakala is on approved leave and no cover has been agreed.",
    openShiftId: "OS-2026-0142",
  },
  {
    id: "2026-08-01",
    date: "2026-08-01",
    dayLabel: "Sat 1 Aug",
    required: 1,
    assignments: [],
    gapNote: "Short by 1. Kitwe Depot stocktake has no one rostered at all.",
    openShiftId: "OS-2026-0143",
  },
  {
    id: "2026-08-02",
    date: "2026-08-02",
    dayLabel: "Sun 2 Aug",
    required: 1,
    assignments: [{ employeeId: "w-1001", shift: "On-call 08:00–20:00" }],
  },
  {
    id: "2026-08-03",
    date: "2026-08-03",
    dayLabel: "Mon 3 Aug",
    required: 3,
    assignments: [
      { employeeId: "w-1001", shift: "Early 07:00–15:30 (Ndola Plant)" },
      { employeeId: "w-1002", shift: "Day 08:30–17:00" },
      { employeeId: "w-1005", shift: "Late 13:00–21:30" },
    ],
  },
  {
    id: "2026-08-04",
    date: "2026-08-04",
    dayLabel: "Tue 4 Aug",
    required: 3,
    assignments: [
      { employeeId: "w-1001", shift: "Early 07:00–15:30" },
      { employeeId: "w-1002", shift: "Day 08:30–17:00" },
    ],
    gapNote:
      "Short by 1. Ndola Plant maintenance window pulls Thandiwe Banda off this group.",
    openShiftId: "OS-2026-0147",
  },
];

/* ------------------------------------------------------------ open shifts */

export interface OpenShift {
  id: string;
  date: string;
  dayLabel: string;
  shiftName: string;
  start: string;
  end: string;
  location: string;
  reason: string;
  payNote: string;
  closesOn: string;
  owner: string;
  requestsSoFar: number;
}

const openShifts: OpenShift[] = [
  {
    id: "OS-2026-0142",
    date: "2026-07-31",
    dayLabel: "Fri 31 Jul",
    shiftName: "Late shift (L1) — planning cover",
    start: "13:00",
    end: "21:30",
    location: "Lusaka HQ",
    reason: "Emmanuel Sakala is on approved leave.",
    payNote: "Standard rate, plus the evening allowance for hours after 18:00.",
    closesOn: "Thu 30 Jul, 12:00",
    owner: "Mutale Kabwe (Operations Manager)",
    requestsSoFar: 1,
  },
  {
    id: "OS-2026-0143",
    date: "2026-08-01",
    dayLabel: "Sat 1 Aug",
    shiftName: "Weekend cover — depot stocktake",
    start: "08:00",
    end: "16:00",
    location: "Kitwe Depot",
    reason: "Half-year stocktake; the depot needs one planner on site.",
    payNote: "Weekend premium at 150%. Counts towards the weekly hours cap.",
    closesOn: "Thu 30 Jul, 17:00",
    owner: "Emmanuel Sakala (Depot Supervisor) — covered by Mutale Kabwe while he is on leave",
    requestsSoFar: 0,
  },
  {
    id: "OS-2026-0147",
    date: "2026-08-04",
    dayLabel: "Tue 4 Aug",
    shiftName: "Early shift (E1) — maintenance window",
    start: "07:00",
    end: "15:30",
    location: "Ndola Plant",
    reason: "Planned maintenance window needs a second planner on site.",
    payNote: "Standard rate. Travel time from Lusaka HQ is paid.",
    closesOn: "Sun 2 Aug, 12:00",
    owner: "Mutale Kabwe (Operations Manager)",
    requestsSoFar: 2,
  },
];

/* ------------------------------------------------------------------- api */

export const timeclockApi = {
  today: async () => {
    await delay();
    return todayRecord;
  },
  recent: async () => {
    await delay();
    return recentDays;
  },
  schedule: async () => {
    await delay();
    return rosterDays;
  },
  swapCandidates: async () => {
    await delay();
    return swapCandidates;
  },
  coverage: async () => {
    await delay();
    return coverageDays;
  },
  openShifts: async () => {
    await delay();
    return openShifts;
  },
};
