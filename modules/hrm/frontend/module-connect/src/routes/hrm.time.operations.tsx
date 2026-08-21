import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { AppShell } from "@/platform/components/AppShell";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/time/operations")({ component: TimeOperations });

type Result = Record<string, unknown>;

function TimeOperations() {
  const history = useApi(realApi.timeOperationsHistory, []);
  const leaveTypes = useApi(() =>
    realApi.leaveTypes ? realApi.leaveTypes({ includeInactive: false }) : Promise.resolve({ items: [] as unknown[] }),
  );
  const encashments = useApi(() => realApi.encashments({ pageSize: 25 }), []);
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

  // Leave encashment form state (M41 Gap 6a).
  const [encWorker, setEncWorker] = useState("");
  const [encLeaveType, setEncLeaveType] = useState("ANNUAL");
  const [encDays, setEncDays] = useState("1");
  const [encNote, setEncNote] = useState("");
  const [encQuote, setEncQuote] = useState<Record<string, unknown> | null>(null);
  const [encBusy, setEncBusy] = useState(false);
  const encashmentItems = Array.isArray(encashments.data)
    ? encashments.data
    : encashments.data?.items ?? [];

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

  const importRows = () => {
    const parsed = rows
      .split(/\r?\n/)
      .filter(Boolean)
      .map((line) => {
        const [employeeNo, workDate, clockIn, clockOut] = line
          .split(",")
          .map((value) => value.trim());
        return { employeeNo, workDate, clockIn: clockIn || null, clockOut: clockOut || null };
      });
    return run("Attendance import", () =>
      realApi.importAttendance({ fileName: "manual-import.csv", rows: parsed }),
    );
  };

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Time and leave"
          title="Time operations"
          description="Import attendance, run controlled leave accruals, correct balances and escalate overdue approvals."
        />
        <div className="grid gap-6 lg:grid-cols-2" data-testid="time-operations">
          <Card>
            <CardHeader>
              <CardTitle>Shift rule</CardTitle>
              <CardDescription>Create a reusable working-hours and overtime rule.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-3 sm:grid-cols-2">
              <div>
                <Label htmlFor="shift-code">Code</Label>
                <Input
                  id="shift-code"
                  value={shiftCode}
                  onChange={(event) => setShiftCode(event.target.value)}
                />
              </div>
              <div>
                <Label htmlFor="shift-name">Name</Label>
                <Input
                  id="shift-name"
                  value={shiftName}
                  onChange={(event) => setShiftName(event.target.value)}
                />
              </div>
              <div>
                <Label htmlFor="shift-start">Starts</Label>
                <Input
                  id="shift-start"
                  type="time"
                  value={shiftStart}
                  onChange={(event) => setShiftStart(event.target.value)}
                />
              </div>
              <div>
                <Label htmlFor="shift-end">Ends</Label>
                <Input
                  id="shift-end"
                  type="time"
                  value={shiftEnd}
                  onChange={(event) => setShiftEnd(event.target.value)}
                />
              </div>
              <Button
                className="sm:col-span-2"
                onClick={() =>
                  run(
                    "Shift creation",
                    () =>
                      realApi.createShift({
                        code: shiftCode,
                        name: shiftName,
                        startTime: shiftStart,
                        endTime: shiftEnd,
                        unpaidBreakMinutes: 60,
                        standardHours: 8,
                        dailyOvertimeThresholdHours: 8,
                        weekdayOvertimeMultiplier: 1.5,
                        restDayOvertimeMultiplier: 2,
                        holidayOvertimeMultiplier: 2,
                      }) as Promise<Result>,
                  )
                }
                disabled={busy !== null || !shiftCode || !shiftName}
              >
                Create shift rule
              </Button>
            </CardContent>
          </Card>
          <Card>
            <CardHeader>
              <CardTitle>Shift assignment</CardTitle>
              <CardDescription>
                Effective-date a worker's shift. The previous open assignment closes automatically.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <div>
                <Label htmlFor="assignment-worker">Worker ID</Label>
                <Input
                  id="assignment-worker"
                  value={assignmentWorker}
                  onChange={(event) => setAssignmentWorker(event.target.value)}
                />
              </div>
              <div>
                <Label htmlFor="assignment-shift">Shift ID</Label>
                <Input
                  id="assignment-shift"
                  value={assignmentShift}
                  onChange={(event) => setAssignmentShift(event.target.value)}
                />
              </div>
              <div>
                <Label htmlFor="effective-from">Effective from</Label>
                <Input
                  id="effective-from"
                  type="date"
                  value={effectiveFrom}
                  onChange={(event) => setEffectiveFrom(event.target.value)}
                />
              </div>
              <Button
                onClick={() =>
                  run(
                    "Shift assignment",
                    () =>
                      realApi.assignShift(assignmentWorker, {
                        shiftId: assignmentShift,
                        calendarId: null,
                        effectiveFrom,
                      }) as Promise<Result>,
                  )
                }
                disabled={busy !== null || !assignmentWorker || !assignmentShift}
              >
                Assign shift
              </Button>
            </CardContent>
          </Card>
          <Card>
            <CardHeader>
              <CardTitle>Attendance import</CardTitle>
              <CardDescription>
                One CSV row per employee and work date: employee number, date, clock in, clock out.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <Label htmlFor="attendance-rows">Attendance rows</Label>
              <Textarea
                id="attendance-rows"
                value={rows}
                onChange={(event) => setRows(event.target.value)}
                rows={6}
              />
              <Button onClick={importRows} disabled={busy !== null}>
                Import and reconcile
              </Button>
            </CardContent>
          </Card>
          <Card>
            <CardHeader>
              <CardTitle>Monthly leave accrual</CardTitle>
              <CardDescription>
                Creates one auditable ledger entry per active worker and leave type. A period can
                run only once.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <Label htmlFor="period">Accrual period</Label>
              <Input
                id="period"
                type="month"
                value={period}
                onChange={(event) => setPeriod(event.target.value)}
              />
              <Button
                onClick={() => run("Leave accrual", () => realApi.runLeaveAccrual(period))}
                disabled={busy !== null || !period}
              >
                Run accrual
              </Button>
            </CardContent>
          </Card>
          <Card>
            <CardHeader>
              <CardTitle>Leave balance adjustment</CardTitle>
              <CardDescription>
                Post a traceable positive or negative correction to a worker's leave ledger.
              </CardDescription>
            </CardHeader>
            <CardContent className="grid gap-3 sm:grid-cols-2">
              <div className="sm:col-span-2">
                <Label htmlFor="worker-id">Worker ID</Label>
                <Input
                  id="worker-id"
                  value={workerId}
                  onChange={(event) => setWorkerId(event.target.value)}
                />
              </div>
              <div>
                <Label htmlFor="leave-type">Leave type</Label>
                <Input
                  id="leave-type"
                  value={leaveType}
                  onChange={(event) => setLeaveType(event.target.value)}
                />
              </div>
              <div>
                <Label htmlFor="adjustment-days">Days</Label>
                <Input
                  id="adjustment-days"
                  type="number"
                  step="0.25"
                  value={days}
                  onChange={(event) => setDays(event.target.value)}
                />
              </div>
              <div className="sm:col-span-2">
                <Label htmlFor="adjustment-reason">Reason</Label>
                <Input
                  id="adjustment-reason"
                  value={reason}
                  onChange={(event) => setReason(event.target.value)}
                />
              </div>
              <Button
                className="sm:col-span-2"
                onClick={() =>
                  run("Balance adjustment", () =>
                    realApi.adjustLeaveBalance({
                      workerId,
                      leaveTypeCode: leaveType,
                      days: Number(days),
                      reason,
                    }),
                  )
                }
                disabled={busy !== null || !workerId || !reason}
              >
                Post adjustment
              </Button>
            </CardContent>
          </Card>
          <Card>
            <CardHeader>
              <CardTitle>Leave encashment</CardTitle>
              <CardDescription>
                Convert a worker's unused leave balance into a cash payout at their daily rate
                (basic monthly salary ÷ 26 working days). The request needs approval before the
                balance deduction is posted.
              </CardDescription>
            </CardHeader>
            <CardContent className="grid gap-3 sm:grid-cols-2">
              <div className="sm:col-span-2">
                <Label htmlFor="enc-worker">Employee ID</Label>
                <Input
                  id="enc-worker"
                  value={encWorker}
                  onChange={(event) => setEncWorker(event.target.value)}
                />
              </div>
              <div>
                <Label htmlFor="enc-leave-type">Leave type</Label>
                {leaveTypes.data && leaveTypes.data.items.length ? (
                  <Select value={encLeaveType} onValueChange={setEncLeaveType}>
                    <SelectTrigger id="enc-leave-type">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {leaveTypes.data.items.map((item) => (
                        <SelectItem key={String(item.id)} value={String(item.code)}>
                          {String(item.name)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                ) : (
                  <Input
                    id="enc-leave-type"
                    value={encLeaveType}
                    onChange={(event) => setEncLeaveType(event.target.value)}
                  />
                )}
              </div>
              <div>
                <Label htmlFor="enc-days">Days to convert</Label>
                <Input
                  id="enc-days"
                  type="number"
                  step="0.25"
                  min="0.25"
                  value={encDays}
                  onChange={(event) => setEncDays(event.target.value)}
                />
              </div>
              <div className="sm:col-span-2">
                <Label htmlFor="enc-note">Note (optional)</Label>
                <Input
                  id="enc-note"
                  value={encNote}
                  onChange={(event) => setEncNote(event.target.value)}
                  placeholder="e.g. December leave payout"
                />
              </div>
              <Button
                variant="outline"
                className="sm:col-span-1"
                onClick={quoteEncashment}
                disabled={busy !== null || encBusy || !encWorker || !encLeaveType}
              >
                Quote payout
              </Button>
              <Button
                className="sm:col-span-1"
                onClick={submitEncashment}
                disabled={busy !== null || encBusy || !encWorker || !encLeaveType || !encQuote}
              >
                Submit encashment request
              </Button>
              {encQuote ? (
                <p className="text-sm text-muted-foreground sm:col-span-2">
                  Estimated payout: {String(encQuote.estimatedGross)} {String(encQuote.currency)} ·
                  daily rate {String(encQuote.dailyRate)} · monthly basic {String(encQuote.monthlyBasic)}
                </p>
              ) : (
                <p className="text-sm text-muted-foreground sm:col-span-2">
                  Quote the payout first to see the estimated amount and confirm the available
                  balance.
                </p>
              )}
            </CardContent>
          </Card>
          <Card>
            <CardHeader>
              <CardTitle>Approval escalation</CardTitle>
              <CardDescription>
                Reassigns overdue leave and attendance-correction approvals up the manager chain and
                refreshes their due date.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <Button
                onClick={() => run("Approval escalation", realApi.escalateTimeApprovals)}
                disabled={busy !== null}
              >
                Escalate overdue approvals
              </Button>
            </CardContent>
          </Card>
        </div>
        {result ? (
          <Card className="mt-6" data-testid="operation-result">
            <CardHeader>
              <CardTitle>Last operation result</CardTitle>
            </CardHeader>
            <CardContent>
              <pre className="overflow-auto rounded-md bg-muted p-4 text-xs">
                {JSON.stringify(result, null, 2)}
              </pre>
            </CardContent>
          </Card>
        ) : null}
        <Card className="mt-6" data-testid="operations-history">
          <CardHeader>
            <CardTitle>Recent operational history</CardTitle>
            <CardDescription>
              Persisted import reconciliation, accrual, and balance-adjustment records.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4 text-sm">
            {history.loading ? <p>Loading history…</p> : null}
            {history.error ? <p className="text-destructive">{history.error}</p> : null}
            {history.data ? (
              <>
                <div>
                  <p className="font-medium">Attendance imports</p>
                  {history.data.imports.length ? (
                    history.data.imports.map((item) => (
                      <p key={String(item.batchId)} className="text-muted-foreground">
                        {String(item.fileName)} · {String(item.status)} ·{" "}
                        {String(item.importedCount)} imported · {String(item.rejectedCount)}{" "}
                        rejected
                      </p>
                    ))
                  ) : (
                    <p className="text-muted-foreground">No imports yet.</p>
                  )}
                </div>
                <div>
                  <p className="font-medium">Accrual runs</p>
                  {history.data.accruals.length ? (
                    history.data.accruals.map((item) => (
                      <p key={String(item.id)} className="text-muted-foreground">
                        {String(item.period)} · {String(item.status)} ·{" "}
                        {String(item.ledgerEntryCount)} entries
                      </p>
                    ))
                  ) : (
                    <p className="text-muted-foreground">No accrual runs yet.</p>
                  )}
                </div>
                <div>
                  <p className="font-medium">Balance adjustments</p>
                  {history.data.adjustments.length ? (
                    history.data.adjustments.map((item) => (
                      <p key={String(item.id)} className="text-muted-foreground">
                        {String(item.workerName)} · {String(item.leaveTypeCode)} ·{" "}
                        {String(item.days)} days · {String(item.reason)}
                      </p>
                    ))
                  ) : (
                    <p className="text-muted-foreground">No adjustments yet.</p>
                  )}
                </div>
                <div>
                  <p className="font-medium">Leave encashments</p>
                  {encashmentItems.length ? (
                    encashmentItems.map((item) => (
                      <div key={String(item.id)} className="flex flex-wrap items-center gap-2 text-muted-foreground">
                        <span>
                          {String(item.workerName ?? "—")} · {String(item.leaveTypeCode)} ·{" "}
                          {String(item.days)} day(s) · {String(item.grossAmount)} {String(item.currency ?? "ZMW")}
                        </span>
                        <span
                          className={
                            item.status === "approved"
                              ? "rounded bg-emerald-100 px-1.5 py-0.5 text-xs font-medium text-emerald-700"
                              : item.status === "rejected"
                                ? "rounded bg-red-100 px-1.5 py-0.5 text-xs font-medium text-red-700"
                                : "rounded bg-amber-100 px-1.5 py-0.5 text-xs font-medium text-amber-700"
                          }
                        >
                          {String(item.status)}
                        </span>
                        {item.status === "submitted" ? (
                          <>
                            <Button
                              size="sm"
                              variant="outline"
                              className="h-6 text-xs"
                              onClick={() => decideEncashment(String(item.id), "approve")}
                              disabled={busy !== null || encBusy}
                            >
                              Approve
                            </Button>
                            <Button
                              size="sm"
                              variant="outline"
                              className="h-6 text-xs"
                              onClick={() => decideEncashment(String(item.id), "reject")}
                              disabled={busy !== null || encBusy}
                            >
                              Reject
                            </Button>
                          </>
                        ) : null}
                      </div>
                    ))
                  ) : (
                    <p className="text-muted-foreground">No encashment requests yet.</p>
                  )}
                </div>
              </>
            ) : null}
          </CardContent>
        </Card>
      </AppShell>
    </AuthGate>
  );
}
