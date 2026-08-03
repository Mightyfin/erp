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
    { id: "home", label: "Home", icon: Home, to: "/" },
    {
      id: "people",
      label: "People",
      icon: Users,
      items: [
        { label: "Employees", to: "/employees" },
        { label: "My profile", to: "/employees/$id", params: { id: "w-1001" } },
        { label: "Positions", to: "/people/positions", roles: ["hr_ops", "hr_admin", "manager"] },
        { label: "Organisation structure", to: "/people/org", roles: ["hr_ops", "hr_admin", "manager"] },
        { label: "Documents", to: "/people/documents" },
        { label: "Privacy and consent", to: "/people/privacy" },
        { label: "Data quality", to: "/people/data-quality", roles: ["hr_ops", "hr_admin"] },
      ],
    },
    {
      id: "lifecycle",
      label: "Lifecycle",
      icon: UserCog,
      roles: ["hr_ops", "hr_admin", "manager"],
      items: [
        { label: "Onboarding", to: "/lifecycle/onboarding" },
        { label: "Movements and transfers", to: "/lifecycle/movements" },
        { label: "Offboarding", to: "/lifecycle/offboarding" },
        { label: "Assets and access", to: "/lifecycle/assets" },
        { label: "Journeys", to: "/lifecycle/journeys" },
        { label: "Assignments", to: "/lifecycle/mobility" },
        { label: "Alumni and rehire", to: "/lifecycle/alumni" },
      ],
    },
    {
      id: "recruitment",
      label: "Recruitment",
      icon: Briefcase,
      roles: ["hr_ops", "hr_admin", "manager"],
      items: [
        { label: "Requisitions", to: "/recruitment/requisitions" },
        { label: "Vacancies", to: "/recruitment/vacancies" },
        { label: "Candidates", to: "/recruitment/candidates" },
        { label: "Offers and referrals", to: "/recruitment/offers" },
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
            { label: "Leave requests", to: "/leave" },
            { label: "Request leave", to: "/leave/new" },
          ],
        },
        {
          label: "Attendance",
          items: [
            { label: "Clock in and out", to: "/attendance/clock" },
            { label: "Corrections", to: "/attendance" },
            { label: "Raise a correction", to: "/attendance/new" },
          ],
        },
        {
          label: "Scheduling and claims",
          items: [
            { label: "Schedules and rosters", to: "/time/schedules" },
            { label: "Timesheets", to: "/time/timesheets" },
            { label: "Time off in lieu", to: "/time/toil" },
            { label: "Utilisation", to: "/time/utilisation" },
            { label: "Travel", to: "/time/travel" },
            { label: "Expenses", to: "/time/expenses" },
          ],
        },
      ],
    },
    {
      id: "payroll",
      label: "Payroll",
      icon: Banknote,
      items: [
        { label: "My payslips", to: "/payslips" },
        { label: "Compensation and benefits", to: "/pay/compensation" },
        { label: "Payroll administration", to: "/payroll", roles: ["payroll", "hr_admin"] },
        { label: "Pay runs", to: "/payroll/runs", roles: ["payroll", "hr_admin"] },
        { label: "Payroll exceptions", to: "/payroll/exceptions", roles: ["payroll", "hr_admin"] },
      ],
    },
    {
      id: "talent",
      label: "Talent",
      icon: Sparkles,
      items: [
        { label: "Goals", to: "/talent/goals" },
        { label: "Performance reviews", to: "/talent/reviews" },
        { label: "Learning", to: "/talent/learning" },
        { label: "Feedback and plans", to: "/talent/feedback" },
        { label: "Succession and skills", to: "/talent/succession" },
      ],
    },
    {
      id: "experience",
      label: "Employee experience",
      icon: MessagesSquare,
      items: [
        { label: "HR requests", to: "/requests" },
        { label: "Raise a request", to: "/requests/new" },
        { label: "Letters", to: "/experience/letters" },
        { label: "Knowledge", to: "/experience/knowledge" },
        { label: "Engagement", to: "/experience/announcements" },
        { label: "Speak up", to: "/speak-up" },
      ],
    },
    {
      id: "relations",
      label: "Relations and safety",
      icon: Shield,
      roles: ["hr_ops", "hr_admin"],
      items: [
        { label: "Cases", to: "/relations/cases" },
        { label: "Warning register", to: "/relations/discipline" },
        { label: "Health and safety", to: "/relations/safety" },
        { label: "Ethics and declarations", to: "/relations/ethics" },
        { label: "Agreements and roll call", to: "/relations/labour" },
      ],
    },
    {
      id: "approvals",
      label: "Approvals",
      icon: ShieldCheck,
      to: "/approvals",
      roles: ["manager", "hr_ops", "hr_admin", "payroll"],
    },
    {
      id: "reports",
      label: "Reports",
      icon: BarChart3,
      roles: ["manager", "hr_ops", "hr_admin", "payroll"],
      items: [
        { label: "Reports and analytics", to: "/reports" },
        { label: "Report builder", to: "/reports/builder" },
      ],
    },
    {
      id: "configuration",
      label: "Configuration",
      icon: Settings,
      to: "/configuration",
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
      { label: "Legal entities and branches", detail: "3 entities · 6 branches", to: "/configuration/organisation" },
      { label: "Departments and cost centres", detail: "12 departments", to: "/configuration/organisation" },
      { label: "Jobs, grades and pay ranges", detail: "9 grades", to: "/configuration/business" },
      { label: "Calendars and public holidays", detail: "3 calendars", to: "/configuration/business" },
      { label: "Country packs", detail: "NL, KE, DE · effective-dated", to: "/configuration/business" },
      { label: "Language and localisation", detail: "1 language active", to: "/configuration/business" },
      { label: "Payroll configuration", detail: "Pay groups, components, tax rules", to: "/configuration/business" },
    ],
  },
  {
    label: "Process design",
    description: "How work moves: policies, approval routes and forms.",
    items: [
      { label: "Leave policies and accruals", detail: "5 policies · 1 draft", to: "/configuration/process" },
      { label: "Attendance and shift rules", detail: "4 rule sets", to: "/configuration/process" },
      { label: "Approval routing", detail: "7 routes", to: "/configuration/process" },
      { label: "Request categories and SLAs", detail: "11 categories", to: "/configuration/process" },
      { label: "Forms and fields", detail: "Custom fields and validation", to: "/configuration/process" },
      { label: "Automation", detail: "Trigger, condition and action rules", to: "/configuration/process" },
      { label: "Templates", detail: "Letters and notifications", to: "/configuration/process" },
      { label: "Self-service experience", detail: "What employees can see and do", to: "/configuration/process" },
    ],
  },
  {
    label: "Security and compliance",
    description: "Access, sensitive data handling and audit obligations.",
    items: [
      { label: "Roles and permissions", detail: "5 roles", to: "/configuration/roles" },
      { label: "Sensitive field masking", detail: "6 masked fields", to: "/configuration/roles" },
      { label: "Protected disclosure handling", detail: "Restricted to 2 handlers", to: "/configuration/compliance" },
      { label: "Privacy and consent administration", detail: "Purposes and retention", to: "/configuration/compliance" },
      { label: "Retention and audit", detail: "Audit log enabled", to: "/configuration/compliance" },
    ],
  },
  {
    label: "Technical",
    description: "Integrations and data movement. Rarely changed after go-live.",
    items: [
      { label: "Import and export", detail: "CSV templates", to: "/configuration/technical" },
      { label: "Payroll interface", detail: "Mock connector", to: "/configuration/technical" },
      { label: "Identity provider", detail: "Not configured", to: "/configuration/technical" },
      { label: "Vendor and contract management", detail: "No vendors recorded", to: "/configuration/technical" },
      { label: "Numbering and references", detail: "Prefix: HR-", to: "/configuration/technical" },
    ],
  },
];
