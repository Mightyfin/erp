import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { AlertTriangle, Ban, Info, Lock, Plus, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { money, payrollRunApi } from "@/mock/payrollrun";
import type { LineComponent, RunLine } from "@/mock/payrollrun";
import { isEditableSource } from "@/mock/payrollrun";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { EditPage } from "@/platform/components/EditPage";
import type { EditSection } from "@/platform/components/EditPage";
import { RestrictedState } from "@/platform/components/States";
import { markRunStale } from "@/mock/calculation";
import { feedback } from "@/platform/feedback";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/payroll/runs/$id/edit")({
  head: () => ({
    meta: [
      { title: "Edit pay run — Mightyfin ERP HRM" },
      { name: "description", content: "Edit a pay run: period and dates, population, adjustments to individual pay lines, and the note the approver sees." },
      { property: "og:title", content: "Edit pay run — Mightyfin ERP HRM" },
      { property: "og:description", content: "Edit period, population, pay-line adjustments and approver notes on a pay run." },
    ],
  }),
  component: EditRun,
});

/* -------------------------------------------------------------------------- */

/** One employee's pay line, expanded so its components can be adjusted. */
function LineRow({
  line,
  currency,
  adjustment,
  onAdjust,
  onRemove,
  locked,
}: {
  line: RunLine;
  currency: string;
  adjustment: { label: string; amount: string } | undefined;
  onAdjust: (v: { label: string; amount: string } | undefined) => void;
  onRemove: () => void;
  locked: boolean;
}) {
  const [open, setOpen] = useState(false);
  const adjAmount = Number(adjustment?.amount ?? 0) || 0;
  const newNet = line.net + adjAmount;
  const variance =
    line.priorNet && line.priorNet > 0 ? ((newNet - line.priorNet) / line.priorNet) * 100 : null;
  const material = variance !== null && Math.abs(variance) >= 2;

  return (
    <li className="rounded-lg border bg-surface">
      <div className="flex flex-wrap items-start gap-3 p-3">
        <button
          type="button"
          onClick={() => setOpen((o) => !o)}
          aria-expanded={open}
          className="min-w-0 flex-1 text-left"
        >
          <span className="block text-sm font-medium">{line.employee}</span>
          <span className="block text-xs text-muted-foreground">
            {line.jobTitle} · {line.grade} · {open ? "Hide" : "Show"} the {line.components.length}{" "}
            components
          </span>
        </button>

        <div className="shrink-0 text-right">
          <span className="tabular block text-sm font-medium">{money(newNet, currency)}</span>
          <span className="block text-xs text-muted-foreground">
            net{adjAmount ? ` (was ${money(line.net, currency)})` : ""}
          </span>
        </div>
      </div>

      {line.flags.length ? (
        <ul className="space-y-1 px-3 pb-2">
          {line.flags.map((f) => (
            <li key={f} className="flex gap-1.5 text-xs text-warning">
              <AlertTriangle className="mt-0.5 size-3 shrink-0" aria-hidden />
              {f}
            </li>
          ))}
        </ul>
      ) : null}

      {material ? (
        <p className="flex gap-1.5 px-3 pb-2 text-xs text-warning">
          <AlertTriangle className="mt-0.5 size-3 shrink-0" aria-hidden />
          Net pay moves {variance! > 0 ? "up" : "down"} {Math.abs(variance!).toFixed(1)}% on last
          period. Material, so the approver will ask why.
        </p>
      ) : null}

      {open ? (
        <div className="border-t px-3 py-3">
          <table className="w-full text-left text-xs">
            <caption className="sr-only">Pay components for {line.employee}</caption>
            <thead>
              <tr className="text-muted-foreground">
                <th scope="col" className="pb-1 font-medium">Component</th>
                <th scope="col" className="pb-1 font-medium">How it is calculated</th>
                <th scope="col" className="pb-1 text-right font-medium">Amount</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {line.components.map((c: LineComponent) => (
                <tr key={c.code}>
                  <th scope="row" className="py-1.5 font-normal">
                    <span className="font-medium">{c.label}</span>
                    {!isEditableSource(c.source) ? (
                      <span className="ml-1.5 inline-flex items-center gap-1 text-[11px] text-muted-foreground">
                        <Lock className="size-3 shrink-0" aria-hidden />
                        Statutory
                      </span>
                    ) : null}
                  </th>
                  <td className="py-1.5 text-muted-foreground">{c.basis}</td>
                  <td
                    className={`tabular py-1.5 text-right ${
                      c.kind === "Deduction" ? "text-muted-foreground" : ""
                    }`}
                  >
                    {c.kind === "Deduction" ? "−" : ""}
                    {money(c.amount, currency)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          <p className="mt-2 flex gap-1.5 text-xs text-muted-foreground">
            <Info className="mt-0.5 size-3 shrink-0" aria-hidden />
            A statutory line is recalculated from the country pack, never typed in. To change one,
            change the pack or the input it reads.
          </p>
        </div>
      ) : null}

      <div className="border-t px-3 py-3">
        {adjustment ? (
          <div className="flex flex-wrap items-end gap-2">
            <div className="min-w-0 flex-1">
              <label htmlFor={`adj-label-${line.employeeId}`} className="text-xs font-medium">
                One-off adjustment
              </label>
              <Input
                id={`adj-label-${line.employeeId}`}
                className="mt-1 h-8 text-sm"
                value={adjustment.label}
                placeholder="Why this employee is paid something extra or less"
                onChange={(e) => onAdjust({ ...adjustment, label: e.target.value })}
              />
            </div>
            <div className="w-32">
              <label htmlFor={`adj-amount-${line.employeeId}`} className="text-xs font-medium">
                Amount ({currency})
              </label>
              <Input
                id={`adj-amount-${line.employeeId}`}
                type="number"
                step="0.01"
                className="mt-1 h-8 text-sm"
                value={adjustment.amount}
                onChange={(e) => onAdjust({ ...adjustment, amount: e.target.value })}
              />
            </div>
            <Button
              variant="ghost"
              size="sm"
              className="h-8 gap-1.5 text-xs"
              onClick={() => {
                onAdjust(undefined);
                onRemove();
              }}
            >
              <Trash2 className="size-3.5" aria-hidden />
              Remove
            </Button>
          </div>
        ) : (
          <Button
            variant="outline"
            size="sm"
            className="h-8 gap-1.5 text-xs"
            disabled={locked}
            onClick={() => onAdjust({ label: "", amount: "" })}
          >
            <Plus className="size-3.5" aria-hidden />
            Add a one-off adjustment
          </Button>
        )}
        {locked ? (
          <p className="mt-1.5 flex gap-1.5 text-xs text-warning">
            <Lock className="mt-0.5 size-3 shrink-0" aria-hidden />
            This run is approved, so pay lines are frozen. Reopen it to adjust anyone.
          </p>
        ) : null}
      </div>
    </li>
  );
}

/* -------------------------------------------------------------------------- */

function EditRun() {
  const { id } = Route.useParams();
  const navigate = useNavigate();
  const state = useMock(() => payrollRunApi.run(id), [id]);
  const lines = useMock(() => payrollRunApi.linesFor(id), [id]);

  const [adjustments, setAdjustments] = useState<
    Record<string, { label: string; amount: string }>
  >({});
  const [dropped, setDropped] = useState<string[]>([]);

  return (
    <AppShell>
      <Async state={state} rows={4}>
        {(run) => {
          if (!run) return <RestrictedState />;

          const locked = run.status === "Approved" || run.status === "Paid" || run.status === "Closed";
          const rows = (lines.data ?? []).filter((l) => !dropped.includes(l.employeeId));
          const adjTotal = Object.values(adjustments).reduce(
            (t, a) => t + (Number(a.amount) || 0),
            0,
          );

          const sections: EditSection[] = [
            {
              id: "period",
              title: "Period and dates",
              description:
                "The dates that bound the run. Moving the cutoff changes which approved time is picked up.",
              fields: [
                {
                  name: "payGroup",
                  label: "Pay group",
                  required: true,
                  hint: "Employees are pulled from this group when the run is refreshed.",
                },
                { name: "cutoff", label: "Time cutoff", type: "date", required: true },
                {
                  name: "payDate",
                  label: "Pay date",
                  type: "date",
                  required: true,
                  validate: (v, all) =>
                    v && all.cutoff && v < all.cutoff
                      ? "The pay date cannot be before the time cutoff, or approved time would miss this run."
                      : null,
                },
                {
                  name: "dueDate",
                  label: "Approval due by",
                  type: "date",
                  required: true,
                  validate: (v, all) =>
                    v && all.payDate && v > all.payDate
                      ? "Approval has to be due before the pay date — there is no time to pay otherwise."
                      : null,
                },
              ],
            },
            {
              id: "lines",
              title: "Pay lines",
              description:
                "One line per employee. Open a line to see how its figures were derived; statutory components are calculated, not typed.",
              render: () => (
                <div className="space-y-3">
                  <ul className="space-y-2">
                    {rows.map((l) => (
                      <LineRow
                        key={l.id}
                        line={l}
                        currency={run.currency}
                        locked={locked}
                        adjustment={adjustments[l.employeeId]}
                        onAdjust={(v) =>
                          setAdjustments((s) => {
                            const next = { ...s };
                            if (v) next[l.employeeId] = v;
                            else delete next[l.employeeId];
                            return next;
                          })
                        }
                        onRemove={() => undefined}
                      />
                    ))}
                  </ul>

                  {adjTotal !== 0 ? (
                    <p className="rounded-md border border-info/40 bg-info-soft p-3 text-xs text-info">
                      Adjustments change net pay by {money(adjTotal, run.currency)} across{" "}
                      {Object.keys(adjustments).length} employee
                      {Object.keys(adjustments).length === 1 ? "" : "s"}. Each one needs a reason
                      before the run can be approved.
                    </p>
                  ) : null}
                </div>
              ),
            },
            {
              id: "population",
              title: "Population",
              description:
                "Taking someone out of a run is a decision with a reason, not a deletion.",
              render: () => (
                <div className="space-y-3">
                  <ul className="space-y-2">
                    {(lines.data ?? []).map((l) => {
                      const out = dropped.includes(l.employeeId);
                      return (
                        <li
                          key={l.employeeId}
                          className="flex flex-wrap items-center justify-between gap-3 rounded-md border p-3"
                        >
                          <span className="min-w-0 flex-1">
                            <span className="block text-sm font-medium">{l.employee}</span>
                            <span className="block text-xs text-muted-foreground">
                              {out ? "Excluded from this run" : `${l.jobTitle} · included`}
                            </span>
                          </span>
                          <Button
                            variant="outline"
                            size="sm"
                            disabled={locked}
                            onClick={() =>
                              setDropped((d) =>
                                out ? d.filter((x) => x !== l.employeeId) : [...d, l.employeeId],
                              )
                            }
                          >
                            {out ? "Put back in" : "Exclude"}
                          </Button>
                        </li>
                      );
                    })}
                  </ul>

                  {run.excluded.length ? (
                    <div className="rounded-md border bg-surface-muted p-3">
                      <p className="text-xs font-medium">Already excluded, with reasons</p>
                      <ul className="mt-1.5 space-y-1">
                        {run.excluded.map((x) => (
                          <li key={x.employee} className="flex gap-1.5 text-xs">
                            <Ban className="mt-0.5 size-3 shrink-0 text-muted-foreground" aria-hidden />
                            <span>
                              <span className="font-medium">{x.employee}</span> — {x.reason}
                            </span>
                          </li>
                        ))}
                      </ul>
                    </div>
                  ) : null}
                </div>
              ),
            },
            {
              id: "notes",
              title: "Notes for the approver",
              description:
                "Whoever approves this run reads this first. Explain anything that moved.",
              fields: [
                {
                  name: "approverNote",
                  label: "Note",
                  type: "textarea",
                  hint: "A material variance without an explanation will come straight back to you.",
                },
              ],
            },
          ];

          return (
            <EditPage
              title={`${run.period} — ${run.payGroup}`}
              reference={run.id}
              description={`${run.entityName}. Changes apply to this run only and are recorded in its audit trail.`}
              sections={sections}
              initial={{
                payGroup: run.payGroup,
                cutoff: "2026-08-24",
                payDate: "2026-08-28",
                dueDate: run.dueDate,
                approverNote: "",
              }}
              extraChanges={[
                ...Object.entries(adjustments).map(([employeeId, a]) => {
                  const who = (lines.data ?? []).find((l) => l.employeeId === employeeId);
                  return {
                    id: `adj-${employeeId}`,
                    label: `Adjustment for ${who?.employee ?? employeeId}`,
                    detail: `${money(Number(a.amount) || 0, run.currency)}${
                      a.label.trim() ? ` — ${a.label.trim()}` : " — no reason given yet"
                    }`,
                  };
                }),
                ...dropped.map((employeeId) => {
                  const who = (lines.data ?? []).find((l) => l.employeeId === employeeId);
                  return {
                    id: `drop-${employeeId}`,
                    label: "Excluded from this run",
                    detail: who?.employee ?? employeeId,
                  };
                }),
              ]}
              saveLabel="Save the run"
              footerNote={
                locked
                  ? "This run is approved — dates and notes can still be corrected, pay lines cannot."
                  : "Saving does not recalculate. Run the calculation again to pick up a changed input."
              }
              onCancel={() => navigate({ to: "/hrm/payroll/runs/$id", params: { id } })}
              onSave={(_values, changed) => {
                const unexplained = Object.entries(adjustments).filter(([, a]) => !a.label.trim());
                if (unexplained.length) {
                  feedback.blocked(
                    "Every adjustment needs a reason",
                    `${unexplained.length} one-off adjustment${
                      unexplained.length === 1 ? " has" : "s have"
                    } no explanation. An approver cannot sign off a figure nobody can account for.`,
                  );
                  return;
                }
                const total =
                  changed.length + Object.keys(adjustments).length + dropped.length;
                // The figures on the run no longer reflect what was just saved.
                markRunStale(
                  run.id,
                  `${total} change${total === 1 ? "" : "s"} were saved after the last calculation.`,
                );
                feedback.saved(
                  `${run.id} updated — ${total} change${total === 1 ? "" : "s"}.`,
                  () => feedback.note("Changes to the run reverted."),
                );
                navigate({ to: "/hrm/payroll/runs/$id", params: { id } });
              }}
            />
          );
        }}
      </Async>
    </AppShell>
  );
}
