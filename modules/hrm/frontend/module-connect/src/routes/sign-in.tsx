import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { AlertTriangle, KeyRound, LifeBuoy, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useApp } from "@/platform/app-context";
import { useAuth } from "@/platform/auth";
import { hrmApi } from "@/platform/api-client";

export const Route = createFileRoute("/sign-in")({
  head: () => ({
    meta: [
      { title: "Sign in — Mightyfin HRMS" },
      { name: "description", content: "Sign in to the Mightyfin HRMS workspace." },
      { property: "og:title", content: "Sign in — Mightyfin HRMS" },
      { property: "og:description", content: "Sign in to the Mightyfin HRMS workspace." },
    ],
  }),
  component: SignIn,
});

const USE_REAL = (import.meta.env.VITE_USE_REAL_API as string | undefined) === "true";

function SignIn() {
  const navigate = useNavigate();
  const { setRole } = useApp();
  const { authenticated, signIn } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [setupPassword, setSetupPassword] = useState("");
  const [setupBusy, setSetupBusy] = useState(false);
  const accessToken = typeof window === "undefined" ? null : new URLSearchParams(window.location.search).get("token");

  useEffect(() => {
    if (USE_REAL && authenticated) void navigate({ to: "/hrm", replace: true });
  }, [authenticated, navigate]);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const user = await signIn(email, password);
      if (user.mustChangePassword) {
        void navigate({ to: "/hrm", replace: true });
      } else {
        void navigate({ to: "/hrm", replace: true });
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to sign in. Check your email and password.");
    } finally {
      setBusy(false);
    }
  };

  const continueDemo = () => {
    setRole("hr_admin");
    void navigate({ to: "/" });
  };

  const completeSetup = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!accessToken) return;
    setError(null);
    setSetupBusy(true);
    try {
      await hrmApi.auth.setPassword(accessToken, setupPassword);
      setSetupPassword("");
      window.history.replaceState({}, "", "/sign-in");
      setError("Password set. You can now sign in.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "This account link is invalid or has expired.");
    } finally {
      setSetupBusy(false);
    }
  };

  if (!USE_REAL) {
    return (
      <div className="grid min-h-screen lg:grid-cols-2">
        <div className="hidden flex-col justify-between bg-rail p-10 text-rail-foreground lg:flex">
          <div
            className="flex h-24 w-36 shrink-0 items-start justify-start"
            data-testid="signin-brand-logo-container"
          >
            <img
              src="/mightyfin-logo-light.png"
              alt="Mightyfin HRMS"
              data-testid="signin-brand-logo"
              className="block max-h-full max-w-full object-contain object-left"
            />
          </div>
          <div className="max-w-md"><h1 className="text-2xl font-semibold">Human resources</h1><p className="mt-3 text-sm text-rail-muted">One place for your profile, leave, attendance, pay and requests.</p></div>
          <p className="text-xs text-rail-muted">Demonstration build — no real accounts.</p>
        </div>
        <main className="flex items-center justify-center px-4 py-12 sm:px-8">
          <div className="w-full max-w-sm">
            <div
              className="mb-6 flex h-20 w-32 shrink-0 items-start justify-start lg:hidden"
              data-testid="signin-mobile-brand-logo-container"
            >
              <img
                src="/mightyfin-logo-color.png"
                alt="Mightyfin HRMS"
                data-testid="signin-mobile-brand-logo"
                className="block max-h-full max-w-full object-contain object-left"
              />
            </div>
            <h2 className="text-xl font-semibold">Sign in</h2>
            <p className="mt-1 text-sm text-muted-foreground">
              Demo mode — choose a role to explore the app.
            </p>
            <Button className="mt-6 w-full" onClick={continueDemo}>Enter the workspace</Button>
          </div>
        </main>
      </div>
    );
  }

  return (
    <div className="grid min-h-screen lg:grid-cols-2">
      <div className="hidden flex-col justify-between bg-rail p-10 text-rail-foreground lg:flex">
        <div
          className="flex h-24 w-36 shrink-0 items-start justify-start"
          data-testid="signin-brand-logo-container"
        >
          <img
            src="/mightyfin-logo-light.png"
            alt="Mightyfin HRMS"
            data-testid="signin-brand-logo"
            className="block max-h-full max-w-full object-contain object-left"
          />
        </div>
        <div className="max-w-md">
          <h1 className="text-2xl font-semibold">Human resources</h1>
          <p className="mt-3 text-sm text-rail-muted">One place for your profile, leave, attendance, pay and requests — and for the people who administer them.</p>
          <ul className="mt-6 space-y-2 text-sm text-rail-muted">
            <li className="flex gap-2"><ShieldCheck className="mt-0.5 size-4 shrink-0" aria-hidden />Access is scoped to your role, entity and branch.</li>
            <li className="flex gap-2"><KeyRound className="mt-0.5 size-4 shrink-0" aria-hidden />Accounts and passwords are managed by Mightyfin HRMS.</li>
          </ul>
        </div>
        <p className="text-xs text-rail-muted">Secure local sign-in with the HRMS account database.</p>
      </div>

      <main className="flex items-center justify-center px-4 py-12 sm:px-8">
        <div className="w-full max-w-sm">
          <div
            className="mb-6 flex h-20 w-32 shrink-0 items-start justify-start lg:hidden"
            data-testid="signin-mobile-brand-logo-container"
          >
            <img
              src="/mightyfin-logo-color.png"
              alt="Mightyfin HRMS"
              data-testid="signin-mobile-brand-logo"
              className="block max-h-full max-w-full object-contain object-left"
            />
          </div>
          <h2 className="mt-6 text-xl font-semibold lg:mt-0">{accessToken ? "Set your password" : "Sign in"}</h2>
          <p className="mt-1 text-sm text-muted-foreground">{accessToken ? "Choose a password for your Mightyfin HRMS account." : "Use an account created by a Mightyfin HRMS administrator."}</p>

          {accessToken ? <form className="mt-6 space-y-4" onSubmit={completeSetup}>
            <div><Label htmlFor="setup-password">New password</Label><Input id="setup-password" type="password" autoComplete="new-password" className="mt-1" value={setupPassword} onChange={(e) => setSetupPassword(e.target.value)} minLength={12} required /></div>
            {error && <div className="rounded-lg border border-destructive/40 bg-destructive/5 p-3 text-sm text-destructive" role="alert"><AlertTriangle className="mr-2 inline size-4" aria-hidden />{error}</div>}
            <Button className="w-full" type="submit" disabled={setupBusy}>{setupBusy ? "Setting password…" : "Set password"}</Button>
          </form> : <form className="mt-6 space-y-4" onSubmit={submit}>
            <div><Label htmlFor="email">Email</Label><Input id="email" type="email" autoComplete="username" className="mt-1" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="you@company.example" required /></div>
            <div><Label htmlFor="password">Password</Label><Input id="password" type="password" autoComplete="current-password" className="mt-1" value={password} onChange={(e) => setPassword(e.target.value)} required /></div>
            {error && <div className="rounded-lg border border-destructive/40 bg-destructive/5 p-3 text-sm text-destructive" role="alert"><AlertTriangle className="mr-2 inline size-4" aria-hidden />{error}</div>}
            <Button className="w-full" type="submit" disabled={busy}>{busy ? "Signing in…" : "Sign in"}</Button>
          </form>}

          <p className="mt-6 flex items-center justify-center gap-1.5 text-xs text-muted-foreground"><LifeBuoy className="size-3.5" aria-hidden />Need to report something confidentially? <a href="/speak-up" className="text-primary underline underline-offset-2">Speak up without signing in</a></p>
        </div>
      </main>
    </div>
  );
}
