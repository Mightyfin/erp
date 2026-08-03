/**
 * Travel, expenses, advances and timesheets (HRM-030/031/032, UI-EXP-001..005, UI-TIM-010).
 *
 * Product rules encoded here:
 *  - An advance is money already given. A claim must reconcile against it, and
 *    the result can be money owed BACK BY the employee, not only to them.
 *  - Foreign currency always shows the rate and the date it was taken.
 *  - Per diem rates are configuration, not product behaviour.
 */
const delay = (ms = 420) => new Promise((r) => setTimeout(r, ms));

export const money = (v: number, currency: string) =>
  new Intl.NumberFormat(undefined, { style: "currency", currency }).format(v);

export type ClaimStatus =
  | "Draft"
  | "Submitted"
  | "In review"
  | "Approved"
  | "Returned"
  | "Paid"
  | "Rejected";

export interface ExpenseLine {
  id: string;
  date: string;
  category: string;
  merchant: string;
  amount: number;
  currency: string;
  /** Present when the line was incurred in a currency other than the claim's. */
  converted?: { amount: number; rate: number; rateDate: string };
  purpose: string;
  costCentre: string;
  receipt: "Attached" | "Missing — declared";
  missingReason?: string;
  /** Policy findings shown beside the line, never lumped at the bottom. */
  warnings?: string[];
  /** Set when this looks like something already claimed. */
  possibleDuplicateOf?: string;
}

export interface Claim {
  id: string;
  employee: string;
  purpose: string;
  currency: string;
  status: ClaimStatus;
  owner: string;
  nextAction: string;
  dueDate: string;
  submitted?: string;
  lines: ExpenseLine[];
  /** Advance already paid to the employee for this trip, if any. */
  advanceId?: string;
  advanceAmount?: number;
  tripId?: string;
}

export interface Advance {
  id: string;
  employee: string;
  reason: string;
  amount: number;
  currency: string;
  requested: string;
  paid?: string;
  status: "Requested" | "Approved" | "Paid" | "Retired" | "Partly retired";
  /** How much has been accounted for with receipts. */
  retired: number;
  tripId?: string;
  owner: string;
  nextAction: string;
  dueDate: string;
}

export interface Trip {
  id: string;
  employee: string;
  purpose: string;
  destination: string;
  dutyStation: string;
  from: string;
  to: string;
  transport: string;
  accommodation: string;
  estimatedCost: number;
  currency: string;
  status: "Draft" | "Submitted" | "Approved" | "Completed" | "Cancelled";
  owner: string;
  nextAction: string;
  dueDate: string;
  riskLevel: "Standard" | "Elevated";
  riskNote?: string;
  visaRequired: boolean;
  visaNote?: string;
  coverArrangement: string;
}

export interface PerDiemRate {
  destination: string;
  currency: string;
  meals: number;
  incidentals: number;
  accommodationCap: number;
  effectiveFrom: string;
}

export interface TimesheetRow {
  id: string;
  project: string;
  costCentre: string;
  billable: boolean;
  /** Mon..Sun ordinary hours. */
  hours: number[];
  /** Mon..Sun overtime hours. */
  overtime: number[];
}

export interface Timesheet {
  id: string;
  employee: string;
  weekStarting: string;
  weekEnding: string;
  status: "Draft" | "Submitted" | "Partly approved" | "Approved" | "Returned";
  contractedHours: number;
  rows: TimesheetRow[];
  /** Two distinct approvals with different purposes. */
  projectApproval: { by: string; state: "Pending" | "Approved" | "Returned"; purpose: string };
  lineApproval: { by: string; state: "Pending" | "Approved" | "Returned"; purpose: string };
  lockedNote: string;
}

export const perDiemRates: PerDiemRate[] = [
  { destination: "Zambia — domestic", currency: "ZMW", meals: 350, incidentals: 120, accommodationCap: 1_500, effectiveFrom: "2026-01-01" },
  { destination: "Zambia — Livingstone", currency: "ZMW", meals: 420, incidentals: 120, accommodationCap: 1_800, effectiveFrom: "2026-01-01" },
  { destination: "Zambia — Chingola", currency: "ZMW", meals: 380, incidentals: 120, accommodationCap: 1_600, effectiveFrom: "2026-04-01" },
  { destination: "Zambia — Solwezi", currency: "ZMW", meals: 400, incidentals: 120, accommodationCap: 1_700, effectiveFrom: "2026-04-01" },
];

export const trips: Trip[] = [
  {
    id: "TRV-2026-0088",
    employee: "Chanda Mwansa-Chileshe",
    purpose: "Maintenance planning review with the Livingstone fabrication team",
    destination: "Livingstone, Zambia",
    dutyStation: "Livingstone Works",
    from: "2026-08-18",
    to: "2026-08-21",
    transport: "Coach — Lusaka to Ndola, return",
    accommodation: "Hotel, 3 nights (within the K1,800 cap)",
    estimatedCost: 9_800,
    currency: "ZMW",
    status: "Approved",
    owner: "Chanda Mwansa-Chileshe",
    nextAction: "Travel, then retire the advance within 14 days of return",
    dueDate: "2026-09-04",
    riskLevel: "Standard",
    visaRequired: false,
    visaNote: "No visa required — travel within the SADC region on a Zambian passport.",
    coverArrangement: "Thandiwe Banda covers planning queries while away.",
  },
  {
    id: "TRV-2026-0091",
    employee: "Nalukui Simasiku",
    purpose: "Statutory payroll filing workshop and bank onboarding",
    destination: "Solwezi, Zambia",
    dutyStation: "Solwezi Yard",
    from: "2026-09-02",
    to: "2026-09-05",
    transport: "Domestic flight — Lusaka to Solwezi, return",
    accommodation: "Hotel, 3 nights",
    estimatedCost: 14_200,
    currency: "ZMW",
    status: "Submitted",
    owner: "Mutale Kabwe (Manager)",
    nextAction: "Approval decision",
    dueDate: "2026-08-20",
    riskLevel: "Standard",
    visaRequired: false,
    coverArrangement: "Payroll cutoff moved forward two days to avoid a clash.",
  },
  {
    id: "TRV-2026-0084",
    employee: "Kondwani Mwanza",
    purpose: "Supplier site inspection",
    destination: "Johannesburg, South Africa",
    dutyStation: "Supplier premises",
    from: "2026-07-07",
    to: "2026-07-09",
    transport: "Flight, return",
    accommodation: "Hotel, 2 nights",
    estimatedCost: 18_600,
    currency: "ZMW",
    status: "Completed",
    owner: "Completed",
    nextAction: "Closed — advance retired in full",
    dueDate: "2026-07-23",
    riskLevel: "Elevated",
    riskNote:
      "Site visit involves active fabrication areas. A risk assessment and site induction were completed before travel, and emergency contacts were confirmed.",
    visaRequired: false,
    coverArrangement: "Shift cover arranged at Livingstone Works.",
  },
];

export const advances: Advance[] = [
  {
    id: "ADV-2026-0044",
    employee: "Chanda Mwansa-Chileshe",
    reason: "Livingstone planning review — accommodation and subsistence",
    amount: 7_500,
    currency: "ZMW",
    requested: "2026-07-28",
    paid: "2026-08-04",
    status: "Paid",
    retired: 0,
    tripId: "TRV-2026-0088",
    owner: "Chanda Mwansa-Chileshe",
    nextAction: "Retire with receipts within 14 days of return",
    dueDate: "2026-09-04",
  },
  {
    id: "ADV-2026-0039",
    employee: "Kondwani Mwanza",
    reason: "Johannesburg supplier inspection",
    amount: 12_000,
    currency: "ZMW",
    requested: "2026-06-25",
    paid: "2026-07-01",
    status: "Retired",
    retired: 12_000,
    tripId: "TRV-2026-0084",
    owner: "Closed",
    nextAction: "Closed — K1,180.00 unused was returned on 21 Jul",
    dueDate: "2026-07-23",
  },
];

export const claims: Claim[] = [
  {
    id: "EXP-2026-0311",
    employee: "Chanda Mwansa-Chileshe",
    purpose: "Livingstone planning review — retire advance ADV-2026-0044",
    currency: "ZMW",
    status: "Draft",
    owner: "Chanda Mwansa-Chileshe",
    nextAction: "Add remaining receipts and submit",
    dueDate: "2026-09-04",
    advanceId: "ADV-2026-0044",
    advanceAmount: 7_500,
    tripId: "TRV-2026-0088",
    lines: [
      {
        id: "L1",
        date: "2026-08-18",
        category: "Transport",
        merchant: "Power Tools Coach Services",
        amount: 1_450.0,
        currency: "ZMW",
        purpose: "Coach, Lusaka to Ndola",
        costCentre: "CC-OPS-LUS",
        receipt: "Attached",
      },
      {
        id: "L2",
        date: "2026-08-18",
        category: "Accommodation",
        merchant: "Protea Hotel Ndola",
        amount: 1_950.0,
        currency: "ZMW",
        purpose: "Night 1 of 3",
        costCentre: "CC-OPS-LUS",
        receipt: "Attached",
        warnings: ["K150.00 above the K1,800.00 Livingstone accommodation cap. Needs a reason at approval."],
      },
      {
        id: "L3",
        date: "2026-08-19",
        category: "Meals",
        merchant: "Site canteen, Ndola Plant",
        amount: 145.0,
        currency: "ZMW",
        purpose: "Lunch on site",
        costCentre: "CC-OPS-LUS",
        receipt: "Missing — declared",
        missingReason: "Card receipt not issued by the on-site canteen.",
        warnings: ["Missing receipt — needs approval one level above the usual approver."],
      },
      {
        id: "L4",
        date: "2026-08-19",
        category: "Transport",
        merchant: "Ndola local taxi",
        amount: 240.0,
        currency: "ZAR",
        converted: { amount: 355.2, rate: 1.48, rateDate: "2026-08-19" },
        purpose: "Local transport whilst on site",
        costCentre: "CC-OPS-LUS",
        receipt: "Attached",
      },
      {
        id: "L5",
        date: "2026-08-18",
        category: "Transport",
        merchant: "Power Tools Coach Services",
        amount: 189.4,
        currency: "ZMW",
        purpose: "Coach, Lusaka to Ndola",
        costCentre: "CC-OPS-LUS",
        receipt: "Attached",
        possibleDuplicateOf: "L1",
        warnings: ["Same date, amount and merchant as line 1. Check this is not the same receipt entered twice."],
      },
    ],
  },
  {
    id: "EXP-2026-0288",
    employee: "Kondwani Mwanza",
    purpose: "Johannesburg supplier inspection — retire advance ADV-2026-0039",
    currency: "ZMW",
    status: "Paid",
    owner: "Closed",
    nextAction: "Closed — settled in the July pay run",
    dueDate: "2026-07-23",
    submitted: "2026-07-14",
    advanceId: "ADV-2026-0039",
    advanceAmount: 12_000,
    tripId: "TRV-2026-0084",
    lines: [
      { id: "M1", date: "2026-07-07", category: "Transport", merchant: "Proflight Zambia", amount: 6_240.0, currency: "ZMW", purpose: "Return flight", costCentre: "CC-MFG-LVS", receipt: "Attached" },
      { id: "M2", date: "2026-07-07", category: "Accommodation", merchant: "Protea Hotel Johannesburg", amount: 3_100.0, currency: "ZAR", converted: { amount: 4_588.0, rate: 1.48, rateDate: "2026-07-07" }, purpose: "2 nights", costCentre: "CC-MFG-LVS", receipt: "Attached" },
      { id: "M3", date: "2026-07-08", category: "Meals", merchant: "Various", amount: 640.0, currency: "ZMW", purpose: "Subsistence within per diem", costCentre: "CC-MFG-LVS", receipt: "Attached" },
    ],
  },
];

export const timesheets: Timesheet[] = [
  {
    id: "TS-2026-W30",
    employee: "Chanda Mwansa-Chileshe",
    weekStarting: "2026-07-20",
    weekEnding: "2026-07-26",
    status: "Submitted",
    contractedHours: 40,
    rows: [
      { id: "R1", project: "Planned maintenance programme", costCentre: "CC-OPS-LUS", billable: false, hours: [6, 6, 5, 6, 5, 0, 0], overtime: [0, 0, 0, 0, 0, 0, 0] },
      { id: "R2", project: "Client retrofit — Zuidhaven", costCentre: "CC-PRJ-114", billable: true, hours: [2, 2, 3, 2, 3, 0, 0], overtime: [0, 1.5, 0, 0, 2, 0, 0] },
    ],
    projectApproval: {
      by: "Project manager — Zuidhaven retrofit",
      state: "Approved",
      purpose: "Confirms the billable hours are correct against the project and can be invoiced.",
    },
    lineApproval: {
      by: "Mutale Kabwe (Line manager)",
      state: "Pending",
      purpose: "Confirms the person actually worked these hours and authorises the overtime for pay.",
    },
    lockedNote:
      "Once submitted, cells are locked. A change needs the timesheet returned to you first — this stops hours being edited after they have been approved or invoiced.",
  },
];

export const expensesApi = {
  claims: async () => {
    await delay();
    return claims;
  },
  claim: async (id: string) => {
    await delay();
    return claims.find((c) => c.id === id) ?? null;
  },
  advances: async () => {
    await delay();
    return advances;
  },
  trips: async () => {
    await delay();
    return trips;
  },
  perDiem: async () => {
    await delay(280);
    return perDiemRates;
  },
  timesheets: async () => {
    await delay();
    return timesheets;
  },
};

/** Claim total in the claim's own currency, using the converted value where present. */
export const claimTotal = (c: Claim) =>
  c.lines.reduce((sum, l) => sum + (l.converted ? l.converted.amount : l.amount), 0);
