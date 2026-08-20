import { MapPin, Globe2 } from "lucide-react";
import { useApp } from "@/platform/app-context";
import { realApi, useApi } from "@/platform/use-api";

const USE_REAL = (import.meta.env.VITE_USE_REAL_API as string | undefined) === "true";

/**
 * M44 branch scoping: a small status badge on operational pages telling the
 * operator exactly which scope their create/list requests execute under.
 * Reads the server-echoed work scope (GET /hrm/shell honours the same
 * X-Shell-Location header the client attaches everywhere) so the badge
 * always matches reality. Falls back to the switcher's branch name when the
 * shell lookup is unavailable.
 */
export function ScopeBadge() {
  const { branch } = useApp();
  const shellState = useApi(
    () => (USE_REAL ? realApi.shell().catch(() => null) : Promise.resolve(null)),
    [],
  );
  const locationsState = useApi(
    () => (USE_REAL ? realApi.locations().catch(() => null) : Promise.resolve(null)),
    [],
  );
  const shell = shellState.data;
  const scopedToBranch = shell?.scopedToBranch ?? Boolean(branch);
  // realApi.locations() always returns an array (it unwraps the { items } envelope)
  const locations = Array.isArray(locationsState.data) ? locationsState.data : [];
  const locationId = shell?.locationId ?? (branch || null);
  const locationName = locationId
    ? (locations as Record<string, unknown>[]).find((l) => l.id === locationId)?.name
    : undefined;
  const displayName = locationName ? String(locationName) : locationId ? `Branch ${String(locationId).slice(0, 8)}…` : "Branch selected";

  if (!USE_REAL) return null;
  if (!scopedToBranch) {
    return (
      <span className="inline-flex items-center gap-1.5 rounded-full border border-border bg-muted/50 px-2.5 py-1 text-[11px] font-medium text-muted-foreground">
        <Globe2 className="size-3.5" aria-hidden />
        Organisation-wide
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1.5 rounded-full border border-primary/30 bg-primary/10 px-2.5 py-1 text-[11px] font-semibold text-primary">
      <MapPin className="size-3.5" aria-hidden />
      {(shellState.loading || locationsState.loading) && !locationName ? "Branch…" : displayName}
    </span>
  );
}
