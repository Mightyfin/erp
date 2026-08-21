import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { ShieldCheck, ShieldOff } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { realApi, useApi } from "@/platform/use-api";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/configuration/roles")({
  head: () => ({
    meta: [
      { title: "Roles — New World Cargo HRM" },
      {
        name: "description",
        content:
          "Tenant-scoped HRM role assignments: enable the capabilities your organisation's administrators, payroll officers and approvers need.",
      },
      { property: "og:title", content: "Roles — New World Cargo HRM" },
      {
        property: "og:description",
        content:
          "Tenant-scoped HRM role assignments: enable the capabilities your organisation's administrators, payroll officers and approvers need.",
      },
    ],
  }),
  component: RolesConfig,
});

const description =
  "Roles control what administrators inside this tenant can do. Every role is scoped to this organisation, so switching a role off here does not touch any other tenant's setup. Role changes take effect for new sessions immediately.";

interface RoleRow {
  id: string;
  roleKey: string;
  roleName: string;
  category: string;
  description: string;
  active: boolean;
}

const categoryLabels: Record<string, string> = {
  hrm: "HR administration",
  payroll: "Payroll",
  system: "System",
};

const roleDescriptions: Record<string, string> = {
  hr_admin:
    "Full HR administration: employees, payroll setup, recruitment, relations cases and organisation configuration.",
  hr_ops:
    "Day-to-day HR operations: employee records, leave and time administration, basic onboarding.",
  payroll:
    "Payroll officer: run payroll, maintain components and structures, release payments and correct runs.",
  approver:
    "Approver: decide leave requests, time corrections and any workflow item routed to them.",
  recruitment: "Recruitment: vacancies, candidates, offers and preboarding.",
  relations: "Employee relations: discipline, grievances, safety and labour-case management.",
  auditor: "Read-only auditor: reports, audit logs and compliance evidence without write access.",
};

function RolesConfig() {
  const [tick, setTick] = useState(0);
  const state = useApi(() => realApi.roles(), [tick]);

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Configuration"
          title="Roles"
          description={description}
        />
        <Async state={state}>
          {(rows) => {
            const roleRows: RoleRow[] = ((rows ?? []) as Record<string, unknown>[]).map((r) => ({
              id: String(r.id ?? ""),
              roleKey: String(r.roleKey ?? ""),
              roleName: String(r.roleName ?? r.roleKey ?? ""),
              category: String(r.category ?? "hrm"),
              description:
                roleDescriptions[String(r.roleKey ?? "")] ?? String(r.roleName ?? ""),
              active: Boolean(r.active ?? true),
            }));
            const activeCount = roleRows.filter((r) => r.active).length;

            return (
              <div className="space-y-4">
                <dl className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                  <div className="rounded-lg border bg-surface p-4">
                    <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                      Roles
                    </dt>
                    <dd className="mt-1 text-lg font-semibold">{roleRows.length}</dd>
                  </div>
                  <div className="rounded-lg border bg-surface p-4">
                    <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                      Active
                    </dt>
                    <dd className="mt-1 text-lg font-semibold">{activeCount}</dd>
                  </div>
                  <div className="rounded-lg border bg-surface p-4">
                    <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                      Switched off
                    </dt>
                    <dd className="mt-1 text-lg font-semibold">{roleRows.length - activeCount}</dd>
                  </div>
                </dl>

                <div className="divide-y rounded-lg border bg-surface">
                  {roleRows.map((role) => (
                    <div key={role.roleKey} className="flex items-start gap-4 p-4">
                      {role.active ? (
                        <ShieldCheck aria-hidden className="mt-1 size-5 text-primary" />
                      ) : (
                        <ShieldOff aria-hidden className="mt-1 size-5 text-muted-foreground" />
                      )}
                      <div className="min-w-0 flex-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="font-medium">{role.roleName}</span>
                          <span className="rounded bg-surface-muted px-1.5 py-0.5 text-[11px] font-medium uppercase tracking-wide text-muted-foreground">
                            {categoryLabels[role.category] ?? role.category}
                          </span>
                          <StatusBadge status={role.active ? "active" : "inactive"} />
                        </div>
                        <p className="mt-1 text-sm text-muted-foreground">{role.description}</p>
                        <code className="mt-1 block text-xs text-muted-foreground">
                          {role.roleKey}
                        </code>
                      </div>
                      <div className="flex items-center gap-3 pt-1">
                        <Switch
                          checked={role.active}
                          onCheckedChange={async (next) => {
                            if (next === role.active) return;
                            try {
                              await realApi.updateRole(role.roleKey, { active: next });
                              feedback.submitted(
                                next ? `${role.roleName} enabled` : `${role.roleName} switched off`,
                                next
                                  ? "Users with this role will gain its permissions on their next session."
                                  : "Users with this role keep their existing sessions, but new logins will not receive it.",
                              );
                              setTick((t) => t + 1);
                            } catch (err) {
                              feedback.blocked(
                                "Could not change the role",
                                err instanceof Error ? err.message : "Unknown error",
                              );
                            }
                          }}
                          aria-label={`Toggle ${role.roleName}`}
                        />
                        <Button
                          variant="ghost"
                          size="sm"
                          disabled
                          onClick={() => undefined}
                          title="Direct role-to-user assignment is managed per employee on their profile."
                        >
                          Assign
                        </Button>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            );
          }}
        </Async>
      </AppShell>
    </AuthGate>
  );
}
