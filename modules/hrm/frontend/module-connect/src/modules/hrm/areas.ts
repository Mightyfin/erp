import type { AreaDefinition } from "@/platform/components/AreaOverview";

/**
 * Area definitions map the HRM feature catalogue's 61 parent features onto the
 * grouped navigation. Every nav destination resolves to one of these, so the
 * information architecture is fully walkable even where a capability is not
 * built yet — "planned" is stated honestly rather than hidden behind a dead link.
 */
export const areas = {
  documents: {
    eyebrow: "People",
    title: "Documents and employee files",
    description: "The digital employee file: generation, signature, retention and access history.",
    catalogueRefs: "HRM-006",
    capabilities: [
      { label: "Digital employee file", detail: "Categorised documents with confidentiality classification.", tier: "Essentials", state: "built", to: "/hrm/people/documents" },
      { label: "Template-based generation", detail: "Merge fields, bulk generation and verification codes.", tier: "Essentials", state: "built", to: "/hrm/people/documents" },
      { label: "Electronic signatures", detail: "Signature workflow and versioning via an external provider.", tier: "Essentials", state: "built", to: "/hrm/people/documents" },
      { label: "Retention and legal hold", detail: "Expiry alerts, archival and lawful disposal.", tier: "Essentials", state: "built", to: "/hrm/people/documents" },
    ],
  },
  dataQuality: {
    eyebrow: "People",
    title: "Data quality and stewardship",
    description: "Keeping the record trustworthy: completeness, duplicates and safe bulk correction.",
    catalogueRefs: "HRM-011",
    capabilities: [
      { label: "Completeness and consistency rules", detail: "Required-field and cross-record checks with a quality dashboard.", tier: "Essentials", state: "built", to: "/hrm/people/data-quality" },
      { label: "Duplicate detection and merge", detail: "Including reversal of an incorrect merge.", tier: "Essentials", state: "built", to: "/hrm/people/data-quality" },
      { label: "Bulk correction", detail: "Validation, dry-run simulation and rollback before commit.", tier: "Essentials", state: "built", to: "/hrm/people/data-quality" },
      { label: "Import reconciliation", detail: "Source-system tracking and data lineage.", tier: "Essentials", state: "built", to: "/hrm/people/data-quality" },
    ],
  },
  privacy: {
    eyebrow: "People",
    title: "Privacy and consent",
    description: "What personal data is held, why, and how an employee exercises their rights over it.",
    catalogueRefs: "HRM-059",
    capabilities: [
      { label: "Personal-data inventory by purpose", detail: "Grouped by why it's held, not by underlying table.", tier: "Essentials", state: "built", to: "/hrm/people/privacy" },
      { label: "Consent capture and withdrawal", detail: "Biometric, health, background-check and AI-assisted processing.", tier: "Essentials", state: "built", to: "/hrm/people/privacy" },
      { label: "Subject access and correction requests", detail: "Employee-raised, deadline-tracked and auditable.", tier: "Essentials", state: "built", to: "/hrm/people/privacy" },
      { label: "Erasure within retention limits", detail: "Preserves legally-retained payroll and audit records.", tier: "Essentials", state: "built", to: "/hrm/people/privacy" },
    ],
  },
  lifecycle: {
    eyebrow: "Lifecycle",
    title: "Employee lifecycle",
    description: "Joining, moving and leaving — the events that change someone's employment record.",
    catalogueRefs: "HRM-017, HRM-019, HRM-022, HRM-023, HRM-024, HRM-056, HRM-057, HRM-058",
    capabilities: [
      { label: "Preboarding and onboarding", detail: "Pre-employment checks, checklists, provisioning and probation start.", tier: "Essentials", state: "built", to: "/hrm/lifecycle/onboarding" },
      { label: "Movements and career changes", detail: "Promotion, transfer, secondment and effective-dated assignment change.", tier: "Essentials", state: "built", to: "/hrm/lifecycle/movements" },
      { label: "Assets and access lifecycle", detail: "Equipment and access from provisioning through clearance.", tier: "Essentials", state: "built", to: "/hrm/lifecycle/assets" },
      { label: "Separation and final settlement", detail: "Notice, clearance, final pay and separation documents.", tier: "Essentials", state: "built", to: "/hrm/lifecycle/offboarding" },
      { label: "Employee journeys and life events", detail: "Guided journeys for promotion, relocation, parental leave and return.", tier: "Advanced", state: "built", to: "/hrm/lifecycle/journeys" },
      { label: "Global mobility and assignments", detail: "Home/host organisation, permits and assignment packages.", tier: "Enterprise", state: "built", to: "/hrm/lifecycle/mobility" },
      { label: "Alumni and rehire", detail: "Former-employee directory, rehire eligibility and previous service.", tier: "Advanced", state: "built", to: "/hrm/lifecycle/alumni" },
    ],
  },
  recruitment: {
    eyebrow: "Recruitment",
    title: "Recruitment",
    description: "From an approved vacancy through to an accepted offer handed to preboarding.",
    catalogueRefs: "HRM-020, HRM-021",
    capabilities: [
      { label: "Requisitions and approval", detail: "Replacement and new-position hiring against establishment.", tier: "Essentials", state: "built", to: "/hrm/recruitment/requisitions" },
      { label: "Vacancies and careers portal", detail: "Internal and external posting with mobile application.", tier: "Essentials", state: "built", to: "/hrm/recruitment/vacancies" },
      { label: "Candidates and screening", detail: "Profiles, talent pools, blind screening and shortlisting.", tier: "Essentials", state: "built", to: "/hrm/recruitment/candidates" },
      { label: "Interviews and scorecards", detail: "Panels, scheduling, assessments and ranking.", tier: "Essentials", state: "built", to: "/hrm/recruitment/candidates" },
      { label: "Offers", detail: "Creation, approval, acceptance and conversion to preboarding.", tier: "Essentials", state: "built", to: "/hrm/recruitment/offers" },
      { label: "Employee referrals", detail: "Referral programme, conflict declaration and reward payment.", tier: "Advanced", state: "built", to: "/hrm/recruitment/offers" },
    ],
  },
  schedules: {
    eyebrow: "Time operations",
    title: "Schedules and rosters",
    description: "Who is expected to work when, and how coverage gaps get resolved.",
    catalogueRefs: "HRM-025, HRM-026, HRM-028",
    capabilities: [
      { label: "Work calendars and shift assignment", detail: "Public holidays, shift patterns and roster publication.", tier: "Essentials", state: "built", to: "/hrm/time/schedules" },
      { label: "Open shifts, swaps and bidding", detail: "Employee-initiated changes within coverage rules.", tier: "Enterprise", state: "built", to: "/hrm/time/schedules" },
      { label: "Rest periods and fatigue rules", detail: "Maximum consecutive work and overtime-risk alerts.", tier: "Enterprise", state: "built", to: "/hrm/time/toil" },
      { label: "Compensatory time and time off in lieu", detail: "Dedicated overtime-to-leave request, expiry and approval workflow.", tier: "Advanced", state: "planned", to: "/hrm/leave/control" },
    ],
  },
  timesheets: {
    eyebrow: "Time operations",
    title: "Attendance timesheets",
    description: "Hours captured from attendance and rosters for review, overtime approval, and payroll evidence.",
    catalogueRefs: "HRM-030",
    capabilities: [
      { label: "Attendance review and approval", detail: "Live attendance records, correction workflow, and overtime review.", tier: "Essentials", state: "built", to: "/hrm/time/timesheets" },
      { label: "Payroll evidence hand-off", detail: "Approved attendance-derived overtime with source traceability.", tier: "Essentials", state: "built", to: "/hrm/time/timesheets" },
      { label: "Project and cost-centre allocation", detail: "Not part of the current attendance-timesheet model.", tier: "Advanced", state: "planned", to: "/hrm/time/timesheets" },
      { label: "Utilisation reporting", detail: "Attendance and overtime reporting without project costing.", tier: "Advanced", state: "planned", to: "/hrm/time/timesheets" },
    ],
  },
  expenses: {
    eyebrow: "Time operations",
    title: "Travel and expenses",
    description: "Duty travel, advances and claims, settled against policy limits.",
    catalogueRefs: "HRM-031, HRM-032",
    capabilities: [
      { label: "Travel authorisation", detail: "Purpose, itinerary, per diem and travel advance.", tier: "Advanced", state: "planned", to: "/hrm/time/travel" },
      { label: "Expense claims", detail: "Receipts, categories, policy limits and duplicate detection.", tier: "Advanced", state: "planned", to: "/hrm/time/expenses" },
      { label: "Advance reconciliation", detail: "Return of unused funds and outstanding-advance control.", tier: "Advanced", state: "planned", to: "/hrm/time/travel" },
      { label: "Reimbursement", detail: "Explicit settlement route after dedicated expense contracts are delivered.", tier: "Advanced", state: "planned", to: "/hrm/time/expenses" },
    ],
  },
  compensation: {
    eyebrow: "Payroll",
    title: "Compensation and benefits",
    description: "What someone is paid and what they are enrolled in, separate from running a pay period.",
    catalogueRefs: "HRM-034, HRM-035, HRM-036, HRM-037",
    capabilities: [
      { label: "Compensation records", detail: "Grades, bands, salary history and change requests.", tier: "Essentials", state: "built", to: "/hrm/pay/compensation" },
      { label: "Benefit enrolment", detail: "Eligibility, dependants, coverage levels and life events.", tier: "Essentials", state: "built", to: "/hrm/pay/compensation" },
      { label: "Compensation planning", detail: "Review cycles, budgets, merit matrices and calibration.", tier: "Advanced", state: "built", to: "/hrm/pay/compensation" },
      { label: "Pay equity analysis", detail: "Gap analysis, statutory reporting and remediation tracking.", tier: "Advanced", state: "built", to: "/hrm/pay/compensation" },
      { label: "Insurance and claims", detail: "Policies, premiums and claim references with data minimisation.", tier: "Advanced", state: "built", to: "/hrm/pay/compensation" },
    ],
  },
  talent: {
    eyebrow: "Talent",
    title: "Performance, learning and talent",
    description: "Goals, reviews, development and succession — how people grow and are retained.",
    catalogueRefs: "HRM-038, HRM-039, HRM-040, HRM-041",
    capabilities: [
      { label: "Goals and performance reviews", detail: "Cycles, cascading goals, self/manager/peer review and calibration.", tier: "Advanced", state: "built", to: "/hrm/performance" },
      { label: "Continuous feedback and check-ins", detail: "Lightweight feedback between formal cycles.", tier: "Advanced", state: "built", to: "/hrm/talent/feedback" },
      { label: "Performance improvement plans", detail: "Structured plans with disagreement and appeal paths.", tier: "Advanced", state: "built", to: "/hrm/talent/feedback" },
      { label: "Learning and development", detail: "Catalogue, enrolment, certificates, expiry and recertification.", tier: "Advanced", state: "built", to: "/hrm/talent/learning" },
      { label: "Talent and succession", detail: "Nine-box, critical roles, successors and readiness.", tier: "Advanced", state: "built", to: "/hrm/talent/succession" },
      { label: "Skills and internal marketplace", detail: "Skills taxonomy, gaps, internal gigs and redeployment.", tier: "Enterprise", state: "built", to: "/hrm/talent/succession" },
    ],
  },
  letters: {
    eyebrow: "Employee experience",
    title: "Letters and certificates",
    description: "Self-service documents an employee can request and verify without opening a case.",
    catalogueRefs: "HRM-045",
    capabilities: [
      { label: "Employment and salary confirmation", detail: "Eligibility check, template merge and approval where required.", tier: "Essentials", state: "built", to: "/hrm/experience/letters" },
      { label: "Visa, bank and reference letters", detail: "Purpose and addressee captured before generation.", tier: "Essentials", state: "built", to: "/hrm/experience/letters" },
      { label: "Service certificates", detail: "Issued on separation with verification code.", tier: "Essentials", state: "built", to: "/hrm/experience/letters" },
      { label: "Delivery and history", detail: "Download history and third-party verification.", tier: "Essentials", state: "built", to: "/hrm/experience/letters" },
    ],
  },
  knowledge: {
    eyebrow: "Employee experience",
    title: "Knowledge and assistance",
    description: "Answers before cases: policies, FAQs and guided help scoped to the person asking.",
    catalogueRefs: "HRM-046",
    capabilities: [
      { label: "Policy and FAQ articles", detail: "Country, organisation and role targeting with review dates.", tier: "Advanced", state: "built", to: "/hrm/experience/knowledge" },
      { label: "Search and suggested answers", detail: "Offered before a case is raised, never blocking one.", tier: "Advanced", state: "built", to: "/hrm/experience/knowledge" },
      { label: "Digital HR assistant", detail: "Grounded in approved content with source citation.", tier: "Advanced", state: "built", to: "/hrm/experience/knowledge" },
      { label: "Escalation to a case", detail: "Hands over context rather than restarting the question.", tier: "Advanced", state: "built", to: "/hrm/experience/knowledge" },
    ],
  },
  announcements: {
    eyebrow: "Employee experience",
    title: "Engagement and announcements",
    description: "Organisation-wide communication, surveys and recognition.",
    catalogueRefs: "HRM-047, HRM-048",
    capabilities: [
      { label: "Announcements and notices", detail: "Audience targeting, scheduled publication and acknowledgements.", tier: "Advanced", state: "built", to: "/hrm/experience/announcements" },
      { label: "Surveys and pulse checks", detail: "Anonymous feedback with small-group privacy thresholds.", tier: "Advanced", state: "built", to: "/hrm/experience/announcements" },
      { label: "Recognition programmes", detail: "Peer and manager recognition with reward linkage.", tier: "Advanced", state: "built", to: "/hrm/experience/announcements" },
      { label: "Wellbeing initiatives", detail: "Workload and burnout feedback, assistance referrals.", tier: "Advanced", state: "built", to: "/hrm/experience/announcements" },
    ],
  },
  relations: {
    eyebrow: "Relations and safety",
    title: "Relations, safety and compliance",
    description: "Restricted case work: grievances, discipline, safety and ethics. Access is tightly scoped.",
    catalogueRefs: "HRM-049 to HRM-055",
    capabilities: [
      { label: "Employee relations cases", detail: "Grievances, investigations, hearings and outcomes in restricted queues.", tier: "Advanced", state: "built", to: "/hrm/relations/cases" },
      { label: "Discipline and warning register", detail: "Warnings, expiry, appeals and repeat-offence rules.", tier: "Essentials", state: "built", to: "/hrm/relations/discipline" },
      { label: "Health, safety and occupational health", detail: "Incidents, fitness to work, accommodations and return-to-work plans.", tier: "Advanced", state: "built", to: "/hrm/relations/safety" },
      { label: "Ethics and declarations", detail: "Conflict of interest, gifts and periodic declaration campaigns.", tier: "Advanced", state: "built", to: "/hrm/relations/ethics" },
      { label: "Protected disclosures", detail: "Anonymous intake is live; investigator triage is the next slice.", tier: "Essentials", state: "built", to: "/hrm/speak-up" },
      { label: "Labour relations and unions", detail: "Bargaining units, collective agreements and industrial action.", tier: "Enterprise", state: "built", to: "/hrm/relations/labour" },
      { label: "Emergency workforce accountability", detail: "Mass notification, safe check-in and muster lists.", tier: "Enterprise", state: "built", to: "/hrm/relations/labour" },
    ],
  },
  reports: {
    eyebrow: "Reports",
    title: "Reports and analytics",
    description: "Operational reporting and workforce analytics, respecting the same permissions as the screens.",
    catalogueRefs: "HRM-008",
    capabilities: [
      { label: "Operational reports", detail: "Headcount, turnover, absence, overtime and workforce cost.", tier: "Essentials", state: "built", to: "/hrm/reports" },
      { label: "Point-in-time reporting", detail: "Effective-date reporting so history stays reproducible.", tier: "Essentials", state: "built", to: "/hrm/reports" },
      { label: "Certified metrics and lineage", detail: "Agreed definitions with documented calculation and source.", tier: "Essentials", state: "built", to: "/hrm/reports" },
      { label: "Small-group privacy suppression", detail: "Prevents re-identification in aggregated output.", tier: "Essentials", state: "built", to: "/hrm/reports" },
      { label: "Custom report builder", detail: "Saved definitions with scheduled secure distribution.", tier: "Essentials", state: "built", to: "/hrm/reports/builder" },
    ],
  },
} satisfies Record<string, AreaDefinition>;
