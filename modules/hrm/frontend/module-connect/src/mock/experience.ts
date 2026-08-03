/**
 * Knowledge, announcements, engagement and wellbeing (HRM-046/047/048).
 *
 * Product rules encoded here:
 *  - The assistant answers ONLY from approved articles and cites them. If there
 *    is no approved source it says so and offers a case, rather than inventing.
 *  - Survey results are suppressed below a group-size threshold, and free text
 *    is treated as riskier than scores because writing style identifies people.
 *  - Employee-assistance usage is never visible to HR. Not aggregated, not
 *    counted per person — only that the service exists and is used at all.
 */
const delay = (ms = 420) => new Promise((r) => setTimeout(r, ms));

export interface Article {
  id: string;
  title: string;
  category: string;
  /** Approved content is the only thing the assistant may answer from. */
  state: "Approved" | "In review" | "Expired";
  owner: string;
  version: string;
  updated: string;
  reviewDue: string;
  /** Which populations it applies to — a site rule is not a national policy. */
  appliesTo: string;
  languages: string[];
  views30d: number;
  helpful: number;
  notHelpful: number;
  summary: string;
}

export interface AssistantExchange {
  question: string;
  /** Null when there is no approved source — the assistant must not guess. */
  answer: string | null;
  citations: { articleId: string; title: string }[];
  escalated: boolean;
  note: string;
}

export interface Announcement {
  id: string;
  title: string;
  body: string;
  kind: "Company" | "Policy" | "Emergency" | "Local";
  audience: string;
  published: string;
  expires?: string;
  requiresAcknowledgement: boolean;
  acknowledged: number;
  audienceSize: number;
  author: string;
}

export interface SurveyResult {
  id: string;
  name: string;
  kind: "Annual engagement" | "Pulse check" | "Exit sentiment";
  closed: string;
  invited: number;
  responded: number;
  /** Null where the group is too small to report without identifying someone. */
  score: number | null;
  suppressed: boolean;
  suppressionNote?: string;
  /** Scores can be reported at smaller sizes than free text. */
  freeTextReleased: boolean;
  freeTextNote: string;
  themes: { theme: string; direction: "up" | "down" | "flat"; comment: string }[];
}

export interface Recognition {
  id: string;
  from: string;
  to: string;
  value: string;
  message: string;
  when: string;
  visibility: "Public" | "Team only" | "Private to recipient";
}

export const articles: Article[] = [
  {
    id: "KB-0012",
    title: "Annual leave: how much you get and how to book it",
    category: "Leave and absence",
    state: "Approved",
    owner: "HR operations",
    version: "v4",
    updated: "2026-06-02",
    reviewDue: "2027-06-02",
    appliesTo: "all Zambian entities",
    languages: ["English", "Bemba"],
    views30d: 184,
    helpful: 41,
    notHelpful: 3,
    summary:
      "Entitlement by contract type, the 21-day notice expectation, carry-over limits and what happens to unused days when you leave.",
  },
  {
    id: "KB-0031",
    title: "Sick leave and medical certificates",
    category: "Leave and absence",
    state: "Approved",
    owner: "HR operations",
    version: "v6",
    updated: "2026-05-18",
    reviewDue: "2027-05-18",
    appliesTo: "All entities",
    languages: ["English", "Bemba", "Nyanja"],
    views30d: 97,
    helpful: 22,
    notHelpful: 5,
    summary:
      "When to notify, when a certificate is required (after day 2), who sees it, and what is recorded on your file. Your diagnosis is never recorded.",
  },
  {
    id: "KB-0044",
    title: "Getting an employment or salary confirmation letter",
    category: "Documents and letters",
    state: "Approved",
    owner: "HR operations",
    version: "v2",
    updated: "2026-04-11",
    reviewDue: "2027-04-11",
    appliesTo: "All entities",
    languages: ["English"],
    views30d: 63,
    helpful: 18,
    notHelpful: 1,
    summary:
      "Which letter to request for a mortgage, a tenancy or a visa, exactly what each one discloses, and how a recipient verifies it.",
  },
  {
    id: "KB-0058",
    title: "Claiming expenses and retiring a travel advance",
    category: "Pay and expenses",
    state: "Approved",
    owner: "Payroll",
    version: "v3",
    updated: "2026-07-08",
    reviewDue: "2027-07-08",
    appliesTo: "All entities",
    languages: ["English"],
    views30d: 44,
    helpful: 11,
    notHelpful: 4,
    summary:
      "Per diem caps, what to do when a receipt is missing, how foreign currency is converted, and why an unspent advance has to be returned.",
  },
  {
    id: "KB-0067",
    title: "Parental leave in Zambia",
    category: "Leave and absence",
    state: "In review",
    owner: "HR operations",
    version: "v1 draft",
    updated: "2026-07-21",
    reviewDue: "2026-08-15",
    appliesTo: "the Zambian entity",
    languages: ["English"],
    views30d: 0,
    helpful: 0,
    notHelpful: 0,
    summary:
      "Draft. Not yet approved, so it is not published and the assistant will not answer from it.",
  },
  {
    id: "KB-0009",
    title: "Home working allowance 2024",
    category: "Pay and expenses",
    state: "Expired",
    owner: "Payroll",
    version: "v2",
    updated: "2024-01-15",
    reviewDue: "2025-01-15",
    appliesTo: "the Zambian entity",
    languages: ["English", "Bemba"],
    views30d: 6,
    helpful: 2,
    notHelpful: 8,
    summary:
      "Superseded. Kept for reference because people were paid under it, but it must not be quoted as current.",
  },
];

export const exchanges: AssistantExchange[] = [
  {
    question: "How much notice do I need to give for annual leave?",
    answer:
      "The policy expects 21 days' notice for annual leave. Less notice is not automatically refused — your manager can still approve it, and the request will show the shortfall so they can decide.",
    citations: [{ articleId: "KB-0012", title: "Annual leave: how much you get and how to book it" }],
    escalated: false,
    note: "Answered from one approved article, quoted rather than paraphrased loosely.",
  },
  {
    question: "Can I take parental leave in Zambia and how much is paid?",
    answer: null,
    citations: [],
    escalated: true,
    note:
      "The only relevant article (KB-0067) is still in review, so there is no approved source. The assistant refuses to answer and offers a case instead of guessing — a wrong answer about paid leave is worse than no answer.",
  },
  {
    question: "What is the home working allowance?",
    answer:
      "There is no current home working allowance. The 2024 policy that provided one has expired and must not be relied on.",
    citations: [{ articleId: "KB-0009", title: "Home working allowance 2024" }],
    escalated: false,
    note:
      "An expired article can be used to say something no longer applies, but never to state a current entitlement.",
  },
];

export const announcements: Announcement[] = [
  {
    id: "ANN-2026-0031",
    title: "Annual leave notice period changes to 21 days from 1 September",
    body:
      "The expected notice for annual leave moves from 14 to 21 days. Requests already submitted are unaffected. Your manager can still approve shorter notice where cover allows.",
    kind: "Policy",
    audience: "all Zambian entities",
    published: "2026-07-22",
    requiresAcknowledgement: true,
    acknowledged: 4,
    audienceSize: 5,
    author: "Thandiwe Banda (HR operations)",
  },
  {
    id: "ANN-2026-0029",
    title: "Gate 3 badge reader out of service — use the supervisor terminal",
    body:
      "The Gate 3 reader at Livingstone Works is being replaced this week. Clock in at the supervisor terminal instead. Any missed punches will be corrected without a penalty.",
    kind: "Local",
    audience: "Livingstone Works",
    published: "2026-07-20",
    expires: "2026-08-03",
    requiresAcknowledgement: false,
    acknowledged: 0,
    audienceSize: 2,
    author: "Mutale Kabwe (Operations Manager)",
  },
  {
    id: "ANN-2026-0026",
    title: "Solwezi Yard closed Monday — port access restrictions",
    body:
      "The yard is closed on Monday because of port access restrictions. Do not travel to site. Your shift is treated as a paid company closure day.",
    kind: "Emergency",
    audience: "Solwezi Yard",
    published: "2026-07-18",
    expires: "2026-07-21",
    requiresAcknowledgement: true,
    acknowledged: 1,
    audienceSize: 1,
    author: "Mutale Kabwe (Operations Manager)",
  },
];

export const surveys: SurveyResult[] = [
  {
    id: "SRV-2026-PULSE-Q3",
    name: "Q3 pulse check",
    kind: "Pulse check",
    closed: "2026-07-15",
    invited: 8,
    responded: 7,
    score: null,
    suppressed: true,
    suppressionNote:
      "Fewer than the 10-response threshold. Publishing a score for 7 people in a 3-branch organisation would let a manager infer individual answers.",
    freeTextReleased: false,
    freeTextNote:
      "Free-text comments are withheld at any size below 25 responses. Writing style, role-specific detail and phrasing identify people far more reliably than a score does.",
    themes: [],
  },
  {
    id: "SRV-2025-ANNUAL",
    name: "Annual engagement survey 2025",
    kind: "Annual engagement",
    closed: "2025-11-30",
    invited: 6,
    responded: 6,
    score: null,
    suppressed: true,
    suppressionNote: "Whole-organisation response count below the reporting threshold.",
    freeTextReleased: false,
    freeTextNote: "Withheld — same threshold applies.",
    themes: [
      { theme: "Clarity of shift allocation", direction: "down", comment: "Raised often enough to act on without publishing a score." },
      { theme: "Confidence in equipment safety", direction: "up", comment: "Improved after the strap inspection programme." },
    ],
  },
];

export const recognitions: Recognition[] = [
  {
    id: "REC-0091",
    from: "Mutale Kabwe",
    to: "Thandiwe Banda",
    value: "Looks after people",
    message: "Rebuilt the onboarding checklist so new starters stop falling through the gaps in week one.",
    when: "2026-07-24",
    visibility: "Public",
  },
  {
    id: "REC-0088",
    from: "Chanda Mwansa-Chileshe",
    to: "Kondwani Mwanza",
    value: "Does it properly",
    message: "Flagged the strap failure at Solwezi before anyone got hurt, and wrote it up clearly.",
    when: "2026-07-25",
    visibility: "Public",
  },
  {
    id: "REC-0084",
    from: "Thandiwe Banda",
    to: "Nalukui Simasiku",
    value: "Gets it over the line",
    message: "Held the July payroll cutoff together despite two late attendance corrections.",
    when: "2026-07-28",
    visibility: "Team only",
  },
];

export const experienceApi = {
  articles: async () => {
    await delay();
    return articles;
  },
  exchanges: async () => {
    await delay(300);
    return exchanges;
  },
  announcements: async () => {
    await delay();
    return announcements;
  },
  surveys: async () => {
    await delay();
    return surveys;
  },
  recognitions: async () => {
    await delay(320);
    return recognitions;
  },
};
