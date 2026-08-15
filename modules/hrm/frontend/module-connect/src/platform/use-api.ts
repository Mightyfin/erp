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

export function useApi<T>(fn: () => Promise<T>, deps: unknown[] = []): ApiState<T> {
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
    const toText = (v: unknown) => (v === undefined || v === null ? "" : String(v));
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
        ? (((w.bankDetails as Array<{ accountNumber?: unknown }>)[0]?.accountNumber ??
            "") as string)
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
  workerDocuments: (workerId: string) => hrmApi.get<unknown>(`/hrm/documents/worker/${workerId}`),
  reports: (params: Record<string, unknown>) => hrmApi.get<unknown>("/hrm/reports", params),
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
    const page = await hrmApi.get<{ items?: unknown[] }>("/hrm/admin/locations");
    return page.items ?? page;
  },
  /** Download a document and return { url, fileName }. Caller revokes url. */
  downloadDocument: async (documentId: string, fileName: string) => {
    const url = await hrmApi.downloadDocument(documentId);
    return { url, fileName };
  },

  /* ------------------------------------------------------------------ */
  /* Additional surfaces wired for M11 — same { items } envelope shape   */
  /* ------------------------------------------------------------------ */

  /**
   * M17 admin leave inbox (roles hr_ops / hr_admin / manager): company-wide
   * leave requests with optional status + worker filters. GET /hrm/time/leave.
   */
  leaveRequests: (params?: Record<string, unknown>) =>
    hrmApi.get<{
      items: {
        id: string;
        workerId: string;
        workerName: string;
        leaveTypeCode: string;
        startDate: string;
        endDate: string;
        requestedDays: number;
        status: string;
        balanceReserved: boolean;
        crossesCutoff: boolean;
        createdAt: string;
      }[];
      totalCount: number;
      page: number;
      pageSize: number;
    }>("/hrm/time/leave", params ?? {}),
  leaveBalances: (workerId: string) =>
    hrmApi.get<unknown[]>(`/hrm/time/leave/balances/${workerId}`),
  createLeaveRequest: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/time/leave", body),
  /** Backend expects { action: 'approve'|'reject'|'return', reason? } */
  decideLeaveRequest: (id: string, action: string, reason?: string) =>
    hrmApi.post<unknown>(`/hrm/time/leave/${id}/decide`, { action, reason }),
  leaveTypes: (params?: Record<string, unknown>) =>
    hrmApi.get<unknown[]>("/hrm/admin/leave-types", {
      includeInactive: false,
      ...(params ?? {}),
    }),
  timeCorrections: (params?: Record<string, unknown>) =>
    hrmApi.get<{ items: unknown[] }>("/hrm/time/corrections", params ?? {}),
  createCorrection: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/time/corrections", body),
  decideCorrection: (id: string, action: string, reason?: string) =>
    hrmApi.post<unknown>(`/hrm/time/corrections/${id}/decide`, { action, reason }),

  /** Time: attendance (clocking). */
  clockIn: (workerId: string) =>
    hrmApi.post<unknown>(`/hrm/time/attendance/${workerId}/clock-in`, null),
  clockOut: (workerId: string) =>
    hrmApi.post<unknown>(`/hrm/time/attendance/${workerId}/clock-out`, null),
  attendanceToday: (workerId: string) =>
    hrmApi.get<unknown>(`/hrm/time/attendance/${workerId}/today`),
  attendanceHistory: (workerId: string, params?: Record<string, unknown>) =>
    hrmApi.get<unknown>(`/hrm/time/attendance/${workerId}`, params ?? {}),
  roster: (workerId: string, params?: Record<string, unknown>) =>
    hrmApi.get<unknown>(`/hrm/time/roster/${workerId}`, params ?? {}),

  /** Workflow: shared approval queue + request detail/decisions. */
  workflowQueue: () => hrmApi.get<{ items: unknown[] }>("/hrm/workflow/queue"),
  workflowRequest: (id: string) => hrmApi.get<unknown>(`/hrm/workflow/requests/${id}`),
  /** Backend expects { action, reason? } (camelCase from WorkflowDecisionRequest). */
  workflowDecide: (id: string, action: string, reason?: string) =>
    hrmApi.post<unknown>(`/hrm/workflow/requests/${id}/decisions`, { action, reason }),
  workflowEscalate: (id: string, body: Record<string, unknown>) =>
    hrmApi.post<unknown>(`/hrm/workflow/requests/${id}/escalate`, body),

  /** Experience: letters, service requests, speak-up. */
  experienceRequests: (params?: Record<string, unknown>) =>
    hrmApi.get<{ items: unknown[]; totalCount: number }>("/hrm/experience/requests", params ?? {}),
  createExperienceRequest: (body: Record<string, unknown>) =>
    hrmApi.post<unknown>("/hrm/experience/requests", body),
  addRequestMessage: (id: string, body: Record<string, unknown>) =>
    hrmApi.post<unknown>(`/hrm/experience/requests/${id}/messages`, body),
  resolveRequest: (id: string) =>
    hrmApi.post<unknown>(`/hrm/experience/requests/${id}/resolve`, null),
  /** Onboarding readiness for one worker — 5-item statutory/banking checklist. */
  onboardingPlan: (workerId: string) =>
    hrmApi.get<{
      workerId?: string;
      isOnboarded?: boolean;
      tasksCompleted?: number;
      tasksTotal?: number;
    }>(`/hrm/workers/${workerId}/onboarding`),
  experienceLetters: (params?: Record<string, unknown>) =>
    hrmApi.get<{ items: unknown[] }>("/hrm/experience/letters", params ?? {}),
  createLetter: (body: Record<string, unknown>) =>
    hrmApi.post<unknown>("/hrm/experience/letters", body),
  approveLetter: (id: string) =>
    hrmApi.post<unknown>(`/hrm/experience/letters/${id}/approve`, null),
  speakUp: (body: Record<string, unknown>) =>
    hrmApi.post<{ caseReference?: string; accessCode?: string }>("/hrm/experience/speak-up", body),
  speakUpStatus: (caseReference: string, accessCode: string) =>
    hrmApi.get<unknown>("/hrm/experience/speak-up/status", { caseReference, accessCode }),

  /** Payroll: configuration + runs. */
  payrollComponents: (params?: Record<string, unknown>) =>
    hrmApi.get<unknown[]>("/hrm/payroll/components", params ?? {}),
  payrollPayGroups: () => hrmApi.get<unknown[]>("/hrm/payroll/pay-groups"),
  payrollPayGroupPeriods: (groupId: string) =>
    hrmApi.get<unknown[]>(`/hrm/payroll/pay-groups/${groupId}/periods`),
  payrollTaxSlabs: (taxYear: string) =>
    hrmApi.get<unknown[]>("/hrm/payroll/tax-slabs", { taxYear }),
  payrollContributionRules: () => hrmApi.get<unknown[]>("/hrm/payroll/contribution-rules"),

  /* ------------------------------------------------------------------ */
  /* M23 statutory compliance: PAYE return + NAPSA/NHIMA remittance files */
  /* ------------------------------------------------------------------ */

  /** Generate a statutory CSV for one period and hand back a downloadable blob URL. */
  statutoryGenerate: async (exportType: string, periodId: string) => {
    const blob = await hrmApi.statutoryExport(exportType, periodId);
    const url = URL.createObjectURL(blob);
    const fileName = `${exportType}-${periodId}.csv`;
    return { url, fileName };
  },
  /** Aggregate statutory liability summary for one period (no download). */
  statutorySummary: (periodId: string) =>
    hrmApi.get<Record<string, unknown>>(`/hrm/statutory-exports/summary?periodId=${periodId}`),
  payrollStructures: () => hrmApi.get<unknown[]>("/hrm/payroll/structures"),
  updateStructure: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<unknown>(`/hrm/payroll/structures/${id}`, body),
  payrollProfiles: (params?: Record<string, unknown>) =>
    hrmApi.get<unknown[]>("/hrm/payroll/profiles", params ?? {}),
  createPayrollProfile: (workerId: string, body: Record<string, unknown>) =>
    hrmApi.post<unknown>(`/hrm/payroll/profiles/${workerId}`, body),
  /** The backend has no list endpoint for runs; pages select a period via
   *  payrollPayGroupPeriods(groupId) and then read payrollRun(id). */
  payrollRuns: () =>
    Promise.resolve<{ items: never[]; totalCount: number }>({ items: [], totalCount: 0 }),
  payrollRun: (id: string) => hrmApi.get<unknown>(`/hrm/payroll/runs/${id}`),
  createPayrollRun: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/payroll/runs", body),
  calculatePayrollRun: (id: string) =>
    hrmApi.post<unknown>(`/hrm/payroll/runs/${id}/calculate`, null),
  lockPayrollRun: (id: string) => hrmApi.post<unknown>(`/hrm/payroll/runs/${id}/lock`, null),
  payrollRunApprove: (id: string) => hrmApi.post<unknown>(`/hrm/payroll/runs/${id}/approve`, null),
  payrollRunRelease: (id: string) => hrmApi.post<unknown>(`/hrm/payroll/runs/${id}/release`, null),
  payrollRunReverse: (id: string) => hrmApi.post<unknown>(`/hrm/payroll/runs/${id}/reverse`, null),
  payrollRunLines: (id: string) => hrmApi.get<unknown>(`/hrm/payroll/runs/${id}/lines`),
  /** M24: per-run statutory identity readiness — who blocks the release gate. */
  payrollRunStatutoryReadiness: (id: string) =>
    hrmApi.get<{
      runId?: string;
      periodLabel?: string;
      isReady?: boolean;
      workerCount?: number;
      workers?: Array<{
        workerId?: string;
        employeeNo?: string;
        fullName?: string;
        hasNrc?: boolean;
        hasTpin?: boolean;
        hasNapsaNumber?: boolean;
        hasNhimaNumber?: boolean;
        ready?: boolean;
      }>;
    }>(`/hrm/payroll/runs/${id}/statutory-readiness`),
  /** M24: payslip by id — the snapshot includes statutory references. */
  payslipById: (id: string) => hrmApi.get<unknown>(`/hrm/payroll/payslips/id/${id}`),
  /** Trigger payslip document (PDF) generation, returns the updated payslip. */
  payslipGenerate: (id: string) =>
    hrmApi.post<unknown>(`/hrm/payroll/payslips/${id}/generate`, null),

  /** Recruitment: vacancies, candidates, offers. */
  recruitmentVacancies: (params?: Record<string, unknown>) =>
    hrmApi.get<{ items: unknown[] }>("/hrm/recruitment/vacancies", params ?? {}),
  createVacancy: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/recruitment/vacancies", body),
  publishVacancy: (id: string) =>
    hrmApi.post<unknown>(`/hrm/recruitment/vacancies/${id}/publish`, null),
  closeVacancy: (id: string) =>
    hrmApi.post<unknown>(`/hrm/recruitment/vacancies/${id}/close`, null),
  vacancyCandidates: (vacancyId: string, params?: Record<string, unknown>) =>
    hrmApi.get<{ items: unknown[] }>(
      `/hrm/recruitment/vacancies/${vacancyId}/candidates`,
      params ?? {},
    ),
  createCandidate: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/recruitment/candidates", body),
  advanceCandidate: (id: string, body: Record<string, unknown>) =>
    hrmApi.post<unknown>(`/hrm/recruitment/candidates/${id}/advance`, body),
  createOffer: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/recruitment/offers", body),
  issueOffer: (id: string) => hrmApi.post<unknown>(`/hrm/recruitment/offers/${id}/issue`, null),
  acceptOffer: (id: string, body: Record<string, unknown>) =>
    hrmApi.post<unknown>(`/hrm/recruitment/offers/${id}/accept`, body),

  /** Relations: cases. */
  relationsCases: (params?: Record<string, unknown>) =>
    hrmApi.get<{ items: unknown[] }>("/hrm/relations/cases", params ?? {}),
  createCase: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/relations/cases", body),

  /** Admin config: org tree, legal entities, calendars, holidays, capabilities. */
  orgTree: () => hrmApi.get<unknown>("/hrm/admin/org-units/tree"),
  legalEntities: () => hrmApi.get<unknown[]>("/hrm/admin/legal-entities"),
  calendars: () => hrmApi.get<unknown[]>("/hrm/admin/calendars"),
  capabilities: () => hrmApi.get<unknown[]>("/hrm/admin/capabilities"),

  /* ------------------------------------------------------------------ */
  /* M19 organisation configuration CRUD (write surfaces)                */
  /* ------------------------------------------------------------------ */

  /** Create an org unit (department / cost centre). */
  createOrgUnit: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/admin/org-units", body),
  /** Patch-update an org unit (fields sent as-is, backend accepts partial). */
  updateOrgUnit: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/admin/org-units/${id}`, body),
  /** Effectively-date a unit closure (EffectiveDate must be today or later). */
  closeOrgUnit: (id: string, effectiveDate: string, reason?: string) =>
    hrmApi.post<unknown>(`/hrm/admin/org-units/${id}/close`, { effectiveDate, reason }), // OrgUnitCloseRequest.EffectiveDate
  /** Create a work location. */
  createLocation: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/admin/locations", body),
  /** Patch-update a work location. */
  updateLocation: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/admin/locations/${id}`, body),
  /** Create a legal entity. */
  createLegalEntity: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/admin/legal-entities", body),

  /* ------------------------------------------------------------------ */
  /* M20 payroll setup configuration (pay groups, ZRA PAYE slabs,        */
  /* NAPSA/NHIMA contribution rules, salary components)                  */
  /* ------------------------------------------------------------------ */

  /** Full pay-group list with statuses — GET /payroll/pay-groups/full. */
  payGroupsFull: () => hrmApi.get<unknown[]>("/hrm/payroll/pay-groups/full"),
  /** Patch-update a pay group (frequency, currency, payday calendar, defaults). */
  updatePayGroup: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/payroll/pay-groups/${id}`, body),
  /** Patch-update a ZRA PAYE tax slab (rate, band ceiling). */
  updateTaxSlab: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/payroll/tax-slabs/${id}`, body),
  /** Patch-update a statutory contribution rule (NAPSA/NHIMA rate/ceiling/floor). */
  updateContributionRule: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/payroll/contribution-rules/${id}`, body),
  /** Patch-update a salary component (rate, fixed amount, taxable flag, archive). */
  updateSalaryComponent: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/payroll/components/${id}`, body),

  /** Worker lifecycle: onboarding snapshot, offboarding, bank details. */
  workerOnboarding: (workerId: string) =>
    hrmApi.get<unknown>(`/hrm/workers/${workerId}/onboarding`),
  offboardWorker: (workerId: string, body: Record<string, unknown>) =>
    hrmApi.post<unknown>(`/hrm/workers/${workerId}/offboard`, body),
  addBankDetails: (workerId: string, body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/workers/${workerId}/bank-details`, body),
  removeBankDetails: (workerId: string, bankId: string) =>
    hrmApi.delete<unknown>(`/hrm/workers/${workerId}/bank-details/${bankId}`),
};
