import { useEffect } from "react";
import { useNavigate } from "@tanstack/react-router";
import { Loader2, LogOut, ShieldAlert } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useApp } from "@/platform/app-context";
import { useAuth } from "@/platform/auth";

const USE_REAL = (import.meta.env.VITE_USE_REAL_API as string | undefined) === "true";

/**
 * Renders children only when the user is authenticated.
 *
 * - Demo mode (VITE_USE_REAL_API=false): always passes through.
 * - Real mode: while the session is being bootstrapped a neutral loading
 *   state renders (this is what makes silent SSO feel seamless); once the
 *   session fails, the user is sent to the ERP-hosted `/sign-in` page
 *   rather than an error screen.
 *
 * The identity is also mirrored into the shell's demo role so every existing
 * page that gates on `useApp().role` keeps working: realm roles map to the
 * closest demo workspace (see auth.tsx), defaulting to "employee".
 */
export function AuthGate({ children }: { children: React.ReactNode }) {
  const navigate = useNavigate();
  const { loading, authenticated, user, signOut, resolveRole } = useAuth();
  const { role, setRole } = useApp();

  useEffect(() => {
    if (!USE_REAL || loading || !authenticated || !user) return;
    const next = resolveRole();
    if (next !== role) setRole(next);
  }, [loading, authenticated, user, resolveRole, role, setRole]);

  useEffect(() => {
    if (!USE_REAL || loading) return;
    if (!authenticated) {
      void navigate({ to: "/sign-in", replace: true });
    }
  }, [loading, authenticated, navigate]);

  if (!USE_REAL) return <>{children}</>;
  if (loading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background">
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="size-4 animate-spin" aria-hidden />
          Checking your session…
        </div>
      </div>
    );
  }
  if (!authenticated) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background px-4">
        <div className="max-w-sm text-center">
          <ShieldAlert className="mx-auto size-8 text-muted-foreground" aria-hidden />
          <p className="mt-4 text-sm text-muted-foreground">Taking you to sign in…</p>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}

/** Small header affordance: who is signed in + sign out. */
export function SignedInBadge() {
  const { user, signOut } = useAuth();
  const label = user?.preferredUsername ?? user?.email ?? "Organisation account";
  return (
    <div className="flex items-center gap-2">
      <span className="hidden truncate text-xs text-muted-foreground sm:inline" title={label}>
        {label}
      </span>
      <Button variant="ghost" size="sm" className="h-8 gap-1.5" onClick={() => signOut()}>
        <LogOut className="size-3.5" aria-hidden />
        Sign out
      </Button>
    </div>
  );
}
