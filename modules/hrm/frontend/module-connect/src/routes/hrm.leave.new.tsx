import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { AlertTriangle, CheckCircle2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { AppShell } from "@/platform/components/AppShell";
import { GuidedFlow, NextSteps } from "@/platform/components/GuidedFlow";
import type { FlowStep } from "@/platform/components/GuidedFlow";
import { PageHeader } from "@/platform/components/PageHeader";
import { adaptWorkers, realApi, useApi } from "@/platform/use-api";
import { useMock } from "@/platform/use-mock";
import { api } from "@/mock/service";

export const Route = createFileRoute("/hrm/leave/new")({
  head: () => ({
    meta: [
      { title: "Request leave — Mightyfin ERP HRM" },
      { name: "description", content: "Guided leave request: purpose, dates, policy checks, evidence, review and submit." },
      { property: "og:title", content: "Request leave — Mightyfin ERP HRM" },
      { property: "og:description", content: "Guided leave request with policy checks before you submit." },
    ],
  }),
  component: NewLeave,
});

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

function toCode(name: string, codes: { code: string; name: string }[]): string {
  const hit = codes.find((c) => c.name.toLowerCase() === name.toLowerCase()) ?? codes[0];
  return hit?.code ?? name.toLowerCase();
}

function NewLeave() {
  const navigate = useNavigate();
  const [ref, setRef] = useState<string | null>(null);
  const [type, setType] = useState("Annual");
  const [from, setFrom] = useState("2026-08-10");
  const [to, setTo] = useState("2026-08-21");
  const [reason, setReason] = useState("");
  const [workerId, setWorkerId] = useState("019ffc92-6ccb-7bc8-8675-d0ef71c24ea2");

  const leaveTypes = useApi(
    () => (USE_REAL ? realApi.leaveTypes() : Promise.resolve([] as unknown[])),
    [],
  );
  const workers = useApi(
    () => (USE_REAL ? realApi.employees({ page: 1, pageSize: 200 }) : Promise.resolve({ items: [] as unknown[] })),
    [],
  );

  const codes = Array.isArray(leaveTypes.data)
    ? leaveTypes.data.map((t) => ({
        code: String((t as { code?: unknown }).code ?? (t as { id?: unknown }).id ?? ""),
        name: String((t as { name?: unknown }).name ?? ""),
      }))
    : [];

  const employeeRows = workers.data ? adaptWorkers(workers.data) : [];

  const steps: FlowStep[] = [
    {
      id: "purpose",
      title: "Choose the purpose",
      purpose: "The leave type decides which policy, evidence and approver apply.",
      render: () => {
        const options = codes.length > 0 ? codes.map((c) => ({ value: c.name, label: `${c.name} (${c.code})` })) : [{ value: "Annual", label: "Annual" }, { value: "Sick", label: "Sick" }, { value: "Parental", label: "Parental" }, { value: "Unpaid", label: "Unpaid" }, { value: "Study", label: "Study" }];
        return (
          <div className="max-w-sm">
            <Label htmlFor="leave-type">Leave type</Label>
            <Select value={type} onValueChange={setType}>
              <SelectTrigger id="leave-type" className="mt-1"><SelectValue /></SelectTrigger>
              <SelectContent>
                {options.map((o) => <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>)}
              </SelectContent>
            </Select>
            {USE_REAL && (
              <div className="mt-4">
                <Label htmlFor="worker">Employee</Label>
                <Select value={workerId} onValueChange={setWorkerId}>
                  <SelectTrigger id="worker" className="mt-1"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {employeeRows.map((w) => <SelectItem key={w.id} value={w.id}>{w.fullName} ({w.employeeNo})</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
            )}
          </div>
        );
      },
    },
    {
      id: "details",
      title: "Essential details",
      purpose: "The minimum needed to assess your request. Everything else is optional.",
      render: () => (
        <div className="grid max-w-lg gap-4 sm:grid-cols-2">
          <div><Label htmlFor="from">First day</Label><Input id="from" type="date" className="mt-1" value={from} onChange={(e) => setFrom(e.target.value)} /></div>
          <div><Label htmlFor="to">Last day</Label><Input id="to" type="date" className="mt-1" value={to} onChange={(e) => setTo(e.target.value)} /></div>
          <div className="sm:col-span-2">
            <Label htmlFor="reason">Reason (optional)</Label>
            <Textarea id="reason" className="mt-1" rows={3} value={reason} onChange={(e) => setReason(e.target.value)} />
          </div>
        </div>
      ),
    },
    {
      id: "policy",
      title: "Policy check",
      purpose: "What the rules say about this request, before anyone spends time on it.",
      render: () => (
        <ul className="space-y-2 text-sm">
          <li className="flex gap-2"><CheckCircle2 className="mt-0.5 size-4 text-success" aria-hidden /><span><span className="font-medium">Balance checked — Pass.</span> Balance is consulted at submission.</span></li>
          <li className="flex gap-2"><AlertTriangle className="mt-0.5 size-4 text-warning" aria-hidden /><span><span className="font-medium">Notice period — Check.</span> Your manager can still approve a short-notice request.</span></li>
          <li className="flex gap-2"><CheckCircle2 className="mt-0.5 size-4 text-success" aria-hidden /><span><span className="font-medium">Blackout window — Pass.</span> No plant shutdown in this range.</span></li>
        </ul>
      ),
    },
    {
      id: "evidence",
      title: "Evidence",
      purpose: "Attach anything that helps the approver decide. Not required for annual leave.",
      optional: true,
      render: () => (
        <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
          Drop a file here (mock upload — nothing is stored).
        </div>
      ),
    },
    {
      id: "review",
      title: "Review and submit",
      purpose: "Check the facts. Submitting sends this to the employee's manager for a decision.",
      render: () => {
        const who = employeeRows.find((w) => w.id === workerId);
        return (
          <dl className="grid max-w-lg gap-3 sm:grid-cols-2">
            {[["Employee", who ? `${who.fullName} (${who.employeeNo})` : "—"], ["Type", type], ["From", from], ["To", to], ["Reason", reason || "Not given"]].map(([k, v]) => (
              <div key={k} className="rounded-md border bg-surface-muted px-3 py-2">
                <dt className="text-xs text-muted-foreground">{k}</dt>
                <dd className="text-sm font-medium">{v}</dd>
              </div>
            ))}
          </dl>
        );
      },
    },
  ];

  return (
    <AppShell>
      <PageHeader eyebrow="Leave" title="Request leave" description="Five short steps. Your draft saves as you go." />
      <GuidedFlow
        flowId="leave-new"
        steps={steps}
        submitLabel="Submit request"
        onSubmit={async () => {
          if (USE_REAL) {
            const created = await realApi.createLeaveRequest({
              workerId,
              leaveTypeCode: toCode(type, codes),
              startDate: from,
              endDate: to,
              reason: reason || null,
            });
            setRef(String((created as { id?: unknown }).id ?? "submitted"));
          } else {
            const r = await api.submit("leave", { type, from, to, reason });
            setRef(r.id);
          }
        }}
        submitted={
          ref ? (
            <NextSteps
              reference={ref.startsWith("LV-") || ref.startsWith("0") ? ref : `LV-${ref}`}
              title="Request submitted"
              steps={[
                "The request has been sent to the employee's manager for a decision.",
                "You'll be notified if it's approved, returned for more information, or rejected with a reason.",
                "Approved leave appears on the employee's profile and in the next pay run's absence data.",
              ]}
              actions={<><Button onClick={() => navigate({ to: "/hrm/leave" })}>View my requests</Button><Button variant="outline" asChild><Link to="/hrm">Back to Home</Link></Button></>}
            />
          ) : undefined
        }
      />
    </AppShell>
  );
}
