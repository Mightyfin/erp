import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/time/operations")({ component: TimeOperations });

type Result = Record<string, unknown>;

function TimeOperations() {
  const history = useApi(realApi.timeOperationsHistory, []);
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
              </>
            ) : null}
          </CardContent>
        </Card>
      </AppShell>
    </AuthGate>
  );
}
