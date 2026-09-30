/** Runtime, server-owned login policy. Missing/failed policy never enables OIDC. */
export interface AuthSettings {
  ready: boolean;
  available: boolean;
  oidcEnabled: boolean;
  localEnabled: boolean;
}

export const INITIAL_AUTH_SETTINGS: AuthSettings = {
  ready: false,
  available: false,
  oidcEnabled: false,
  localEnabled: false,
};

let settings = INITIAL_AUTH_SETTINGS;
let pending: Promise<AuthSettings> | undefined;

export function oidcLoginEnabled(): boolean {
  return settings.oidcEnabled;
}

export function loadAuthSettings(): Promise<AuthSettings> {
  if (pending) return pending;
  pending = (async () => {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 10_000);
    try {
      const base = (import.meta.env.VITE_HRM_API_BASE as string | undefined)?.trim().replace(/\/$/, "") || "/api";
      // Do not attach an old bearer token: this anonymous endpoint decides
      // whether that token is allowed to be used in the first place.
      const response = await fetch(`${base}/hrm/auth/capabilities`, {
        credentials: "same-origin",
        cache: "no-store",
        headers: { Accept: "application/json" },
        signal: controller.signal,
      });
      if (!response.ok) throw new Error("Sign-in settings unavailable");
      const data = await response.json() as Record<string, unknown>;
      const validMode = ["local", "hybrid", "oidc"].includes(String(data.mode));
      settings = {
        ready: true,
        available: validMode,
        oidcEnabled: validMode && (data.mode === "oidc" || data.mode === "hybrid") && data.identityConfigured === true,
        localEnabled: validMode && (data.mode === "local" || data.mode === "hybrid") && data.localUsersEnabled === true,
      };
    } catch {
      settings = { ...INITIAL_AUTH_SETTINGS, ready: true };
    } finally {
      clearTimeout(timeout);
    }
    return settings;
  })();
  return pending;
}
