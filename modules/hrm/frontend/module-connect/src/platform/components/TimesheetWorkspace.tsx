import { useEffect, type ReactNode } from "react";
import { Link, useRouterState, useNavigate } from "@tanstack/react-router";
import { isTimesheetOnly, useAuth } from "@/platform/auth";
import { SignedInBadge } from "@/platform/components/AuthGate";
import { Button } from "@/components/ui/button";

const USE_REAL = (import.meta.env.VITE_USE_REAL_API as string | undefined) === "true";
const paths = new Set(["/hrm/time/timesheets", "/hrm/time/attendance/import", "/sign-in"]);

/** Block unrelated pages before they mount or request data. */
export function TimesheetBoundary({ children }: { children: ReactNode }) {
  const { loading, user } = useAuth();
  const pathname = useRouterState({ select: (state) => state.matches.at(-1)?.pathname ?? state.location.pathname });
  const navigate = useNavigate();
  const denied = USE_REAL && isTimesheetOnly(user?.roles ?? []) && !paths.has(pathname.replace(/\/$/, ""));
  useEffect(() => {
    if (denied) void navigate({ to: "/hrm/time/timesheets", replace: true });
  }, [denied, navigate]);
  if ((USE_REAL && loading && pathname !== "/sign-in") || denied)
    return <div className="p-8 text-sm text-muted-foreground" role="status">Loading your workspace…</div>;
  return <>{children}</>;
}

export function TimesheetShell({ children }: { children: ReactNode }) {
  return <div className="min-h-screen bg-background">
    <header className="flex flex-wrap items-center justify-between gap-4 border-b px-6 py-4">
      <span className="font-semibold">Mightyfin HRMS · Timesheets</span>
      <SignedInBadge />
    </header>
    <nav aria-label="Timesheet navigation" className="flex gap-2 border-b px-6 py-3">
      <Button variant="ghost" asChild><Link to="/hrm/time/timesheets">Timesheets</Link></Button>
      <Button variant="ghost" asChild><Link to="/hrm/time/attendance/import">Import attendance</Link></Button>
    </nav>
    <main className="mx-auto max-w-screen-2xl space-y-6 p-6">{children}</main>
  </div>;
}
