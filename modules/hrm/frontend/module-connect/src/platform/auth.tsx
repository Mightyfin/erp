/**
 * Authentication provider for the ERP web shell.
 *
 * Drives the hybrid login experience agreed in the auth design document
 * (`deployment/auth/README.md`):
 *
 * - **Hybrid mode (production, VITE_USE_REAL_API=true).** On every app load
 *   the shell checks for a locally-stored Keycloak session and refreshes it
 *   silently. Routes that require a user are gated: without a session the
 *   user is sent to the ERP-hosted `/sign-in` page, which first tries silent
 *   SSO (`prompt=none`) and otherwise shows the email/password form that
 *   hands off to the Keycloak hosted login via PKCE.
 * - **Demo mode (local dev with VITE_USE_REAL_API=false).** The gate is
 *   skipped so the mock app remains explorable without an IdP; the shell
 *   continues to use the mock role picker in AppProvider.
 *
 * Identity is always owned by the IdP — the shell never stores or validates
 * credentials itself.
 */
import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import type { Role } from "@/mock/types";
import {
  clearSession,
  decodeIdToken,
  ensureFreshSession,
  getSession,
  isSessionValid,
  signOut as oidcSignOut,
  type OidcSession,
  type OidcUser,
} from "@/platform/oidc";

const USE_REAL = (import.meta.env.VITE_USE_REAL_API as string | undefined) === "true";

interface AuthState {
  /** Real OIDC session when the hybrid flow is active; null in demo mode. */
  session: OidcSession | null;
  user: OidcUser | null;
  loading: boolean;
  /** True when the user is allowed past the sign-in gate. */
  authenticated: boolean;
  /** Map Keycloak realm roles to the shell's demo workspace role. */
  resolveRole: () => Role;
  signInInteractive: () => void;
  signOut: () => void;
}

const Ctx = createContext<AuthState | null>(null);

/**
 * Realm roles the ERP understands. Anything else (including no roles) lands
 * in the broadest demo workspace so a new IdP user is never locked out of
 * the first screen; fine-grained authorization is enforced by the backend
 * scopes and the tenant model, not by this mapping.
 */
function mapRolesToDemoRole(roles: string[]): Role {
  const set = new Set(roles);
  if (set.has("payroll")) return "payroll";
  if (set.has("hr_admin") || set.has("admin")) return "hr_admin";
  if (set.has("hr_ops")) return "hr_ops";
  if (set.has("manager")) return "manager";
  return "employee";
}

/** Public paths that never need a session (speak-up must stay anonymous). */
const PUBLIC_PATHS = new Set(["/sign-in", "/speak-up"]);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<OidcSession | null>(null);
  const [loading, setLoading] = useState(true);
  const [origin, setOrigin] = useState<string>("/hrm");

  useEffect(() => {
    if (!USE_REAL) {
      setLoading(false);
      return;
    }
    let cancelled = false;
    (async () => {
      const fresh = await ensureFreshSession();
      if (!cancelled) {
        setSession(fresh);
        setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  // Remember where the user was heading so a login round-trip can return them
  // to the same place (e.g. a deep link like /hrm/leave/abc).
  useEffect(() => {
    if (!loading && !isSessionValid(session)) {
      const path = window.location.pathname;
      setOrigin(PUBLIC_PATHS.has(path) ? "/hrm" : path);
    }
  }, [loading, session]);

  const user = useMemo(() => (session?.idToken ? decodeIdToken(session.idToken) : null), [session]);

  const authenticated = !USE_REAL || isSessionValid(session);

  const signInInteractive = useCallback(() => {
    const path = PUBLIC_PATHS.has(window.location.pathname) ? "/hrm" : window.location.pathname;
    // Imported lazily so demo builds can stay tree-shaken away from unused
    // network code paths; startInteractiveLogin lives next to the flow.
    void import("@/platform/oidc").then((m) => m.startInteractiveLogin(path));
  }, []);

  const signOut = useCallback(() => {
    clearSession();
    setSession(null);
    if (USE_REAL) {
      oidcSignOut("/sign-in");
    }
  }, []);

  const resolveRole = useCallback((): Role => {
    if (!USE_REAL) return "employee";
    return mapRolesToDemoRole(user?.roles ?? []);
  }, [user]);

  const value = useMemo<AuthState>(
    () => ({ session, user, loading, authenticated, resolveRole, signInInteractive, signOut }),
    [session, user, loading, authenticated, resolveRole, signInInteractive, signOut],
  );

  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useAuth() {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error("useAuth must be used inside AuthProvider");
  return ctx;
}
