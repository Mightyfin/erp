import { useEffect, useRef, useState } from "react";

/**
 * M47 scope-switch overlay. When the operator picks a different entity or
 * branch in the top-nav organisation switcher, the whole screen is briefly
 * covered by an intense-but-slightly-transparent white blur. A rotating
 * two-arrow "switch" mark sits above a live message — "Switching to
 * M3 Test HQ…" for a branch or "Switching to Mighty Finance Limited
 * (organisation-wide)…" when the whole entity is selected — so the text is
 * fully legible on the frosted cover.
 *
 * Rides on a `storage` event (cross-tab) plus a short polling bridge for
 * same-tab writes, because `useApp` persists the new scope to
 * `erp.shell.state.v1` synchronously as soon as the switcher fires — so the
 * overlay appears regardless of how the selection was triggered.
 *
 * `targetLabel` is supplied by the switcher host, which already knows the
 * human-readable entity/branch names (the overlay never fetches anything).
 */
const STORAGE_KEY = "erp.shell.state.v1";

interface ScopeSnapshot {
  entityId?: string;
  branch?: string;
}

function readScope(): ScopeSnapshot {
  try {
    if (typeof localStorage === "undefined") return {};
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return {};
    const parsed = JSON.parse(raw);
    return typeof parsed === "object" && parsed !== null
      ? (parsed as ScopeSnapshot)
      : {};
  } catch {
    return {};
  }
}

/** Two interlocking arrows forming the rotating "switch" mark. */
function SwitchGlyph() {
  return (
    <svg viewBox="0 0 48 48" fill="none" className="size-12 animate-spin-slow text-primary" aria-hidden>
      <path
        d="M10 17h22m0 0-6-6m6 6-6 6M38 31H16m0 0 6-6m-6 6 6 6"
        stroke="currentColor"
        strokeWidth="3.4"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export function ScopeSwitchOverlay({ targetLabel }: { targetLabel: string }) {
  const [visible, setVisible] = useState(false);
  const [fading, setFading] = useState(false);
  const mounted = useRef(true);
  const timers = useRef<ReturnType<typeof setTimeout>[]>([]);

  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      timers.current.forEach(clearTimeout);
    };
  }, []);

  const show = () => {
    timers.current.forEach(clearTimeout);
    timers.current = [];
    setVisible(true);
    setFading(false);
    // Hold the cover just long enough to register, then fade out.
    timers.current.push(setTimeout(() => setFading(true), 550));
    timers.current.push(setTimeout(() => setVisible(false), 1050));
  };

  useEffect(() => {
    // Show immediately on any scope change. A no-op write (same branch) is
    // ignored by comparing against the current stored value.
    let lastScope: ScopeSnapshot = readScope();
    const tryShow = (next: ScopeSnapshot) => {
      if (
        next.branch === lastScope.branch &&
        next.entityId === lastScope.entityId
      ) {
        lastScope = next;
        return;
      }
      lastScope = next;
      if (mounted.current) show();
    };

    const handleStorage = (e: StorageEvent) => {
      if (e.key !== STORAGE_KEY) return;
      tryShow(readScope());
    };

    // Same-tab bridge: the `storage` event only fires across tabs, so poll
    // the persisted shell state between frame intervals.
    const poll = setInterval(() => {
      const next = readScope();
      if (
        next.branch !== lastScope.branch ||
        next.entityId !== lastScope.entityId
      ) {
        tryShow(next);
      }
    }, 200);

    window.addEventListener("storage", handleStorage);
    return () => {
      clearInterval(poll);
      window.removeEventListener("storage", handleStorage);
    };
  }, []);

  if (!visible) return null;

  return (
    <div
      role="status"
      aria-live="polite"
      aria-busy="true"
      className="pointer-events-none fixed inset-0 z-[100] flex flex-col items-center justify-start bg-white/85 backdrop-blur-xl"
      style={fading ? { animation: "scopeOverlayOut 450ms ease-in both" } : { animation: "scopeOverlayIn 220ms ease-out both" }}
    >
      <div className="mt-[22vh] flex flex-col items-center gap-6">
        <SwitchGlyph />
        <span className="max-w-[80vw] truncate px-4 text-lg font-semibold text-foreground sm:text-xl">
          {targetLabel}
        </span>
      </div>
    </div>
  );
}
