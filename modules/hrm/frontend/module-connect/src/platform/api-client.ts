/**
 * Real HTTP client for the ASP.NET Core HRM backend.
 *
 * - Base URL comes from `VITE_HRM_API_BASE` (defaults to `/api` for same-host
 *   reverse-proxy deployments and `http://localhost:5199/api` in local dev).
 * - Every request carries the `HRM-Default-TenantId` header so the backend can
 *   scope queries to the right tenant.
 * - All responses follow the backend's problem-details-ish envelope and this
 *   client normalises them into an `ApiError` class the UI can surface.
 * - Hybrid auth (M12): when `VITE_USE_REAL_API=true` and the shell holds a
 *   Keycloak session, requests carry `Authorization: Bearer <access_token>`.
 */

import { getSession } from "@/platform/oidc";

export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
    public code?: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

const BASE = (import.meta.env.VITE_HRM_API_BASE as string | undefined)?.trim()
  ? (import.meta.env.VITE_HRM_API_BASE as string).replace(/\/$/, "")
  : "/api";
const TENANT_ID =
  (import.meta.env.VITE_HRM_TENANT_ID as string | undefined)?.trim() ||
  "019ffa8b-0fb0-71e6-849a-f76e5a28e0b5";

// M15 self-service: only the fields a worker may edit on their own record.
// Admin-only fields (name, grade, job title, status, ...) are deliberately
// absent so they can never be submitted from the client.
export interface SelfProfileUpdate {
  preferredName?: string;
  email?: string;
  phone?: string;
  nrc?: string;
  passportNo?: string;
  tpin?: string;
  napsaNumber?: string;
  nhimaNumber?: string;
  nationality?: string;
  dateOfBirth?: string;
  emergencyContacts?: { relationship: string; fullName: string; phone?: string; isPrimary: boolean }[];
  bankDetails?: { bankName: string; branchCode: string; accountNumber: string; accountName: string; isPrimary: boolean; paymentMethod?: string; mobileMoneyNumber?: string }[];
}

/** Minimal shape of the linked worker returned by `hrmApi.myProfile()`. */
export interface LinkedWorker {
  id: string;
  employeeNo: string;
  fullName: string;
  preferredName?: string | null;
  jobTitle?: string | null;
  grade?: string | null;
  email?: string | null;
  photoUrl?: string | null;
  status: string;
}

/** Hybrid auth (M12): attach the bearer token when a Keycloak session exists. */
const USE_REAL = (import.meta.env.VITE_USE_REAL_API as string | undefined) === "true";

async function handleResponse<T>(res: Response): Promise<T> {
  if (res.status === 204) return undefined as T;
  let payload: unknown = undefined;
  const text = await res.text();
  if (text) {
    try {
      payload = JSON.parse(text);
    } catch {
      payload = text;
    }
  }
  if (!res.ok) {
    const title =
      payload && typeof payload === "object" && "title" in payload
        ? String((payload as { title?: unknown }).title)
        : `HTTP ${res.status}`;
    const code =
      payload && typeof payload === "object" && "code" in payload
        ? String((payload as { code?: unknown }).code)
        : undefined;
    throw new ApiError(title || text || `HTTP ${res.status}`, res.status, code);
  }
  return payload as T;
}

function headers(extra?: Record<string, string>): Record<string, string> {
  const authHeaders: Record<string, string> = {};
  // Hybrid auth (M12): when the shell holds a Keycloak session, every API
  // call carries the bearer token. The backend (JWT bearer mode) resolves
  // the tenant-scoped identity from the token's claims.
  if (USE_REAL) {
    const session = getSession();
    if (session) authHeaders.Authorization = `Bearer ${session.accessToken}`;
  }
  return {
    Accept: "application/json",
    "HRM-Default-TenantId": TENANT_ID,
    ...authHeaders,
    ...extra,
  };
}

function qs(params: Record<string, unknown>): string {
  const entries: [string, string][] = [];
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === "") continue;
    entries.push([encodeURIComponent(key), encodeURIComponent(String(value))]);
  }
  if (entries.length === 0) return "";
  return "?" + entries.map(([k, v]) => `${k}=${v}`).join("&");
}

/** Generic typed wrapper around the HRM API surface. */
export const hrmApi = {
  async get<T>(path: string, params?: Record<string, unknown>): Promise<T> {
    const res = await fetch(`${BASE}${path}${qs(params ?? {})}`, {
      headers: headers(),
    });
    return handleResponse<T>(res);
  },

  async post<T>(path: string, body: unknown): Promise<T> {
    const res = await fetch(`${BASE}${path}`, {
      method: "POST",
      headers: { ...headers(), "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return handleResponse<T>(res);
  },

  async put<T>(path: string, body: unknown): Promise<T> {
    const res = await fetch(`${BASE}${path}`, {
      method: "PUT",
      headers: { ...headers(), "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return handleResponse<T>(res);
  },

  async delete<T>(path: string): Promise<T> {
    const res = await fetch(`${BASE}${path}`, {
      method: "DELETE",
      headers: headers(),
    });
    return handleResponse<T>(res);
  },

  /** Upload a file for a worker document (multipart). */
  async uploadDocument(
    workerId: string,
    file: File,
    category: string,
    title: string,
  ): Promise<unknown> {
    const form = new FormData();
    form.append("file", file);
    form.append("workerId", workerId);
    form.append("category", category);
    form.append("title", title);
    const res = await fetch(`${BASE}/hrm/documents/upload`, {
      method: "POST",
      headers: { "HRM-Default-TenantId": TENANT_ID },
      body: form,
    });
    return handleResponse(res);
  },

  /** Stream a document by id and return a Blob URL caller must revoke. */
  async downloadDocument(documentId: string): Promise<string> {
    const res = await fetch(`${BASE}/hrm/documents/${documentId}/download`, {
      headers: headers(),
    });
    if (!res.ok) {
      const text = await res.text();
      throw new ApiError(text || `HTTP ${res.status}`, res.status);
    }
    const blob = await res.blob();
    return URL.createObjectURL(blob);
  },

  /**
   * M14 identity link: resolve the worker record bound to the caller's Keycloak
   * subject. Returns `{ linked, worker, subject }` — worker is null when the
   * signed-in identity is not linked to an HRM worker yet.
   */
  myProfile: () =>
    hrmApi.get<{ linked: boolean; worker: LinkedWorker | null; subject: string }>(
      "/hrm/me",
    ),

  // M15 self-service: update the worker record linked to the caller's token
  // subject. PUT /hrm/me/profile — the backend re-reads the subject from the
  // token, so the client cannot target another worker.
  updateSelfProfile: (body: SelfProfileUpdate) =>
    hrmApi.put<{ worker: LinkedWorker }>("/hrm/me/profile", body),

  /** Data-quality check summary for the tenant. */
  dqChecks: () => hrmApi.get<unknown>("/hrm/dq/checks"),

  /** Statutory export CSV (NAPSA / NHIMA / ZRA / napsa-bankfile). */
  statutoryExport: (exportType: string, periodId: string) =>
    hrmApi.get<Blob>(`/hrm/statutory-exports`, { exportType, periodId }),

  // ---- M16: self-service leave (always keyed on the caller's token subject)

  /**
   * The signed-in worker's own leave inbox: identity, balances across every
   * leave type, and their own leave requests. GET /hrm/me/leave.
   */
  myLeave: () => hrmApi.get<MyLeave>("/hrm/me/leave"),

  /**
   * Cancel an open leave request owned by the caller. POST
   * /hrm/me/leave/{id}/cancel — only submitted/in-review/returned requests can
   * be cancelled and the balance reservation is released.
   */
  cancelLeave: (leaveId: string) =>
    hrmApi.post<LeaveRequestLine>(`/hrm/me/leave/${leaveId}/cancel`, {}),
};

/** One balance row returned by the self-service leave inbox. */
export interface MyLeaveBalance {
  leaveTypeCode: string;
  leaveTypeName: string;
  accrued: number;
  taken: number;
  reserved: number;
  available: number;
}

/** One leave request row inside the caller's own inbox. */
export interface SelfLeaveRequest {
  id: string;
  leaveTypeCode: string;
  startDate: string;
  endDate: string;
  requestedDays: number;
  status: string;
  rejectionReason?: string | null;
  crossesCutoff: boolean;
  createdAt: string;
}

/** Full self-service leave envelope: identity, balances and own requests. */
export interface MyLeave {
  workerId: string;
  workerName: string;
  employeeNo?: string | null;
  linked: boolean;
  balances: MyLeaveBalance[];
  requests: SelfLeaveRequest[];
}

/** Cancel endpoint returns the updated admin-style leave row. */
export interface LeaveRequestLine {
  id: string;
  status: string;
  requestedDays: number;
}

