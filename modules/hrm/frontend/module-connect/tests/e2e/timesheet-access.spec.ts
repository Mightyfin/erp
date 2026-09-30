import { expect, test } from "@playwright/test";

// Run against a frontend built with VITE_USE_REAL_API=true.
test("timesheet operator sees employee attendance and cannot navigate to administration", async ({ page }) => {
  const unexpected: string[] = [];
  await page.route("**/api/hrm/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    let body: unknown;
    if (path === "/api/hrm/auth/capabilities") body = { mode: "local", localUsersEnabled: true, identityConfigured: false };
    else if (path === "/api/hrm/auth/me") body = { authenticated: true, user: {
      id: "clerk", displayName: "Timesheet Clerk", email: "clerk@example.test",
      roles: ["timesheet_operator"], isActive: true, mustChangePassword: false,
    } };
    else if (path === "/api/hrm/branding") body = {};
    else if (path === "/api/hrm/time/attendance") body = [{
      id: "record", workerId: "another-employee", workerName: "Other Employee", workerEmployeeNo: "EMP-002",
      workDate: new Date().toISOString().slice(0, 10), clockIn: "08:00", clockOut: "19:00",
      totalHours: 11, regularHours: 8, scheduledHours: 8, overtimeHours: 3,
      overtimeStatus: "pending", derivedStatus: "present", source: "device-import",
    }];
    else if (path === "/api/hrm/import/schemas") body = [{
      typeKey: "attendance", displayName: "Attendance logs", fields: [
        { key: "employeeNo", label: "Employee number", required: true },
        { key: "workDate", label: "Date", required: true },
        { key: "clockIn", label: "Clock in", required: false },
        { key: "clockOut", label: "Clock out", required: false },
      ],
    }];
    else { unexpected.push(path); await route.fulfill({ status: 403, json: { message: "Denied" } }); return; }
    await route.fulfill({ json: body });
  });
  await page.goto("/hrm/configuration/users");
  await expect(page).toHaveURL(/\/hrm\/time\/timesheets$/, { timeout: 20000 });
  await expect(page.getByRole("heading", { name: "Timesheet summary" })).toBeVisible();
  await expect(page.getByRole("navigation", { name: "Timesheet navigation" })).toBeVisible();
  await expect(page.getByText("Other Employee").first()).toBeVisible();
  await page.getByText("Other Employee").first().click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await expect(page.getByRole("button", { name: "Review overtime", exact: true })).toHaveCount(0);
  await page.getByRole("button", { name: "Close", exact: true }).first().click();
  await page.getByRole("navigation", { name: "Timesheet navigation" }).getByRole("link", { name: "Import attendance" }).click();
  await expect(page.getByRole("heading", { name: "Import attendance", level: 1 })).toBeVisible();
  await expect(page.getByText("Import overtime hours only")).toHaveCount(0);
  await expect(page.getByRole("link", { name: "User access" })).toHaveCount(0);
  expect(unexpected).toEqual([]);
});
