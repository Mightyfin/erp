import { createFileRoute } from "@tanstack/react-router";
import {
  ArrowRight,
  CalendarDays,
  CheckCircle2,
  ChevronDown,
  Clock3,
  FileClock,
  Info,
  ListFilter,
  LockKeyhole,
  RefreshCw,
  Search,
  Settings2,
  TimerReset,
  Upload,
  UserRound,
  WalletCards,
  XCircle,
} from "lucide-react";
import { useMemo, useRef, useState } from "react";
import type { LucideIcon } from "lucide-react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/time/operations")({ component: TimeOperations });

type Result = Record<string, unknown>;
type QueueTab = "needs-review" | "approved" | "rejected" | "paid" | "all";
type OvertimeStatus = "pending" | "approved" | "rejected" | "paid" | "none";

const STATUS_COPY: Record<OvertimeStatus, string> = {
  pending: "Needs review",
  approved: "Approved for payroll",
  rejected: "Rejected",
  paid: "Paid in payroll",
  none: "Not applicable",
};

const STATUS_CLASS: Record<OvertimeStatus, string> = {
  pending: "border-warning/40 bg-warning-soft text-warning-foreground",
  approved: "border-info/30 bg-info-soft text-info-foreground",
  rejected: "border-danger/30 bg-danger-soft text-danger",
  paid: "border-success/30 bg-success-soft text-success-foreground",
  none: "border-border bg-muted text-muted-foreground",
};

function displayStatus(value: unknown): OvertimeStatus {
  const status = String(value ?? "none").toLowerCase() as OvertimeStatus;
  return status in STATUS_COPY ? status : "none";
}

function hours(value: unknown) {
  return Number(value ?? 0).toFixed(2);
}

function ToolCard({
  icon: Icon,
  title,
  description,
  children,
}: {
  icon: LucideIcon;
  title: string;
  description: string;
  children: React.ReactNode;
}) {
  return (
    <Card className="border-border/80 shadow-none">
      <CardHeader className="pb-4">
        <div className="flex items-start gap-3">
          <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-primary-soft text-primary-foreground">
            <Icon className="size-4" aria-hidden />
          </span>
          <div>
            <CardTitle className="text-base">{title}</CardTitle>
            <CardDescription className="mt-1 text-sm leading-5">{description}</CardDescription>
          </div>
        </div>
      </CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  );
}

function WorkflowStep({
  number,
  title,
  description,
  active,
  complete,
}: {
  number: number;
  title: string;
  description: string;
  active: boolean;
  complete: boolean;
}) {
  return (
    <div
      className={`flex min-w-0 items-start gap-3 rounded-xl border p-3 transition-colors ${
        active ? "border-primary bg-primary-soft/60" : "border-border bg-card"
      }`}
    >
      <span
        className={`flex size-8 shrink-0 items-center justify-center rounded-full text-sm font-semibold ${
          complete ? "bg-success text-success-foreground" : active ? "bg-primary text-primary-foreground" : "bg-muted text-muted-foreground"
        }`}
      >
        {complete ? <CheckCircle2 className="size-4" aria-hidden /> : number}
      </span>
      <div className="min-w-0">
        <p className="text-sm font-semibold text-foreground">{title}</p>
        <p className="mt-0.5 text-xs leading-4 text-muted-foreground">{description}</p>
      </div>
    </div>
  );
}

function TimeOperations() {
  const history = useApi(realApi.timeOperationsHistory, []);
  const overtime = useApi(() => realApi.overtime({}), []);
  const leaveTypes = useApi(() =>
    realApi.leaveTypes ? realApi.leaveTypes({ includeInactive: false }) : Promise.resolve([] as unknown[]),
  );
  const encashments = useApi(() => realApi.encashments({ pageSize: 25 }), []);
  const importSectionRef = useRef<HTMLDetailsElement>(null);
  const [queueTab, setQueueTab] = useState<QueueTab>("needs-review");
  const [toolsOpen, setToolsOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [reviewReasons, setReviewReasons] = useState<Record<string, string>>({});
  const [optimisticDecisions, setOptimisticDecisions] = useState<Record<string, OvertimeStatus>>({});
  const [rows, setRows] = useState("EMP-0001,2026-08-15,08:00,17:30");
  const [period, setPeriod] = useState(new Date().toISOString().slice(0, 7));
  const [workerId, setWorkerId] = useState("");
  const [leaveType, setLeaveType] = useState("ANNUAL");
  const [days, setDays] = useState("1");
  const [reason, setReason] = useState("");
  const [shiftCode, setShiftCode] = useState("DAY");
  const [shiftName, setShiftName] = useState("Day shift");
  const [shiftStart, setShiftStart] = useState("08:00");
  const [shiftEnd, setShiftEnd] = useState("17:00");
  const [assignmentWorker, setAssignmentWorker] = useState("");
  const [assignmentShift, setAssignmentShift] = useState("");
  const [effectiveFrom, setEffectiveFrom] = useState(new Date().toISOString().slice(0, 10));
  const [result, setResult] = useState<Result | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [encWorker, setEncWorker] = useState("");
  const [encLeaveType, setEncLeaveType] = useState("ANNUAL");
  const [encDays, setEncDays] = useState("1");
  const [encNote, setEncNote] = useState("");
  const [encQuote, setEncQuote] = useState<Record<string, unknown> | null>(null);
  const [encBusy, setEncBusy] = useState(false);

  const encashmentItems = Array.isArray(encashments.data)
    ? encashments.data
    : encashments.data?.items ?? [];
  const leaveTypeItems = (leaveTypes.data ?? []) as Array<{ id?: unknown; code?: unknown; name?: unknown }>;
  const overtimeRows = (overtime.data ?? []) as Array<Record<string, unknown>>;
  const statusFor = (item: Record<string, unknown>) =>
    optimisticDecisions[String(item.id)] ?? displayStatus(item.overtimeStatus);

  const queueSummary = useMemo(() => {
    const summary = { pending: 0, approved: 0, rejected: 0, paid: 0, pendingHours: 0, approvedHours: 0, paidHours: 0 };
    for (const item of overtimeRows) {
      const status = optimisticDecisions[String(item.id)] ?? displayStatus(item.overtimeStatus);
      const overtimeHours = Number(item.overtimeHours ?? 0);
      if (status === "pending") {
        summary.pending += 1;
        summary.pendingHours += overtimeHours;
      }
      if (status === "approved") {
        summary.approved += 1;
        summary.approvedHours += overtimeHours;
      }
      if (status === "rejected") summary.rejected += 1;
      if (status === "paid") {
        summary.paid += 1;
        summary.paidHours += overtimeHours;
      }
    }
    return summary;
  }, [optimisticDecisions, overtimeRows]);

  const visibleRows = useMemo(() => {
    const normalized = search.trim().toLowerCase();
    return overtimeRows.filter((item) => {
      const status = optimisticDecisions[String(item.id)] ?? displayStatus(item.overtimeStatus);
      const matchesTab =
        queueTab === "all" ||
        (queueTab === "needs-review" && status === "pending") ||
        (queueTab === status);
      const haystack = [item.workerName, item.workerId, item.workDate, item.source, item.overtimeDecisionReason]
        .map((value) => String(value ?? "").toLowerCase())
        .join(" ");
      return matchesTab && (!normalized || haystack.includes(normalized));
    });
  }, [optimisticDecisions, overtimeRows, queueTab, search]);

  const workflowStage = queueSummary.pending > 0 ? 2 : queueSummary.approved > 0 ? 3 : queueSummary.paid > 0 ? 4 : 1;
  const nextStep = queueSummary.pending > 0
    ? "Review the waiting rows below. Approved hours become eligible for payroll."
    : queueSummary.approved > 0
      ? "Approved hours are ready for the payroll preparer to include in the pay run."
      : queueSummary.paid > 0
        ? "All paid rows are historical. New attendance imports will appear here for review."
        : "Import attendance to derive overtime and start the review workflow.";

  const scrollToImport = () => {
    setToolsOpen(true);
    window.requestAnimationFrame(() => importSectionRef.current?.scrollIntoView({ behavior: "smooth", block: "center" }));
  };

  const quoteEncashment = async () => {
    if (!encWorker || !encLeaveType) return;
    setEncBusy(true);
    try {
      const quote = await realApi.encashmentRate(encWorker, encLeaveType, Number(encDays) || 0);
      setEncQuote(quote);
    } catch (error) {
      setEncQuote(null);
      toast.error(error instanceof Error ? error.message : "Rate quote failed");
    } finally {
      setEncBusy(false);
    }
  };

  const submitEncashment = async () => {
    if (!encWorker || !encLeaveType) return;
    setEncBusy(true);
    try {
      const created = await realApi.createEncashment({
        workerId: encWorker,
        leaveTypeCode: encLeaveType,
        days: Number(encDays) || 0,
        note: encNote || undefined,
      });
      toast.success("Encashment request created — approval is required before the payout is posted.");
      setResult(created);
      encashments.reload();
      setEncDays("1");
      setEncNote("");
      setEncQuote(null);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Encashment request failed");
    } finally {
      setEncBusy(false);
    }
  };

  const decideEncashment = async (id: string, action: string) => {
    setEncBusy(true);
    try {
      await realApi.decideEncashment(id, { action, reason: action === "approve" ? "Approved by HR" : undefined });
      toast.success(`Encashment ${action === "approve" ? "approved" : "rejected"}`);
      encashments.reload();
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Decision failed");
    } finally {
      setEncBusy(false);
    }
  };

  const run = async (name: string, operation: () => Promise<Result>) => {
    setBusy(name);
    try {
      const response = await operation();
      setResult(response);
      history.reload();
      toast.success(`${name} completed`);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : `${name} failed`);
    } finally {
      setBusy(null);
    }
  };

  const decideOvertime = async (id: string, action: "approve" | "reject") => {
    const rowReason = reviewReasons[id]?.trim() ?? "";
    if (action === "reject" && !rowReason) {
      toast.error("Add a reason to explain why this overtime is being rejected.");
      return;
    }
    const nextStatus: OvertimeStatus = action === "approve" ? "approved" : "rejected";
    setOptimisticDecisions((current) => ({ ...current, [id]: nextStatus }));
    try {
      await realApi.decideOvertime(id, action, rowReason || undefined);
      toast.success(action === "approve" ? "Overtime approved for payroll." : "Overtime rejected with reason recorded.");
      setReviewReasons((current) => {
        const next = { ...current };
        delete next[id];
        return next;
      });
      setOptimisticDecisions((current) => {
        const next = { ...current };
        delete next[id];
        return next;
      });
      overtime.reload();
    } catch (error) {
      setOptimisticDecisions((current) => {
        const next = { ...current };
        delete next[id];
        return next;
      });
      toast.error(error instanceof Error ? error.message : "Overtime decision failed");
    }
  };

  const importRows = () => {
    const parsed = rows
      .split(/\r?\n/)
      .filter(Boolean)
      .map((line) => {
        const [employeeNo, workDate, clockIn, clockOut] = line.split(",").map((value) => value.trim());
        return { employeeNo, workDate, clockIn: clockIn || null, clockOut: clockOut || null };
      });
    return run("Attendance import", () => realApi.importAttendance({ fileName: "manual-import.csv", rows: parsed }));
  };

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Time and leave / daily operations"
          title="Overtime & attendance"
          description="Review attendance-derived overtime, make safe decisions, and hand approved hours to payroll."
          primaryAction={
            <Button onClick={scrollToImport} className="gap-2">
              <Upload className="size-4" aria-hidden />
              Import attendance
            </Button>
          }
          meta={
            <>
              <Badge variant="outline" className="gap-1.5 border-info/30 bg-info-soft text-info-foreground">
                <LockKeyhole className="size-3" aria-hidden />
                Live payroll workflow
              </Badge>
              <span className="text-xs text-muted-foreground">Scope follows your organisation and branch access.</span>
            </>
          }
        />

        <div className="space-y-6" data-testid="time-operations">
          <section aria-labelledby="workflow-heading" className="space-y-3">
            <div className="flex flex-wrap items-end justify-between gap-3">
              <div>
                <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">How this works</p>
                <h2 id="workflow-heading" className="mt-1 text-lg font-semibold">From clocked time to payroll</h2>
              </div>
              <p className="max-w-xl text-sm text-muted-foreground">{nextStep}</p>
            </div>
            <div className="grid gap-2 md:grid-cols-4">
              <WorkflowStep number={1} title="Import" description="Bring in clocked attendance." active={workflowStage === 1} complete={workflowStage > 1} />
              <WorkflowStep number={2} title="Review" description="Check derived overtime." active={workflowStage === 2} complete={workflowStage > 2} />
              <WorkflowStep number={3} title="Approve" description="Make hours payroll-eligible." active={workflowStage === 3} complete={workflowStage > 3} />
              <WorkflowStep number={4} title="Payroll" description="Release and link the source row." active={workflowStage === 4} complete={false} />
            </div>
          </section>

          <section aria-label="Overtime queue summary" className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <Card className={queueSummary.pending > 0 ? "border-warning/60 bg-warning-soft/30 shadow-none" : "shadow-none"}>
              <CardContent className="flex items-start justify-between gap-3 p-4">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Needs review</p>
                  <p className="mt-1 text-2xl font-semibold tabular">{queueSummary.pending}</p>
                  <p className="mt-1 text-xs text-muted-foreground">{hours(queueSummary.pendingHours)} hours waiting</p>
                </div>
                <span className="flex size-10 items-center justify-center rounded-xl bg-warning-soft text-warning-foreground"><TimerReset className="size-5" aria-hidden /></span>
              </CardContent>
            </Card>
            <Card className="shadow-none">
              <CardContent className="flex items-start justify-between gap-3 p-4">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Approved</p>
                  <p className="mt-1 text-2xl font-semibold tabular">{queueSummary.approved}</p>
                  <p className="mt-1 text-xs text-muted-foreground">{hours(queueSummary.approvedHours)} hours eligible</p>
                </div>
                <span className="flex size-10 items-center justify-center rounded-xl bg-info-soft text-info-foreground"><CheckCircle2 className="size-5" aria-hidden /></span>
              </CardContent>
            </Card>
            <Card className="shadow-none">
              <CardContent className="flex items-start justify-between gap-3 p-4">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Rejected</p>
                  <p className="mt-1 text-2xl font-semibold tabular">{queueSummary.rejected}</p>
                  <p className="mt-1 text-xs text-muted-foreground">Excluded from payroll</p>
                </div>
                <span className="flex size-10 items-center justify-center rounded-xl bg-danger-soft text-danger"><XCircle className="size-5" aria-hidden /></span>
              </CardContent>
            </Card>
            <Card className="shadow-none">
              <CardContent className="flex items-start justify-between gap-3 p-4">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Paid in payroll</p>
                  <p className="mt-1 text-2xl font-semibold tabular">{queueSummary.paid}</p>
                  <p className="mt-1 text-xs text-muted-foreground">{hours(queueSummary.paidHours)} historical hours</p>
                </div>
                <span className="flex size-10 items-center justify-center rounded-xl bg-success-soft text-success-foreground"><WalletCards className="size-5" aria-hidden /></span>
              </CardContent>
            </Card>
          </section>

          <Card className="overflow-hidden border-border/90 shadow-sm" data-testid="overtime-queue">
            <CardHeader className="border-b bg-surface-muted/50 pb-4">
              <div className="flex flex-wrap items-start justify-between gap-4">
                <div>
                  <div className="flex items-center gap-2">
                    <CardTitle>Overtime review queue</CardTitle>
                    <Badge variant="outline" className="font-normal">{overtimeRows.length} records</Badge>
                  </div>
                  <CardDescription className="mt-1 max-w-2xl">Work through the rows that need your decision. Approve only the hours you are comfortable sending to payroll.</CardDescription>
                </div>
                <Button variant="outline" size="sm" onClick={() => overtime.reload()} disabled={overtime.loading} className="gap-2">
                  <RefreshCw className={`size-4 ${overtime.loading ? "animate-spin" : ""}`} aria-hidden />
                  Refresh queue
                </Button>
              </div>
            </CardHeader>
            <CardContent className="p-0">
              <div className="space-y-4 border-b p-4">
                <Tabs value={queueTab} onValueChange={(value) => setQueueTab(value as QueueTab)}>
                  <TabsList className="h-auto w-full justify-start gap-1 overflow-x-auto bg-transparent p-0">
                    <TabsTrigger value="needs-review" className="gap-2 border border-transparent px-3 py-2 data-[state=active]:border-warning/50 data-[state=active]:bg-warning-soft/60">
                      Needs review <span className="rounded-full bg-warning-soft px-1.5 text-xs tabular">{queueSummary.pending}</span>
                    </TabsTrigger>
                    <TabsTrigger value="approved" className="gap-2 border border-transparent px-3 py-2 data-[state=active]:border-info/30 data-[state=active]:bg-info-soft/60">
                      Approved <span className="rounded-full bg-info-soft px-1.5 text-xs tabular">{queueSummary.approved}</span>
                    </TabsTrigger>
                    <TabsTrigger value="rejected" className="gap-2 border border-transparent px-3 py-2 data-[state=active]:border-danger/30 data-[state=active]:bg-danger-soft/60">
                      Rejected <span className="rounded-full bg-danger-soft px-1.5 text-xs tabular">{queueSummary.rejected}</span>
                    </TabsTrigger>
                    <TabsTrigger value="paid" className="gap-2 border border-transparent px-3 py-2 data-[state=active]:border-success/30 data-[state=active]:bg-success-soft/60">
                      Paid <span className="rounded-full bg-success-soft px-1.5 text-xs tabular">{queueSummary.paid}</span>
                    </TabsTrigger>
                    <TabsTrigger value="all" className="gap-2 border border-transparent px-3 py-2">All records</TabsTrigger>
                  </TabsList>
                </Tabs>
                <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_180px]">
                  <div className="relative">
                    <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden />
                    <Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search employee, date, or import source" className="pl-9" aria-label="Search overtime records" />
                  </div>
                  <div className="flex items-center gap-2 rounded-md border bg-card px-3 text-sm text-muted-foreground">
                    <CalendarDays className="size-4 shrink-0" aria-hidden />
                    <span>All available dates</span>
                  </div>
                </div>
              </div>

              {overtime.loading ? (
                <div className="space-y-3 p-6" aria-live="polite">
                  {[1, 2].map((item) => <div key={item} className="h-36 animate-pulse rounded-xl bg-muted" />)}
                </div>
              ) : overtime.error ? (
                <div className="m-4 flex items-start gap-3 rounded-xl border border-danger/30 bg-danger-soft/30 p-4 text-sm" role="alert">
                  <Info className="mt-0.5 size-4 shrink-0 text-danger" aria-hidden />
                  <div><p className="font-medium text-danger">The overtime queue could not be loaded.</p><p className="mt-1 text-muted-foreground">{overtime.error}</p><Button variant="outline" size="sm" className="mt-3" onClick={() => overtime.reload()}>Try again</Button></div>
                </div>
              ) : visibleRows.length === 0 ? (
                <div className="flex flex-col items-center justify-center px-6 py-14 text-center">
                  <span className="flex size-12 items-center justify-center rounded-2xl bg-primary-soft text-primary-foreground">
                    {overtimeRows.length === 0 ? <Upload className="size-5" aria-hidden /> : <ListFilter className="size-5" aria-hidden />}
                  </span>
                  <h3 className="mt-4 text-base font-semibold">{overtimeRows.length === 0 ? "No overtime has been imported yet" : queueTab === "needs-review" ? "Nothing needs your decision" : `No ${queueTab} records found`}</h3>
                  <p className="mt-1 max-w-md text-sm text-muted-foreground">
                    {overtimeRows.length === 0 ? "Import attendance to let the system derive overtime from the worker’s assigned shift." : queueTab === "needs-review" ? "That is good news. Approved, rejected, and paid history remains available in the tabs above." : "Try another tab or clear the search to see the rest of the queue."}
                  </p>
                  {overtimeRows.length === 0 ? <Button className="mt-5 gap-2" onClick={scrollToImport}><Upload className="size-4" aria-hidden />Import attendance</Button> : queueTab === "needs-review" ? <Button variant="outline" className="mt-5" onClick={() => setQueueTab("all")}>View all records</Button> : null}
                </div>
              ) : (
                <div className="divide-y divide-border">
                  {visibleRows.map((item) => {
                    const id = String(item.id);
                    const status = statusFor(item);
                    const rowReason = reviewReasons[id] ?? "";
                    const isPending = status === "pending";
                    return (
                      <article key={id} className="space-y-4 p-4 transition-colors hover:bg-surface-muted/35 sm:p-5" data-testid={`overtime-row-${id}`}>
                        <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                          <div className="flex min-w-0 items-start gap-3">
                            <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-secondary text-secondary-foreground"><UserRound className="size-5" aria-hidden /></span>
                            <div className="min-w-0">
                              <div className="flex flex-wrap items-center gap-2">
                                <h3 className="font-semibold">{String(item.workerName ?? item.workerId ?? "Worker")}</h3>
                                <Badge variant="outline" className={STATUS_CLASS[status]}>{STATUS_COPY[status]}</Badge>
                              </div>
                              <p className="mt-1 text-sm text-muted-foreground">{String(item.workDate ?? "—")} · {String(item.workerId ?? "No worker reference")}</p>
                              <p className="mt-1 text-xs text-muted-foreground">Source: {String(item.source ?? "attendance")}{item.importBatchId ? ` · import ${String(item.importBatchId).slice(0, 8)}` : ""}</p>
                            </div>
                          </div>
                          <div className="flex items-center gap-2 text-sm text-muted-foreground">
                            {status === "paid" ? <><LockKeyhole className="size-4" aria-hidden /> Historical payroll record</> : status === "approved" ? <><CheckCircle2 className="size-4 text-info" aria-hidden /> Eligible for payroll</> : status === "rejected" ? <><XCircle className="size-4 text-danger" aria-hidden /> Excluded from payroll</> : <><Clock3 className="size-4 text-warning-foreground" aria-hidden /> Waiting for review</>}
                          </div>
                        </div>

                        <div className="grid grid-cols-2 gap-3 rounded-xl bg-surface-muted/70 p-3 sm:grid-cols-4">
                          <div><p className="text-xs text-muted-foreground">Scheduled</p><p className="mt-1 font-semibold tabular">{hours(item.scheduledHours)}h</p></div>
                          <div><p className="text-xs text-muted-foreground">Worked</p><p className="mt-1 font-semibold tabular">{hours(item.totalHours)}h</p></div>
                          <div><p className="text-xs text-muted-foreground">Regular</p><p className="mt-1 font-semibold tabular">{hours(item.regularHours)}h</p></div>
                          <div><p className="text-xs text-muted-foreground">Overtime</p><p className="mt-1 font-semibold tabular text-primary-foreground">{hours(item.overtimeHours)}h <span className="text-xs font-normal text-muted-foreground">×{hours(item.overtimeMultiplier)}</span></p></div>
                        </div>

                        {item.overtimeDecisionReason ? <div className="rounded-lg border border-border bg-card px-3 py-2 text-sm"><span className="font-medium">Decision note:</span> <span className="text-muted-foreground">{String(item.overtimeDecisionReason)}</span></div> : null}
                        {item.overtimePayrollRunId ? <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground"><WalletCards className="size-3.5" aria-hidden />Payroll run linked: <span className="font-mono">{String(item.overtimePayrollRunId)}</span>{item.overtimePayrollLineId ? <><span>·</span><span className="font-mono">line {String(item.overtimePayrollLineId)}</span></> : null}</div> : null}

                        {isPending ? (
                          <div className="grid gap-3 rounded-xl border border-warning/40 bg-warning-soft/25 p-3 md:grid-cols-[minmax(0,1fr)_auto_auto] md:items-end">
                            <div>
                              <Label htmlFor={`overtime-reason-${id}`} className="text-sm">Decision note <span className="font-normal text-muted-foreground">(required to reject)</span></Label>
                              <Input id={`overtime-reason-${id}`} value={rowReason} onChange={(event) => setReviewReasons((current) => ({ ...current, [id]: event.target.value }))} placeholder="e.g. Approved by line manager / missing clock-out" className="mt-1.5 bg-card" />
                            </div>
                            <Button onClick={() => decideOvertime(id, "approve")} disabled={Boolean(busy) || Boolean(Object.keys(optimisticDecisions).length)} className="gap-2"><CheckCircle2 className="size-4" aria-hidden />Approve hours</Button>
                            <Button variant="outline" onClick={() => decideOvertime(id, "reject")} disabled={Boolean(busy) || !rowReason.trim() || Boolean(Object.keys(optimisticDecisions).length)} className="gap-2 border-danger/40 text-danger hover:bg-danger-soft"><XCircle className="size-4" aria-hidden />Reject</Button>
                          </div>
                        ) : null}
                      </article>
                    );
                  })}
                </div>
              )}
            </CardContent>
          </Card>

          <details ref={importSectionRef} open={toolsOpen} onToggle={(event) => setToolsOpen(event.currentTarget.open)} className="group rounded-xl border border-border bg-card shadow-sm">
            <summary className="flex cursor-pointer list-none items-center justify-between gap-4 p-5 [&::-webkit-details-marker]:hidden">
              <div className="flex items-start gap-3">
                <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-secondary text-secondary-foreground"><Settings2 className="size-4" aria-hidden /></span>
                <div><p className="font-semibold">Attendance and leave tools</p><p className="mt-1 text-sm text-muted-foreground">Import attendance, configure shift rules, and manage controlled leave operations when you need them.</p></div>
              </div>
              <ChevronDown className="size-5 shrink-0 text-muted-foreground transition-transform group-open:rotate-180" aria-hidden />
            </summary>
            <div className="grid gap-4 border-t p-4 sm:p-5 lg:grid-cols-2">
              <ToolCard icon={Upload} title="Import attendance" description="Paste one row per employee and work date. The system validates and derives overtime from the assigned shift.">
                <div className="space-y-3">
                  <Label htmlFor="attendance-rows">Employee no, date, clock in, clock out</Label>
                  <Textarea id="attendance-rows" value={rows} onChange={(event) => setRows(event.target.value)} rows={5} placeholder="EMP-0001,2026-08-15,08:00,17:30" />
                  <div className="flex flex-wrap items-center justify-between gap-3"><p className="text-xs text-muted-foreground">Use a new line for each attendance record.</p><Button onClick={importRows} disabled={busy !== null || !rows.trim()} className="gap-2"><Upload className="size-4" aria-hidden />Import and reconcile</Button></div>
                </div>
              </ToolCard>
              <ToolCard icon={Clock3} title="Shift rule" description="Create a reusable working-hours and overtime rule. This is setup work, not a daily review step.">
                <div className="grid gap-3 sm:grid-cols-2">
                  <div><Label htmlFor="shift-code">Code</Label><Input id="shift-code" value={shiftCode} onChange={(event) => setShiftCode(event.target.value)} /></div>
                  <div><Label htmlFor="shift-name">Name</Label><Input id="shift-name" value={shiftName} onChange={(event) => setShiftName(event.target.value)} /></div>
                  <div><Label htmlFor="shift-start">Starts</Label><Input id="shift-start" type="time" value={shiftStart} onChange={(event) => setShiftStart(event.target.value)} /></div>
                  <div><Label htmlFor="shift-end">Ends</Label><Input id="shift-end" type="time" value={shiftEnd} onChange={(event) => setShiftEnd(event.target.value)} /></div>
                  <Button className="sm:col-span-2" onClick={() => run("Shift creation", () => realApi.createShift({ code: shiftCode, name: shiftName, startTime: shiftStart, endTime: shiftEnd, unpaidBreakMinutes: 60, standardHours: 8, dailyOvertimeThresholdHours: 8, weekdayOvertimeMultiplier: 1.5, restDayOvertimeMultiplier: 2, holidayOvertimeMultiplier: 2 }) as Promise<Result>)} disabled={busy !== null || !shiftCode || !shiftName}>Create shift rule</Button>
                </div>
              </ToolCard>
              <ToolCard icon={CalendarDays} title="Shift assignment" description="Effective-date a worker’s shift. The previous open assignment closes automatically.">
                <div className="space-y-3">
                  <div><Label htmlFor="assignment-worker">Worker ID</Label><Input id="assignment-worker" value={assignmentWorker} onChange={(event) => setAssignmentWorker(event.target.value)} placeholder="Paste worker ID" /></div>
                  <div><Label htmlFor="assignment-shift">Shift ID</Label><Input id="assignment-shift" value={assignmentShift} onChange={(event) => setAssignmentShift(event.target.value)} placeholder="Paste shift ID" /></div>
                  <div><Label htmlFor="effective-from">Effective from</Label><Input id="effective-from" type="date" value={effectiveFrom} onChange={(event) => setEffectiveFrom(event.target.value)} /></div>
                  <Button onClick={() => run("Shift assignment", () => realApi.assignShift(assignmentWorker, { shiftId: assignmentShift, calendarId: null, effectiveFrom }) as Promise<Result>)} disabled={busy !== null || !assignmentWorker || !assignmentShift}>Assign shift</Button>
                </div>
              </ToolCard>
              <ToolCard icon={TimerReset} title="Monthly leave accrual" description="Create one auditable ledger entry per active worker and leave type for a period.">
                <div className="space-y-3"><Label htmlFor="period">Accrual period</Label><Input id="period" type="month" value={period} onChange={(event) => setPeriod(event.target.value)} /><Button onClick={() => run("Leave accrual", () => realApi.runLeaveAccrual(period))} disabled={busy !== null || !period}>Run accrual</Button></div>
              </ToolCard>
              <ToolCard icon={ListFilter} title="Leave balance adjustment" description="Post a traceable positive or negative correction to a worker’s leave ledger.">
                <div className="grid gap-3 sm:grid-cols-2"><div className="sm:col-span-2"><Label htmlFor="worker-id">Worker ID</Label><Input id="worker-id" value={workerId} onChange={(event) => setWorkerId(event.target.value)} /></div><div><Label htmlFor="leave-type">Leave type</Label><Input id="leave-type" value={leaveType} onChange={(event) => setLeaveType(event.target.value)} /></div><div><Label htmlFor="adjustment-days">Days</Label><Input id="adjustment-days" type="number" step="0.25" value={days} onChange={(event) => setDays(event.target.value)} /></div><div className="sm:col-span-2"><Label htmlFor="adjustment-reason">Reason</Label><Input id="adjustment-reason" value={reason} onChange={(event) => setReason(event.target.value)} /></div><Button className="sm:col-span-2" onClick={() => run("Balance adjustment", () => realApi.adjustLeaveBalance({ workerId, leaveTypeCode: leaveType, days: Number(days), reason }))} disabled={busy !== null || !workerId || !reason}>Post adjustment</Button></div>
              </ToolCard>
              <ToolCard icon={WalletCards} title="Leave encashment" description="Quote unused leave at the configured daily rate before submitting a request for approval.">
                <div className="grid gap-3 sm:grid-cols-2"><div className="sm:col-span-2"><Label htmlFor="enc-worker">Employee ID</Label><Input id="enc-worker" value={encWorker} onChange={(event) => setEncWorker(event.target.value)} /></div><div><Label htmlFor="enc-leave-type">Leave type</Label>{leaveTypeItems.length ? <Select value={encLeaveType} onValueChange={setEncLeaveType}><SelectTrigger id="enc-leave-type"><SelectValue /></SelectTrigger><SelectContent>{leaveTypeItems.map((item) => <SelectItem key={String(item.id)} value={String(item.code)}>{String(item.name)}</SelectItem>)}</SelectContent></Select> : <Input id="enc-leave-type" value={encLeaveType} onChange={(event) => setEncLeaveType(event.target.value)} />}</div><div><Label htmlFor="enc-days">Days to convert</Label><Input id="enc-days" type="number" step="0.25" min="0.25" value={encDays} onChange={(event) => setEncDays(event.target.value)} /></div><div className="sm:col-span-2"><Label htmlFor="enc-note">Note (optional)</Label><Input id="enc-note" value={encNote} onChange={(event) => setEncNote(event.target.value)} placeholder="e.g. December leave payout" /></div><Button variant="outline" onClick={quoteEncashment} disabled={busy !== null || encBusy || !encWorker || !encLeaveType}>Quote payout</Button><Button onClick={submitEncashment} disabled={busy !== null || encBusy || !encWorker || !encLeaveType || !encQuote}>Submit request</Button>{encQuote ? <p className="text-sm text-muted-foreground sm:col-span-2">Estimated payout: {String(encQuote.estimatedGross)} {String(encQuote.currency)} · daily rate {String(encQuote.dailyRate)}</p> : <p className="text-xs text-muted-foreground sm:col-span-2">Quote first so the balance and estimated payout are visible before submission.</p>}</div>
              </ToolCard>
              <ToolCard icon={ArrowRight} title="Approval escalation" description="Reassign overdue leave and attendance-correction approvals up the manager chain.">
                <Button onClick={() => run("Approval escalation", realApi.escalateTimeApprovals)} disabled={busy !== null}>Escalate overdue approvals</Button>
              </ToolCard>
            </div>
          </details>

          <Card data-testid="operations-history" className="shadow-none">
            <CardHeader className="pb-4"><CardTitle className="text-base">Recent activity</CardTitle><CardDescription>Confirmation of persisted imports and leave operations—not a raw API response.</CardDescription></CardHeader>
            <CardContent className="grid gap-4 text-sm sm:grid-cols-2 lg:grid-cols-4">
              <div><p className="font-medium">Attendance imports</p>{history.loading ? <p className="mt-1 text-muted-foreground">Loading…</p> : history.data?.imports.length ? history.data.imports.slice(0, 3).map((item) => <p key={String(item.batchId)} className="mt-1 text-muted-foreground">{String(item.fileName)} · {String(item.importedCount)} imported · {String(item.rejectedCount)} rejected</p>) : <p className="mt-1 text-muted-foreground">No imports yet.</p>}</div>
              <div><p className="font-medium">Accrual runs</p>{history.data?.accruals.length ? history.data.accruals.slice(0, 3).map((item) => <p key={String(item.id)} className="mt-1 text-muted-foreground">{String(item.period)} · {String(item.status)}</p>) : <p className="mt-1 text-muted-foreground">No accrual runs yet.</p>}</div>
              <div><p className="font-medium">Balance adjustments</p>{history.data?.adjustments.length ? history.data.adjustments.slice(0, 3).map((item) => <p key={String(item.id)} className="mt-1 text-muted-foreground">{String(item.workerName)} · {String(item.days)} days</p>) : <p className="mt-1 text-muted-foreground">No adjustments yet.</p>}</div>
              <div><p className="font-medium">Leave encashments</p>{encashmentItems.length ? encashmentItems.slice(0, 3).map((item) => <p key={String(item.id)} className="mt-1 text-muted-foreground">{String(item.workerName ?? "Worker")} · {String(item.days)} day(s) · {String(item.status)}</p>) : <p className="mt-1 text-muted-foreground">No encashment requests yet.</p>}</div>
            </CardContent>
          </Card>

          {result ? <details className="rounded-xl border border-border bg-card"><summary className="cursor-pointer list-none p-4 text-sm font-medium [&::-webkit-details-marker]:hidden">Show last operation details</summary><pre className="m-4 max-h-72 overflow-auto rounded-lg bg-muted p-4 text-xs">{JSON.stringify(result, null, 2)}</pre></details> : null}
        </div>
      </AppShell>
    </AuthGate>
  );
}
