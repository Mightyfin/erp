/**
 * Drop-in data hook that mirrors `useMock`'s shape (`data`, `loading`,
 * `degraded`, `error`, `reload`) but reads from the real ASP.NET backend when
 * `VITE_USE_REAL_API=true` is set. Otherwise it delegates to the provided
 * mock reader, keeping the entire UI green on mock data by default.
 *
 * When the real backend is unreachable or returns 5xx the hook surfaces a
 * `degraded` banner instead of a hard failure, so screens stay usable with
 * fallback data.
 */
import { useCallback, useEffect, useState } from "react";
import { ApiError, hrmApi } from "@/platform/api-client";

export interface ApiState<T> {
  data: T | null;
  loading: boolean;
  degraded: string | null;
  error: string | null;
  reload: () => void;
}

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

export function useApi<T>(
  fn: () => Promise<T>,
  deps: unknown[] = [],
): ApiState<T> {
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(true);
  const [degraded, setDegraded] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [nonce, setNonce] = useState(0);

  useEffect(() => {
    if (!USE_REAL) {
      // Real backend off — fall back to the mock reader untouched.
      let live = true;
      setLoading(true);
      setDegraded(null);
      setError(null);
      fn()
        .then((d) => live && setData(d))
        .catch((e) => {
          if (!live) return;
          setError(e instanceof Error ? e.message : "Unknown error");
        })
        .finally(() => live && setLoading(false));
      return () => {
        live = false;
      };
    }
    let live = true;
    setLoading(true);
    setDegraded(null);
    setError(null);
    fn()
      .then(async (d) => {
        if (!live) return;
        setData(d);
      })
      .catch((e) => {
        if (!live) return;
        if (e instanceof ApiError && e.status >= 500) {
          setDegraded("hrm-api");
        } else {
          setError(e instanceof Error ? e.message : "Unknown error");
        }
      })
      .finally(() => live && setLoading(false));
    return () => {
      live = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, nonce, USE_REAL]);

  const reload = useCallback(() => setNonce((n) => n + 1), []);
  return { data, loading, degraded, error, reload };
}

/**
 * Adapts a backend WorkerDto list into the mock `Employee` shape so existing
 * column definitions keep working when the real backend is switched on.
 */
export function adaptWorkers(backend: unknown): Array<import("@/mock/types").Employee> {
  const rows = Array.isArray(backend)
    ? backend
    : backend && typeof backend === "object" && "items" in backend
      ? Array.from((backend as { items?: unknown[] }).items ?? [])
      : [];
  return rows.map((raw) => {
    const w = raw as Record<string, unknown>;
    const toText = (v: unknown) =>
      v === undefined || v === null ? "" : String(v);
    return {
      id: toText(w.id),
      employeeNo: toText(w.employeeNo),
      fullName: toText(w.fullName),
      preferredName: w.preferredName ? toText(w.preferredName) : undefined,
      jobTitle: toText(w.jobTitle),
      department: toText(w.orgUnitName),
      entityId: "",
      branch: toText(w.locationName),
      employmentType: (toText(w.workerType) || "Permanent") as never,
      status: toText(w.status) as never,
      startDate: toText(w.startDate),
      endDate: w.endDate ? toText(w.endDate) : undefined,
      email: w.email ? toText(w.email) : undefined,
      phone: w.phone ? toText(w.phone) : undefined,
      location: toText(w.locationName),
      grade: toText(w.grade),
      nationalId: toText(w.nrc),
      bankAccount: Array.isArray(w.bankDetails)
        ? ((w.bankDetails as Array<{ accountNumber?: unknown }>)[0]?.accountNumber ?? "") as string
        : "",
    };
  });
}

/** Shortcut readers for the flagship backend surfaces used by pages. */
export const realApi = {
  /** The backend returns `Paged<T>` — { items, totalCount, page, pageSize }. */
  employees: (params?: Record<string, unknown>) =>
    hrmApi.get<{ items: unknown[]; totalCount: number }>("/hrm/workers", {
      page: 1,
      pageSize: 200,
      ...params,
    }),
  worker: (id: string) => hrmApi.get<unknown>(`/hrm/workers/${id}`),
  dqChecks: () => hrmApi.get<unknown>("/hrm/dq/checks"),
  workerDocuments: (workerId: string) =>
    hrmApi.get<unknown>(`/hrm/documents/worker/${workerId}`),
  reports: (params: Record<string, unknown>) =>
    hrmApi.get<unknown>("/hrm/reports", params),
  /** Create a worker and return the created WorkerDto. */
  createWorker: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/workers", body),
  /** Patch-update a worker (fields sent as-is, backend accepts partial). */
  updateWorker: (id: string, body: Record<string, unknown>) =>
    hrmApi.put<Record<string, unknown>>(`/hrm/workers/${id}`, body),
  /** Soft-archive a worker. */
  archiveWorker: (id: string) => hrmApi.post<unknown>(`/hrm/workers/${id}/archive`, null),
  /** Upload a document for a worker. */
  uploadDocument: (workerId: string, file: File, category: string, title: string) =>
    hrmApi.uploadDocument(workerId, file, category, title),
  /** Org units (config) — used for department placement selects. */
  orgUnits: () => hrmApi.get<unknown[]>("/hrm/admin/org-units"),
  /** Work locations (config) — used for location placement selects. */
  locations: async () => {
    // Config endpoints return a paginated envelope { items, totalCount, ... }
    const page = await hrmApi.get<{ items?: unknown[] }>('/hrm/admin/locations');
    return page.items ?? page;
  },
  /** Download a document and return { url, fileName }. Caller revokes url. */
  downloadDocument: async (documentId: string, fileName: string) => {
    const url = await hrmApi.downloadDocument(documentId);
    return { url, fileName };
  },
};
