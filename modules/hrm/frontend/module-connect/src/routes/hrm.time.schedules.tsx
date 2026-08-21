import { createFileRoute, Link } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import {
  AlertTriangle,
  Building2,
  CalendarDays,
  CheckCircle2,
  Clock,
  Info,
  Moon,
  PhoneCall,
  Plane,
  Users,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { employees } from "@/mock/data";
import { holidayNote, timeclockApi } from "@/mock/timeclock";
import { realApi, useApi } from "@/platform/use-api";
import type { CoverageDay, OpenShift, RosterDay, ShiftKind, SwapCandidate } from "@/mock/timeclock";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { Input } from "@/components/ui/input";
import { feedback } from "@/platform/feedback";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/time/schedules")({
  head: () => ({
    meta: [
      { title: "My schedule — New World Cargo HRM" },
      {
        name: "description",
        content:
          "The next 14 days of your roster, with shift swaps, time-off requests, team coverage gaps and open shifts you can pick up.",
      },
      { property: "og:title", content: "My schedule — New World Cargo HRM" },
      {
        property: "og:description",
        content:
          "The next 14 days of your roster, with shift swaps, time-off requests, team coverage gaps and open shifts you can pick up.",
      },
    ],
  }),
  component: SchedulePage,
});

const MANAGER = "Mutale Kabwe (Operations Manager)";

const employeeName = (id: string) =>
  employees.find((e) => e.id === id)?.fullName ?? "Unknown employee";

const employeeMeta = (id: string) => {
  const e = employees.find((w) => w.id === id);
  return e ? `${e.jobTitle} · ${e.branch}` : "No longer in this cover group";
};

const longDate = (date: string) =>
  new Date(`${date}T00:00:00Z`).toLocaleDateString("en-GB", {
    weekday: "long",
    day: "numeric",
    month: "long",
    year: "numeric",
    timeZone: "UTC",
  });

/** Decisions are due two days before the shift so cover can still be arranged. */
const decisionDue = (date: string) => {
  const d = new Date(`${date}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() - 2);
  return longDate(d.toISOString().slice(0, 10));
};

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";
const SCHEDULE_WORKER = "019ffa92-9fe5-7057-84b3-cb76b7449ba0";

const rosterDayKind = (r: Record<string, unknown>): string => {
  const st = String(r.status ?? "");
  if (r.isPublicHoliday) return "Public holiday";
  if (!r.isWorkingDay) return "Rest day";
  if (st.startsWith("missing") || st === "no-shift") return "Rest day";
  return r.shiftName ? "Normal shift" : "Rest day";
};

function adaptRoster(rows: unknown[]): RosterDay[] {
  const today = new Date().toISOString().slice(0, 10);
  return rows.map((raw, i) => {
    const r = raw as Record<string, unknown>;
    const date = String(r.date ?? "");
    const shiftName = typeof r.shiftName === "string" && r.shiftName ? String(r.shiftName) : "Unscheduled";
    return {
      id: String(r.date ?? `roster-${i}`),
      date,
      dayLabel: new Date(`${date}T00:00:00Z`).toLocaleDateString("en-GB", {
        weekday: "short",
        day: "numeric",
        month: "short",
        timeZone: "UTC",
      }),
      kind: rosterDayKind(r) as RosterDay["kind"],
      shiftName,
      start: typeof r.shiftStart === "string" ? String(r.shiftStart) : null,
      end: typeof r.shiftEnd === "string" ? String(r.shiftEnd) : null,
      location: typeof r.calendarName === "string" ? String(r.calendarName) : "Lusaka HQ",
      isToday: date === today,
      changeable: r.isWorkingDay === true && !r.isPublicHoliday && typeof r.shiftName === "string" && !!r.shiftName,
      note: String(r.status ?? ""),
      leave: undefined,
    } satisfies RosterDay;
  });
}

const newReference = (prefix: string) => `${prefix}-2026-0${Math.floor(Math.random() * 900) + 100}`;

const kindIcon: Record<ShiftKind, typeof Clock> = {
  "Normal shift": Clock,
  "On-call": PhoneCall,
  "Rest day": Moon,
  "Public holiday": CalendarDays,
  "Company closure": Building2,
};

const kindTone: Record<ShiftKind, string> = {
  "Normal shift": "border-border bg-muted text-muted-foreground",
  "On-call": "border-info/30 bg-info-soft text-info",
  "Rest day": "border-border bg-muted text-muted-foreground",
  "Public holiday": "border-success/30 bg-success-soft text-success",
  "Company closure": "border-success/30 bg-success-soft text-success",
};

function KindBadge({ kind }: { kind: ShiftKind }) {
  const Icon = kindIcon[kind];
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium ${kindTone[kind]}`}
    >
      <Icon className="size-3.5 shrink-0" aria-hidden />
      {kind}
    </span>
  );
}

/* ------------------------------------------------- shift change request */

type ChangeKind = "swap" | "time-off" | "hours";

const changeLabels: Record<ChangeKind, string> = {
  swap: "Swap with a colleague",
  "time-off": "Request time off for this shift",
  hours: "Change the start or end time",
};

function ShiftChangeDialog({
  open,
  onOpenChange,
  days,
  candidates,
  initialShiftId,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
  days: RosterDay[];
  candidates: SwapCandidate[];
  initialShiftId?: string;
}) {
  const changeable = days.filter((d) => d.changeable);
  const [shiftId, setShiftId] = useState(initialShiftId ?? changeable[0]?.id ?? "");
  const [kind, setKind] = useState<ChangeKind>("swap");
  const [colleagueId, setColleagueId] = useState(
    candidates.find((c) => c.eligible)?.employeeId ?? "",
  );
  const [reason, setReason] = useState("");
  const [reference, setReference] = useState<string | null>(null);

  const shift = changeable.find((d) => d.id === shiftId) ?? changeable[0];
  const colleague = candidates.find((c) => c.employeeId === colleagueId);

  const close = () => {
    onOpenChange(false);
    // Reset only after the dialog is dismissed so the confirmation stays readable.
    window.setTimeout(() => setReference(null), 250);
  };

  return (
    <Dialog open={open} onOpenChange={(o) => (o ? onOpenChange(true) : close())}>
      <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-lg">
        {reference ? (
          <>
            <DialogHeader>
              <DialogTitle>Shift change requested</DialogTitle>
              <DialogDescription>
                Reference <span className="font-mono">{reference}</span> — this is a demonstration
                screen, so nothing has actually been sent.
              </DialogDescription>
            </DialogHeader>
            <ol className="space-y-3 text-sm">
              <li className="flex gap-2">
                <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-success" aria-hidden />
                <span>
                  Goes to <span className="font-medium">{MANAGER}</span> for a decision.
                </span>
              </li>
              <li className="flex gap-2">
                <Clock className="mt-0.5 size-4 shrink-0 text-info" aria-hidden />
                <span>
                  Decision due by{" "}
                  <span className="font-medium">{decisionDue(shift?.date ?? "")}</span>, which is
                  two days before the shift so cover can still be arranged.
                </span>
              </li>
              {kind === "swap" ? (
                <li className="flex gap-2">
                  <Info className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden />
                  <span>
                    {employeeName(colleagueId)} is asked to accept first. If they decline, the
                    request stops there and you keep the shift.
                  </span>
                </li>
              ) : null}
              <li className="flex gap-2">
                <AlertTriangle className="mt-0.5 size-4 shrink-0 text-warning" aria-hidden />
                <span>
                  Until it is approved the shift is still yours, and you are expected to clock in
                  for it.
                </span>
              </li>
            </ol>
            <DialogFooter>
              <Button variant="outline" onClick={close}>
                Close
              </Button>
            </DialogFooter>
          </>
        ) : (
          <>
            <DialogHeader>
              <DialogTitle>Request a shift change</DialogTitle>
              <DialogDescription>
                Rest days, closures and days you are already on leave for cannot be changed here.
              </DialogDescription>
            </DialogHeader>

            <div className="space-y-4">
              <div>
                <Label htmlFor="shift">Which shift</Label>
                <Select value={shiftId} onValueChange={setShiftId}>
                  <SelectTrigger id="shift" className="mt-1">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {changeable.map((d) => (
                      <SelectItem key={d.id} value={d.id}>
                        {d.dayLabel} · {d.shiftName} · {d.start}–{d.end}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {shift ? (
                  <p className="mt-1 text-xs text-muted-foreground">{shift.location}</p>
                ) : null}
              </div>

              <div>
                <Label htmlFor="kind">What you are asking for</Label>
                <Select value={kind} onValueChange={(v) => setKind(v as ChangeKind)}>
                  <SelectTrigger id="kind" className="mt-1">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {(Object.keys(changeLabels) as ChangeKind[]).map((k) => (
                      <SelectItem key={k} value={k}>
                        {changeLabels[k]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              {kind === "swap" ? (
                <div>
                  <Label htmlFor="colleague">Swap with</Label>
                  <Select value={colleagueId} onValueChange={setColleagueId}>
                    <SelectTrigger id="colleague" className="mt-1">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {candidates.map((c) => (
                        <SelectItem key={c.employeeId} value={c.employeeId} disabled={!c.eligible}>
                          {employeeName(c.employeeId)}
                          {c.eligible ? "" : " — not available"}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  {colleague ? (
                    <p className="mt-1 text-xs text-muted-foreground">{colleague.note}</p>
                  ) : null}
                </div>
              ) : null}

              {kind === "time-off" ? (
                <p className="flex gap-2 rounded-md border border-info/30 bg-info-soft p-3 text-xs text-info">
                  <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                  <span>
                    This raises a leave request for the shift and checks it against your balance and
                    the cover rules for {shift?.location ?? "your site"}.
                  </span>
                </p>
              ) : null}

              <div>
                <Label htmlFor="reason">Reason (helps the decision, not required)</Label>
                <Textarea
                  id="reason"
                  className="mt-1"
                  rows={3}
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                  placeholder="e.g. medical appointment in the morning"
                />
              </div>
            </div>

            <DialogFooter>
              <Button variant="outline" onClick={close}>
                Cancel
              </Button>
              <Button
                onClick={() => setReference(newReference("SC"))}
                disabled={!shift || (kind === "swap" && !colleague?.eligible)}
              >
                Send request
              </Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}

/* ------------------------------------------------------ open shift pickup */

function OpenShiftDialog({
  shift,
  onOpenChange,
}: {
  shift: OpenShift | null;
  onOpenChange: (o: boolean) => void;
}) {
  const [reference, setReference] = useState<string | null>(null);
  const close = () => {
    onOpenChange(false);
    window.setTimeout(() => setReference(null), 250);
  };

  return (
    <Dialog open={shift !== null} onOpenChange={(o) => (o ? onOpenChange(true) : close())}>
      <DialogContent className="sm:max-w-lg">
        {shift ? (
          <>
            <DialogHeader>
              <DialogTitle>{reference ? "Request sent" : `Request ${shift.shiftName}`}</DialogTitle>
              <DialogDescription>
                {shift.dayLabel}, {shift.start}–{shift.end} at {shift.location}.
              </DialogDescription>
            </DialogHeader>

            {reference ? (
              <ol className="space-y-3 text-sm">
                <li className="flex gap-2">
                  <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-success" aria-hidden />
                  <span>
                    Reference <span className="font-mono">{reference}</span>. Demonstration only —
                    nothing has actually been sent.
                  </span>
                </li>
                <li className="flex gap-2">
                  <Clock className="mt-0.5 size-4 shrink-0 text-info" aria-hidden />
                  <span>
                    {shift.owner} decides. Requests close {shift.closesOn}, and everyone who asked
                    is told either way.
                  </span>
                </li>
                <li className="flex gap-2">
                  <Info className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden />
                  <span>
                    {shift.requestsSoFar === 0
                      ? "You are the first to ask for this shift."
                      : `${shift.requestsSoFar} other request${shift.requestsSoFar === 1 ? "" : "s"} already in.`}
                  </span>
                </li>
              </ol>
            ) : (
              <dl className="space-y-3 text-sm">
                <div>
                  <dt className="text-xs text-muted-foreground">Why it is open</dt>
                  <dd>{shift.reason}</dd>
                </div>
                <div>
                  <dt className="text-xs text-muted-foreground">How it is paid</dt>
                  <dd>{shift.payNote}</dd>
                </div>
                <div>
                  <dt className="text-xs text-muted-foreground">Requests close</dt>
                  <dd>
                    {shift.closesOn} · decided by {shift.owner}
                  </dd>
                </div>
              </dl>
            )}

            <DialogFooter>
              <Button variant="outline" onClick={close}>
                {reference ? "Close" : "Cancel"}
              </Button>
              {reference ? null : (
                <Button onClick={() => setReference(newReference("OS"))}>Request this shift</Button>
              )}
            </DialogFooter>
          </>
        ) : null}
      </DialogContent>
    </Dialog>
  );
}

/* --------------------------------------------------------------- my plan */

function RosterList({
  days,
  onRequestChange,
}: {
  days: RosterDay[];
  onRequestChange: (id: string) => void;
}) {
  return (
    <ul className="divide-y rounded-lg border bg-surface">
      {days.map((d) => (
        <li
          key={d.id}
          className={`p-4 ${d.isToday ? "border-l-4 border-l-primary bg-primary-soft" : ""}`}
        >
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div className="min-w-0 flex-1">
              <p className="flex flex-wrap items-center gap-2">
                <span className="text-sm font-semibold">{d.dayLabel}</span>
                {d.isToday ? (
                  <span className="inline-flex items-center gap-1 rounded-full border border-primary bg-primary-soft px-2 py-0.5 text-xs font-medium text-primary">
                    <CheckCircle2 className="size-3" aria-hidden />
                    Today
                  </span>
                ) : null}
                <KindBadge kind={d.kind} />
                {d.leave ? (
                  <span className="inline-flex items-center gap-1.5 rounded-full border border-info/30 bg-info-soft px-2.5 py-0.5 text-xs font-medium text-info">
                    <Plane className="size-3.5 shrink-0" aria-hidden />
                    {d.leave.status} {d.leave.type.toLowerCase()}
                  </span>
                ) : null}
              </p>

              <p className="mt-1 text-sm">
                <span className="font-medium">{d.shiftName}</span>
                {d.start && d.end ? (
                  <>
                    {" · "}
                    <span className="tabular">
                      {d.start}–{d.end}
                    </span>
                  </>
                ) : null}
              </p>
              <p className="text-xs text-muted-foreground">{d.location}</p>

              {d.leave ? (
                <p className="mt-1 text-xs text-muted-foreground">
                  Leave <span className="font-mono">{d.leave.reference}</span> · covered by{" "}
                  {d.leave.coveredBy}
                </p>
              ) : null}
              {d.note ? <p className="mt-1 text-xs text-muted-foreground">{d.note}</p> : null}
            </div>

            <div className="shrink-0">
              {d.changeable ? (
                <Button variant="outline" size="sm" onClick={() => onRequestChange(d.id)}>
                  Request a change
                  <span className="sr-only"> to {d.dayLabel}</span>
                </Button>
              ) : (
                <span className="block max-w-40 text-xs text-muted-foreground sm:text-right">
                  {d.leave
                    ? "Already on approved leave"
                    : d.kind === "Rest day"
                      ? "Rest day — nothing to change"
                      : "Site closed — nothing to change"}
                </span>
              )}
            </div>
          </div>
        </li>
      ))}
    </ul>
  );
}

/* ------------------------------------------------------------- coverage */

function CoverageSection({ days }: { days: CoverageDay[] }) {
  return (
    <ul className="divide-y rounded-lg border bg-surface">
      {days.map((d) => {
        const short = d.assignments.length < d.required;
        return (
          <li key={d.id} className="p-4">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div className="min-w-0 flex-1">
                <p className="flex flex-wrap items-center gap-2">
                  <span className="text-sm font-semibold">{d.dayLabel}</span>
                  {short ? (
                    <span className="inline-flex items-center gap-1.5 rounded-full border border-danger/30 bg-danger-soft px-2.5 py-0.5 text-xs font-medium text-danger">
                      <AlertTriangle className="size-3.5 shrink-0" aria-hidden />
                      Understaffed — {d.assignments.length} of {d.required}
                    </span>
                  ) : (
                    <span className="inline-flex items-center gap-1.5 rounded-full border border-success/30 bg-success-soft px-2.5 py-0.5 text-xs font-medium text-success">
                      <CheckCircle2 className="size-3.5 shrink-0" aria-hidden />
                      Covered — {d.assignments.length} of {d.required}
                    </span>
                  )}
                </p>

                {d.assignments.length ? (
                  <ul className="mt-2 space-y-1 text-xs">
                    {d.assignments.map((a) => (
                      <li key={a.employeeId} className="min-w-0">
                        <span className="font-medium">{employeeName(a.employeeId)}</span>
                        <span className="text-muted-foreground"> — {a.shift}</span>
                        <span className="block text-muted-foreground">
                          {employeeMeta(a.employeeId)}
                        </span>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="mt-2 text-xs text-muted-foreground">Nobody is scheduled.</p>
                )}

                {d.gapNote ? (
                  <p className="mt-2 flex gap-2 rounded-md border border-warning/40 bg-warning-soft p-2 text-xs text-warning">
                    <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                    <span>
                      {d.gapNote}
                      {d.openShiftId ? (
                        <>
                          {" "}
                          Open shift <span className="font-mono">{d.openShiftId}</span> has been
                          published — owner {MANAGER}.
                        </>
                      ) : null}
                    </span>
                  </p>
                ) : null}
              </div>
            </div>
          </li>
        );
      })}
    </ul>
  );
}

interface ShiftCatalogRow {
  id: string;
  code: string;
  name: string;
  start: string;
  end: string;
  active: boolean;
}

interface WorkerRef {
  id: string;
  name: string;
  employeeNo: string;
}

function CreateShiftDialog({
  open,
  onOpenChange,
  onSaved,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: () => void;
}) {
  const [saving, setSaving] = useState(false);
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [start, setStart] = useState("08:00");
  const [end, setEnd] = useState("17:00");

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Create shift</DialogTitle>
          <DialogDescription>
            A shift is a named block of working time. Give it a code and hours; assign it to a
            worker afterwards.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-4 py-2">
          <div className="grid gap-2">
            <Label htmlFor="shift-code">Code</Label>
            <Input id="shift-code" placeholder="e.g. DAY-1" value={code} onChange={(e) => setCode(e.target.value)} />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="shift-name">Name</Label>
            <Input id="shift-name" placeholder="e.g. Day shift — Lusaka" value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div className="grid gap-2">
              <Label htmlFor="shift-start">Start time</Label>
              <Input id="shift-start" type="time" value={start} onChange={(e) => setStart(e.target.value)} />
            </div>
            <div className="grid gap-2">
              <Label htmlFor="shift-end">End time</Label>
              <Input id="shift-end" type="time" value={end} onChange={(e) => setEnd(e.target.value)} />
            </div>
          </div>
        </div>
        <DialogFooter>
          <Button variant="ghost" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            onClick={async () => {
              if (!code.trim() || !name.trim()) {
                feedback.blocked("Code and name are required", "Every shift needs both a code and a name.");
                return;
              }
              setSaving(true);
              try {
                await realApi.createShift({
                  code: code.trim(),
                  name: name.trim(),
                  startTime: start,
                  endTime: end,
                });
                feedback.submitted(
                  "Shift created",
                  `${name.trim()} (${code.trim()}) is now in the catalogue. Assign it to a worker next.`,
                );
                setCode("");
                setName("");
                onSaved();
                onOpenChange(false);
              } catch (err) {
                feedback.blocked(
                  "Could not create the shift",
                  err instanceof Error ? err.message : "Unknown error",
                );
              } finally {
                setSaving(false);
              }
            }}
            disabled={saving}
          >
            {saving ? "Saving…" : "Create shift"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function AssignShiftDialog({
  open,
  onOpenChange,
  shifts,
  workers,
  initialShiftId,
  onSaved,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  shifts: ShiftCatalogRow[];
  workers: WorkerRef[];
  initialShiftId?: string;
  onSaved: () => void;
}) {
  const [saving, setSaving] = useState(false);
  const [shiftId, setShiftId] = useState(initialShiftId ?? "");
  const [workerId, setWorkerId] = useState("");
  const [from, setFrom] = useState(new Date().toISOString().slice(0, 10));
  const [to, setTo] = useState("");

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Assign shift to worker</DialogTitle>
          <DialogDescription>
            Put a named shift on a worker's schedule. The assignment starts on the effective date
            and (optionally) runs until an end date.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-4 py-2">
          <div className="grid gap-2">
            <Label>Worker</Label>
            <Select value={workerId || undefined} onValueChange={setWorkerId}>
              <SelectTrigger>
                <SelectValue placeholder="Pick a worker" />
              </SelectTrigger>
              <SelectContent>
                {workers.map((w) => (
                  <SelectItem key={w.id} value={w.id}>
                    {w.employeeNo ? `${w.employeeNo} — ` : ""}
                    {w.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Shift</Label>
            <Select value={shiftId || undefined} onValueChange={setShiftId}>
              <SelectTrigger>
                <SelectValue placeholder="Pick a shift" />
              </SelectTrigger>
              <SelectContent>
                {shifts.map((s) => (
                  <SelectItem key={s.id} value={s.id}>
                    {s.name} ({s.code}) · {s.start}–{s.end}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div className="grid gap-2">
              <Label htmlFor="assign-from">Effective from</Label>
              <Input id="assign-from" type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
            </div>
            <div className="grid gap-2">
              <Label htmlFor="assign-to">Effective to (optional)</Label>
              <Input id="assign-to" type="date" value={to} onChange={(e) => setTo(e.target.value)} />
            </div>
          </div>
        </div>
        <DialogFooter>
          <Button variant="ghost" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            onClick={async () => {
              if (!workerId || !shiftId) {
                feedback.blocked("Pick a worker and a shift", "Both are required before the assignment can be saved.");
                return;
              }
              setSaving(true);
              try {
                await realApi.assignShift(workerId, {
                  shiftId,
                  effectiveFrom: from,
                  effectiveTo: to || null,
                });
                const shift = shifts.find((s) => s.id === shiftId);
                const worker = workers.find((w) => w.id === workerId);
                feedback.submitted(
                  "Shift assigned",
                  `${worker?.name ?? "The worker"} now carries ${shift?.name ?? "the shift"} from ${from}.`,
                );
                setWorkerId("");
                setShiftId("");
                setTo("");
                onSaved();
                onOpenChange(false);
              } catch (err) {
                feedback.blocked(
                  "Could not assign the shift",
                  err instanceof Error ? err.message : "Unknown error",
                );
              } finally {
                setSaving(false);
              }
            }}
            disabled={saving}
          >
            {saving ? "Saving…" : "Assign shift"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/* ------------------------------------------------------------------ page */

function SchedulePage() {
  const mockSchedule = useMemo(() => timeclockApi.schedule(), []);
  const schedule = useApi(
    async () => (USE_REAL ? adaptRoster((await realApi.roster(SCHEDULE_WORKER)) as unknown[]) : await mockSchedule),
    [],
  );
  const candidates = useMock(() => timeclockApi.swapCandidates());
  const coverage = useMock(() => timeclockApi.coverage());
  const open = useMock(() => timeclockApi.openShifts());

  const [changeOpen, setChangeOpen] = useState(false);
  const [changeShiftId, setChangeShiftId] = useState<string | undefined>(undefined);
  const [pickup, setPickup] = useState<OpenShift | null>(null);
  const [createShiftOpen, setCreateShiftOpen] = useState(false);
  const [assignOpen, setAssignOpen] = useState(false);
  const [assignShiftId, setAssignShiftId] = useState<string | undefined>(undefined);
  const [tick, setTick] = useState(0);
  const shiftsState = useApi(
    async () => {
      const [shifts, workers] = await Promise.all([realApi.shifts(), realApi.employees({ page: 1, pageSize: 200 })]);
      return {
        shifts: ((shifts ?? []) as Record<string, unknown>[]).map((s) => ({
          id: String(s.id ?? ""),
          code: String(s.code ?? ""),
          name: String(s.name ?? ""),
          start: String(s.startTime ?? ""),
          end: String(s.endTime ?? ""),
          active: Boolean(s.isActive ?? true),
        })),
        workers: (((workers as { items?: Record<string, unknown>[] } | undefined)?.items ?? []) as Record<string, unknown>[]).map((w) => ({
          id: String(w.id ?? ""),
          name: String(w.name ?? String(w.fullName ?? "")),
          employeeNo: String(w.employeeNo ?? ""),
        })),
      };
    },
    [tick],
  );

  const openChange = (id?: string) => {
    setChangeShiftId(id);
    setChangeOpen(true);
  };

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Time operations"
        title="My schedule"
        description="The next 14 days as they stand today. Anything you want changed goes to your manager — the shift stays yours until they decide."
        primaryAction={
          <div className="flex flex-wrap items-center gap-2">
            <Button onClick={() => openChange(undefined)}>Request a shift change</Button>
            {USE_REAL ? (
              <>
                <Button variant="outline" onClick={() => setAssignOpen(true)}>Assign shift</Button>
                <Button variant="outline" onClick={() => setCreateShiftOpen(true)}>Create shift</Button>
              </>
            ) : null}
          </div>
        }
        meta={
          <>
            <span className="inline-flex items-center gap-1.5 rounded-full border bg-muted px-2.5 py-0.5 text-xs font-medium text-muted-foreground">
              <CalendarDays className="size-3.5" aria-hidden />
              29 July – 11 August 2026 · Lusaka HQ
            </span>
            <Link
              to="/hrm/attendance/clock"
              className="text-xs font-medium text-primary underline underline-offset-2"
            >
              Clock in and out
            </Link>
          </>
        }
      />

      <section aria-label="My schedule for the next 14 days" className="space-y-3">
        <div>
          <h2 className="text-sm font-semibold">My schedule</h2>
          <p className="mt-1 max-w-2xl text-xs text-muted-foreground">{holidayNote}</p>
        </div>
        <Async state={schedule} rows={6}>
          {(days) => <RosterList days={days} onRequestChange={openChange} />}
        </Async>
      </section>

      <section aria-label="Open shifts" className="space-y-3 pt-2">
        <div>
          <h2 className="text-sm font-semibold">Open shifts you can pick up</h2>
          <p className="mt-1 max-w-2xl text-xs text-muted-foreground">
            Published because nobody is rostered. Asking for one does not commit you — the shift
            owner still decides, and everyone who asked is told either way.
          </p>
        </div>
        <Async state={open} rows={3}>
          {(shifts) => (
            <ul className="grid gap-3 sm:grid-cols-2">
              {shifts.map((s) => (
                <li key={s.id} className="flex flex-col rounded-lg border bg-surface p-4">
                  <p className="flex flex-wrap items-center gap-2">
                    <span className="font-mono text-xs text-muted-foreground">{s.id}</span>
                    <span className="inline-flex items-center gap-1.5 rounded-full border border-warning/40 bg-warning-soft px-2.5 py-0.5 text-xs font-medium text-warning">
                      <AlertTriangle className="size-3.5 shrink-0" aria-hidden />
                      Unfilled
                    </span>
                  </p>
                  <p className="mt-1 text-sm font-semibold">{s.shiftName}</p>
                  <p className="text-sm">
                    {s.dayLabel} ·{" "}
                    <span className="tabular">
                      {s.start}–{s.end}
                    </span>
                  </p>
                  <p className="text-xs text-muted-foreground">{s.location}</p>
                  <p className="mt-2 text-xs text-muted-foreground">{s.reason}</p>
                  <p className="mt-1 text-xs text-muted-foreground">{s.payNote}</p>
                  <p className="mt-1 text-xs text-muted-foreground">
                    Requests close {s.closesOn} · owner {s.owner} ·{" "}
                    {s.requestsSoFar === 0
                      ? "no requests yet"
                      : `${s.requestsSoFar} request${s.requestsSoFar === 1 ? "" : "s"} so far`}
                  </p>
                  <div className="mt-3 pt-1">
                    <Button variant="outline" size="sm" onClick={() => setPickup(s)}>
                      Request this shift
                      <span className="sr-only">
                        {" "}
                        on {s.dayLabel} at {s.location}
                      </span>
                    </Button>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </Async>
      </section>

      <section aria-label="Team coverage" className="space-y-3 pt-2">
        <div>
          <h2 className="flex items-center gap-2 text-sm font-semibold">
            <Users className="size-4 shrink-0" aria-hidden />
            Team coverage — Lusaka HQ planning cover group
          </h2>
          <p className="mt-1 flex max-w-2xl gap-2 rounded-md border border-info/30 bg-info-soft p-2 text-xs text-info">
            <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            <span>
              Manager-facing section. Employees without a manager or HR role do not see who else is
              scheduled.
            </span>
          </p>
        </div>
        <Async state={coverage} rows={4}>
          {(days) => <CoverageSection days={days} />}
        </Async>
      </section>

      {changeOpen && candidates.data && schedule.data ? (
        <ShiftChangeDialog
          key={changeShiftId ?? "any"}
          open={changeOpen}
          onOpenChange={setChangeOpen}
          days={schedule.data}
          candidates={candidates.data}
          initialShiftId={changeShiftId}
        />
      ) : null}

      <OpenShiftDialog
        shift={pickup}
        onOpenChange={(o) => {
          if (!o) setPickup(null);
        }}
      />

      {USE_REAL ? (
        <>
          <section aria-label="Shift definitions" className="space-y-3 pt-6">
            <div>
              <h2 className="text-sm font-semibold">Shift definitions</h2>
              <p className="mt-1 max-w-2xl text-xs text-muted-foreground">
                The shift catalogue: every named shift with its start and end time. Create a shift
                here, then assign it to a worker to put it on their schedule.
              </p>
            </div>
            <Async state={shiftsState} rows={3}>
              {({ shifts, workers }: { shifts: ShiftCatalogRow[]; workers: WorkerRef[] }) => (
                <>
                  <ul className="divide-y rounded-lg border bg-surface">
                    {shifts.map((s) => (
                      <li key={s.id} className="flex flex-wrap items-center gap-4 p-4">
                        <div className="min-w-0 flex-1">
                          <div className="flex flex-wrap items-center gap-2">
                            <span className="font-medium">{s.name}</span>
                            <span className="font-mono text-xs text-muted-foreground">{s.code}</span>
                            <span className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium ${s.active ? "border-success/40 bg-success-soft text-success" : "border-muted bg-muted text-muted-foreground"}`}>{s.active ? "Active" : "Inactive"}</span>
                          </div>
                          <p className="text-sm">
                            <span className="tabular">
                              {s.start}–{s.end}
                            </span>
                          </p>
                        </div>
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => {
                            setAssignShiftId(s.id);
                            setAssignOpen(true);
                            void workers;
                          }}
                        >
                          Assign to worker
                        </Button>
                      </li>
                    ))}
                    {shifts.length === 0 ? (
                      <li className="p-8 text-center text-sm text-muted-foreground">
                        No shifts defined yet. Create the first one above.
                      </li>
                    ) : null}
                  </ul>
                  <CreateShiftDialog
                    open={createShiftOpen}
                    onOpenChange={setCreateShiftOpen}
                    onSaved={() => setTick((t) => t + 1)}
                  />
                  <AssignShiftDialog
                    open={assignOpen}
                    onOpenChange={(o) => {
                      if (!o) {
                        setAssignOpen(false);
                        setAssignShiftId(undefined);
                      }
                    }}
                    shifts={shifts}
                    workers={workers}
                    initialShiftId={assignShiftId}
                    onSaved={() => setTick((t) => t + 1)}
                  />
                </>
              )}
            </Async>
          </section>
        </>
      ) : null}
    </AppShell>
      </AuthGate>
  );
}
