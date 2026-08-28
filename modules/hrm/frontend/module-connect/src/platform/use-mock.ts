import { useCallback, useEffect, useState } from "react";
import { DegradedServiceError } from "@/mock/service";

export interface MockState<T> {
  data: T | null;
  loading: boolean;
  degraded: string | null;
  error: string | null;
  reload: () => void;
}

/** Thin async reader for the mock service: loading / degraded / error / data. */
export function useMock<T>(fn: () => Promise<T>, deps: unknown[] = []): MockState<T> {
  // Production must never execute or display mock loaders. Mixed routes may
  // retain a mock fallback for demo mode, but live API mode gets a settled,
  // empty mock state so the real branch remains the only data source.
  const realApiMode = import.meta.env.VITE_USE_REAL_API === "true";
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(!realApiMode);
  const [degraded, setDegraded] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [nonce, setNonce] = useState(0);

  useEffect(() => {
    if (realApiMode) return;
    let live = true;
    setLoading(true);
    setDegraded(null);
    setError(null);
    fn()
      .then((d) => live && setData(d))
      .catch((e) => {
        if (!live) return;
        if (e instanceof DegradedServiceError) setDegraded(e.service);
        else setError(e instanceof Error ? e.message : "Unknown error");
      })
      .finally(() => live && setLoading(false));
    return () => {
      live = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [realApiMode, ...deps, nonce]);

  const reload = useCallback(() => setNonce((n) => n + 1), []);
  return { data, loading, degraded, error, reload };
}
