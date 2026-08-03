import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { AlertTriangle, Building2, KeyRound, LifeBuoy, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { workspaces } from "@/mock/data";
import type { Role } from "@/mock/types";
import { useApp } from "@/platform/app-context";

export const Route = createFileRoute("/sign-in")({
  head: () => ({
    meta: [
      { title: "Sign in — Meridian ERP" },
      { name: "description", content: "Sign in to the HR workspace." },
      { property: "og:title", content: "Sign in — Meridian ERP" },
      { property: "og:description", content: "Sign in to the HR workspace." },
    ],
  }),
  component: SignIn,
});

/**
 * UI-FND-001. Identity is owned by the organisation's identity provider, not by
 * HRM — this screen only collects the handover and resolves it to an employee
 * record. No password is ever stored or validated here; the primary route is a
 * redirect to the IdP. The email/role form below exists solely so this mock
 * build can be explored without one.
 */
function SignIn() {
  const navigate = useNavigate();
  const { setRole } = useApp();
  const [email, setEmail] = useState("amara.cvdb@meridian.co.zm");
  const [role, setLocalRole] = useState<Role>("hr_admin");
  const [busy, setBusy] = useState(false);

  const enter = (as: Role) => {
    setBusy(true);
    setRole(as);
    navigate({ to: "/" });
  };

  return (
    <div className="grid min-h-screen lg:grid-cols-2">
      {/* Brand / context panel */}
      <div className="hidden flex-col justify-between bg-rail p-10 text-rail-foreground lg:flex">
        <div className="flex items-center gap-2">
          <Building2 className="size-5" aria-hidden />
          <span className="font-semibold">Meridian ERP</span>
        </div>
        <div className="max-w-md">
          <h1 className="text-2xl font-semibold">Human resources</h1>
          <p className="mt-3 text-sm text-rail-muted">
            One place for your profile, leave, attendance, pay and requests — and for the people who
            administer them.
          </p>
          <ul className="mt-6 space-y-2 text-sm text-rail-muted">
            <li className="flex gap-2">
              <ShieldCheck className="mt-0.5 size-4 shrink-0" aria-hidden />
              Access is scoped to your role, entity and branch.
            </li>
            <li className="flex gap-2">
              <KeyRound className="mt-0.5 size-4 shrink-0" aria-hidden />
              Credentials stay with your organisation's identity provider.
            </li>
          </ul>
        </div>
        <p className="text-xs text-rail-muted">Demonstration build — no real data, no real accounts.</p>
      </div>

      {/* Sign-in panel */}
      <main className="flex items-center justify-center px-4 py-12 sm:px-8">
        <div className="w-full max-w-sm">
          <div className="lg:hidden">
            <div className="flex items-center gap-2">
              <Building2 className="size-5" aria-hidden />
              <span className="font-semibold">Meridian ERP</span>
            </div>
          </div>

          <h2 className="mt-6 text-xl font-semibold lg:mt-0">Sign in</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Use your organisation account. You will be returned here once your identity provider
            confirms who you are.
          </p>

          <Button className="mt-6 w-full" onClick={() => enter(role)} disabled={busy}>
            <ShieldCheck className="size-4" aria-hidden />
            Continue with organisation account
          </Button>

          <div className="my-6 flex items-center gap-3">
            <span className="h-px flex-1 bg-border" />
            <span className="text-xs text-muted-foreground">Demonstration sign-in</span>
            <span className="h-px flex-1 bg-border" />
          </div>

          <div className="rounded-lg border border-warning/40 bg-warning-soft p-3">
            <p className="flex gap-2 text-xs text-warning">
              <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
              <span>
                This build has no real authentication. Choose a role to explore the app as that kind
                of user — it changes what you can see, exactly as a real sign-in would.
              </span>
            </p>
          </div>

          <div className="mt-5 space-y-4">
            <div>
              <Label htmlFor="email">Work email</Label>
              <Input
                id="email"
                type="email"
                autoComplete="username"
                className="mt-1"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
              <p className="mt-1 text-xs text-muted-foreground">
                Resolved to an employee record. No password is requested or stored.
              </p>
            </div>

            <div>
              <Label htmlFor="role">Sign in as</Label>
              <Select value={role} onValueChange={(v) => setLocalRole(v as Role)}>
                <SelectTrigger id="role" className="mt-1">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {workspaces.map((w) => (
                    <SelectItem key={w.id} value={w.id}>
                      {w.label} — {w.description}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <Button variant="outline" className="w-full" onClick={() => enter(role)} disabled={busy}>
              Enter the workspace
            </Button>
          </div>

          <p className="mt-6 flex items-center justify-center gap-1.5 text-xs text-muted-foreground">
            <LifeBuoy className="size-3.5" aria-hidden />
            Need to report something confidentially?{" "}
            <a href="/speak-up" className="text-primary underline underline-offset-2">
              Speak up without signing in
            </a>
          </p>
        </div>
      </main>
    </div>
  );
}
