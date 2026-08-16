import { expect, test } from "@playwright/test";

test("HRM entry renders or reaches the sign-in flow", async ({ page }) => {
  await page.goto("/hrm");

  await expect(page).toHaveURL(/\/(hrm|sign-in)(?:[/?#]|$)/);
  await expect(page.locator("body")).toContainText(
    /Checking your session|HRM|Human Resources|Sign in|Dashboard/i,
  );
});

test("authentication route is served by the HRM frontend", async ({ page }) => {
  const response = await page.goto("/sign-in");

  expect(response?.status()).toBe(200);
  await expect(page).not.toHaveTitle("404 Not Found");
  await expect(page.locator("body")).toContainText(/Sign in|Checking your session/i);
});

test("HRM reverse proxy exposes a healthy API", async ({ request }) => {
  const response = await request.get("/health/ready");

  expect(response.status()).toBe(200);
  expect(await response.json()).toMatchObject({ status: "healthy" });
});

test("HR admin can inspect and retry a failed notification handoff without seeing payload data", async ({
  page,
}) => {
  let retried = false;
  await page.route("**/api/hrm/admin/notifications**", async (route) => {
    if (route.request().method() === "POST") {
      retried = true;
      await route.fulfill({ status: 200, contentType: "application/json", body: "{}" });
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        pending: retried ? 1 : 0,
        publishing: 0,
        published: 12,
        failed: retried ? 0 : 1,
        fallbackDelivered: 0,
        items: retried
          ? []
          : [
              {
                id: "019d0000-0000-7000-8000-000000000001",
                publicId: "evt_0123456789abcdef0123456789abcdef",
                eventType: "hrm.leave.decided",
                status: "failed",
                publishAttempts: 3,
                lastTransport: "nats-jetstream",
                lastError: "broker unavailable",
                correlationId: "corr-browser-test",
                createdAt: "2026-08-16T08:00:00Z",
                availableAt: "2026-08-16T08:05:00Z",
                publishedAt: null,
              },
            ],
      }),
    });
  });

  await page.goto("/hrm/configuration/technical");
  await page.getByRole("tab", { name: "Notification delivery" }).click();

  const status = page.getByTestId("notification-delivery-status");
  await expect(status).toContainText("hrm.leave.decided");
  await expect(status).toContainText("failed");
  await expect(status).not.toContainText("employee@example.test");
  await expect(status).not.toContainText("private_note");

  await status.getByRole("button", { name: "Retry" }).click();
  await expect(page.getByText("Notification queued for retry.")).toBeVisible();
  await expect(status).toContainText("No notification handoffs yet.");
  expect(retried).toBe(true);
});

test("payroll officer can progress the M27 payment workflow through reconciliation", async ({
  page,
}) => {
  const runId = "019d0000-0000-7000-8000-000000000027";
  let paymentStatus = "not-created";
  let runStatus = "released";
  await page.addInitScript(() => {
    const payload = btoa(
      JSON.stringify({
        sub: "playwright-payroll-officer",
        preferred_username: "playwright.payroll",
        realm_access: { roles: ["payroll", "hr_admin"] },
      }),
    );
    const token = `test.${payload}.signature`;
    localStorage.setItem(
      "erp.oidc.session",
      JSON.stringify({ accessToken: token, idToken: token, expiresAt: Date.now() + 3_600_000 }),
    );
  });
  await page.route("**/api/hrm/payroll/runs/**", async (route) => {
    const url = route.request().url();
    const method = route.request().method();
    if (url.endsWith("/audit")) {
      await route.fulfill({ status: 200, contentType: "application/json", body: "[]" });
      return;
    }
    if (url.endsWith("/lines")) {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ items: [], totalCount: 0 }),
      });
      return;
    }
    if (url.endsWith("/statutory-readiness")) {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ isReady: true, workers: [] }),
      });
      return;
    }
    if (method === "POST" && url.endsWith("/payments/generate")) paymentStatus = "generated";
    if (method === "POST" && url.endsWith("/payments/approve")) paymentStatus = "approved";
    if (method === "POST" && url.endsWith("/payments/release")) paymentStatus = "released";
    if (method === "POST" && url.endsWith("/reconcile")) {
      paymentStatus = "reconciled";
      runStatus = "closed";
    }
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        id: runId,
        status: runStatus,
        periodLabel: "August 2026",
        employeeCount: 3,
        totalGross: 42000,
        totalDeductions: 9000,
        totalNet: 33000,
        totalEmployerCost: 44500,
        exceptionCount: 0,
        preparedBySubjectId: "payroll-preparer",
        approvedBySubjectId: "hr-approver",
        releasedBySubjectId: "payroll-releaser",
        paymentStatus,
        paymentFileReference: paymentStatus === "not-created" ? null : "PAY-20260816-M27TEST",
        reconciliationReference: paymentStatus === "reconciled" ? "BANK-ACK-M27" : null,
      }),
    });
  });

  await page.goto(`/hrm/payroll/runs/${runId}`);
  const workflow = page.getByTestId("payment-workflow");
  await expect(workflow).toContainText("not created");
  await workflow.getByRole("button", { name: "Generate bank file" }).click();
  await expect(workflow).toContainText("generated");
  await workflow.getByRole("button", { name: "Approve payment file" }).click();
  await expect(workflow).toContainText("approved");
  await workflow.getByRole("button", { name: "Release to bank" }).click();
  await expect(workflow).toContainText("released");
  await workflow.getByLabel("Bank acknowledgement reference").fill("BANK-ACK-M27");
  await workflow.getByRole("button", { name: "Reconcile and close" }).click();
  await expect(workflow).toContainText("Reconciled and closed · BANK-ACK-M27");
});

test("HR administrator can import attendance and reconcile the M28 batch", async ({ page }) => {
  let submittedRows: unknown[] = [];
  await page.addInitScript(() => {
    const payload = btoa(
      JSON.stringify({
        sub: "playwright-hr-admin",
        preferred_username: "playwright.hr",
        realm_access: { roles: ["hr_admin", "hr_ops"] },
      }),
    );
    const token = `test.${payload}.signature`;
    localStorage.setItem(
      "erp.oidc.session",
      JSON.stringify({
        accessToken: token,
        idToken: token,
        expiresAt: Date.now() + 3_600_000,
      }),
    );
  });
  await page.route("**/api/hrm/time/attendance/import", async (route) => {
    const body = route.request().postDataJSON() as { rows: unknown[] };
    submittedRows = body.rows;
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        batchId: "019d0000-0000-7000-8000-000000000028",
        fileName: "manual-import.csv",
        status: "completed-with-errors",
        rowCount: 2,
        importedCount: 1,
        updatedCount: 0,
        rejectedCount: 1,
        errors: ["UNKNOWN:2026-08-15: employee not found"],
      }),
    });
  });
  await page.route("**/api/hrm/time/operations/history", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ imports: [], accruals: [], adjustments: [] }),
    });
  });

  await page.goto("/hrm/time/operations");
  const operations = page.getByTestId("time-operations");
  await expect(operations).toContainText("Attendance import");
  await operations
    .getByLabel("Attendance rows")
    .fill("EMP-0001,2026-08-15,08:00,17:30\nUNKNOWN,2026-08-15,08:00,17:00");
  await operations.getByRole("button", { name: "Import and reconcile" }).click();

  const result = page.getByTestId("operation-result");
  await expect(result).toContainText("completed-with-errors");
  await expect(result).toContainText('"importedCount": 1');
  await expect(result).toContainText('"rejectedCount": 1');
  expect(submittedRows).toHaveLength(2);
});
