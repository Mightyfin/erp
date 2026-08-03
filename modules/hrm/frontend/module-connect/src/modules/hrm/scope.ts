/**
 * Release scope — the single switch that decides what ships now.
 *
 * This release covers Setup, People, Payroll and Configuration, plus
 * everything payroll actually depends on: a pay run is only as good as the
 * hours, absence and claims that feed it, and the approvals that make those
 * figures final.
 *
 * Everything else is built but deliberately held back and shown as
 * "Coming soon" rather than deleted. Re-enabling an area is a one-line change.
 */

/** Nav sections included in this release, with why they are in. */
export const ENABLED_SECTIONS = new Set([
  "home",
  "people", // the employee record payroll pays
  "time", // hours, absence, timesheets and claims — all payroll inputs
  "payroll",
  "approvals", // an unapproved correction silently drops out of the run
  "configuration",
]);

/** Route prefixes included in this release. */
const ENABLED_PREFIXES = [
  "/employees",
  "/people",
  "/attendance", // hours feeding the run
  "/leave", // paid and unpaid absence
  "/time", // timesheets, TOIL, utilisation, travel and expenses
  "/payslips",
  "/pay",
  "/payroll",
  "/approvals",
  "/configuration",
  "/setup",
  "/help",
  "/sign-in",
];

/** Always reachable regardless of scope. */
const ALWAYS = ["/", "/setup", "/help", "/sign-in", "/speak-up"];

export function isSectionEnabled(id: string) {
  return ENABLED_SECTIONS.has(id);
}

export function isPathEnabled(pathname: string) {
  if (ALWAYS.includes(pathname)) return true;
  return ENABLED_PREFIXES.some((p) => pathname === p || pathname.startsWith(p + "/"));
}

/** Shown on the Coming soon screen so the roadmap is explicit, not a dead end. */
export const COMING_SOON_AREAS = [
  "Lifecycle — onboarding, movements, offboarding, assets",
  "Recruitment — requisitions, vacancies, candidates, offers",
  "Talent — goals, reviews, learning, succession",
  "Employee experience — requests, letters, knowledge, engagement",
  "Relations and safety — cases, discipline, health and safety, ethics",
  "Reporting and analytics",
];
