import { createFileRoute } from '@tanstack/react-router'
import { useState } from "react";
import { CalendarRange, Play } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { ScopeBadge } from "@/platform/components/ScopeBadge";
import { feedback } from "@/platform/feedback";
import { realApi, useApi } from "@/platform/use-api";

type Row = Record<string, unknown>;
const list = (value: unknown): Row[] => Array.isArray(value) ? value as Row[] : ((value as { items?: unknown[] } | null)?.items ?? []) as Row[];
const val = (row: Row, key: string, fallback = "—") => row[key] == null || row[key] === "" ? fallback : String(row[key]);

export const Route = createFileRoute("/hrm/configuration/leave-periods")({
  head: () => ({ meta: [{ title: "Leave Periods — New World Cargo HRM" }, { name: "description", content: "Live leave accrual periods and runs." }] }),
  component: LeavePeriodsPage,
});

function LeavePeriodsPage() {
  const state = useApi(realApi.timeOperationsHistory, []);
  const [open, setOpen] = useState(false); const [period, setPeriod] = useState(new Date().toISOString().slice(0, 7)); const [busy, setBusy] = useState(false);
  const history = (state.data ?? {}) as Record<string, unknown>; const accruals = list(history.accruals);
  const run = async () => { if (!/^\d{4}-(0[1-9]|1[0-2])$/.test(period)) { feedback.blocked("Accrual was not started.", "Period must use YYYY-MM format."); return; } setBusy(true); try { await realApi.runLeaveAccrual(period); feedback.success("Leave accrual run completed.", `Period ${period} was processed through the live HRM API.`); setOpen(false); state.reload(); } catch (error) { feedback.blocked("Accrual was not started.", error instanceof Error ? error.message : "The HRM API rejected the run."); } finally { setBusy(false); } };
  return <AppShell><PageHeader eyebrow="Configuration · Leave" title="Leave Periods & Accruals" description="Process effective leave periods and review the live accrual-run history. Payroll periods remain separate and are not silently reused as leave policy periods." meta={<ScopeBadge />} primaryAction={<Button onClick={() => setOpen(true)}><Play className="mr-2 size-4" />Run accrual</Button>} /><div className="mb-4 flex items-center gap-2 text-sm text-muted-foreground"><CalendarRange className="size-4" aria-hidden /> Accrual runs write explainable ledger entries for the selected period.</div><Async state={state} rows={5}>{() => <div className="overflow-x-auto rounded-lg border bg-surface"><table className="w-full text-left text-sm"><thead className="border-b bg-surface-muted"><tr>{["Period", "Status", "Workers", "Ledger entries", "Days accrued", "Run by", "Created"].map((h) => <th key={h} className="whitespace-nowrap px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">{h}</th>)}</tr></thead><tbody className="divide-y">{accruals.map((row) => <tr key={val(row, "id")}><th className="px-3 py-2 font-medium">{val(row, "period")}</th><td className="px-3 py-2">{val(row, "status")}</td><td className="px-3 py-2 tabular-nums">{val(row, "workerCount", "0")}</td><td className="px-3 py-2 tabular-nums">{val(row, "ledgerEntryCount", "0")}</td><td className="px-3 py-2 tabular-nums">{val(row, "totalDaysAccrued", "0")}</td><td className="px-3 py-2">{val(row, "runBySubjectId")}</td><td className="px-3 py-2">{val(row, "createdAt").slice(0, 10)}</td></tr>)}{accruals.length === 0 ? <tr><td colSpan={7} className="px-3 py-10 text-center text-muted-foreground">No leave accrual runs have been recorded.</td></tr> : null}</tbody></table></div>}</Async><p className="mt-4 text-xs text-muted-foreground">The API protects repeated or invalid periods according to the existing accrual lifecycle. Historical payroll runs are not recalculated by this page.</p><Dialog open={open} onOpenChange={setOpen}><DialogContent><DialogHeader><DialogTitle>Run leave accrual</DialogTitle><DialogDescription>Choose the leave period in YYYY-MM form. This is an HR leave ledger operation, not payroll calculation.</DialogDescription></DialogHeader><div className="space-y-1"><Label htmlFor="leave-period">Leave period</Label><Input id="leave-period" value={period} onChange={(e) => setPeriod(e.target.value)} placeholder="2026-09" /></div><DialogFooter><Button variant="outline" onClick={() => setOpen(false)}>Cancel</Button><Button disabled={busy} onClick={run}>{busy ? "Running…" : "Run accrual"}</Button></DialogFooter></DialogContent></Dialog></AppShell>;
}
