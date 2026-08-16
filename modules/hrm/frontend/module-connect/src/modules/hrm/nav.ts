import {
  Banknote,
  BarChart3,
  Briefcase,
  Clock4,
  Home,
  MessagesSquare,
  Settings,
  Shield,
  ShieldCheck,
  Sparkles,
  UserCog,
  Users,
} from "lucide-react";
import type { ModuleDefinition } from "@/platform/nav";

/**
 * HRM module navigation, following the information-architecture design:
 * grouped by user goal rather than by catalogue entity, with exactly ONE
 * Configuration entry point so admin/setup never leaks into operational areas.
 *
 * Rail sections are capped at ~12 (spatially learned, not re-scanned); any
 * single expanded list is capped at ~8-9 items before it needs sub-grouping.
 */
export const hrmModule: ModuleDefinition = {
  id: "hrm",
  name: "Human resources",
  shortName: "HRM",
  sections: [
    { id: "home", label: "Home", icon: Home, to: "/hrm" },
    { id: "self-service", label: "My HR", icon: UserCog, to: "/hrm/self-service" },
    {
      id: "people",
      label: "People",
      icon: Users,
      items: [
        { label: "Employees", to: "/hrm/employees" },
        // M25: self-service profile — /hrm/my-profile resolves the own worker
        // via GET /hrm/me (M14 link) in real mode, so it can never be
        // misdirected at another worker. Mock mode has no worker identity
        // concept and falls back to the static `w-1001` detail page only
        // where code paths force it; the rail link now prefers the
        // self-service route.
        { label: "My profile", to: "/hrm/my-profile" },
        {
          label: "Positions",
          to: "/hrm/people/positions",
          roles: ["hr_ops", "hr_admin", "manager"],
        },
        {
          label: "Organisation structure",
          to: "/hrm/people/org",
          roles: ["hr_ops", "hr_admin", "manager"],
        },
        { label: "My documents", to: "/hrm/my-documents" },
        {
          label: "Employee documents",
          to: "/hrm/people/documents",
          roles: ["hr_ops", "hr_admin"],
        },
        { label: "Privacy and consent", to: "/hrm/people/privacy" },
        {
          label: "Master data operations",
          to: "/hrm/people/master-data",
          roles: ["hr_ops", "hr_admin"],
        },
        { label: "Data quality", to: "/hrm/people/data-quality", roles: ["hr_ops", "hr_admin"] },
      ],
    },
    {
      id: "lifecycle",
      label: "Lifecycle",
      icon: UserCog,
      roles: ["hr_ops", "hr_admin", "manager"],
      items: [
        { label: "Onboarding", to: "/hrm/lifecycle/onboarding" },
        { label: "Movements and transfers", to: "/hrm/lifecycle/movements" },
        { label: "Offboarding", to: "/hrm/lifecycle/offboarding" },
        { label: "Assets and access", to: "/hrm/lifecycle/assets" },
        { label: "Journeys", to: "/hrm/lifecycle/journeys" },
        { label: "Assignments", to: "/hrm/lifecycle/mobility" },
        { label: "Alumni and rehire", to: "/hrm/lifecycle/alumni" },
      ],
    },
    {
      id: "recruitment",
      label: "Recruitment",
      icon: Briefcase,
      roles: ["hr_ops", "hr_admin", "manager"],
      items: [
        { label: "Hiring operations", to: "/hrm/recruitment/operations" },
        { label: "Requisitions", to: "/hrm/recruitment/requisitions" },
        { label: "Vacancies", to: "/hrm/recruitment/vacancies" },
        { label: "Candidates", to: "/hrm/recruitment/candidates" },
        { label: "Offers and referrals", to: "/hrm/recruitment/offers" },
      ],
    },
    {
      id: "time",
      label: "Time and leave",
      icon: Clock4,
      groups: [
        {
          label: "Leave",
          items: [
            { label: "My leave", to: "/hrm/leave" },
            { label: "Request leave", to: "/hrm/leave/new" },
            {
              label: "Leave approvals",
              to: "/hrm/leave/approvals",
              roles: ["hr_ops", "hr_admin", "manager"],
            },
          ],
        },
        {
          label: "Attendance",
          items: [
            { label: "Clock in and out", to: "/hrm/attendance/clock" },
            { label: "Corrections", to: "/hrm/attendance" },
            { label: "Raise a correction", to: "/hrm/attendance/new" },
          ],
        },
        {
          label: "Scheduling and claims",
          items: [
            { label: "Time operations", to: "/hrm/time/operations", roles: ["hr_ops", "hr_admin"] },
            { label: "Schedules and rosters", to: "/hrm/time/schedules" },
            { label: "Timesheets", to: "/hrm/time/timesheets" },
            { label: "Time off in lieu", to: "/hrm/time/toil" },
            { label: "Utilisation", to: "/hrm/time/utilisation" },
            { label: "Travel", to: "/hrm/time/travel" },
            { label: "Expenses", to: "/hrm/time/expenses" },
          ],
        },
      ],
    },
    {
      id: "payroll",
      label: "Payroll",
      icon: Banknote,
      items: [
        { label: "My payslips", to: "/hrm/payslips" },
        { label: "Compensation and benefits", to: "/hrm/pay/compensation" },
        { label: "Payroll administration", to: "/hrm/payroll", roles: ["payroll", "hr_admin"] },
        { label: "Pay runs", to: "/hrm/payroll/runs", roles: ["payroll", "hr_admin"] },
        {
          label: "Payroll exceptions",
          to: "/hrm/payroll/exceptions",
          roles: ["payroll", "hr_admin"],
        },
        { label: "Payroll setup", to: "/hrm/configuration/payroll", roles: ["hr_ops", "hr_admin"] },
      ],
    },
    {
      id: "talent",
      label: "Talent",
      icon: Sparkles,
      items: [
        { label: "Goals", to: "/hrm/talent/goals" },
        { label: "Performance reviews", to: "/hrm/talent/reviews" },
        { label: "Learning", to: "/hrm/talent/learning" },
        { label: "Feedback and plans", to: "/hrm/talent/feedback" },
        { label: "Succession and skills", to: "/hrm/talent/succession" },
      ],
    },
    {
      id: "experience",
      label: "Employee experience",
      icon: MessagesSquare,
      items: [
        { label: "HR requests", to: "/hrm/requests" },
        { label: "Raise a request", to: "/hrm/requests/new" },
        { label: "Letters", to: "/hrm/experience/letters" },
        { label: "Knowledge", to: "/hrm/experience/knowledge" },
        { label: "Engagement", to: "/hrm/experience/announcements" },
        { label: "Speak up", to: "/hrm/speak-up" },
      ],
    },
    {
      id: "relations",
      label: "Relations and safety",
      icon: Shield,
      roles: ["hr_ops", "hr_admin"],
      items: [
        { label: "Case operations", to: "/hrm/relations/operations" },
        { label: "Protected disclosures", to: "/hrm/relations/protected-disclosures" },
        { label: "Cases", to: "/hrm/relations/cases" },
        { label: "Warning register", to: "/hrm/relations/discipline" },
        { label: "Health and safety", to: "/hrm/relations/safety" },
        { label: "Ethics and declarations", to: "/hrm/relations/ethics" },
        { label: "Agreements and roll call", to: "/hrm/relations/labour" },
      ],
    },
    {
      id: "approvals",
      label: "Approvals",
      icon: ShieldCheck,
      to: "/hrm/approvals",
      roles: ["manager", "hr_ops", "hr_admin", "payroll"],
    },
    {
      id: "reports",
      label: "Reports",
      icon: BarChart3,
      roles: ["manager", "hr_ops", "hr_admin", "payroll"],
      items: [
        { label: "Statutory filings", to: "/hrm/reports" },
        { label: "Report builder", to: "/hrm/reports/builder" },
      ],
    },
    {
      id: "configuration",
      label: "Configuration",
      icon: Settings,
      to: "/hrm/configuration",
      roles: ["hr_admin"],
    },
  ],
};

/**
 * The single Configuration entry point, internally grouped.
 * `to` is set only where a real screen exists; everything else renders as an
 * explicitly planned row rather than a button that silently does nothing.
 */
export const configurationGroups: {
  label: string;
  description: string;
  items: { label: string; detail: string; to?: string }[];
}[] = [
  {
    label: "Business setup",
    description: "Who you are as an employer and how the organisation is shaped.",
    items: [
      {
        label: "Legal entities and branches",
        detail: "3 entities · 6 branches",
        to: "/hrm/configuration/organisation",
      },
      {
        label: "Departments and cost centres",
        detail: "12 departments",
        to: "/hrm/configuration/organisation",
      },
      {
        label: "Jobs, grades and pay ranges",
        detail: "9 grades",
        to: "/hrm/configuration/business",
      },
      {
        label: "Calendars and public holidays",
        detail: "3 calendars",
        to: "/hrm/configuration/business",
      },
      {
        label: "Country packs",
        detail: "NL, KE, DE · effective-dated",
        to: "/hrm/configuration/business",
      },
      {
        label: "Language and localisation",
        detail: "1 language active",
        to: "/hrm/configuration/business",
      },
      {
        label: "Payroll setup",
        detail: "Pay groups · ZRA PAYE · NAPSA · NHIMA · components",
        to: "/hrm/configuration/payroll",
      },
      {
        label: "Payroll configuration",
        detail: "Legacy page — replaced by Payroll setup",
        to: "/hrm/configuration/business",
      },
    ],
  },
  {
    label: "Process design",
    description: "How work moves: policies, approval routes and forms.",
    items: [
      {
        label: "Leave policies and accruals",
        detail: "5 policies · 1 draft",
        to: "/hrm/configuration/process",
      },
      {
        label: "Attendance and shift rules",
        detail: "4 rule sets",
        to: "/hrm/configuration/process",
      },
      { label: "Approval routing", detail: "7 routes", to: "/hrm/configuration/process" },
      {
        label: "Request categories and SLAs",
        detail: "11 categories",
        to: "/hrm/configuration/process",
      },
      {
        label: "Forms and fields",
        detail: "Custom fields and validation",
        to: "/hrm/configuration/process",
      },
      {
        label: "Automation",
        detail: "Trigger, condition and action rules",
        to: "/hrm/configuration/process",
      },
      { label: "Templates", detail: "Letters and notifications", to: "/hrm/configuration/process" },
      {
        label: "Self-service experience",
        detail: "What employees can see and do",
        to: "/hrm/configuration/process",
      },
    ],
  },
  {
    label: "Security and compliance",
    description: "Access, sensitive data handling and audit obligations.",
    items: [
      {
        label: "Roles and permissions",
        detail: "Backend-enforced matrix",
        to: "/hrm/configuration/compliance",
      },
      {
        label: "Sensitive field masking",
        detail: "6 masked fields",
        to: "/hrm/configuration/compliance",
      },
      {
        label: "Protected disclosure handling",
        detail: "Restricted to 2 handlers",
        to: "/hrm/configuration/compliance",
      },
      {
        label: "Privacy and consent administration",
        detail: "Purposes and retention",
        to: "/hrm/configuration/compliance",
      },
      {
        label: "Retention and audit",
        detail: "Tenant isolation · audit · legal holds · control evidence",
        to: "/hrm/configuration/compliance",
      },
    ],
  },
  {
    label: "Technical",
    description: "Integrations and data movement. Rarely changed after go-live.",
    items: [
      { label: "Import and export", detail: "CSV templates", to: "/hrm/configuration/technical" },
      {
        label: "Payroll interface",
        detail: "Finance, banking and statutory hand-offs",
        to: "/hrm/configuration/integrations",
      },
      {
        label: "Identity provider",
        detail: "Workforce link monitoring and sync",
        to: "/hrm/configuration/integrations",
      },
      {
        label: "Production readiness",
        detail: "Go-live gates, evidence, rehearsals and role sign-off",
        to: "/hrm/configuration/go-live",
      },
      {
        label: "Vendor and contract management",
        detail: "No vendors recorded",
        to: "/hrm/configuration/technical",
      },
      {
        label: "Numbering and references",
        detail: "Prefix: HR-",
        to: "/hrm/configuration/technical",
      },
    ],
  },
];
