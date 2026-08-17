import { Link, useRouterState } from "@tanstack/react-router";
import {
  Bell,
  Building2,
  CheckSquare,
  ChevronDown,
  CircleHelp,
  LogOut,
  Menu,
  Moon,
  Search,
  Sun,
  UserRound,
} from "lucide-react";
import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from "@/components/ui/command";
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { Separator } from "@/components/ui/separator";
import {
  attendanceCorrections,
  employees,
  entities,
  hrCases,
  leaveRequests,
  notifications,
  workspaces,
} from "@/mock/data";
import { derivePayslips } from "@/mock/payrollrun";
import type { Role } from "@/mock/types";
import { hrmModule } from "@/modules/hrm/nav";
import { isPathEnabled, isSectionEnabled } from "@/modules/hrm/scope";
import { ComingSoon } from "./ComingSoon";
import type { ModuleDefinition, NavItem, NavSection } from "@/platform/nav";
import { useApp, useRoleGate } from "@/platform/app-context";
import { HRM_STAFF_ROLES, useAuth } from "@/platform/auth";
import { adaptWorkers, realApi, useApi } from "@/platform/use-api";
import { SignedInBadge } from "@/platform/components/AuthGate";
import { modules } from "@/platform/modules";
import { cn } from "@/lib/utils";

function useVisibleSections(mod: ModuleDefinition, role: Role) {
  return mod.sections.filter((s) => !s.roles || s.roles.includes(role));
}

/** Out-of-scope sections stay in the rail, greyed, so the roadmap is visible. */
function SoonSection({ section }: { section: NavSection }) {
  const Icon = section.icon;
  return (
    <div
      className="flex cursor-not-allowed items-center gap-2.5 rounded-md px-2.5 py-2 text-sm font-medium text-rail-muted/50"
      aria-disabled="true"
      title={`${section.label} — coming soon`}
    >
      <Icon className="size-4 shrink-0" aria-hidden />
      <span className="min-w-0 flex-1 text-left">{section.label}</span>
      <span className="shrink-0 rounded-full border border-rail-active px-1.5 py-0.5 text-[10px] font-normal">
        Soon
      </span>
    </div>
  );
}

function NavLink({ item, onNavigate }: { item: NavItem; onNavigate?: () => void }) {
  return (
    <Link
      to={item.to}
      params={item.params as never}
      onClick={onNavigate}
      activeProps={{ className: "bg-rail-active text-rail-foreground font-medium" }}
      activeOptions={{ exact: true }}
      className="block rounded-md px-3 py-1.5 text-sm text-rail-muted transition-colors hover:bg-rail-active hover:text-rail-foreground"
    >
      {item.label}
    </Link>
  );
}

function Section({ section, onNavigate }: { section: NavSection; onNavigate?: () => void }) {
  const Icon = section.icon;
  const { role } = useApp();
  const visible = (i: NavItem) => (!i.roles || i.roles.includes(role)) && isPathEnabled(i.to.split("/$")[0]);
  const items = section.items?.filter(visible);
  const groups = section.groups
    ?.map((g) => ({ ...g, items: g.items.filter(visible) }))
    .filter((g) => g.items.length > 0);
  const pathname = useRouterState({ select: (s) => s.location.pathname });
  const childActive =
    items?.some((i) => pathname.startsWith(i.to.split("/$")[0])) ?? false;
  const [open, setOpen] = useState(childActive);
  useEffect(() => {
    if (childActive) setOpen(true);
  }, [childActive]);

  if (section.to) {
    return (
      <Link
        to={section.to}
        onClick={onNavigate}
        activeProps={{ className: "bg-rail-active text-rail-foreground" }}
        activeOptions={{ exact: section.to === "/hrm" }}
        className="flex items-center gap-2.5 rounded-md px-2.5 py-2 text-sm font-medium text-rail-muted transition-colors hover:bg-rail-active hover:text-rail-foreground"
      >
        <Icon className="size-4 shrink-0" aria-hidden />
        {section.label}
      </Link>
    );
  }

  return (
    <div>
      <button
        type="button"
        aria-expanded={open}
        onClick={() => setOpen((o) => !o)}
        className="flex w-full items-center gap-2.5 rounded-md px-2.5 py-2 text-sm font-medium text-rail-muted transition-colors hover:bg-rail-active hover:text-rail-foreground"
      >
        <Icon className="size-4 shrink-0" aria-hidden />
        <span className="min-w-0 flex-1 text-left">{section.label}</span>
        <ChevronDown className={cn("size-3.5 transition-transform", open && "rotate-180")} aria-hidden />
      </button>
      {open ? (
        <div className="mt-0.5 space-y-0.5 border-l border-rail-active pl-3 ml-4">
          {items?.map((i) => <NavLink key={i.to + i.label} item={i} onNavigate={onNavigate} />)}
          {groups?.map((g) => (
            <div key={g.label} className="pt-2">
              <p className="px-3 pb-1 text-[11px] font-semibold uppercase tracking-wide text-rail-muted/80">
                {g.label}
              </p>
              {g.items.map((i) => <NavLink key={i.to + i.label} item={i} onNavigate={onNavigate} />)}
            </div>
          ))}
        </div>
      ) : null}
    </div>
  );
}

function RailContent({ onNavigate }: { onNavigate?: () => void }) {
  const { role } = useApp();
  const sections = useVisibleSections(hrmModule, role);
  const main = sections.filter((s) => s.id !== "configuration");
  const config = sections.find((s) => s.id === "configuration");

  return (
    <div className="flex h-full flex-col">
      <div className="px-3 py-4">
        <p className="px-2 text-xs font-semibold uppercase tracking-wide text-rail-muted">Module</p>
        <p className="px-2 text-sm font-semibold text-rail-foreground">{hrmModule.name}</p>
      </div>
      <nav aria-label="Main" className="flex-1 space-y-1 overflow-y-auto px-3 pb-4">
        {main
          .filter((s) => isSectionEnabled(s.id))
          .map((s) => (
            <Section key={s.id} section={s} onNavigate={onNavigate} />
          ))}
        {main.some((s) => !isSectionEnabled(s.id)) ? (
          <div className="pt-3">
            <p className="px-2.5 pb-1 text-[10px] font-semibold uppercase tracking-wide text-rail-muted/70">
              Coming soon
            </p>
            {main
              .filter((s) => !isSectionEnabled(s.id))
              .map((s) => (
                <SoonSection key={s.id} section={s} />
              ))}
          </div>
        ) : null}
      </nav>
      {config ? (
        <div className="border-t border-rail-active px-3 py-3">
          <Section section={config} onNavigate={onNavigate} />
          <p className="px-2.5 pt-1 text-[11px] text-rail-muted">
            All setup and admin lives here.
          </p>
        </div>
      ) : null}
    </div>
  );
}

function CommandPalette({ open, onOpenChange }: { open: boolean; onOpenChange: (o: boolean) => void }) {
  const { role } = useApp();
  const workerState = useApi(
    () => USE_REAL ? realApi.employees({ page: 1, pageSize: 100 }) : Promise.resolve({ items: [] as unknown[], totalCount: 0 }),
    [],
  );
  const searchableEmployees = USE_REAL ? adaptWorkers(workerState.data ?? { items: [] }) : employees;
  const sections = useVisibleSections(hrmModule, role);
  const links = sections
    .filter((s) => isSectionEnabled(s.id))
    .flatMap((s) =>
      s.to
        ? [{ label: s.label, to: s.to, params: undefined }]
        : (s.items ?? []).map((i) => ({ label: `${s.label}: ${i.label}`, to: i.to, params: i.params })),
    )
    .filter((l) => isPathEnabled(l.to.split("/$")[0]));

  return (
    <CommandDialog open={open} onOpenChange={onOpenChange}>
      <CommandInput placeholder="Search screens, records and actions…" />
      <CommandList>
        <CommandEmpty>Nothing matched. Try a reference like LV-2026-0412.</CommandEmpty>
        <CommandGroup heading="Go to">
          {links.map((l) => (
            <CommandItem key={l.label} value={l.label} asChild>
              <Link to={l.to} params={l.params as never} onClick={() => onOpenChange(false)}>
                {l.label}
              </Link>
            </CommandItem>
          ))}
        </CommandGroup>
        <CommandGroup heading="People">
          {searchableEmployees.map((e) => (
            <CommandItem key={e.id} value={`${e.fullName} ${e.employeeNo} ${e.jobTitle}`} asChild>
              <Link to="/hrm/employees/$id" params={{ id: e.id }} onClick={() => onOpenChange(false)}>
                <span className="flex min-w-0 flex-col">
                  <span className="truncate">{e.fullName}</span>
                  <span className="truncate text-xs text-muted-foreground">
                    {e.employeeNo} · {e.jobTitle}
                  </span>
                </span>
              </Link>
            </CommandItem>
          ))}
        </CommandGroup>

        {!USE_REAL ? <CommandGroup heading="Requests and cases">
          {leaveRequests.map((r) => (
            <CommandItem key={r.id} value={`${r.id} ${r.type} leave`} asChild>
              <Link to="/hrm/leave/$id" params={{ id: r.id }} onClick={() => onOpenChange(false)}>
                <span className="truncate">
                  {r.id} — {r.type} leave
                </span>
              </Link>
            </CommandItem>
          ))}
          {attendanceCorrections.map((r) => (
            <CommandItem key={r.id} value={`${r.id} attendance correction`} asChild>
              <Link to="/hrm/attendance/$id" params={{ id: r.id }} onClick={() => onOpenChange(false)}>
                <span className="truncate">{r.id} — Attendance correction</span>
              </Link>
            </CommandItem>
          ))}
          {hrCases.map((c) => (
            <CommandItem key={c.id} value={`${c.id} ${c.subject} ${c.category}`} asChild>
              <Link to="/hrm/requests/$id" params={{ id: c.id }} onClick={() => onOpenChange(false)}>
                <span className="truncate">
                  {c.id} — {c.subject}
                </span>
              </Link>
            </CommandItem>
          ))}
        </CommandGroup> : null}

        {!USE_REAL ? <CommandGroup heading="Payslips">
          {derivePayslips().map((p) => (
            <CommandItem key={p.id} value={`${p.id} payslip ${p.period} ${p.employee}`} asChild>
              <Link to="/hrm/payslips/$id" params={{ id: p.id }} onClick={() => onOpenChange(false)}>
                <span className="truncate">
                  {p.employee} — {p.period}
                </span>
              </Link>
            </CommandItem>
          ))}
        </CommandGroup> : null}
      </CommandList>
    </CommandDialog>
  );
}

const APPROVER_ROLES: Role[] = ["manager", "hr_ops", "hr_admin", "payroll"];

const USE_REAL = (import.meta.env.VITE_USE_REAL_API as string | undefined) === "true";

/** User display line: real identity when OIDC-signed-in, otherwise the demo name. */
function RealUserLine() {
  const { user, worker, resolvingWorker } = useAuth();

  if (USE_REAL && user?.name) {
    return (
      <span className="flex min-w-0 items-center gap-2.5">
        <span className="flex size-8 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary/10">
          {worker?.photoUrl ? (
            <img src={worker.photoUrl} alt="" className="size-8 object-cover" />
          ) : (
            <UserRound className="size-4 text-primary" aria-hidden />
          )}
        </span>
        <span className="min-w-0">
          {/* M14 identity link: linked worker name wins; falls back to the IdP name. */}
          <span className="block truncate">
            {worker?.fullName || user.name}
            {worker ? (
              <span className="ml-1.5 rounded border px-1 text-[10px] font-normal text-muted-foreground">
                {worker.employeeNo}
              </span>
            ) : resolvingWorker ? (
              <span className="ml-1.5 text-[10px] font-normal text-muted-foreground">…</span>
            ) : null}
          </span>
          {worker?.jobTitle ? (
            <span className="block truncate text-xs font-normal text-muted-foreground">
              {worker.jobTitle}
            </span>
          ) : user.email ? (
            <span className="block truncate text-xs font-normal text-muted-foreground">{user.email}</span>
          ) : null}
        </span>
      </span>
    );
  }
  return <span>Chanda Mwansa-Chileshe</span>;
}

/** Sign-out action: real OIDC logout in hybrid mode, demo link otherwise. */
function RealSignOut() {
  const { signOut } = useAuth();
  if (USE_REAL) {
    return (
      <button type="button" className="flex w-full items-center gap-2 px-2 py-1.5 text-sm" onClick={() => signOut()}>
        <LogOut className="size-4" aria-hidden /> Sign out
      </button>
    );
  }
  return (
    <Link to="/sign-in">
      <LogOut className="size-4" aria-hidden /> Sign out
    </Link>
  );
}

// M27 P0 UX audit: same open-for-decision predicate used by the Approvals
// page — the shell badge must agree with the page it points to.
const OPEN_STATUSES = new Set(["pending", "submitted", "open", "in progress", "in-review", "in progress", "returned", "awaiting employee"]);

function countOpen(items: unknown[]): number {
  return items.filter((raw) => {
    const x = raw as Record<string, unknown>;
    const s = String(x?.status ?? "").toLowerCase();
    return OPEN_STATUSES.has(s);
  }).length;
}

export function AppShell({ children }: { children: ReactNode }) {
  const { role, setRole, entityId, setEntityId, branch, setBranch, theme, toggleTheme } = useApp();
  const { worker: myWorker, user } = useAuth();
  const shellState = useApi(async () => {
    if (!USE_REAL) return null;
    const [legalEntities, locations, notificationInbox, queue, leave, corrections] = await Promise.all([
      realApi.legalEntities(),
      realApi.locations(),
      realApi.myNotifications(),
      realApi.workflowQueue().catch(() => ({ items: [], totalCount: 0 })),
      realApi.leaveRequests({ page: 1, pageSize: 1 }).catch(() => ({ items: [], totalCount: 0 })),
      realApi.timeCorrections({ page: 1, pageSize: 1 }).catch(() => ({ items: [], totalCount: 0 })),
    ]);
    return {
      legalEntities: (Array.isArray(legalEntities) ? legalEntities : []).map((raw) => {
        const e = raw as Record<string, unknown>;
        return { id: String(e.id ?? ""), registeredName: String(e.registeredName ?? ""), countryCode: String(e.countryCode ?? e.country ?? "") };
      }),
      locations: (Array.isArray(locations) ? locations : []).map((raw) => {
        const l = raw as Record<string, unknown>;
        return { id: String(l.id ?? ""), name: String(l.name ?? ""), legalEntityId: String(l.legalEntityId ?? "") };
      }),
      notificationInbox,
      // M27 P0 UX audit: the approvals badge was a hardcoded mock "3". The
      // shell now counts everything still open for a decision, matching the
      // same is-decidable predicate the Approvals page uses.
      pendingDecisions:
        countOpen(Array.isArray(queue?.items) ? queue.items : []) +
        countOpen(Array.isArray(leave?.items) ? leave.items : []) +
        countOpen(Array.isArray(corrections?.items) ? corrections.items : []),
    };
  }, []);
  const canApprove = useRoleGate()(APPROVER_ROLES);
  const [paletteOpen, setPaletteOpen] = useState(false);
  const liveEntities = shellState.data?.legalEntities ?? [];
  const liveLocations = shellState.data?.locations ?? [];
  const pendingDecisions = shellState.data?.pendingDecisions ?? 0;
  const entity = USE_REAL
    ? liveEntities.find((e) => e.id === entityId) ?? liveEntities[0]
    : entities.find((e) => e.id === entityId) ?? entities[0];
  const entityLocations: { id: string; name: string }[] = USE_REAL
    ? liveLocations.filter((location) => !entity || location.legalEntityId === entity.id)
    : (entity as { branches?: string[] })?.branches?.map((name) => ({ id: String(name), name: String(name) })) ?? [];
  const pathname = useRouterState({ select: (st) => st.location.pathname });
  const inScope = isPathEnabled(pathname);
  const liveNotifications = shellState.data?.notificationInbox.items ?? [];
  const unread = USE_REAL
    ? shellState.data?.notificationInbox.unreadCount ?? 0
    : notifications.filter((n) => n.unread).length;

  useEffect(() => {
    if (!USE_REAL || !liveEntities.length) return;
    if (!liveEntities.some((candidate) => String(candidate.id) === entityId))
      setEntityId(String(liveEntities[0].id));
  }, [entityId, liveEntities, setEntityId]);

  useEffect(() => {
    if (!USE_REAL || !entityLocations.length) return;
    if (!entityLocations.some((candidate) => String(candidate.name ?? candidate.id) === branch))
      setBranch(String(entityLocations[0].name ?? entityLocations[0].id));
  }, [branch, entityLocations, setBranch]);

  useEffect(() => {
    const h = (e: KeyboardEvent) => {
      if (e.key === "k" && (e.metaKey || e.ctrlKey)) {
        e.preventDefault();
        setPaletteOpen((o) => !o);
      }
    };
    window.addEventListener("keydown", h);
    return () => window.removeEventListener("keydown", h);
  }, []);

  return (
    <div className="min-h-screen bg-background">
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:absolute focus:left-3 focus:top-3 focus:z-50 focus:rounded focus:bg-primary focus:px-3 focus:py-2 focus:text-primary-foreground"
      >
        Skip to content
      </a>

      <header className="sticky top-0 z-40 border-b bg-surface">
        <div className="flex h-14 items-center gap-2 px-3">
          <Sheet>
            <SheetTrigger asChild>
              <Button variant="ghost" size="icon" className="lg:hidden" aria-label="Open navigation">
                <Menu className="size-5" aria-hidden />
              </Button>
            </SheetTrigger>
            <SheetContent side="left" className="w-72 bg-rail p-0 text-rail-foreground">
              <SheetHeader className="sr-only">
                <SheetTitle>Navigation</SheetTitle>
              </SheetHeader>
              <RailContent />
            </SheetContent>
          </Sheet>

          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" className="gap-2 px-2">
                <img src="/mightyfin-mark.png" alt="" className="size-5" aria-hidden />
                <span className="hidden font-semibold sm:inline">Mightyfin ERP</span>
                <ChevronDown className="size-3.5" aria-hidden />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="start" className="w-64">
              <DropdownMenuLabel>Modules</DropdownMenuLabel>
              {modules.map((m) => (
                <DropdownMenuItem key={m.id} disabled={!m.available}>
                  {m.label}
                  {!m.available ? <span className="ml-auto text-xs text-muted-foreground">Not enabled</span> : null}
                </DropdownMenuItem>
              ))}
              <DropdownMenuSeparator />
              <DropdownMenuLabel>Workspace</DropdownMenuLabel>
              {USE_REAL ? (
                <div className="px-2 py-1.5 text-xs text-muted-foreground">
                  Assigned roles: {(user?.roles ?? []).filter((r) => HRM_STAFF_ROLES.includes(r as never)).join(", ") || "employee"}
                </div>
              ) : <DropdownMenuRadioGroup value={role} onValueChange={(v) => setRole(v as Role)}>
                {workspaces.map((w) => (
                  <DropdownMenuRadioItem key={w.id} value={w.id}>
                    {w.label}
                  </DropdownMenuRadioItem>
                ))}
              </DropdownMenuRadioGroup>}
            </DropdownMenuContent>
          </DropdownMenu>

          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline" size="sm" className="hidden min-w-0 gap-2 md:flex">
                <Building2 className="size-4 shrink-0" aria-hidden />
                <span className="max-w-48 truncate">{String((entity as Record<string, unknown> | undefined)?.registeredName ?? (entity as Record<string, unknown> | undefined)?.name ?? "Organisation")}</span>
                <span className="text-muted-foreground">· {branch}</span>
                <ChevronDown className="size-3.5 shrink-0" aria-hidden />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="start" className="w-80">
              <DropdownMenuLabel>Legal entity</DropdownMenuLabel>
              <DropdownMenuRadioGroup value={entityId} onValueChange={setEntityId}>
                {(USE_REAL ? liveEntities : entities).map((e) => (
                  <DropdownMenuRadioItem key={String(e.id)} value={String(e.id)}>
                    <span className="min-w-0">
                      <span className="block truncate">{String((e as Record<string, unknown>).registeredName ?? (e as Record<string, unknown>).name ?? e.id)}</span>
                      <span className="block text-xs text-muted-foreground">{String((e as Record<string, unknown>).countryCode ?? (e as Record<string, unknown>).country ?? "")}</span>
                    </span>
                  </DropdownMenuRadioItem>
                ))}
              </DropdownMenuRadioGroup>
              <DropdownMenuSeparator />
              <DropdownMenuLabel>Branch</DropdownMenuLabel>
              <DropdownMenuRadioGroup value={branch} onValueChange={setBranch}>
                {entityLocations.map((location) => {
                  const name = String(location.name ?? location.id);
                  return <DropdownMenuRadioItem key={String(location.id)} value={name}>
                    {name}
                  </DropdownMenuRadioItem>
                })}
              </DropdownMenuRadioGroup>
            </DropdownMenuContent>
          </DropdownMenu>

          <div className="ml-auto flex items-center gap-1">
            <Button
              variant="outline"
              size="sm"
              className="gap-2 text-muted-foreground"
              onClick={() => setPaletteOpen(true)}
            >
              <Search className="size-4" aria-hidden />
              <span className="hidden sm:inline">Search</span>
              <kbd className="hidden rounded border px-1 text-[10px] sm:inline">⌘K</kbd>
            </Button>

            {canApprove ? (
              <Button asChild variant="ghost" size="icon" className="relative" aria-label="Tasks and approvals">
                <Link to="/hrm/approvals">
                  <CheckSquare className="size-5" aria-hidden />
                  {Number(pendingDecisions) > 0 ? (
                    <span className="absolute right-1 top-1 rounded-full bg-warning px-1 text-[10px] font-semibold text-warning-foreground">
                      {pendingDecisions}
                    </span>
                  ) : null}
                </Link>
              </Button>
            ) : null}

            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" className="relative" aria-label={`Notifications, ${unread} unread`}>
                  <Bell className="size-5" aria-hidden />
                  {unread ? (
                    <span className="absolute right-1 top-1 rounded-full bg-danger px-1 text-[10px] font-semibold text-danger-foreground">
                      {unread}
                    </span>
                  ) : null}
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-80">
                <DropdownMenuLabel>Notifications</DropdownMenuLabel>
                {(USE_REAL ? liveNotifications : notifications).map((n) => {
                  const row = n as Record<string, unknown>;
                  const target = String(row.actionUrl ?? row.to ?? "/hrm/self-service");
                  return <DropdownMenuItem key={String(row.id)} asChild>
                    <Link
                      to={target}
                      className="flex cursor-pointer flex-col items-start gap-0.5"
                    >
                      <span className="text-sm font-medium">{String(row.title ?? "HR update")}</span>
                      <span className="text-xs text-muted-foreground">{String(row.status ?? row.body ?? "")}</span>
                      <span className="text-[11px] text-muted-foreground">{row.createdAt ? new Date(String(row.createdAt)).toLocaleString() : String(row.at ?? "")}</span>
                    </Link>
                  </DropdownMenuItem>
                })}
                {USE_REAL && liveNotifications.length === 0 ? (
                  <DropdownMenuItem disabled>No notifications</DropdownMenuItem>
                ) : null}
              </DropdownMenuContent>
            </DropdownMenu>

            <Button variant="ghost" size="icon" aria-label="Help" asChild>
              <Link to="/hrm/help">
                <CircleHelp className="size-5" aria-hidden />
              </Link>
            </Button>

            <Button variant="ghost" size="icon" onClick={toggleTheme} aria-label={`Switch to ${theme === "light" ? "dark" : "light"} mode`}>
              {theme === "light" ? <Moon className="size-5" aria-hidden /> : <Sun className="size-5" aria-hidden />}
            </Button>

            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" aria-label="User menu">
                  <UserRound className="size-5" aria-hidden />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-64">
                <DropdownMenuLabel className="flex flex-col">
                  <RealUserLine />
                  <span className="text-xs font-normal text-muted-foreground">
                    Acting as {workspaces.find((w) => w.id === role)?.label}
                  </span>
                </DropdownMenuLabel>
                <DropdownMenuSeparator />
                <DropdownMenuItem asChild>
                  {/* M14 identity link: jump to the signed-in user's own worker record when linked. */}
                  <Link to={myWorker ? "/hrm/my-profile" : "/hrm/employees/$id"} params={{ id: myWorker?.id ?? "w-1001" }}>
                    My profile{myWorker ? "" : USE_REAL ? " (not linked)" : ""}
                  </Link>
                </DropdownMenuItem>
                {!USE_REAL ? <DropdownMenuItem asChild>
                  <Link to="/hrm/setup">Setup guide</Link>
                </DropdownMenuItem> : null}
                <DropdownMenuSeparator />
                <DropdownMenuItem asChild>
                  <RealSignOut />
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>
        <Separator />
      </header>

      <div className="flex">
        <aside className="sticky top-14 hidden h-[calc(100vh-3.5rem)] w-64 shrink-0 bg-rail text-rail-foreground lg:block">
          <RailContent />
        </aside>
        <main id="main" className="min-w-0 flex-1 px-4 py-6 sm:px-6 lg:px-8">
          <div className="mx-auto flex max-w-6xl flex-col space-y-6">
            {inScope ? children : <ComingSoon />}
          </div>
        </main>
      </div>

      <CommandPalette open={paletteOpen} onOpenChange={setPaletteOpen} />
    </div>
  );
}
