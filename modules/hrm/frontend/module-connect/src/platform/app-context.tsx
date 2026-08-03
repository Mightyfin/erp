import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { entities, workspaces } from "@/mock/data";
import type { Role } from "@/mock/types";

interface AppState {
  role: Role;
  setRole: (r: Role) => void;
  entityId: string;
  setEntityId: (id: string) => void;
  branch: string;
  setBranch: (b: string) => void;
  theme: "light" | "dark";
  toggleTheme: () => void;
  setupComplete: boolean;
  completeSetup: () => void;
  resetSetup: () => void;
  hydrated: boolean;
}

const Ctx = createContext<AppState | null>(null);
const KEY = "erp.shell.state.v1";

export function AppProvider({ children }: { children: ReactNode }) {
  const [role, setRole] = useState<Role>("hr_admin");
  const [entityId, setEntityId] = useState(entities[0].id);
  const [branch, setBranch] = useState(entities[0].branches[0]);
  const [theme, setTheme] = useState<"light" | "dark">("light");
  const [setupComplete, setSetupComplete] = useState(false);
  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    try {
      const raw = localStorage.getItem(KEY);
      if (raw) {
        const s = JSON.parse(raw);
        if (s.role && workspaces.some((w) => w.id === s.role)) setRole(s.role);
        // Stored ids can outlive the data they point at — a renamed or removed
        // entity must not be restored, or every consumer of useEntity() breaks.
        const storedEntity = entities.find((e) => e.id === s.entityId);
        if (storedEntity) {
          setEntityId(storedEntity.id);
          setBranch(
            storedEntity.branches.includes(s.branch) ? s.branch : storedEntity.branches[0],
          );
        }
        if (s.theme === "light" || s.theme === "dark") setTheme(s.theme);
        setSetupComplete(Boolean(s.setupComplete));
      }
    } catch {
      /* ignore corrupt local state */
    }
    setHydrated(true);
  }, []);

  useEffect(() => {
    if (!hydrated) return;
    localStorage.setItem(KEY, JSON.stringify({ role, entityId, branch, theme, setupComplete }));
    document.documentElement.classList.toggle("dark", theme === "dark");
  }, [role, entityId, branch, theme, setupComplete, hydrated]);

  const value = useMemo<AppState>(
    () => ({
      role,
      setRole,
      entityId,
      setEntityId: (id: string) => {
        setEntityId(id);
        const e = entities.find((x) => x.id === id);
        if (e) setBranch(e.branches[0]);
      },
      branch,
      setBranch,
      theme,
      toggleTheme: () => setTheme((t) => (t === "light" ? "dark" : "light")),
      setupComplete,
      completeSetup: () => setSetupComplete(true),
      resetSetup: () => setSetupComplete(false),
      hydrated,
    }),
    [role, entityId, branch, theme, setupComplete, hydrated],
  );

  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useApp() {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error("useApp must be used inside AppProvider");
  return ctx;
}

export function useWorkspace() {
  const { role } = useApp();
  // Fall back rather than throw: a stale role must not blank the whole app.
  return workspaces.find((w) => w.id === role) ?? workspaces[0];
}

export function useEntity() {
  const { entityId } = useApp();
  return entities.find((e) => e.id === entityId) ?? entities[0];
}

export function useRoleGate() {
  const { role } = useApp();
  return useCallback((allowed?: Role[]) => !allowed || allowed.includes(role), [role]);
}