/**
 * Standalone HRMS authentication provider.
 *
 * In real mode the shell uses the application database through same-origin
 * HttpOnly sessions. OIDC is optional integration code only and is not invoked
 * by this provider.
 */
import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import type { Role } from "@/mock/types";
import { hrmApi, type LinkedWorker, type LocalAuthUser } from "@/platform/api-client";

const USE_REAL = (import.meta.env.VITE_USE_REAL_API as string | undefined) === "true";

interface AuthState {
  session: LocalAuthUser | null;
  user: LocalAuthUser | null;
  loading: boolean;
  authenticated: boolean;
  authorized: boolean;
  resolveRole: () => Role;
  signInInteractive: () => void;
  signIn: (email: string, password: string) => Promise<LocalAuthUser>;
  signOut: () => void;
  worker: LinkedWorker | null;
  resolvingWorker: boolean;
}

const Ctx = createContext<AuthState | null>(null);

export const HRM_STAFF_ROLES = [
  "employee",
  "manager",
  "hr_ops",
  "payroll",
  "finance_approver",
  "hr_admin",
  "investigator",
] as const;

export function hasHrmStaffRole(roles: string[]): boolean {
  const set = new Set(roles.map((role) => role.toLowerCase()));
  return HRM_STAFF_ROLES.some((role) => set.has(role));
}

function mapRolesToDemoRole(roles: string[]): Role {
  const set = new Set(roles);
  if (set.has("payroll") || set.has("finance_approver")) return "payroll";
  if (set.has("hr_admin") || set.has("investigator")) return "hr_admin";
  if (set.has("hr_ops")) return "hr_ops";
  if (set.has("manager")) return "manager";
  return "employee";
}

const PUBLIC_PATHS = new Set(["/sign-in", "/speak-up"]);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<LocalAuthUser | null>(null);
  const [loading, setLoading] = useState(true);
  const [worker, setWorker] = useState<LinkedWorker | null>(null);
  const [resolvingWorker, setResolvingWorker] = useState(false);

  useEffect(() => {
    if (!USE_REAL) {
      setLoading(false);
      return;
    }
    let cancelled = false;
    void hrmApi.auth.me()
      .then((result) => {
        if (!cancelled) setSession(result.authenticated ? result.user : null);
      })
      .catch(() => {
        if (!cancelled) setSession(null);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!USE_REAL || !session) return;
    let cancelled = false;
    setResolvingWorker(true);
    void hrmApi.myProfile()
      .then((profile) => {
        if (!cancelled && profile.linked) setWorker(profile.worker);
      })
      .catch(() => {
        if (!cancelled) setWorker(null);
      })
      .finally(() => {
        if (!cancelled) setResolvingWorker(false);
      });
    return () => {
      cancelled = true;
    };
  }, [session]);

  const user = session;
  const authenticated = !USE_REAL || Boolean(session);
  const authorized = !USE_REAL || (authenticated && hasHrmStaffRole(user?.roles ?? []));

  const signInInteractive = useCallback(() => {
    const path = PUBLIC_PATHS.has(window.location.pathname) ? "/hrm" : window.location.pathname;
    window.location.assign(`/sign-in?next=${encodeURIComponent(path)}`);
  }, []);

  const signIn = useCallback(async (email: string, password: string) => {
    const result = await hrmApi.auth.login(email, password);
    setSession(result.user);
    return result.user;
  }, []);

  const signOut = useCallback(() => {
    void hrmApi.auth.logout().catch(() => undefined);
    setSession(null);
    setWorker(null);
    if (window.location.pathname !== "/sign-in") window.location.assign("/sign-in");
  }, []);

  const resolveRole = useCallback((): Role => {
    if (!USE_REAL) return "employee";
    return mapRolesToDemoRole(user?.roles ?? []);
  }, [user]);

  const value = useMemo<AuthState>(
    () => ({
      session,
      user,
      loading,
      authenticated,
      authorized,
      resolveRole,
      signInInteractive,
      signIn,
      signOut,
      worker,
      resolvingWorker,
    }),
    [session, user, loading, authenticated, authorized, resolveRole, signInInteractive, signIn, signOut, worker, resolvingWorker],
  );

  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useAuth() {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error("useAuth must be used inside AuthProvider");
  return ctx;
}
