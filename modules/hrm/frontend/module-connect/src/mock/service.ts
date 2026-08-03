/**
 * Mock service layer. No backend, no persistence beyond the browser session.
 * Every read has a realistic delay and can be forced into a degraded state so
 * screens exercise their loading / empty / failure branches.
 */
import {
  attendanceCorrections,
  employees,
  entities,
  hrCases,
  leaveRequests,
  notifications,
  workItems,
} from "./data";
import type { Role, WorkItem } from "./types";

const delay = (ms = 420) => new Promise((r) => setTimeout(r, ms));

export class DegradedServiceError extends Error {
  constructor(public service: string) {
    super(`${service} is temporarily unavailable`);
    this.name = "DegradedServiceError";
  }
}

const degraded = new Set<string>();
export const setDegraded = (service: string, on: boolean) => {
  if (on) degraded.add(service);
  else degraded.delete(service);
};
export const isDegraded = (service: string) => degraded.has(service);

async function read<T>(service: string, value: T, ms?: number): Promise<T> {
  await delay(ms);
  if (degraded.has(service)) throw new DegradedServiceError(service);
  return value;
}

export const api = {
  entities: () => read("directory", entities, 260),
  employees: () => read("directory", employees),
  employee: (id: string) => read("directory", employees.find((e) => e.id === id) ?? null),
  leaveRequests: () => read("leave", leaveRequests),
  leaveRequest: (id: string) => read("leave", leaveRequests.find((l) => l.id === id) ?? null),
  attendance: () => read("attendance", attendanceCorrections),
  attendanceItem: (id: string) =>
    read("attendance", attendanceCorrections.find((a) => a.id === id) ?? null),
  cases: () => read("cases", hrCases),
  caseItem: (id: string) => read("cases", hrCases.find((c) => c.id === id) ?? null),
  notifications: () => read("notifications", notifications, 200),
  workQueue: (role: Role): Promise<WorkItem[]> =>
    read(
      "workqueue",
      workItems.filter((i) => i.roles.includes(role)),
      380,
    ),
  submit: async (_kind: string, _payload: unknown) => {
    await delay(700);
    return { id: `TMP-${Math.floor(Math.random() * 9000 + 1000)}`, ok: true as const };
  },
};