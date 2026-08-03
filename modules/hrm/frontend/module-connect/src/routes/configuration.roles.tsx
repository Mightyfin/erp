import { createFileRoute, Link } from "@tanstack/react-router";
import {
  AlertTriangle,
  CheckCircle2,
  EyeOff,
  Info,
  Lock,
  ShieldAlert,
  ShieldCheck,
  XCircle,
} from "lucide-react";
import { useState } from "react";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import type { Capability, DataScope, PermissionMatrix, SodRule } from "@/mock/adminconfig";
import {
  adminConfigApi,
  capabilities,
  dataScopeMeaning,
  dataScopes,
  employeeName,
  roleDefs,
  roleLabel,
} from "@/mock/adminconfig";
import type { Role } from "@/mock/types";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { DetailSection } from "@/platform/components/RecordDetail";
import { StatusTimeline } from "@/platform/components/StatusTimeline";
import { useMock } from "@/platform/use-mock";

const description =
  "Decide what each role may do, how far its data scope reaches, which fields stay masked, and which duties must never sit with the same person.";

export const Route = createFileRoute("/configuration/roles")({
  head: () => ({
    meta: [
      { title: "Roles and permissions — Meridian ERP HRM" },
      { name: "description", content: description },
      { property: "og:title", content: "Roles and permissions — Meridian ERP HRM" },
      { property: "og:description", content: description },
    ],
  }),
  component: RolesConfig,
});

const groups: Capability["group"][] = [
  "Employee records",
  "Requests and approvals",
  "Pay",
  "Administration",
  "Data",
];

const th = "px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground";

const initialMatrix = (): PermissionMatrix =>
  Object.fromEntries(capabilities.map((c) => [c.id, { ...c.grants }]));

const initialScopes = () =>
  Object.fromEntries(roleDefs.map((r) => [r.id, r.scope])) as Record<Role, DataScope>;

/* -------------------------------------------------------------------------- */

function GrantCell({
  capability,
  role,
  allowed,
  onToggle,
}: {
  capability: Capability;
  role: Role;
  allowed: boolean;
  onToggle: () => void;
}) {
  if (capability.namedOnly) {
    return (
      <td className="px-3 py-3 text-center align-top">
        <span className="inline-flex flex-col items-center gap-1 text-xs text-muted-foreground">
          <Lock className="size-4" aria-hidden />
          By name only
        </span>
        <span className="sr-only">
          {capability.label} cannot be granted to the {roleLabel(role)} role.
        </span>
      </td>
    );
  }

  return (
    <td className="px-3 py-3 text-center align-top">
      <span className="inline-flex flex-col items-center gap-1">
        <Checkbox
          checked={allowed}
          onCheckedChange={onToggle}
          aria-label={`${capability.label} — ${roleLabel(role)}`}
        />
        <span className="text-xs text-muted-foreground">{allowed ? "Allowed" : "Not allowed"}</span>
      </span>
    </td>
  );
}

/* -------------------------------------------------------------------------- */

function SodCard({ rule, matrix }: { rule: SodRule; matrix: PermissionMatrix }) {
  const [a, b] = rule.pair;
  const capA = capabilities.find((c) => c.id === a);
  const capB = capabilities.find((c) => c.id === b);
  const rolesWithBoth = roleDefs.filter((r) => matrix[a][r.id] && matrix[b][r.id]);
  const open = !rule.mitigated;

  return (
    <li
      className={`rounded-lg border p-4 ${
        open ? "border-warning/40 bg-warning-soft" : "border-border bg-surface"
      }`}
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <h3 className={`text-sm font-semibold ${open ? "text-warning" : ""}`}>{rule.title}</h3>
        <span
          className={`inline-flex shrink-0 items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium ${
            open
              ? "border-warning/40 bg-warning-soft text-warning"
              : "border-success/30 bg-success-soft text-success"
          }`}
        >
          {open ? (
            <AlertTriangle className="size-3.5" aria-hidden />
          ) : (
            <ShieldCheck className="size-3.5" aria-hidden />
          )}
          {open ? "Conflict to resolve" : "Mitigated"}
        </span>
      </div>

      <p className="mt-2 text-sm">
        <span className="font-medium">Conflicting duties: </span>
        {capA?.label} and {capB?.label}.
      </p>
      {rolesWithBoth.length ? (
        <p className="mt-1.5 flex items-start gap-1.5 text-sm text-warning">
          <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
          <span>
            One role carries both duties on its own: {rolesWithBoth.map((r) => r.label).join(", ")}.
            Anyone given that role inherits the conflict, whatever else they hold.
          </span>
        </p>
      ) : null}
      <p className="mt-1.5 text-sm">
        <span className="font-medium">Why it matters: </span>
        {rule.risk}
      </p>

      <dl className="mt-3 grid gap-x-6 gap-y-2 text-xs sm:grid-cols-2">
        <div>
          <dt className="text-muted-foreground">Roles holding both</dt>
          <dd className="font-medium">
            {rolesWithBoth.length
              ? rolesWithBoth.map((r) => r.label).join(", ")
              : "None — no single role carries both"}
          </dd>
        </div>
        <div>
          <dt className="text-muted-foreground">People holding both</dt>
          <dd className="font-medium">
            {rule.holders.length ? (
              <ul className="space-y-0.5">
                {rule.holders.map((h) => (
                  <li key={h.employeeId}>
                    <Link
                      to="/employees/$id"
                      params={{ id: h.employeeId }}
                      className="text-primary underline-offset-2 hover:underline"
                    >
                      {employeeName(h.employeeId)}
                    </Link>{" "}
                    <span className="font-normal text-muted-foreground">
                      ({h.roles.map(roleLabel).join(" + ")})
                    </span>
                  </li>
                ))}
              </ul>
            ) : (
              "Nobody"
            )}
          </dd>
        </div>
      </dl>

      <p className="mt-3 text-sm">
        <span className="font-medium">Required control: </span>
        {rule.control}
      </p>
      {rule.mitigation ? (
        <p className="mt-1.5 text-xs text-muted-foreground">
          <span className="font-medium text-foreground">Accepted risk: </span>
          {rule.mitigation}
        </p>
      ) : null}
    </li>
  );
}

/* -------------------------------------------------------------------------- */

function RolesConfig() {
  const state = useMock(() => adminConfigApi.security());
  const [matrix, setMatrix] = useState<PermissionMatrix>(initialMatrix);
  const [scopes, setScopes] = useState<Record<Role, DataScope>>(initialScopes);
  const [confirm, setConfirm] = useState<{ capId: string; role: Role } | null>(null);

  const apply = (capId: string, role: Role, value: boolean) =>
    setMatrix((m) => ({ ...m, [capId]: { ...m[capId], [role]: value } }));

  const requestToggle = (capability: Capability, role: Role) => {
    const current = matrix[capability.id][role];
    if (capability.namedOnly) return;
    if (current && capability.administrative) {
      const holders = roleDefs.filter((r) => matrix[capability.id][r.id]);
      if (holders.length <= 1) {
        setConfirm({ capId: capability.id, role });
        return;
      }
    }
    apply(capability.id, role, !current);
  };

  const grantChanges = capabilities.flatMap((c) =>
    roleDefs
      .filter((r) => matrix[c.id][r.id] !== c.grants[r.id])
      .map((r) => ({
        id: `${c.id}-${r.id}`,
        text: `${matrix[c.id][r.id] ? "Grant" : "Remove"} “${c.label}” ${
          matrix[c.id][r.id] ? "to" : "from"
        } ${r.label}`,
      })),
  );
  const scopeChanges = roleDefs
    .filter((r) => scopes[r.id] !== r.scope)
    .map((r) => ({
      id: `scope-${r.id}`,
      text: `Change ${r.label} data scope from ${r.scope} to ${scopes[r.id]}`,
    }));
  const pending = [...grantChanges, ...scopeChanges];

  const confirmCapability = confirm ? capabilities.find((c) => c.id === confirm.capId) : undefined;

  return (
    <AppShell>
      <PageHeader
        eyebrow="Configuration"
        title="Roles and permissions"
        description={description}
        primaryAction={
          <Button disabled={pending.length === 0}>
            {pending.length === 0
              ? "No changes to submit"
              : `Submit ${pending.length} change${pending.length === 1 ? "" : "s"} for approval`}
          </Button>
        }
        meta={
          <span className="rounded-full border bg-surface-muted px-2.5 py-0.5 text-xs text-muted-foreground">
            5 roles · {capabilities.length} capabilities · 6 masked fields
          </span>
        }
      />

      <div className="space-y-6">
        <div className="rounded-lg border border-info/30 bg-info-soft p-4 text-sm text-info">
          <p className="flex items-start gap-2 font-medium">
            <Info className="mt-0.5 size-4 shrink-0" aria-hidden />
            Every permission change is recorded, and nothing here is saved in this build
          </p>
          <p className="mt-1.5 pl-6">
            A change records who made it, when, what it was before and what it became. In this
            prototype the toggles below only change what you see on this screen: nothing is written
            anywhere, and reloading the page restores the current configuration.
          </p>
        </div>

        <Async state={state} rows={6}>
          {(data) => (
            <div className="space-y-6">
              <DetailSection
                title="Roles in use"
                description="Five roles, each held by named people. A role is a job to be done, not a person — removing the last holder leaves the role in place but nobody able to do the work."
              >
                <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                  {data.roles.map((r) => (
                    <li key={r.id} className="rounded-lg border bg-surface p-4">
                      <div className="flex flex-wrap items-start justify-between gap-2">
                        <h3 className="text-sm font-semibold">{r.label}</h3>
                        {r.administrative ? (
                          <span className="inline-flex shrink-0 items-center gap-1.5 rounded-full border border-warning/40 bg-warning-soft px-2 py-0.5 text-[11px] font-medium text-warning">
                            <ShieldAlert className="size-3" aria-hidden />
                            Administrative
                          </span>
                        ) : null}
                      </div>
                      <p className="mt-1 text-xs text-muted-foreground">{r.purpose}</p>
                      <p className="mt-3 text-sm font-medium">
                        {r.holderIds.length}{" "}
                        {r.holderIds.length === 1 ? "person holds" : "people hold"} this role
                      </p>
                      <ul className="mt-1 space-y-0.5 text-xs">
                        {r.holderIds.slice(0, 3).map((id) => (
                          <li key={id}>
                            <Link
                              to="/employees/$id"
                              params={{ id }}
                              className="text-primary underline-offset-2 hover:underline"
                            >
                              {employeeName(id)}
                            </Link>
                          </li>
                        ))}
                        {r.holderIds.length > 3 ? (
                          <li className="text-muted-foreground">
                            and {r.holderIds.length - 3} more
                          </li>
                        ) : null}
                      </ul>
                      {r.holderIds.length === 1 ? (
                        <p className="mt-2 flex items-start gap-1.5 text-xs text-warning">
                          <AlertTriangle className="mt-0.5 size-3 shrink-0" aria-hidden />
                          Single holder. If this person is unavailable, nobody can do this work.
                        </p>
                      ) : null}
                    </li>
                  ))}
                </ul>
              </DetailSection>

              <DetailSection
                title="Permission matrix"
                description="What each role may do. Ticking a box changes the action permission only — how many records the role can act on is set separately, under data scope."
              >
                <div className="overflow-x-auto rounded-lg border">
                  <table className="w-full min-w-[52rem] text-left text-sm">
                    <caption className="sr-only">
                      Capabilities down the side, the five roles across the top. Each cell says
                      whether the role is allowed the capability.
                    </caption>
                    <thead className="border-b bg-surface-muted">
                      <tr>
                        <th scope="col" className={th}>
                          Capability
                        </th>
                        {data.roles.map((r) => (
                          <th key={r.id} scope="col" className={`${th} text-center`}>
                            <span className="block">{r.label}</span>
                            <span className="block text-[11px] font-normal normal-case tracking-normal">
                              {r.holderIds.length} {r.holderIds.length === 1 ? "holder" : "holders"}
                            </span>
                          </th>
                        ))}
                      </tr>
                    </thead>
                    {groups.map((g) => {
                      const rows = data.capabilities.filter((c) => c.group === g);
                      if (!rows.length) return null;
                      return (
                        <tbody key={g} className="divide-y border-b last:border-b-0">
                          <tr className="bg-surface-muted/60">
                            <th
                              scope="colgroup"
                              colSpan={data.roles.length + 1}
                              className="px-3 py-1.5 text-left text-xs font-semibold"
                            >
                              {g}
                            </th>
                          </tr>
                          {rows.map((c) => (
                            <tr key={c.id} className="hover:bg-surface-muted">
                              <th scope="row" className="px-3 py-3 text-left align-top font-medium">
                                <span className="block max-w-72">{c.label}</span>
                                <span className="mt-0.5 block max-w-72 text-xs font-normal text-muted-foreground">
                                  {c.description}
                                </span>
                                {c.note ? (
                                  <span className="mt-1 block max-w-72 text-xs font-normal text-muted-foreground">
                                    {c.note}
                                  </span>
                                ) : null}
                              </th>
                              {data.roles.map((r) => (
                                <GrantCell
                                  key={r.id}
                                  capability={c}
                                  role={r.id}
                                  allowed={matrix[c.id][r.id]}
                                  onToggle={() => requestToggle(c, r.id)}
                                />
                              ))}
                            </tr>
                          ))}
                        </tbody>
                      );
                    })}
                  </table>
                </div>
                <p aria-live="polite" className="mt-3 text-xs text-muted-foreground">
                  {pending.length === 0
                    ? "No unsaved changes on this screen."
                    : `${pending.length} unsaved change${
                        pending.length === 1 ? "" : "s"
                      } held in this browser only.`}
                </p>
              </DetailSection>

              <DetailSection
                title="Data scope"
                description="How far each role reaches. Permission answers what the holder may do; scope answers how many people they may do it to. A manager who may approve leave still only sees their own reporting line."
              >
                <div className="overflow-x-auto rounded-lg border">
                  <table className="w-full min-w-[44rem] text-left text-sm">
                    <caption className="sr-only">Data scope for each role</caption>
                    <thead className="border-b bg-surface-muted">
                      <tr>
                        <th scope="col" className={th}>
                          Role
                        </th>
                        <th scope="col" className={th}>
                          Scope
                        </th>
                        <th scope="col" className={th}>
                          What that means
                        </th>
                        <th scope="col" className={th}>
                          Note
                        </th>
                      </tr>
                    </thead>
                    <tbody className="divide-y">
                      {data.roles.map((r) => (
                        <tr key={r.id} className="align-top">
                          <th scope="row" className="px-3 py-3 text-left font-medium">
                            {r.label}
                          </th>
                          <td className="px-3 py-3">
                            <Select
                              value={scopes[r.id]}
                              onValueChange={(v) =>
                                setScopes((s) => ({ ...s, [r.id]: v as DataScope }))
                              }
                              disabled={r.id === "employee"}
                            >
                              <SelectTrigger
                                className="w-full min-w-44"
                                aria-label={`Data scope for ${r.label}`}
                              >
                                <SelectValue />
                              </SelectTrigger>
                              <SelectContent>
                                {dataScopes.map((s) => (
                                  <SelectItem key={s} value={s}>
                                    {s}
                                  </SelectItem>
                                ))}
                              </SelectContent>
                            </Select>
                            {scopes[r.id] !== r.scope ? (
                              <span className="mt-1 block text-xs text-warning">
                                Changed on this screen only — was {r.scope}
                              </span>
                            ) : null}
                          </td>
                          <td className="px-3 py-3">
                            <span className="block max-w-64">{dataScopeMeaning[scopes[r.id]]}</span>
                          </td>
                          <td className="px-3 py-3">
                            <span className="block max-w-72 text-xs text-muted-foreground">
                              {r.id === "employee"
                                ? "Fixed. The Employee role can never be widened beyond the holder's own record."
                                : r.scopeNote}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </DetailSection>

              <DetailSection
                title="Segregation of duties"
                description="Combinations that must not sit with one person. These are checked against the matrix above every time it changes, so a conflict surfaces before it is submitted, not at the next audit."
              >
                <ul className="space-y-3">
                  {data.sod.map((rule) => (
                    <SodCard key={rule.id} rule={rule} matrix={matrix} />
                  ))}
                </ul>
              </DetailSection>

              <DetailSection
                title="Sensitive field masking"
                description="Masking sits underneath the permission matrix. A role may be allowed to open an employee record and still see nothing but the last four characters of a bank account."
              >
                <div className="overflow-x-auto rounded-lg border">
                  <table className="w-full min-w-[48rem] text-left text-sm">
                    <caption className="sr-only">
                      Sensitive fields, which roles can unmask them, and what happens on reveal
                    </caption>
                    <thead className="border-b bg-surface-muted">
                      <tr>
                        <th scope="col" className={th}>
                          Field
                        </th>
                        <th scope="col" className={th}>
                          Can unmask
                        </th>
                        <th scope="col" className={th}>
                          Masked for
                        </th>
                        <th scope="col" className={th}>
                          Rule
                        </th>
                      </tr>
                    </thead>
                    <tbody className="divide-y">
                      {data.masking.map((f) => {
                        const maskedFor = data.roles.filter((r) => !f.visibleTo.includes(r.id));
                        return (
                          <tr key={f.id} className="align-top">
                            <th scope="row" className="px-3 py-3 text-left font-medium">
                              <span className="block">{f.label}</span>
                              <span className="mt-0.5 block text-xs font-normal text-muted-foreground">
                                {f.category}
                              </span>
                            </th>
                            <td className="px-3 py-3">
                              {f.namedHandlersOnly ? (
                                <span className="inline-flex items-center gap-1.5 text-xs font-medium">
                                  <Lock className="size-3.5 shrink-0" aria-hidden />
                                  Named handlers only
                                </span>
                              ) : f.visibleTo.length ? (
                                <span className="inline-flex items-start gap-1.5">
                                  <CheckCircle2 className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                                  {f.visibleTo.map(roleLabel).join(", ")}
                                </span>
                              ) : (
                                <span className="inline-flex items-start gap-1.5 text-xs font-medium">
                                  <XCircle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                                  No role
                                </span>
                              )}
                            </td>
                            <td className="px-3 py-3">
                              <span className="inline-flex items-start gap-1.5 text-xs">
                                <EyeOff className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                                {maskedFor.length === data.roles.length
                                  ? "All five roles"
                                  : maskedFor.map((r) => r.label).join(", ")}
                              </span>
                            </td>
                            <td className="px-3 py-3">
                              <span className="block max-w-96 text-xs">{f.rule}</span>
                              <span className="mt-1 block max-w-96 text-xs text-muted-foreground">
                                {f.onReveal}
                              </span>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              </DetailSection>

              <DetailSection
                title="Protected disclosure handling"
                description="Speak-up reports are handled outside the ordinary HR line. This is a product rule, not a preference an administrator can change."
              >
                <div className="space-y-4">
                  <p className="flex items-start gap-2 rounded-md border border-warning/40 bg-warning-soft px-3 py-2 text-sm text-warning">
                    <ShieldAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
                    <span>
                      Handling protected disclosures cannot be attached to any role, including HR
                      admin. An HR administrator can grant themselves configuration rights, so if
                      the capability sat on the role, the person a disclosure is about could read
                      it. It is granted to named individuals only, and each appointment is reported
                      to the audit committee.
                    </span>
                  </p>
                  <ul className="grid gap-3 sm:grid-cols-2">
                    {data.handlers.map((h) => (
                      <li key={h.id} className="rounded-lg border bg-surface p-4">
                        <div className="flex flex-wrap items-start justify-between gap-2">
                          <div className="min-w-0">
                            <h3 className="text-sm font-semibold">
                              {h.employeeId ? (
                                <Link
                                  to="/employees/$id"
                                  params={{ id: h.employeeId }}
                                  className="text-primary underline-offset-2 hover:underline"
                                >
                                  {h.name}
                                </Link>
                              ) : (
                                h.name
                              )}
                            </h3>
                            <p className="mt-0.5 text-xs text-muted-foreground">{h.title}</p>
                          </div>
                          <span className="inline-flex shrink-0 items-center gap-1.5 rounded-full border bg-surface-muted px-2 py-0.5 text-[11px] font-medium text-muted-foreground">
                            <ShieldCheck className="size-3" aria-hidden />
                            {h.independent ? "Independent" : "Named handler"}
                          </span>
                        </div>
                        <p className="mt-2 text-xs text-muted-foreground">{h.note}</p>
                        <p className="mt-2 text-xs">
                          Appointed{" "}
                          {new Date(`${h.appointedOn}T00:00:00`).toLocaleDateString("en-GB", {
                            day: "numeric",
                            month: "long",
                            year: "numeric",
                          })}
                        </p>
                      </li>
                    ))}
                  </ul>
                  <p className="text-xs text-muted-foreground">
                    Reporters may stay anonymous. Where a reporter gives their name, only the two
                    handlers above can see it, and the case never appears in an employee record, a
                    manager's queue or an export.
                  </p>
                </div>
              </DetailSection>

              <DetailSection
                title="Change history"
                description="Permission changes are audited in the same way as employee data. The trail cannot be edited or removed by an administrator."
              >
                {pending.length ? (
                  <div className="mb-4 rounded-md border border-warning/40 bg-warning-soft p-3">
                    <p className="flex items-start gap-2 text-sm font-medium text-warning">
                      <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />
                      {pending.length} change{pending.length === 1 ? "" : "s"} waiting to be
                      submitted
                    </p>
                    <ul className="mt-2 list-disc space-y-1 pl-9 text-sm text-warning">
                      {pending.map((c) => (
                        <li key={c.id}>{c.text}</li>
                      ))}
                    </ul>
                    <p className="mt-2 pl-9 text-xs text-warning">
                      These are local to this browser session. They would only enter the audit trail
                      once submitted and approved.
                    </p>
                  </div>
                ) : null}
                <StatusTimeline events={data.audit} title="Recent permission changes" />
              </DetailSection>
            </div>
          )}
        </Async>
      </div>

      <AlertDialog open={confirm !== null} onOpenChange={(open) => !open && setConfirm(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              This removes the last role that can {confirmCapability?.label.toLowerCase()}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {confirmCapability?.label} is an administrative capability, and{" "}
              {confirm ? roleLabel(confirm.role) : ""} is the only role that still holds it.
              Removing it would leave nobody able to change the organisation structure, policies,
              routing or permissions — including nobody able to grant this permission back. Recovery
              would need a support intervention.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Keep the permission</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                if (confirm) apply(confirm.capId, confirm.role, false);
                setConfirm(null);
              }}
            >
              Remove it anyway
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </AppShell>
  );
}
