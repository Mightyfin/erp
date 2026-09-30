import { hrmApi } from "./api-client";

export type AttendanceEmployee = { id: string; employeeNo: string; fullName: string; status: string; locationId?: string; locationName?: string; orgUnitId?: string; orgUnitName?: string; startDate?: string; endDate?: string };
export type AttendanceTimes = { clockIn: string; clockOut: string };
export type SavedAttendance = { id: string; workerId: string; clockIn?: string; clockOut?: string; totalHours: number; overtimeStatus: string; overtimePayrollRunId?: string };
export function todayLocal() {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`;
}
export async function attendanceEmployees(): Promise<AttendanceEmployee[]> {
  const rows: AttendanceEmployee[] = [];
  for (let page = 1; ; page++) {
    const result = await hrmApi.get<{ items: AttendanceEmployee[]; totalCount: number }>("/hrm/workers", { page, pageSize: 100 });
    rows.push(...result.items);
    if (!result.items.length || rows.length >= result.totalCount) break;
  }
  return rows.filter(w => ["active", "on-leave", "notice"].includes(w.status)).sort((a, b) => a.fullName.localeCompare(b.fullName));
}
export function activeOn(w: AttendanceEmployee, date: string) {
  return (!w.startDate || w.startDate.slice(0, 10) <= date) && (!w.endDate || w.endDate.slice(0, 10) >= date);
}
export function timesError(times: AttendanceTimes) {
  if (!/^\d{2}:\d{2}$/.test(times.clockIn) || !/^\d{2}:\d{2}$/.test(times.clockOut)) return "Enter both clock-in and clock-out times.";
  if (times.clockIn === times.clockOut) return "Clock-in and clock-out must be different.";
  return "";
}
export function saveAttendance(workDate: string, rows: Array<AttendanceTimes & { workerId: string }>) {
  return hrmApi.post<SavedAttendance[]>("/hrm/time/attendance/manual", { workDate, rows });
}
