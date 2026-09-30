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
  await expect(page.getByRole("navigation", { name: "Main" })).toBeVisible();
  await expect(page.getByText("Other Employee").first()).toBeVisible();
  const menu = page.getByRole("navigation", { name: "Main" });
  await menu.getByRole("button", { name: "Performance", exact: true }).click();
  await expect(menu.getByText("Performance cycles", { exact: true })).toHaveAttribute("aria-disabled", "true");
  await expect(menu.getByRole("link", { name: "Performance cycles" })).toHaveCount(0);

  await page.getByText("Other Employee").first().click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await expect(page.getByRole("button", { name: "Review overtime", exact: true })).toHaveCount(0);
  await page.getByRole("button", { name: "Close", exact: true }).first().click();
  await page.getByRole("navigation", { name: "Main" }).getByRole("link", { name: "Import attendance" }).click();
  await expect(page.getByRole("heading", { name: "Import attendance", level: 1 })).toBeVisible();
  await expect(page.getByText("Import overtime hours only")).toHaveCount(0);
  await expect(page.getByRole("link", { name: "User access" })).toHaveCount(0);
  expect(unexpected).toEqual([]);
});

test("front desk can record single and bulk attendance without the full employee directory", async ({ page }) => {
  const saved: any[] = [];
  const unexpected: string[] = [];
  const employees = [1, 2].map(n => ({ id: `worker-${n}`, employeeNo: `EMP-${n}`, fullName: `Employee ${n}`, status: "active" }));
  await page.route("**/api/hrm/**", async route => {
    const path = new URL(route.request().url()).pathname;
    let body: unknown;
    if (path.endsWith("/auth/capabilities")) body = { mode: "local", localUsersEnabled: true, identityConfigured: false };
    else if (path.endsWith("/auth/me")) body = { authenticated: true, user: { id: "front-desk", email: "desk@example.test", displayName: "Front Desk", roles: ["front_desk", "timesheet_operator"], isActive: true } };
    else if (path.endsWith("/branding")) body = {};
    else if (path.endsWith("/attendance/employees")) body = employees;
    else if (path.endsWith("/attendance/manual")) {
      const input = route.request().postDataJSON(); saved.push(input);
      body = input.rows.map((r: any) => ({ ...r, id: r.workerId, totalHours: 8, overtimeStatus: "none" }));
    }
    else if (path.endsWith("/time/attendance")) body = [];
    else { unexpected.push(path); await route.fulfill({ status: 403, json: { message: "Denied" } }); return; }
    await route.fulfill({ json: body });
  });
  await page.goto("/hrm/time/timesheets");
  await page.getByRole("button", { name: "Add attendance", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Employee", { exact: true }).selectOption("worker-1");
  await dialog.getByLabel("Clock in", { exact: true }).fill("08:00");
  await dialog.getByLabel("Clock out", { exact: true }).fill("17:00");
  await dialog.getByRole("button", { name: "Save attendance" }).click();
  await expect(dialog).toHaveCount(0);
  await page.getByRole("navigation", { name: "Main" }).getByRole("link", { name: "Add bulk attendance" }).click();
  await expect(page.getByTestId("bulk-attendance-page")).toBeVisible();
  await page.getByLabel("Clock in for EMP-2", { exact: true }).fill("09:00");
  await page.getByLabel("Clock out for EMP-2", { exact: true }).fill("18:00");
  await page.getByRole("button", { name: /Save.*attendance|Save.*row/i }).click();
  await expect.poll(() => saved.length).toBe(2);
  expect(saved[0].rows).toEqual([{ workerId: "worker-1", clockIn: "08:00", clockOut: "17:00" }]);
  expect(saved[1].rows).toEqual([{ workerId: "worker-2", clockIn: "09:00", clockOut: "18:00" }]);
  expect(unexpected).toEqual([]);
});


test("front desk sidebar is available on mobile with restricted modules disabled", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.route("**/api/hrm/**", async route => {
    const path = new URL(route.request().url()).pathname;
    const body = path.endsWith("/auth/capabilities") ? { mode: "local", localUsersEnabled: true, identityConfigured: false }
      : path.endsWith("/auth/me") ? { authenticated: true, user: { id: "desk", roles: ["front_desk", "timesheet_operator"], email: "desk@example.test", isActive: true } }
      : path.endsWith("/branding") ? {} : [];
    await route.fulfill({ json: body });
  });
  await page.goto("/hrm/time/timesheets");
  await page.getByRole("button", { name: "Open navigation" }).click();
  const menu = page.getByRole("dialog").getByRole("navigation", { name: "Main" });
  await expect(menu.getByRole("link", { name: "Timesheets", exact: true })).toBeVisible();
  await menu.getByRole("button", { name: "Performance", exact: true }).click();
  await expect(menu.getByText("Performance cycles", { exact: true })).toHaveAttribute("aria-disabled", "true");
  await menu.getByRole("link", { name: "Add bulk attendance", exact: true }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await expect(page.getByTestId("bulk-attendance-page")).toBeVisible();
});
