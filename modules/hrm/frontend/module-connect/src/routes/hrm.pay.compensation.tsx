import { createFileRoute } from "@tanstack/react-router";
import { useEffect, useRef, useState } from "react";
import { Info, Pencil, ShieldAlert, Unplug } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { ImportDialog } from "@/platform/components/ImportExport/ImportDialog";
import { realApi, useApi } from "@/platform/use-api";
import { useAuth } from "@/platform/auth";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/pay/compensation")({
  head: () => ({
    meta: [
      { title: "Compensation and benefits — New World Cargo HRM" },
      {
        name: "description",
        content:
          "Per-worker salary structures and component amounts driving the next pay run. Benefits and review cycles are not yet administered here.",
      },
      { property: "og:title", content: "Compensation and benefits — New World Cargo HRM" },
      {
        property: "og:description",
        content:
          "Per-worker salary structures and component amounts driving the next pay run. Benefits and review cycles are not yet administered here.",
      },
    ],
  }),
  component: CompensationPage,
});

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

type Raw = Record<string, unknown>;

/** Compa-ratio is shown as a number and a word — never a bare colour. */
function CompaRatio({ value }: { value: number }) {
  const label =
    value < 0.9 ? "Below band midpoint" : value > 1.1 ? "Above band midpoint" : "Around midpoint";
  const tone = value < 0.9 ? "text-warning" : value > 1.1 ? "text-info" : "";
  return (
    <span className="block">
      <span className={`tabular text-sm font-medium ${tone}`}>{value.toFixed(2)}</span>
      <span className="block text-[11px] text-muted-foreground">{label}</span>
    </span>
  );
}

/* ------------------------------------------------------------------ */

async function loadCompensation() {
  if (!USE_REAL) return { workers: [], profiles: [], components: [], groups: [] };
  const [workers, profiles, components, groups] = await Promise.all([
    realApi.employees({ page: 1, pageSize: 200, status: "active" }),
    realApi.payrollProfiles(),
    realApi.payrollComponents(),
    realApi.payrollPayGroups(),
  ]);
  return {
    workers: Array.isArray(workers) ? (workers as Raw[]) : (workers?.items ?? []) as Raw[],
    profiles: Array.isArray(profiles) ? (profiles as Raw[]) : [],
    components: Array.isArray(components) ? (components as Raw[]) : [],
    groups: Array.isArray(groups) ? (groups as Raw[]) : [],
  };
}

function WorkerPayDialog({
  worker,
  state,
  open,
  onOpenChange,
}: {
  worker: Raw | null;
  state: { profiles: Raw[]; components: Raw[]; groups: Raw[] };
  open: boolean;
  onOpenChange: (o: boolean) => void;
}) {
  const [payGroupId, setPayGroupId] = useState("");
  const [payBasis, setPayBasis] = useState<"salary" | "timesheet">("salary");
  const [effectiveFrom, setEffectiveFrom] = useState(
    () => new Date().toISOString().slice(0, 10),
  );
  const [values, setValues] = useState<Raw[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [resolvedComponents, setResolvedComponents] = useState<Raw[]>([]);
  const [resolvedGroups, setResolvedGroups] = useState<Raw[]>([]);

  // Self-healing loader: the dialog fetches its own data when it opens, and
  // uses it to initialize values exactly once per open. It must NEVER re-run
  // the initialization after the user has started editing — so the reset is
  // guarded by a ref that flips when the profile is first applied.
  const initializedRef = useRef(false);
  const latestStateRef = useRef(state);
  latestStateRef.current = state;
  useEffect(() => {
    if (!open || !worker) {
      initializedRef.current = false;
      return;
    }
    let live = true;
    Promise.all([realApi.payrollComponents(), realApi.payrollProfiles(), realApi.payrollPayGroups()])
      .then(([components, profiles, groups]) => {
        if (!live) return;
        setResolvedComponents(Array.isArray(components) ? (components as Raw[]) : []);
        setResolvedGroups(Array.isArray(groups) ? (groups as Raw[]) : []);
        if (initializedRef.current) return; // user already editing — never overwrite
        initializedRef.current = true;
        const snap = latestStateRef.current;
        const wid = String(worker.id ?? "");
        const profilesArr = Array.isArray(profiles) ? (profiles as Raw[]) : [];
        const existing = profilesArr.find((p) => String(p.workerId) === wid);
        const groupList = resolvedGroups.length ? resolvedGroups : snap.groups;
        const defaults = groupList.find((g) => Boolean(g.isDefault))?.id ?? groupList[0]?.id ?? "";
        setPayGroupId(existing ? String(existing.payGroupId ?? "") : String(defaults));
        const basis = String(existing?.payBasis ?? "salary").toLowerCase();
        setPayBasis(basis === "timesheet" ? "timesheet" : "salary");
        setEffectiveFrom(
          existing
            ? String(existing.effectiveFrom ?? new Date().toISOString().slice(0, 10))
            : new Date().toISOString().slice(0, 10),
        );
        const comps = Array.isArray(components) ? (components as Raw[]) : snap.components;
        const statutoryCodes = new Set(
          snap.components
            .filter((c) => Boolean(c.isStatutory) && Boolean(c.isActive))
            .map((c) => String(c.code ?? "")),
        );
        const existingBasis = String(existing?.payBasis ?? "salary").toLowerCase();
        setPayBasis(existingBasis === "timesheet" ? "timesheet" : "salary");
        setValues(
          comps
            .filter((c) => Boolean(c.isActive) && !c.isArchived)
            .map((comp) => {
              const code = String(comp.code ?? "");
              const existingValue = ((existing?.values as Raw[] | undefined) ?? []).find(
                (v) => String(v.componentId) === String(comp.id),
              );
              return {
                componentId: String(comp.id ?? ""),
                code,
                name: String(comp.name ?? code),
                isStatutory: statutoryCodes.has(code),
                isOptional: Boolean(comp.componentType !== "earning"),
                amount: existingValue ? String(existingValue.amount ?? "0") : "0",
              };
            }),
        );
      })
      .catch(() => {
        // Leave the dialog usable with the parent snapshot already passed.
      });
    return () => {
      live = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, worker?.id]);

  if (!worker) return null;
  const workerId = String(worker.id ?? "");
  const workerName = String(worker.fullName ?? `${worker.firstName ?? ""} ${worker.lastName ?? ""}`);
  const openProfile = state.profiles.find((p) => String(p.workerId) === workerId);
  const statutoryCodes = new Set(
    state.components
      .filter((c) => Boolean(c.isStatutory) && Boolean(c.isActive))
      .map((c) => String(c.code ?? "")),
  );

  return (
    <Dialog
      open={open}
      onOpenChange={(o) => {
        onOpenChange(o);
        // The async effect initializes the form exactly once per open, so the
        // synchronous handler never clobbers a user's in-flight selection.
        if (o && !initializedRef.current) initializedRef.current = true;
      }}
    >
      <DialogContent className="max-w-xl">
        <DialogHeader>
          <DialogTitle>{workerName}</DialogTitle>
          <DialogDescription>
            {openProfile
              ? `Updating the open profile effective from ${String(openProfile.effectiveFrom)}. A run calculates on the profile open at the period start.`
              : "No pay profile exists yet — assigning one decides what the next run posts for this worker."}
          </DialogDescription>
        </DialogHeader>
        <form
          className="space-y-4"
          onSubmit={async (e) => {
            e.preventDefault();
            setError(null);
            if (!payGroupId) {
              setError("Pick the pay group this worker runs on.");
              return;
            }
            if (!/^\d{4}-\d{2}-\d{2}$/.test(effectiveFrom)) {
              setError("Effective from must be a date like 2026-09-01.");
              return;
            }
            const payload = values
              .filter((v) => !v.isStatutory || Number(v.amount) > 0)
              .map((v) => ({
                componentId: String(v.componentId),
                amount: Number(v.amount) || 0,
              }));
            const basicId = values.find((v) => String(v.code).toLowerCase() === "basic")?.componentId;
            const basic = basicId ? payload.find((v) => String(v.componentId) === String(basicId)) : undefined;
            if (!basic || !Number(basic.amount)) {
              setError("Basic pay is mandatory — every worker needs a starting basic.");
              return;
            }
            setBusy(true);
            try {
              await realApi.createPayrollProfile(workerId, {
                payGroupId,
                effectiveFrom,
                values: payload,
                payBasis,
              });
              feedback.saved(`${workerName}'s pay structure saved for the ${effectiveFrom} start date.`);
              // Refresh the parent snapshot so the workers table reflects the new profile immediately.
              try {
                const refreshed = await realApi.payrollProfiles();
                const arr = Array.isArray(refreshed) ? (refreshed as Raw[]) : [];
                state.profiles.length = 0;
                state.profiles.push(...arr);
              } catch {
                // Table keeps the older snapshot — harmless.
              }
              onOpenChange(false);
            } catch (err) {
              setError(err instanceof Error ? err.message : "Server rejected the change.");
            } finally {
              setBusy(false);
          }
        }}
      >
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="min-w-0">
            <Label>Pay group</Label>
            <Select value={payGroupId} onValueChange={setPayGroupId}>
              <SelectTrigger className="mt-1.5 w-full">
                <SelectValue placeholder="Pick the pay group this worker runs on" />
              </SelectTrigger>
              <SelectContent>
                {(resolvedGroups.length ? resolvedGroups : state.groups).map((g) => (
                  <SelectItem key={String(g.id)} value={String(g.id)}>
                    {String(g.name ?? g.code)}
                    {Boolean(g.isDefault) ? " — default" : ""}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="min-w-0">
            <Label htmlFor="cp-effective">Effective from</Label>
            <Input
              id="cp-effective"
              type="date"
              value={effectiveFrom}
              onChange={(e) => setEffectiveFrom(e.target.value)}
              className="mt-1.5"
            />
          </div>
        </div>
        <div className="rounded-md border border-border bg-surface-muted p-3">
          <Label className="flex items-center gap-2">
            Pay basis
            <span className="text-xs font-normal text-muted-foreground">— control, not a new pay mode yet</span>
          </Label>
          <div className="mt-2 flex flex-wrap gap-3">
            {(["salary", "timesheet"] as const).map((b) => (
              <label key={b} className="flex cursor-pointer items-center gap-2 text-sm">
                <input
                  type="radio"
                  name="pay-basis"
                  checked={payBasis === b}
                  onChange={() => setPayBasis(b)}
                  aria-label={`Pay basis ${b}`}
                />
                {b === "salary" ? "Salary (monthly, per component)" : "Timesheet (when timesheet pay ships)"}
              </label>
            ))}
          </div>
          <p className="mt-2 text-xs text-muted-foreground">
            Nobody is paid hourly today. “Timesheet” is a planning flag HR can switch on ahead of
            the future timesheet-driven pay feature — every run still calculates salary-basis
            regardless of what is selected here.
          </p>
        </div>
        <div className="space-y-2">
          <Label>Component amounts — the opening figures a run posts to</Label>
          <div className="max-h-72 space-y-2 overflow-y-auto rounded-md border p-3">
            {values.map((v) => (
              <div key={String(v.componentId)} className="flex items-center gap-2">
                <span className={`min-w-0 flex-1 truncate text-sm ${v.isStatutory ? "text-muted-foreground" : ""}`}>
                  {String(v.name ?? v.code)}
                  {v.isStatutory ? (
                    <span className="ml-1.5 align-middle rounded-full border border-info/40 bg-info-soft px-1.5 py-0.5 text-[10px] text-info">
                      statutory
                    </span>
                  ) : null}
                </span>
                {v.isStatutory ? (
                  <span className="text-xs text-muted-foreground">
                    Computed from basic at run time
                  </span>
                ) : (
                  <Input
                    type="number"
                    min={0}
                    step="0.01"
                    value={String(v.amount)}
                    onChange={(e) =>
                      setValues((prev) =>
                        prev.map((x) =>
                          String(x.componentId) === String(v.componentId) ? { ...x, amount: e.target.value } : x,
                        ),
                      )
                    }
                    className="w-28"
                    aria-label={`Amount for ${String(v.name ?? v.code)}`}
                  />
                )}
              </div>
            ))}
            {!values.length ? (
              <p className="py-4 text-center text-xs text-muted-foreground">
                No active components exist yet — configure Salary components first.
              </p>
            ) : null}
          </div>
          <p className="text-xs text-muted-foreground">
            Statutory components (PAYE, NAPSA, NHIMA) compute themselves from basic pay — their
            rates live on the payroll setup screens and are not re-typed per worker.
          </p>
        </div>
        {error ? (
          <p className="rounded-md border border-warning/40 bg-warning-soft px-3 py-2 text-sm text-warning">
            {error}
          </p>
        ) : null}
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
            Cancel
          </Button>
          <Button type="submit" disabled={busy}>
            {busy ? "Saving…" : openProfile ? "Update pay structure" : "Assign pay structure"}
          </Button>
        </div>
      </form>
    </DialogContent>
  </Dialog>
  );
}

/* ------------------------------------------------------------------ */

function CompensationPage() {
  const state = useApi(loadCompensation);
  const userRoles = new Set(useAuth().user?.roles ?? []);
  const canAct = userRoles.has("hr_admin") || userRoles.has("hr_ops") || userRoles.has("payroll");
  const [tab, setTab] = useState<"pay" | "benefits" | "equity">("pay");
  const [editingWorker, setEditingWorker] = useState<Raw | null>(null);
  const [search, setSearch] = useState("");

  const data = state.data ?? { workers: [], profiles: [], components: [], groups: [] };
  const searchTerm = search.trim().toLowerCase();
  const visibleWorkers = data.workers.filter((w) => {
    const key = `${String(w.employeeNo ?? "")} ${String(w.fullName ?? "")} ${String(w.jobTitle ?? "")}`.toLowerCase();
    return !searchTerm || key.includes(searchTerm);
  });

  const profileFor = (w: Raw) => data.profiles.find((p) => String(p.workerId) === String(w.id));

  return (
    <AppShell>
      <PageHeader
        eyebrow="Payroll"
        title="Compensation and benefits"
        description="Pay is restricted data. This view manages the per-worker salary structures the next run calculates from; benefits and review cycles are not yet administered here."
        meta={
          <span className="inline-flex items-center gap-1.5 rounded-full border border-danger/30 bg-danger-soft px-2.5 py-0.5 text-xs font-medium text-danger">
            <ShieldAlert className="size-3.5" aria-hidden />
            Restricted — visible to Payroll and HR admin only
          </span>
        }
      />
      <div className="-mt-4 mb-4 flex justify-end">
        <ImportDialog
          typeKey="payroll-profiles"
          onDone={() => void state.reload()}
          demoSample={[
            { employeeNo: "EMP-001", payGroup: "Monthly ZMW", basic: "45000", "housing-allowance": "6750" },
          ]}
        />
      </div>

      {!USE_REAL ? (
        <p className="flex items-start gap-2 rounded-lg border border-warning/40 bg-warning-soft px-3 py-2 text-sm text-warning">
          <Unplug className="mt-0.5 size-4 shrink-0" aria-hidden />
          Live API is off for this build. The real surfaces exist on the backend — this page shows
          the intended layout without connecting.
        </p>
      ) : null}

      <div className="flex flex-wrap gap-2" role="tablist" aria-label="Compensation views">
        {([
          ["pay", "Pay and bands"],
          ["benefits", "Benefits and insurance"],
          ["equity", "Review cycles and pay gap"],
        ] as const).map(([id, label]) => (
          <button
            key={id}
            role="tab"
            aria-selected={tab === id}
            onClick={() => setTab(id)}
            className={`rounded-full border px-3 py-1 text-sm transition-colors ${
              tab === id
                ? "border-primary bg-primary-soft font-medium text-primary"
                : "bg-surface text-muted-foreground hover:border-border-strong"
            }`}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === "pay" ? (
        <>
          <div className="mt-4 rounded-lg border border-info/30 bg-info-soft p-4 text-sm text-info">
            <p className="flex items-start gap-2 font-medium">
              <Info className="mt-0.5 size-4 shrink-0" aria-hidden />
              The salary structure is what a run posts to
            </p>
            <p className="mt-1.5 pl-6">
              Every active worker should carry an open pay profile: a pay group, an effective date
              and component amounts. Basic pay is mandatory; statutory components (PAYE, NAPSA,
              NHIMA) compute themselves from basic at run time, so they never need re-typing here.
            </p>
          </div>

          <div className="mt-4 flex flex-wrap items-center gap-3">
            <div className="min-w-0 flex-1">
              <Label htmlFor="cp-search" className="sr-only">
                Search workers
              </Label>
              <Input
                id="cp-search"
                placeholder="Search by employee number, name or job title"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="max-w-sm"
              />
            </div>
          </div>

          <Async state={state} rows={5}>
            {(d) => (
              <div className="mt-4 overflow-x-auto rounded-lg border bg-surface">
                <table className="w-full min-w-[48rem] text-left text-sm">
                  <caption className="sr-only">Workers and their open pay profiles</caption>
                  <thead className="border-b bg-surface-muted">
                    <tr>
                      {["Employee", "No.", "Job title", "Pay group", "Effective from", "Pay basis", "Action"].map((h) => (
                        <th
                          key={h}
                          scope="col"
                          className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
                        >
                          {h}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody className="divide-y">
                    {visibleWorkers.map((w) => {
                      const profile = profileFor(w);
                      const group = data.groups.find(
                        (g) => String(g.id) === (profile ? String(profile.payGroupId) : ""),
                      );
                      return (
                        <tr key={String(w.id)} className="hover:bg-surface-muted">
                          <td className="max-w-52 truncate px-3 py-3 font-medium">{String(w.fullName ?? "")}</td>
                          <td className="px-3 py-3 font-mono text-xs text-muted-foreground">{String(w.employeeNo ?? "")}</td>
                          <td className="max-w-48 truncate px-3 py-3 text-muted-foreground">{String(w.jobTitle ?? "—")}</td>
                          <td className="px-3 py-3">{profile ? String(group?.name ?? group?.code ?? "—") : <span className="text-warning">Not assigned</span>}</td>
                          <td className="px-3 py-3 font-mono text-xs">{profile ? String(profile.effectiveFrom) : "—"}</td>
                          <td className="px-3 py-3">
                            {profile && canAct ? (
                              <Select
                                value={String(profile.payBasis ?? "salary").toLowerCase() === "timesheet" ? "timesheet" : "salary"}
                                onValueChange={async (next: string) => {
                                  const basis = next === "timesheet" ? "timesheet" : "salary";
                                  try {
                                    await realApi.setPayBasis(String(w.id), basis);
                                    try {
                                      const refreshed = await realApi.payrollProfiles();
                                      const arr = Array.isArray(refreshed) ? (refreshed as Raw[]) : [];
                                      state.profiles.length = 0;
                                      state.profiles.push(...arr);
                                    } catch {
                                      // Keep older snapshot — harmless.
                                    }
                                    feedback.saved(
                                      basis === "timesheet"
                                        ? "Timesheet flag set — salary-basis pay still applies until timesheet pay ships."
                                        : "Pay basis is salary — standard per-component calculation.",
                                    );
                                  } catch {
                                    feedback.error("Could not update the pay basis.");
                                  }
                                }}
                              >
                                <SelectTrigger className="h-8 w-32">
                                  <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                  <SelectItem value="salary">Salary</SelectItem>
                                  <SelectItem value="timesheet">Timesheet — not live</SelectItem>
                                </SelectContent>
                              </Select>
                            ) : (
                              <span className="text-xs text-muted-foreground">{profile ? String(profile.payBasis ?? "salary") : "—"}</span>
                            )}
                          </td>
                          <td className="px-3 py-3 text-right">
                            {canAct ? (
                              <Button
                                variant="ghost"
                                size="sm"
                                className="h-8"
                                onClick={() => setEditingWorker(w)}
                              >
                                <Pencil className="size-3.5" aria-hidden />
                                Edit pay
                                <span className="sr-only"> pay structure for {String(w.fullName ?? "")}</span>
                              </Button>
                            ) : (
                              <span className="text-xs text-muted-foreground">Read-only</span>
                            )}
                          </td>
                        </tr>
                      );
                    })}
                    {!visibleWorkers.length ? (
                      <tr>
                        <td colSpan={7} className="px-3 py-8 text-center text-sm text-muted-foreground">
                          No workers match{search ? ` "${search}"` : ""}.
                        </td>
                      </tr>
                    ) : null}
                  </tbody>
                </table>
              </div>
            )}
          </Async>

          <p className="mt-3 flex gap-2 text-xs text-muted-foreground">
            <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            Exact amounts stay off the screen by default — opening the edit dialog shows them and
            the change lands in the profile, never on a colleague's file they are not authorised to
            see.
          </p>
        </>
      ) : null}

      {tab === "benefits" ? (
        <section aria-label="Benefits and insurance" className="mt-4">
          <div className="rounded-lg border border-dashed p-10 text-center">
            <p className="text-sm font-medium">Benefits and insurance — coming in a later milestone</p>
            <p className="mx-auto mt-2 max-w-md text-xs text-muted-foreground">
              The backend has no benefit-administration surface yet. When it exists this tab will
              carry NAPSA/NHIMA enrolment, medical cover and insurance claims with the same
              restricted visibility as pay.
            </p>
          </div>
        </section>
      ) : null}

      {tab === "equity" ? (
        <section aria-label="Review cycles and pay gap" className="mt-4">
          <div className="rounded-lg border border-dashed p-10 text-center">
            <p className="text-sm font-medium">Review cycles and pay-gap reporting — coming in a later milestone</p>
            <p className="mx-auto mt-2 max-w-md text-xs text-muted-foreground">
              Compensation review cycles, budgets and pay-gap analytics need grade bands and
              review data the backend does not hold yet. They will appear here once that data
              model ships.
            </p>
          </div>
        </section>
      ) : null}

      <WorkerPayDialog
        worker={editingWorker}
        state={state.data ?? { profiles: [], components: [], groups: [] }}
        open={editingWorker !== null}
        onOpenChange={(o) => {
          if (!o) setEditingWorker(null);
        }}
      />
    </AppShell>
  );
}
