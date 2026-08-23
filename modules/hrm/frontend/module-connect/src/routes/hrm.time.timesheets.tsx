import { createFileRoute } from "@tanstack/react-router";
import { addDays, format, parseISO } from "date-fns";
import { ChevronDown, ChevronLeft, ChevronRight, Clock3, Download, Edit3, FileClock, Filter, LockKeyhole, MoreHorizontal, Plus, Search, TimerReset, UserRound } from "lucide-react";
import { useMemo, useState } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Badge } from "@/components/ui/badge";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { useMock } from "@/platform/use-mock";
import { expensesApi } from "@/mock/expenses";
import type { Timesheet } from "@/mock/expenses";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/time/timesheets")({
  head: () => ({
    meta: [
      { title: "Timesheets — New World Cargo HRM" },
      { name: "description", content: "Record and review working time by day and payroll period." },
    ],
  }),
  component: TimesheetsPage,
});

const DAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"] as const;
type Entry = { id: string; project: string; detail: string; total: number; overtime: number; status: string };
type DayGroup = { date: string; label: string; entries: Entry[]; total: number; overtime: number };

function TimesheetsPage() {
  const state = useMock(() => expensesApi.timesheets());
  const [weekOffset, setWeekOffset] = useState(0);
  const [view, setView] = useState<"weekly" | "daily">("weekly");
  const [query, setQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState<"all" | "attention" | "recorded">("all");
  const [addOpen, setAddOpen] = useState(false);
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});
  const [draftProject, setDraftProject] = useState("");
  const [draftHours, setDraftHours] = useState("8");
  const [draftDate, setDraftDate] = useState("");

  const source = (state.data ?? []) as Timesheet[];
  const timesheet = source[0];
  const weekStart = useMemo(() => {
    const base = timesheet?.weekStarting ? parseISO(timesheet.weekStarting) : new Date();
    return addDays(base, weekOffset * 7);
  }, [timesheet?.weekStarting, weekOffset]);
  const weekDates = useMemo(() => DAYS.map((_, index) => addDays(weekStart, index)), [weekStart]);
  const weekLabel = `${format(weekStart, "d MMMM yyyy")} – ${format(addDays(weekStart, 6), "d MMMM yyyy")}`;

  const allDayGroups = useMemo<DayGroup[]>(() => {
    if (!timesheet) return [];
    return DAYS.map((day, index) => {
      const entries = timesheet.rows
        .map((row) => {
          const total = Number(row.hours[index] ?? 0) + Number(row.overtime[index] ?? 0);
          return { id: `${row.id}-${day}`, project: row.project, detail: `${row.costCentre} · ${row.billable ? "Billable" : "Non-billable"}`, total, overtime: Number(row.overtime[index] ?? 0), status: row.overtime[index] ? "Needs overtime review" : "Recorded" };
        })
        .filter((entry) => entry.total > 0);
      return { date: format(weekDates[index], "yyyy-MM-dd"), label: `${day}, ${format(weekDates[index], "d MMM yyyy")}`, entries, total: entries.reduce((sum, entry) => sum + entry.total, 0), overtime: entries.reduce((sum, entry) => sum + entry.overtime, 0) };
    });
  }, [timesheet, weekDates]);

  const dayGroups = useMemo<DayGroup[]>(() => {
    const needle = query.trim().toLowerCase();
    return allDayGroups.map((day) => ({
      ...day,
      entries: day.entries.filter((entry) => {
        const matchesQuery = !needle || `${entry.project} ${entry.detail}`.toLowerCase().includes(needle);
        const matchesStatus = statusFilter === "all" || (statusFilter === "attention" && entry.status !== "Recorded") || (statusFilter === "recorded" && entry.status === "Recorded");
        return matchesQuery && matchesStatus;
      }),
    })).map((day) => ({ ...day, total: day.entries.reduce((sum, entry) => sum + entry.total, 0), overtime: day.entries.reduce((sum, entry) => sum + entry.overtime, 0) }));
  }, [allDayGroups, query, statusFilter]);

  const totals = useMemo(() => ({
    total: allDayGroups.reduce((sum, day) => sum + day.total, 0),
    overtime: allDayGroups.reduce((sum, day) => sum + day.overtime, 0),
    attention: allDayGroups.reduce((sum, day) => sum + day.entries.filter((entry) => entry.status !== "Recorded").length, 0),
  }), [allDayGroups]);

  function openAddTimer() {
    setDraftDate(format(new Date(), "yyyy-MM-dd"));
    setAddOpen(true);
  }

  function submitDraft(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setAddOpen(false);
    feedback.note("Draft time entry prepared", "It will be saved to the live timesheet service when that integration is enabled.");
  }

  return <AuthGate><AppShell><PageHeader eyebrow="Time and leave / work records" title="Timesheets" description="See your working time by day, keep the week complete, and resolve anything that needs attention." meta={<Badge variant="outline" className="gap-1.5 border-info/30 bg-info-soft text-info-foreground"><LockKeyhole className="size-3" aria-hidden /> Frontend workflow preview</Badge>} primaryAction={<Button onClick={openAddTimer} className="gap-2"><Plus className="size-4" aria-hidden />Add time entry</Button>} />
    <div className="space-y-5" data-testid="timesheets-page">
      <div className="flex flex-col gap-3 border-b pb-4 sm:flex-row sm:items-center sm:justify-between"><div className="flex items-center gap-2"><Button variant="outline" size="icon" aria-label="Previous week" onClick={() => setWeekOffset((value) => value - 1)}><ChevronLeft className="size-4" aria-hidden /></Button><Button variant="outline" size="icon" aria-label="Next week" onClick={() => setWeekOffset((value) => value + 1)}><ChevronRight className="size-4" aria-hidden /></Button><Button variant="ghost" size="sm" onClick={() => setWeekOffset(0)}>This week</Button><h2 className="ml-1 text-xl font-semibold">{weekLabel}</h2><Badge variant="outline" className="hidden sm:inline-flex">{timesheet ? "Draft timesheet" : "Live connection pending"}</Badge></div><div className="flex items-center gap-2"><Select value={view} onValueChange={(value) => setView(value as "weekly" | "daily")}><SelectTrigger className="w-28" aria-label="Timesheet view"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="weekly">Weekly</SelectItem><SelectItem value="daily">Daily</SelectItem></SelectContent></Select><Button variant="outline" className="gap-2" onClick={() => feedback.note("Download prepared", "The timesheet export will use the selected period and filters.")}><Download className="size-4" aria-hidden />Download</Button></div></div>

      {addOpen ? <Card className="border-primary/30 bg-primary-soft/20 shadow-none"><CardContent className="p-4"><form onSubmit={submitDraft} className="grid gap-3 md:grid-cols-[1fr_180px_180px_auto] md:items-end"><div><Label htmlFor="draft-project">Work item</Label><Input id="draft-project" value={draftProject} onChange={(event) => setDraftProject(event.target.value)} placeholder="Project or work item" required /></div><div><Label htmlFor="draft-date">Date</Label><Input id="draft-date" type="date" value={draftDate} onChange={(event) => setDraftDate(event.target.value)} required /></div><div><Label htmlFor="draft-hours">Hours</Label><Input id="draft-hours" type="number" min="0.25" step="0.25" value={draftHours} onChange={(event) => setDraftHours(event.target.value)} required /></div><div className="flex gap-2"><Button type="submit">Add draft</Button><Button type="button" variant="ghost" onClick={() => setAddOpen(false)}>Cancel</Button></div></form><p className="mt-3 text-xs text-muted-foreground">This frontend-first draft interaction is intentionally not connected to an API yet; no production record is written.</p></CardContent></Card> : null}

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4" aria-label="Timesheet summary"><Summary label="Total hours" value={`${totals.total.toFixed(2)} h`} detail="This selected week" icon={Clock3} tone="primary" /><Summary label="Regular hours" value={`${(totals.total - totals.overtime).toFixed(2)} h`} detail="Within scheduled time" icon={UserRound} tone="neutral" /><Summary label="Overtime" value={`${totals.overtime.toFixed(2)} h`} detail="Needs separate review" icon={TimerReset} tone="warning" /><Summary label="Needs attention" value={String(totals.attention)} detail="Entries to check" icon={FileClock} tone="danger" /></section>
      {!state.loading && !timesheet ? <Card className="shadow-none"><CardContent className="flex flex-col items-center justify-center px-6 py-16 text-center"><span className="flex size-12 items-center justify-center rounded-2xl bg-primary-soft text-primary-foreground"><FileClock className="size-5" aria-hidden /></span><h3 className="mt-4 text-base font-semibold">Timesheets are ready for live connection</h3><p className="mt-1 max-w-lg text-sm leading-6 text-muted-foreground">The weekly workspace is ready. No rows are shown until the live timesheet service is connected, so this page will not invent hours or projects.</p><Button variant="outline" className="mt-5 gap-2" onClick={() => feedback.note("Timesheet service pending", "Connect the real attendance/timesheet API before enabling production entry persistence.")}><TimerReset className="size-4" aria-hidden />View connection status</Button></CardContent></Card> : null}
      <div className="flex flex-col gap-3 border-b pb-4 sm:flex-row sm:items-end sm:justify-between"><div><div className="flex items-center gap-2"><h2 className="text-lg font-semibold">Your week</h2><Badge variant="outline" className="sm:hidden">{timesheet ? "Draft" : "Not connected"}</Badge></div><p className="text-sm text-muted-foreground">Entries are grouped by day so missing or unusual time is easy to spot.</p></div><div className="flex flex-col gap-2 sm:flex-row"><div className="relative"><Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden /><Input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Search work items" className="w-full pl-9 sm:w-64" aria-label="Search work items" /></div><Select value={statusFilter} onValueChange={(value) => setStatusFilter(value as "all" | "attention" | "recorded")}><SelectTrigger className="w-full gap-2 sm:w-44" aria-label="Filter timesheet entries"><Filter className="size-4" aria-hidden /><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">All entries</SelectItem><SelectItem value="attention">Needs attention</SelectItem><SelectItem value="recorded">Recorded</SelectItem></SelectContent></Select></div></div><div className="space-y-3">{timesheet ? (view === "daily" ? dayGroups.filter((day) => day.date === format(new Date(), "yyyy-MM-dd")) : dayGroups).map((day) => <DaySection key={day.date} day={day} open={expanded[day.date] ?? true} onToggle={() => setExpanded((current) => ({ ...current, [day.date]: !(current[day.date] ?? true) }))} />) : null}</div>
    </div></AppShell></AuthGate>;
}

function Summary({ label, value, detail, icon: Icon, tone }: { label: string; value: string; detail: string; icon: typeof Clock3; tone: "primary" | "neutral" | "warning" | "danger" }) { const classes = { primary: "bg-primary text-primary-foreground", neutral: "bg-secondary text-secondary-foreground", warning: "bg-warning-soft text-warning-foreground", danger: "bg-danger-soft text-danger" }; return <Card className="shadow-none"><CardContent className="flex items-start justify-between gap-3 p-4"><div><p className="text-sm font-medium text-muted-foreground">{label}</p><p className="mt-1 text-2xl font-semibold tabular">{value}</p><p className="mt-1 text-xs text-muted-foreground">{detail}</p></div><span className={`flex size-10 items-center justify-center rounded-xl ${classes[tone]}`}><Icon className="size-5" aria-hidden /></span></CardContent></Card>; }

function DaySection({ day, open, onToggle }: { day: DayGroup; open: boolean; onToggle: () => void }) { return <section className="overflow-hidden rounded-xl border bg-card shadow-sm"><button type="button" onClick={onToggle} className="flex w-full items-center justify-between gap-4 border-b bg-surface-muted/60 px-4 py-3 text-left hover:bg-surface-muted sm:px-5"><div><p className="font-semibold">{day.label}</p><p className="mt-0.5 text-xs text-muted-foreground">{day.entries.length} {day.entries.length === 1 ? "entry" : "entries"}</p></div><div className="flex items-center gap-4"><span className="text-sm font-medium tabular">{day.total.toFixed(2)} h{day.overtime ? <span className="ml-2 text-xs text-warning-foreground">· {day.overtime.toFixed(2)} OT</span> : null}</span><ChevronDown className={`size-4 text-muted-foreground transition-transform ${open ? "rotate-180" : ""}`} aria-hidden /></div></button>{open ? <div className="divide-y">{day.entries.length ? day.entries.map((entry, index) => <div key={entry.id} className="flex flex-col gap-3 px-4 py-4 sm:flex-row sm:items-center sm:px-5"><div className="flex min-w-0 flex-1 items-start gap-3"><span className="mt-0.5 flex size-8 shrink-0 items-center justify-center rounded-lg bg-secondary text-secondary-foreground"><FileClock className="size-4" aria-hidden /></span><div className="min-w-0"><p className="truncate font-semibold">{entry.project}</p><p className="mt-1 truncate text-xs text-muted-foreground">{entry.detail}</p></div></div><div className="grid grid-cols-3 gap-4 text-sm sm:flex sm:items-center sm:gap-7"><div><p className="text-[11px] uppercase tracking-wide text-muted-foreground">Source</p><p className="mt-1">Attendance</p></div><div><p className="text-[11px] uppercase tracking-wide text-muted-foreground">Hours</p><p className="mt-1 font-medium tabular">{entry.total.toFixed(2)} h</p></div><div><p className="text-[11px] uppercase tracking-wide text-muted-foreground">Status</p><p className={`mt-1 text-xs font-medium ${entry.overtime ? "text-warning-foreground" : "text-success-foreground"}`}>{entry.status}</p></div><Button variant="outline" size="sm" className="gap-2 sm:min-w-20" onClick={() => feedback.note(`Entry ${index + 1} selected`, "Open the detailed time entry when the live timesheet service is connected.")}><Edit3 className="size-3.5" aria-hidden /><span className="hidden sm:inline">Edit</span></Button><Button variant="ghost" size="icon" aria-label={`More actions for ${entry.project}`} onClick={() => feedback.note("More actions", "Correction and audit actions will be connected to this row.")}><MoreHorizontal className="size-4" aria-hidden /></Button></div></div>) : <div className="p-6 text-center text-sm text-muted-foreground">No time recorded for this day.</div>}</div> : null}</section>; }
