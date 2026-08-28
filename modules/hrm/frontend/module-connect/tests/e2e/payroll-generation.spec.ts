import { expect, test, type Page, type Route } from "@playwright/test";

const runId = "019f0000-0000-7000-8000-000000000901";
const payGroupId = "019f0000-0000-7000-8000-000000000902";
const periodId = "019f0000-0000-7000-8000-000000000903";
const workerId = "019f0000-0000-7000-8000-000000000904";
const lineId = "019f0000-0000-7000-8000-000000000905";

const hrManager = {
  id: "hr-manager-qa",
  email: "hr.manager@example.test",
  displayName: "HR Manager QA",
  roles: ["hr_admin", "hr_ops", "payroll"],
  workerId,
  isActive: true,
  mustChangePassword: false,
};

const entityTree = [
  {
    id: "entity-zm",
    code: "NWC-ZM",
    name: "New World Cargo Zambia Ltd",
    unitType: "entity",
    status: "active",
    managerId: null,
    managerName: null,
    effectiveFrom: "2026-01-01",
    effectiveTo: null,
    children: [
      {
        id: "branch-lusaka",
        code: "LUS",
        name: "Lusaka Operations",
        unitType: "department",
        status: "active",
        managerId: null,
        managerName: null,
        effectiveFrom: "2026-01-01",
        effectiveTo: null,
        children: [],
      },
    ],
  },
];

function json(route: Route, body: unknown, status = 200) {
  return route.fulfill({
    status,
    contentType: "application/json",
    body: JSON.stringify(body),
  });
}

async function stubShell(page: Page) {
  await page.addInitScript(() => {
    localStorage.clear();
    sessionStorage.clear();
  });
  await page.route("**/api/hrm/**", async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;

    if (path.endsWith("/hrm/auth/me")) {
      await json(route, { authenticated: true, user: hrManager });
      return;
    }
    if (path.endsWith("/hrm/me")) {
      await json(route, {
        linked: true,
        subject: hrManager.id,
        worker: {
          id: workerId,
          employeeNo: "EMP-0904",
          fullName: "HR Manager QA",
          jobTitle: "HR Manager",
          status: "active",
        },
      });
      return;
    }
    if (path.endsWith("/hrm/admin/legal-entities")) {
      await json(route, [{ id: "entity-zm", registeredName: "New World Cargo Zambia Ltd", countryCode: "ZM" }]);
      return;
    }
    if (path.endsWith("/hrm/admin/locations")) {
      await json(route, { items: [{ id: "branch-lusaka", name: "Lusaka Operations", legalEntityId: "entity-zm" }] });
      return;
    }
    if (path.endsWith("/hrm/shell")) {
      await json(route, { entityId: "entity-zm", locationId: null, scopedToBranch: false, assignedLocationIds: [], confined: false });
      return;
    }
    if (path.endsWith("/hrm/me/notifications")) {
      await json(route, { unreadCount: 0, items: [] });
      return;
    }
    if (path.endsWith("/hrm/workflow/queue")) {
      await json(route, { items: [], totalCount: 0 });
      return;
    }
    if (path.endsWith("/hrm/time/leave")) {
      await json(route, { items: [], totalCount: 0 });
      return;
    }
    if (path.endsWith("/hrm/time/corrections")) {
      await json(route, { items: [], totalCount: 0 });
      return;
    }
    if (path.endsWith("/hrm/setup/state")) {
      await json(route, { status: "complete" });
      return;
    }
    if (path.endsWith("/hrm/admin/org-units/entity-tree")) {
      await json(route, entityTree);
      return;
    }

    await json(route, { items: [], totalCount: 0 });
  });
}

async function stubPayrollSetup(page: Page, options: { emptyPopulation?: boolean; blockingPreflight?: boolean } = {}) {
  await page.route("**/api/hrm/payroll/**", async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;
    const method = request.method();

    if (path.endsWith("/payroll/pay-groups")) {
      await json(route, [{ id: payGroupId, code: "MONTHLY", name: "Monthly salaried", currency: "ZMW", isDefault: true }]);
      return;
    }
    if (path.endsWith(`/payroll/pay-groups/${payGroupId}/periods`)) {
      await json(route, [{
        id: periodId,
        periodLabel: "August 2026",
        startDate: "2026-08-01",
        endDate: "2026-08-31",
        cutoffDate: "2026-08-24",
        payDate: "2026-08-28",
        status: "open",
      }]);
      return;
    }
    if (path.endsWith("/payroll/profiles")) {
      await json(route, options.emptyPopulation ? [] : [{
        id: "profile-1",
        workerId,
        workerName: "Chanda Mwansa",
        payGroupId,
        payGroupName: "Monthly salaried",
        effectiveFrom: "2026-01-01",
        values: [{ amount: 25000 }],
      }]);
      return;
    }
    if (path.endsWith("/payroll/runs/preflight")) {
      await json(route, {
        payPeriodId: periodId,
        payGroupId,
        ready: !options.blockingPreflight,
        includedWorkerCount: options.emptyPopulation ? 0 : 1,
        warningCount: 0,
        checks: options.blockingPreflight
          ? [{ id: "population", label: "Workers have payroll profiles", state: "fail", detail: "No active payroll profiles were found.", count: 0 }]
          : [{ id: "population", label: "Workers have payroll profiles", state: "pass", detail: "1 worker will be included.", count: 1 }],
      });
      return;
    }
    if (method === "POST" && path.endsWith("/payroll/runs")) {
      await json(route, { id: runId, periodLabel: "August 2026", status: "draft" });
      return;
    }

    await route.fallback();
  });

  await page.route("**/api/hrm/workers**", async (route) => {
    await json(route, {
      items: [{
        id: workerId,
        employeeNo: "EMP-0904",
        firstName: "Chanda",
        lastName: "Mwansa",
        fullName: "Chanda Mwansa",
        jobTitle: "Operations Associate",
        status: "active",
        bankAccount: "001234567890",
        nationalId: "123456/10/1",
        tpin: "1000000001",
        napsaNumber: "200000000001",
        nhimaNumber: "300000000001",
      }],
      totalCount: 1,
    });
  });
}

function runDto(status: string, paymentStatus = "not-created") {
  return {
    id: runId,
    status,
    periodLabel: "August 2026",
    entityName: "New World Cargo Zambia Ltd",
    payGroup: "Monthly salaried",
    currency: "ZMW",
    employeeCount: status === "draft" || status === "locked" ? 0 : 1,
    totalGross: status === "draft" || status === "locked" ? 0 : 25000,
    totalDeductions: status === "draft" || status === "locked" ? 0 : 4125,
    totalNet: status === "draft" || status === "locked" ? 0 : 20875,
    totalEmployerCost: status === "draft" || status === "locked" ? 0 : 26375,
    preparedBySubjectId: "payroll-preparer",
    approvedBySubjectId: status === "approved" || status === "released" || status === "closed" ? "hr-approver" : null,
    releasedBySubjectId: status === "released" || status === "closed" ? "payroll-release-officer" : null,
    paymentStatus,
    paymentFileReference: paymentStatus === "not-created" ? null : "PAY-202608-HRMQA",
    paymentFileGeneratedBySubjectId: paymentStatus === "not-created" ? null : "payroll-file-maker",
    paymentApprovedBySubjectId: paymentStatus === "approved" || paymentStatus === "released" || paymentStatus === "reconciled" ? "finance-approver" : null,
    paymentReleasedBySubjectId: paymentStatus === "released" || paymentStatus === "reconciled" ? "treasury-releaser" : null,
    reconciliationReference: paymentStatus === "reconciled" ? "BANK-ACK-HRMQA" : null,
  };
}

async function stubPayrollLifecycle(page: Page) {
  let status = "draft";
  let paymentStatus = "not-created";

  await page.route("**/api/hrm/payroll/runs/**", async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;
    const method = request.method();

    if (path.endsWith("/audit")) {
      await json(route, []);
      return;
    }
    if (path.endsWith("/calculation-readiness")) {
      await json(route, {
        ready: true,
        includedWorkerCount: 1,
        blockingCount: 0,
        warningCount: 0,
        checks: [
          { id: "earning-components", label: "Earning components configured", state: "pass", detail: "Basic pay is configured.", count: 1 },
          { id: "tax-slabs", label: "Tax slabs configured", state: "pass", detail: "PAYE slabs are configured.", count: 1 },
        ],
        issues: [],
      });
      return;
    }
    if (path.endsWith("/lines")) {
      await json(route, status === "draft" || status === "locked" ? { items: [], totalCount: 0 } : {
        items: [{
          id: lineId,
          workerId,
          workerName: "Chanda Mwansa",
          grossPay: 25000,
          totalDeductions: 4125,
          netPay: 20875,
          employerCost: 26375,
          hasException: false,
          exceptionStatus: "resolved",
          components: [
            { componentCode: "BASIC", componentName: "Basic salary", componentType: "earning", amount: 25000, explanation: "Monthly salary from active payroll profile." },
            { componentCode: "PAYE", componentName: "PAYE", componentType: "deduction", amount: 4125, isStatutory: true, explanation: "Configured PAYE tax slab." },
          ],
        }],
        totalCount: 1,
      });
      return;
    }
    if (path.endsWith("/statutory-readiness")) {
      await json(route, { isReady: true, workers: [{ workerId, employeeNo: "EMP-0904", fullName: "Chanda Mwansa", ready: true }] });
      return;
    }
    if (path.endsWith("/payments/readiness")) {
      await json(route, { ready: true, payableCount: 1, totalNet: 20875, missingBankDetailsCount: 0, issues: [] });
      return;
    }
    if (path.endsWith("/payslips")) {
      await json(route, { items: [], totalCount: 0 });
      return;
    }
    if (method === "POST" && path.endsWith("/payments/generate")) paymentStatus = "generated";
    else if (method === "POST" && path.endsWith("/payments/approve")) paymentStatus = "approved";
    else if (method === "POST" && path.endsWith("/payments/release")) paymentStatus = "released";
    else if (method === "POST" && path.endsWith("/reconcile")) {
      status = "closed";
      paymentStatus = "reconciled";
    }
    else if (method === "POST" && path.endsWith("/lock")) status = "locked";
    else if (method === "POST" && path.endsWith("/calculate")) status = "calculated";
    else if (method === "POST" && path.endsWith("/approve")) status = "approved";
    else if (method === "POST" && path.endsWith("/release")) status = "released";
    else if (method === "POST" && path.endsWith("/cancel")) status = "reversed";
    else if (method === "POST" && path.endsWith("/reverse")) {
      status = "reversed";
      await json(route, { ...runDto("draft"), isReversal: true, reversesRunId: runId });
      return;
    }

    await json(route, runDto(status, paymentStatus));
  });
}

test.describe("HR manager payroll generation", () => {
  test.beforeEach(async ({ page }) => {
    const browserErrors: string[] = [];
    page.on("console", (message) => {
      if (message.type() === "error") browserErrors.push(message.text());
    });
    page.on("pageerror", (error) => browserErrors.push(error.message));
    await stubShell(page);
    await page.exposeFunction("__assertNoBrowserErrors", () => {
      expect(browserErrors).toEqual([]);
    });
  });

  test("opens a payroll run only after readiness and population checks pass", async ({ page }) => {
    await stubPayrollSetup(page);

    await page.goto("/hrm/payroll/runs/new");
    await expect(page.getByRole("heading", { name: "Start a pay run" })).toBeVisible();
    await page.getByRole("button", { name: "Continue" }).click();
    await expect(page.getByText("Chanda Mwansa")).toBeVisible();
    await page.getByRole("button", { name: "Continue" }).click();
    await expect(page.getByText("Workers have payroll profiles")).toBeVisible();
    await page.getByRole("button", { name: "Continue" }).click();
    await expect(page.getByText("Employees included")).toBeVisible();
    await page.getByRole("button", { name: "Open the run", exact: true }).click();

    await expect(page.getByText("Pay run opened")).toBeVisible();
    await expect(page.getByText(runId)).toBeVisible();
    await page.evaluate(() => (window as unknown as { __assertNoBrowserErrors: () => void }).__assertNoBrowserErrors());
  });

  test("blocks run creation when the selected pay group has no payroll population", async ({ page }) => {
    await stubPayrollSetup(page, { emptyPopulation: true, blockingPreflight: true });

    await page.goto("/hrm/payroll/runs/new");
    await page.getByRole("button", { name: "Continue" }).click();
    await expect(page.getByText("No active payroll profiles were found for this pay group")).toBeVisible();
    await page.getByRole("button", { name: "Continue" }).click();
    await expect(page.getByText("Fix 1 blocking readiness issue before opening this run.")).toBeVisible();
    await page.getByRole("button", { name: "Continue" }).click();
    await page.getByRole("button", { name: "Open the run", exact: true }).click();

    await expect(page.getByText("Readiness blockers remain")).toBeVisible();
    await expect(page.getByText("Pay run opened")).toHaveCount(0);
    await page.evaluate(() => (window as unknown as { __assertNoBrowserErrors: () => void }).__assertNoBrowserErrors());
  });

  test("runs the payroll detail lifecycle through calculation, approval, release, payment and reconciliation", async ({ page }) => {
    await stubPayrollLifecycle(page);

    await page.goto(`/hrm/payroll/runs/${runId}`);
    await expect(page.getByRole("heading", { name: "August 2026 — Monthly salaried" })).toBeVisible();

    await page.getByRole("button", { name: "Lock inputs" }).click();
    await expect(page.getByText("Payroll inputs locked.")).toBeVisible();
    await page.getByRole("button", { name: "Calculate run" }).click();
    await expect(page.getByText("Calculation complete.")).toBeVisible();
    await expect(page.getByRole("button", { name: /Chanda Mwansa/ })).toBeVisible();

    await page.getByRole("button", { name: "approve" }).click();
    await page.getByRole("button", { name: "Record decision" }).click();
    await expect(page.getByText("August 2026 approved for 1 employees.")).toBeVisible();

    await page.getByRole("button", { name: "Release payslips" }).click();
    await page.getByRole("alertdialog", { name: "Release payslips" }).getByRole("button", { name: "Release payslips" }).click();
    await expect(page.getByText("Payslips released to 1 employees.")).toBeVisible();
    await page.getByRole("button", { name: "Generate bank file" }).click();
    await expect(page.getByText("Payment file generated")).toBeVisible();
    await page.getByRole("button", { name: "Approve payment file" }).click();
    await expect(page.getByText("Payment file approved")).toBeVisible();
    await page.getByRole("button", { name: "Release to bank" }).click();
    await expect(page.getByText("Payment instruction released")).toBeVisible();
    await page.getByLabel("Bank acknowledgement reference").fill("BANK-ACK-HRMQA");
    await page.getByRole("button", { name: "Reconcile and close" }).click();

    await expect(page.getByText("Reconciled and closed · BANK-ACK-HRMQA")).toBeVisible();
    await page.evaluate(() => (window as unknown as { __assertNoBrowserErrors: () => void }).__assertNoBrowserErrors());
  });

  test("lets a top admin void an unreleased payroll run with an audit reason", async ({ page }) => {
    await stubPayrollLifecycle(page);

    await page.goto(`/hrm/payroll/runs/${runId}`);
    await page.getByRole("button", { name: "Lock inputs" }).click();
    await page.getByRole("button", { name: "Calculate run" }).click();
    await page.getByRole("button", { name: "Void unreleased run" }).click();
    await page.getByLabel("Reason").fill("Wrong period selected during testing");
    await page.getByRole("button", { name: "Void run" }).click();

    await expect(page.getByText("Payroll run voided.")).toBeVisible();
    await expect(page.getByText("Current backend status: reversed.")).toBeVisible();
    await page.evaluate(() => (window as unknown as { __assertNoBrowserErrors: () => void }).__assertNoBrowserErrors());
  });
});
