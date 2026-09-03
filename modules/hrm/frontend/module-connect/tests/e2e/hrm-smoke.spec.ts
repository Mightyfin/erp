import { expect, test } from "@playwright/test";

test("HRM entry renders or reaches the sign-in flow", async ({ page }) => {
  await page.goto("/hrm");

  await expect(page).toHaveURL(/\/(hrm|sign-in)(?:[/?#]|$)|auth\.mightyfinance\.co\.zm/);
  await expect(page.locator("body")).toContainText(
    /Checking your session|HRM|Human Resources|Sign in|Dashboard/i,
  );
});

test("login_required settles on a stable sign-in page and interactive login uses prompt=login", async ({
  page,
}) => {
  let silentAttempts = 0;
  let interactivePrompt: string | null = null;
  await page.route("https://auth.mightyfinance.co.zm/**", async (route) => {
    const requestUrl = new URL(route.request().url());
    const prompt = requestUrl.searchParams.get("prompt");
    if (prompt === "none") {
      silentAttempts += 1;
      const redirectUri = requestUrl.searchParams.get("redirect_uri");
      const state = requestUrl.searchParams.get("state");
      if (!redirectUri || !state) throw new Error("OIDC request is missing redirect_uri or state");
      const callback = new URL(redirectUri);
      callback.searchParams.set("error", "login_required");
      callback.searchParams.set("state", state);
      callback.searchParams.set("iss", "https://auth.mightyfinance.co.zm/realms/mightyfin-sandbox");
      await route.fulfill({ status: 302, headers: { location: callback.toString() } });
      return;
    }
    interactivePrompt = prompt;
    await route.fulfill({
      status: 200,
      contentType: "text/html",
      body: "<title>Organisation sign in</title><h1>Organisation sign in</h1>",
    });
  });

  const response = await page.goto("/sign-in");

  expect(response?.status()).toBe(200);
  await expect(
    page.getByRole("button", { name: "Continue with organisation account" }),
  ).toBeVisible();
  await expect.poll(() => silentAttempts, { timeout: 5_000 }).toBe(1);
  await expect(page).toHaveURL(/\/sign-in$/);

  await page.getByRole("button", { name: "Continue with organisation account" }).click();
  await expect(page).toHaveTitle("Organisation sign in");
  expect(interactivePrompt).toBe("login");
});

test("HRM reverse proxy exposes a healthy API", async ({ request }) => {
  const response = await request.get("/health/ready");

  expect(response.status()).toBe(200);
  expect(await response.json()).toMatchObject({ status: "healthy" });
  expect(response.headers()["x-content-type-options"]).toBe("nosniff");
  expect(response.headers()["x-frame-options"]).toBe("DENY");
});

test("a shared IdP identity without an HRM workforce role is denied ERP entry", async ({
  page,
}) => {
  await page.addInitScript(() => {
    const payload = btoa(
      JSON.stringify({
        sub: "playwright-efaas-tenant-owner",
        preferred_username: "tenant.owner@example.test",
        realm_access: { roles: ["tenant_owner"] },
      }),
    );
    const token = `test.${payload}.signature`;
    localStorage.setItem(
      "erp.oidc.session",
      JSON.stringify({ accessToken: token, idToken: token, expiresAt: Date.now() + 3_600_000 }),
    );
  });
  await page.route("**/api/hrm/me", async (route) => {
    await route.fulfill({
      status: 403,
      contentType: "application/json",
      body: JSON.stringify({ code: "forbidden", message: "HRM access not assigned", details: [] }),
    });
  });

  await page.goto("/hrm");

  await expect(page.getByTestId("hrm-access-denied")).toContainText("HRM access not assigned");
  await expect(page.getByTestId("hrm-access-denied")).toContainText("no ERP workforce role");
  await expect(page.getByText("Human Resources", { exact: true })).toHaveCount(0);
});

test("HR admin keeps Configuration navigation when also assigned payroll roles", async ({
  page,
}) => {
  await page.addInitScript(() => {
    const payload = btoa(
      JSON.stringify({
        sub: "playwright-multi-role-admin",
        preferred_username: "admin@example.test",
        realm_access: { roles: ["employee", "manager", "hr_ops", "payroll", "hr_admin"] },
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
  await page.route("**/api/hrm/**", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ items: [], totalCount: 0, linked: false, worker: null }),
    });
  });

  await page.goto("/hrm");

  await expect(page.locator('a[href="/hrm/configuration"]')).toContainText("Configuration");
  await expect
    .poll(() =>
      page.evaluate(() => JSON.parse(localStorage.getItem("erp.shell.state.v1") ?? "null")?.role),
    )
    .toBe("hr_admin");
});

test("HR admin can add housing allowance as thirty percent of basic", async ({ page }) => {
  let createdBody: Record<string, unknown> | null = null;
  const basic = {
    id: "component-basic",
    code: "basic",
    name: "Basic Salary",
    componentType: "earning",
    calculationBasis: "fixed",
    basisComponentCode: null,
    fixedAmount: 0,
    rate: null,
    ceiling: null,
    isTaxable: true,
    isStatutory: false,
    priority: 10,
    version: 1,
    isActive: true,
  };
  await page.route("**/api/hrm/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    let body: unknown = [];
    let status = 200;
    if (path.endsWith("/payroll/components") && request.method() === "POST") {
      createdBody = request.postDataJSON() as Record<string, unknown>;
      status = 201;
      body = { id: "component-housing", ...createdBody, isActive: true, isStatutory: false };
    } else if (path.endsWith("/payroll/components")) {
      body = createdBody
        ? [basic, { id: "component-housing", ...createdBody, isActive: true, isStatutory: false }]
        : [basic];
    } else if (path.endsWith("/auth/me")) {
      body = {
        authenticated: true,
        user: {
          id: "playwright-payroll-setup-admin",
          email: "playwright.hr.admin@example.test",
          displayName: "Payroll Setup Admin",
          roles: ["hr_admin", "hr_ops"],
          isActive: true,
          mustChangePassword: false,
        },
      };
    } else if (path.endsWith("/setup/state")) {
      body = { status: "complete" };
    } else if (path.endsWith("/me")) {
      body = { linked: false, worker: null };
    } else if (path.endsWith("/me/notifications")) {
      body = { unreadCount: 0, items: [] };
    }
    await route.fulfill({ status, contentType: "application/json", body: JSON.stringify(body) });
  });

  await page.goto("/hrm/configuration/payroll");
  await page.waitForLoadState("networkidle");
  await page.getByRole("tab", { name: "Salary components" }).click();
  await expect(page.getByRole("tab", { name: "Salary components" })).toHaveAttribute("aria-selected", "true");
  await page.getByRole("button", { name: "Add salary component" }).click();
  await page.getByLabel("Code").fill("housing-allowance");
  await page.getByLabel("Name").fill("Housing Allowance");
  await page.getByLabel("Calculation basis").click();
  await page.getByRole("option", { name: "Percent of component" }).click();
  await page.getByLabel("Basis component").click();
  await page.getByRole("option", { name: "Basic Salary" }).click();
  await page.getByLabel("Rate (%)").fill("30");
  await page.getByRole("button", { name: "Add component" }).click();

  await expect(page.getByRole("row", { name: /Housing Allowance/ })).toContainText("30%");
  expect(createdBody).toMatchObject({
    code: "housing-allowance",
    name: "Housing Allowance",
    componentType: "earning",
    calculationBasis: "percent-of",
    basisComponentCode: "basic",
    rate: 30,
    priority: 20,
  });

  // Editing is a controlled-dialog flow. The selected component must hydrate
  // every saved value before an HR administrator makes a change.
  const housingRow = page.getByRole("row", { name: /Housing Allowance/ });
  await housingRow.getByRole("button", { name: /Edit Housing Allowance/ }).click();
  await expect(page.getByRole("dialog", { name: "Edit salary component" })).toBeVisible();
  await expect(page.getByLabel("Name")).toHaveValue("Housing Allowance");
  await expect(page.getByText("Code:").locator("..")).toContainText("housing-allowance");
  await expect(page.getByLabel("Calculation basis")).toContainText("Percent of component");
  await expect(page.getByLabel("Basis component")).toContainText("Basic Salary");
  await expect(page.getByLabel("Rate (%)")).toHaveValue("30");
});

test("HR admin home is assembled from live tenant APIs, not seeded dashboard records", async ({ page }) => {
  await page.addInitScript(() => {
    const payload = btoa(JSON.stringify({
      sub: "playwright-home-admin",
      preferred_username: "playwright.hr.admin",
      realm_access: { roles: ["hr_admin", "hr_ops"] },
    }));
    const token = `test.${payload}.signature`;
    localStorage.setItem("erp.oidc.session", JSON.stringify({
      accessToken: token, idToken: token, expiresAt: Date.now() + 3_600_000,
    }));
  });
  await page.route("**/api/hrm/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    let body: unknown = { items: [] };
    if (path.endsWith("/workflow/queue")) body = { items: [{ requestId: "live-approval", workflowType: "Live promotion", subjectName: "Mary Phiri", status: "submitted" }] };
    else if (path.endsWith("/workers")) body = { items: [], totalCount: 37 };
    else if (path.endsWith("/admin/legal-entities")) body = [{ id: "entity-live", name: "Live Zambia Entity" }];
    else if (path.endsWith("/admin/locations")) body = [{ id: "location-live", name: "Live Lusaka Office", legalEntityId: "entity-live" }];
    else if (path.endsWith("/me/notifications")) body = { unreadCount: 0, items: [] };
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(body) });
  });

  await page.goto("/hrm");
  await expect(page.getByText("Live promotion decision")).toBeVisible();
  await expect(page.getByText("37", { exact: true })).toBeVisible();
  await expect(page.getByText("Attendance gap unresolved for 6 days")).toHaveCount(0);
  await expect(page.getByText("Live Zambia Entity")).toBeVisible();
});

test("attendance import page exposes overtime-only import and time audit evidence", async ({
  page,
}) => {
  let imported = false;
  await page.addInitScript(() => {
    const payload = btoa(JSON.stringify({
      sub: "playwright-time-admin",
      preferred_username: "playwright.time.admin",
      realm_access: { roles: ["hr_admin", "hr_ops", "payroll"] },
    }));
    const token = `test.${payload}.signature`;
    localStorage.setItem("erp.oidc.session", JSON.stringify({
      accessToken: token, idToken: token, expiresAt: Date.now() + 3_600_000,
    }));
  });
  await page.route("**/api/hrm/**", async (route) => {
    const url = new URL(route.request().url());
    const path = url.pathname;
    if (path.endsWith("/time/overtime/import") && route.request().method() === "POST") {
      imported = true;
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ status: "completed", importedCount: 1, updatedCount: 0, rejectedCount: 0 }),
      });
      return;
    }
    if (path.endsWith("/time/operations/history")) {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          imports: [],
          accruals: [],
          adjustments: [],
          encashments: [],
          timeAudits: [{ id: "audit-1", entityType: "time.overtime", entityId: "att-1", action: "overtime-import-create", actorSubjectId: "playwright-time-admin" }],
        }),
      });
      return;
    }
    if (path.endsWith("/me/notifications")) {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ unreadCount: 0, items: [] }) });
      return;
    }
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ items: [] }) });
  });

  await page.goto("/hrm/time/attendance/import");

  await expect(page.getByTestId("overtime-import-card")).toContainText("Import overtime hours only");
  await expect(page.getByText("overtime-import-create")).toBeVisible();
  await page.getByRole("button", { name: "Import overtime" }).click();
  await expect(page.getByTestId("overtime-import-result")).toContainText("Imported: 1");
  expect(imported).toBe(true);
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

test("HR administrator can complete the M29 candidate-to-worker journey", async ({ page }) => {
  const vacancyId = "019d0000-0000-7000-8000-000000000291";
  const candidateId = "019d0000-0000-7000-8000-000000000292";
  const interviewId = "019d0000-0000-7000-8000-000000000293";
  const offerId = "019d0000-0000-7000-8000-000000000294";
  const caseId = "019d0000-0000-7000-8000-000000000295";
  let vacancyStatus = "draft";
  let offerStatus = "draft";
  let accepted = false;
  const tasks = [
    {
      id: "019d0000-0000-7000-8000-000000000296",
      title: "Verify identity document",
      required: true,
      status: "pending",
    },
    {
      id: "019d0000-0000-7000-8000-000000000297",
      title: "Collect signed contract",
      required: true,
      status: "pending",
    },
  ];
  await page.addInitScript(() => {
    const payload = btoa(
      JSON.stringify({
        sub: "playwright-recruiter",
        realm_access: { roles: ["hr_admin", "hr_ops"] },
      }),
    );
    const token = `test.${payload}.signature`;
    localStorage.setItem(
      "erp.oidc.session",
      JSON.stringify({ accessToken: token, idToken: token, expiresAt: Date.now() + 3_600_000 }),
    );
  });
  await page.route("**/api/hrm/admin/org-units/tree", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify([
        { id: "019d0000-0000-7000-8000-000000000290", name: "Operations", children: [] },
      ]),
    }),
  );
  await page.route("**/api/hrm/recruitment/**", async (route) => {
    const url = route.request().url();
    const method = route.request().method();
    let body: unknown = {};
    if (url.includes("/vacancies") && method === "GET")
      body = {
        items: vacancyStatus
          ? [{ id: vacancyId, jobTitle: "Operations Analyst", status: vacancyStatus }]
          : [],
      };
    else if (url.endsWith("/vacancies") && method === "POST")
      body = { id: vacancyId, jobTitle: "Operations Analyst", status: "draft" };
    else if (url.endsWith(`/vacancies/${vacancyId}/publish`)) {
      vacancyStatus = "published";
      body = { id: vacancyId, status: vacancyStatus };
    } else if (url.endsWith("/candidates") && method === "POST")
      body = { id: candidateId, vacancyId, fullName: "Mary Phiri", stage: "applied" };
    else if (url.endsWith(`/candidates/${candidateId}/interviews`))
      body = { id: interviewId, candidateId, status: "scheduled" };
    else if (url.endsWith(`/interviews/${interviewId}/decision`))
      body = { id: interviewId, status: "completed", recommendation: "hire" };
    else if (url.endsWith(`/candidates/${candidateId}/advance`)) body = { id: candidateId };
    else if (url.endsWith("/offers") && method === "GET")
      body = {
        items: offerStatus
          ? [{ id: offerId, candidateId, candidateName: "Mary Phiri", status: offerStatus }]
          : [],
      };
    else if (url.endsWith("/offers") && method === "POST")
      body = { id: offerId, candidateId, status: "draft" };
    else if (url.endsWith(`/offers/${offerId}/approve`)) {
      offerStatus = "approved";
      body = { id: offerId, status: offerStatus };
    } else if (url.endsWith(`/offers/${offerId}/issue`)) {
      offerStatus = "issued";
      body = { id: offerId, status: offerStatus };
    } else if (url.endsWith(`/offers/${offerId}/accept`)) {
      offerStatus = "accepted";
      accepted = true;
      body = { offerId, workerId: "worker-m29", status: "preboarding" };
    } else if (url.includes("/preboarding") && method === "GET")
      body = {
        items: accepted
          ? [
              {
                id: caseId,
                candidateId,
                candidateName: "Mary Phiri",
                employeeNo: "EMP-029",
                status: tasks.every((x) => x.status === "completed") ? "ready" : "preboarding",
                completedTasks: tasks.filter((x) => x.status === "completed").length,
                totalTasks: tasks.length,
                tasks,
              },
            ]
          : [],
      };
    else if (url.includes(`/preboarding/${caseId}/tasks/`) && method === "PATCH") {
      const task = tasks.find((x) => url.endsWith(x.id));
      if (task) task.status = "completed";
      body = task ?? {};
    } else if (url.endsWith(`/preboarding/${caseId}/activate`))
      body = { id: caseId, status: "active" };
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(body),
    });
  });

  await page.goto("/hrm/recruitment/operations");
  const operations = page.getByTestId("recruitment-operations");
  await operations.getByLabel("Org unit").selectOption({ label: "Operations" });
  await operations.getByLabel("Job title").fill("Operations Analyst");
  await operations.getByRole("button", { name: "Create draft" }).click();
  await operations.getByRole("button", { name: "Publish" }).click();
  await operations.getByLabel("Candidate name").fill("Mary Phiri");
  await operations.getByLabel("Candidate email").fill("mary@example.com");
  await operations.getByRole("button", { name: "Record application" }).click();
  await operations.getByRole("button", { name: "Screen" }).click();
  await operations.getByRole("button", { name: "Shortlist" }).click();
  await operations.getByRole("button", { name: "Schedule interview" }).click();
  await operations.getByRole("button", { name: "Record decision" }).click();
  await operations.getByRole("button", { name: "Move to offer" }).click();
  await operations.getByRole("button", { name: "Create offer" }).click();
  await operations.getByRole("button", { name: "Approve" }).click();
  await operations.getByRole("button", { name: "Issue", exact: true }).click();
  await operations.getByRole("button", { name: "Accept and preboard" }).click();
  await expect(operations).toContainText("Mary Phiri");
  for (const task of tasks) {
    await operations
      .locator("li")
      .filter({ hasText: task.title })
      .getByRole("button", { name: "Mark complete" })
      .click();
  }
  await operations.getByRole("button", { name: "Activate worker" }).click();
  await expect(page.getByText("Activate worker completed")).toBeVisible();
  expect(tasks.every((x) => x.status === "completed")).toBe(true);
});

test("HR investigator can complete a restricted M30 case with access, actions, evidence and audit", async ({
  page,
}) => {
  const caseId = "019d0000-0000-7000-8000-000000000301";
  const actionId = "019d0000-0000-7000-8000-000000000302";
  const evidenceId = "019d0000-0000-7000-8000-000000000303";
  let created = false;
  let declared = false;
  let assignedOwner = "";
  let status = "open";
  let actionStatus = "pending";
  let hasAction = false;
  let hasEvidence = false;
  let auditEvents = 1;

  await page.addInitScript(() => {
    const payload = btoa(
      JSON.stringify({
        sub: "playwright-investigator",
        realm_access: { roles: ["investigator", "hr_admin"] },
      }),
    );
    const token = `test.${payload}.signature`;
    localStorage.setItem(
      "erp.oidc.session",
      JSON.stringify({ accessToken: token, idToken: token, expiresAt: Date.now() + 3_600_000 }),
    );
  });

  const detail = () => ({
    case: {
      id: caseId,
      reference: "ER-2026-00001",
      caseType: "grievance",
      status,
      confidentiality: "restricted",
    },
    description: "Restricted allegation detail",
    findings: status === "resolved" || status === "closed" ? "Concern substantiated" : null,
    outcome: status === "resolved" || status === "closed" ? "Corrective action completed" : null,
    actions: hasAction ? [{ id: actionId, title: "Interview witness", status: actionStatus }] : [],
    evidence: hasEvidence
      ? [
          {
            id: evidenceId,
            title: "witness-note.txt",
            fileName: "witness-note.txt",
            classification: "restricted",
          },
        ]
      : [],
    history: Array.from({ length: auditEvents }, (_, index) => ({
      id: `event-${index}`,
      action: index === 0 ? "created" : "status-changed",
    })),
    access: declared ? [{ decision: "no-conflict" }] : [],
  });

  await page.route("**/api/hrm/relations/**", async (route) => {
    const url = route.request().url();
    const method = route.request().method();
    let body: unknown = {};
    if (url.endsWith("/relations/cases") && method === "GET") {
      body = {
        items: created
          ? [
              {
                id: caseId,
                reference: "ER-2026-00001",
                caseType: "grievance",
                status,
                summary: "Restricted case",
              },
            ]
          : [],
      };
    } else if (url.endsWith("/relations/cases") && method === "POST") {
      created = true;
      body = { id: caseId, reference: "ER-2026-00001", status };
    } else if (url.endsWith(`/cases/${caseId}/access-declarations`)) {
      declared = true;
      auditEvents += 1;
      body = { decision: "no-conflict" };
    } else if (url.endsWith(`/cases/${caseId}/assign`)) {
      assignedOwner = (route.request().postDataJSON() as { ownerSubjectId: string }).ownerSubjectId;
      auditEvents += 1;
      body = { id: caseId, ownerSubjectId: assignedOwner };
    } else if (url.endsWith(`/cases/${caseId}`) && method === "GET") {
      body = detail();
    } else if (url.endsWith(`/cases/${caseId}/actions`) && method === "POST") {
      hasAction = true;
      auditEvents += 1;
      body = { id: actionId, title: "Interview witness", status: actionStatus };
    } else if (url.endsWith(`/cases/${caseId}/actions/${actionId}`)) {
      actionStatus = "completed";
      auditEvents += 1;
      body = { id: actionId, title: "Interview witness", status: actionStatus };
    } else if (url.endsWith(`/cases/${caseId}/evidence`) && method === "POST") {
      hasEvidence = true;
      auditEvents += 1;
      body = { id: evidenceId, title: "witness-note.txt" };
    } else if (url.endsWith(`/cases/${caseId}/transition`)) {
      status = (route.request().postDataJSON() as { status: string }).status;
      auditEvents += 1;
      body = detail();
    }
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(body),
    });
  });

  await page.goto("/hrm/relations/operations");
  const operations = page.getByTestId("relations-operations");
  await operations.getByLabel("Neutral summary").fill("Neutral triage label");
  await operations.getByLabel("Allegation or concern").fill("Restricted allegation detail");
  await operations.getByRole("button", { name: "Create restricted case" }).click();
  await expect(operations.getByLabel("Restricted case").locator("option")).toHaveCount(2);
  await operations.getByLabel("Restricted case").selectOption(caseId);
  await operations.getByLabel("Investigator subject ID").fill("playwright-investigator");
  await operations.getByRole("button", { name: "Assign", exact: true }).click();
  await operations.getByRole("button", { name: "I have no conflict — open case" }).click();
  await expect(operations).toContainText("Restricted allegation detail");
  await operations.getByRole("button", { name: "Begin triage" }).click();
  await operations.getByRole("button", { name: "Begin investigation" }).click();
  await operations.getByLabel("Investigation action").fill("Interview witness");
  await operations.getByRole("button", { name: "Add action" }).click();
  await operations.getByRole("button", { name: "Mark complete" }).click();
  await operations.getByLabel("Restricted evidence").setInputFiles({
    name: "witness-note.txt",
    mimeType: "text/plain",
    buffer: Buffer.from("note"),
  });
  await operations.getByRole("button", { name: "Upload evidence" }).click();
  await expect(operations).toContainText("witness-note.txt · restricted");
  await operations.getByLabel("Findings").fill("Concern substantiated");
  await operations.getByLabel("Outcome").fill("Corrective action completed");
  await operations.getByRole("button", { name: "Resolve case" }).click();
  await operations.getByRole("button", { name: "Close case" }).click();
  await expect(operations).toContainText("closed");
  expect(declared).toBe(true);
  expect(assignedOwner).toBe("playwright-investigator");
  expect(actionStatus).toBe("completed");
  expect(hasEvidence).toBe(true);
});

test("M30 protected disclosures stay in a separate redacted investigator queue", async ({
  page,
}) => {
  const disclosureId = "019d0000-0000-7000-8000-000000000304";
  let status = "new";
  let views = 0;
  let assignedTo = "";
  await page.addInitScript(() => {
    const payload = btoa(
      JSON.stringify({
        sub: "playwright-investigator",
        realm_access: { roles: ["investigator"] },
      }),
    );
    const token = `test.${payload}.signature`;
    localStorage.setItem(
      "erp.oidc.session",
      JSON.stringify({ accessToken: token, idToken: token, expiresAt: Date.now() + 3_600_000 }),
    );
  });
  await page.route("**/api/hrm/relations/protected-disclosures**", async (route) => {
    const url = route.request().url();
    const method = route.request().method();
    let body: unknown;
    if (url.endsWith("/protected-disclosures") && method === "GET") {
      body = {
        items: [
          {
            id: disclosureId,
            caseReference: "SD-20260816-TEST01",
            category: "financial-misconduct",
            severity: "high",
            status,
            description: null,
          },
        ],
      };
    } else {
      if (method === "POST") {
        const request = route.request().postDataJSON() as {
          status: string;
          assignedToSubjectId?: string;
        };
        status = request.status;
        assignedTo = request.assignedToSubjectId ?? assignedTo;
      } else views += 1;
      body = {
        id: disclosureId,
        caseReference: "SD-20260816-TEST01",
        category: "financial-misconduct",
        severity: "high",
        status,
        description: "Anonymous protected narrative",
        history: Array.from({ length: views + 1 }, (_, index) => ({ id: index, action: "viewed" })),
      };
    }
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(body),
    });
  });

  await page.goto("/hrm/relations/protected-disclosures");
  const workspace = page.getByTestId("protected-disclosures");
  await expect(workspace).not.toContainText("Anonymous protected narrative");
  await workspace.getByRole("button", { name: /SD-20260816-TEST01/ }).click();
  await expect(workspace).toContainText("Anonymous protected narrative");
  await workspace.getByLabel("Investigator subject ID").fill("playwright-investigator");
  await workspace.getByRole("button", { name: "Triage" }).click();
  await workspace.getByRole("button", { name: "Investigate" }).click();
  await workspace.getByLabel("Outcome").fill("Controls remediated");
  await workspace.getByRole("button", { name: "Resolve" }).click();
  await expect(workspace).toContainText("resolved");
  expect(assignedTo).toBe("playwright-investigator");
});

test("employee can use the ownership-protected M31 self-service workspace", async ({ page }) => {
  const notificationId = "019d0000-0000-7000-8000-000000000311";
  let notificationRead = false;
  let documentUploaded = false;
  await page.addInitScript(() => {
    const payload = btoa(
      JSON.stringify({
        sub: "playwright-employee",
        preferred_username: "playwright.employee",
        realm_access: { roles: ["employee"] },
      }),
    );
    const token = `test.${payload}.signature`;
    localStorage.setItem(
      "erp.oidc.session",
      JSON.stringify({ accessToken: token, idToken: token, expiresAt: Date.now() + 3_600_000 }),
    );
  });
  await page.route("**/api/hrm/me/**", async (route) => {
    const url = new URL(route.request().url());
    const path = url.pathname;
    const method = route.request().method();
    let body: unknown = {};
    if (path.endsWith("/notifications") && method === "GET") {
      body = {
        unreadCount: notificationRead ? 0 : 1,
        items: [
          {
            id: notificationId,
            eventType: "hrm.payslip.released",
            title: "Your payslip is ready",
            status: "published",
            actionUrl: "/hrm/payslips",
            isRead: notificationRead,
            createdAt: "2026-08-16T10:00:00Z",
          },
        ],
      };
    } else if (path.endsWith("/notifications/read-all") && method === "POST") {
      notificationRead = true;
      body = { markedRead: 1 };
    } else if (path.endsWith(`/notifications/${notificationId}/read`) && method === "POST") {
      notificationRead = true;
      body = { id: notificationId, isRead: true };
    } else if (path.endsWith("/requests")) {
      body = { items: [{ id: "request-own", subject: "My payroll query", status: "open" }] };
    } else if (path.endsWith("/leave")) {
      body = {
        linked: true,
        workerId: "worker-own",
        balances: [],
        requests: [{ id: "leave-own" }],
      };
    } else if (path.endsWith("/payslips")) {
      body = { items: [{ id: "payslip-own", periodLabel: "August 2026" }] };
    } else if (path.endsWith("/documents") && method === "POST") {
      documentUploaded = true;
      body = { id: "document-new", title: "Updated NRC", fileName: "nrc.pdf", category: "id" };
    } else if (path.endsWith("/documents")) {
      body = {
        items: [
          {
            id: "document-own",
            title: "Degree",
            fileName: "degree.pdf",
            category: "qualification",
          },
          ...(documentUploaded
            ? [{ id: "document-new", title: "Updated NRC", fileName: "nrc.pdf", category: "id" }]
            : []),
        ],
      };
    } else if (path.endsWith("/letters")) {
      body = {
        items: [{ id: "letter-own", letterType: "employment-confirmation", status: "generated" }],
      };
    }
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(body),
    });
  });

  await page.goto("/hrm/self-service");
  const workspace = page.getByTestId("employee-self-service");
  await expect(workspace).toContainText("Your payslip is ready");
  await expect(workspace).toContainText("1 unread HR update");
  await workspace.getByRole("button", { name: "Mark all read" }).click();
  await expect(workspace).toContainText("0 unread HR updates");

  await page.goto("/hrm/my-documents");
  const documents = page.getByTestId("my-documents");
  await expect(documents).toContainText("Degree");
  await documents.getByLabel("Title").fill("Updated NRC");
  await documents.getByLabel("File").setInputFiles({
    name: "nrc.pdf",
    mimeType: "application/pdf",
    buffer: Buffer.from("test PDF"),
  });
  await documents.getByRole("button", { name: "Upload" }).click();
  await expect(documents).toContainText("Updated NRC");
  expect(documentUploaded).toBe(true);
});

test("HR admin can preview, apply and recover M32 worker master-data operations", async ({
  page,
}) => {
  const batchId = "019d0000-0000-7000-8000-000000000032";
  const archivedWorkerId = "019d0000-0000-7000-8000-000000000033";
  let batchStatus = "previewed";
  let reactivated = false;
  let importedName = "";
  await page.addInitScript(() => {
    const payload = btoa(
      JSON.stringify({
        sub: "playwright-hr-admin",
        preferred_username: "playwright.hr.admin",
        realm_access: { roles: ["hr_admin", "hr_ops"] },
      }),
    );
    const token = `test.${payload}.signature`;
    localStorage.setItem(
      "erp.oidc.session",
      JSON.stringify({ accessToken: token, idToken: token, expiresAt: Date.now() + 3_600_000 }),
    );
  });

  const batch = () => ({
    id: batchId,
    batchType: "worker-import",
    fileName: "workers.csv",
    status: batchStatus,
    effectiveDate: "2026-08-16",
    rowCount: 1,
    readyCount: 1,
    unchangedCount: 0,
    errorCount: 0,
    requestedBySubjectId: "playwright-hr-admin",
    createdAt: "2026-08-16T10:00:00Z",
    appliedAt: batchStatus === "previewed" ? null : "2026-08-16T10:01:00Z",
    rolledBackAt: batchStatus === "rolled-back" ? "2026-08-16T10:02:00Z" : null,
    canRollback: batchStatus === "applied",
    errors: [],
    samples: [
      {
        employeeNo: "EMP-0100",
        action: "create",
        before: "New worker",
        after: importedName || "Chanda Banda",
      },
    ],
  });

  await page.route("**/api/hrm/master-data/**", async (route) => {
    const url = new URL(route.request().url());
    const method = route.request().method();
    if (url.pathname.endsWith("/imports/preview")) {
      const request = route.request().postDataJSON() as {
        fileName: string;
        rows: Array<{ firstName: string; lastName: string }>;
      };
      importedName = `${request.rows[0].firstName} ${request.rows[0].lastName}`;
      batchStatus = "previewed";
    } else if (url.pathname.endsWith(`/${batchId}/apply`)) {
      batchStatus = "applied";
    } else if (url.pathname.endsWith(`/${batchId}/rollback`)) {
      batchStatus = "rolled-back";
    } else if (url.pathname.endsWith(`/${archivedWorkerId}/reactivate`)) {
      const request = route.request().postDataJSON() as { reason: string };
      expect(request.reason).toBe("Returning from approved career break");
      reactivated = true;
    }
    const body =
      url.pathname.endsWith("/batches") && method === "GET"
        ? { items: batchStatus === "previewed" && !importedName ? [] : [batch()] }
        : batch();
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(body),
    });
  });
  await page.route("**/api/hrm/workers**", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        items: reactivated
          ? []
          : [
              {
                id: archivedWorkerId,
                employeeNo: "EMP-0099",
                fullName: "Mwila Zulu",
                isArchived: true,
              },
            ],
      }),
    });
  });
  await page.route("**/api/hrm/dq/checks", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify([
        { id: "dq-1", rule: "missing-statutory-id" },
        { id: "dq-2", rule: "missing-statutory-id" },
      ]),
    });
  });

  await page.goto("/hrm/people/master-data");
  const operations = page.getByTestId("master-data-operations");
  await operations.getByLabel("Worker CSV").setInputFiles({
    name: "workers.csv",
    mimeType: "text/csv",
    buffer: Buffer.from("firstName,lastName,email\nChanda,Banda,chanda@example.com\n"),
  });
  await operations.getByRole("button", { name: "Preview import" }).click();
  const preview = page.getByTestId("master-data-preview");
  await expect(preview).toContainText("Chanda Banda");
  await expect(preview).toContainText("Errors");
  await preview.getByRole("button", { name: "Apply validated batch" }).click();
  await expect(preview.getByRole("button", { name: "Batch applied" })).toBeDisabled();

  await operations.getByRole("tab", { name: "History and recovery" }).click();
  const history = page.getByTestId("master-data-history");
  await expect(history).toContainText("worker-import");
  await history.getByRole("button", { name: "Roll back batch" }).click();
  await expect(history).toContainText("rolled-back");

  await operations.getByRole("tab", { name: "Reactivation" }).click();
  await operations.getByLabel("Archived worker").selectOption(archivedWorkerId);
  await operations.getByLabel("Reason").fill("Returning from approved career break");
  await operations.getByRole("button", { name: "Reactivate worker" }).click();
  await expect(operations.getByLabel("Archived worker").locator("option")).toHaveCount(1);
  expect(reactivated).toBe(true);

  await operations.getByRole("tab", { name: "Quality dashboard" }).click();
  const quality = page.getByTestId("master-data-quality");
  await expect(quality).toContainText("missing statutory id");
  await expect(quality).toContainText("2");
});

test("payroll administrator can prepare and reconcile an M33 finance hand-off", async ({
  page,
}) => {
  const runId = "019d0000-0000-7000-8000-000000000331";
  const operationId = "019d0000-0000-7000-8000-000000000332";
  let operationCreated = false;
  let operationStatus = "ready";
  let externalReference = "";
  await page.addInitScript(() => {
    const payload = btoa(
      JSON.stringify({
        sub: "playwright-payroll-admin",
        preferred_username: "playwright.payroll.admin",
        realm_access: { roles: ["hr_admin", "payroll"] },
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
  const operation = () => ({
    id: operationId,
    publicId: "int_playwright_finance",
    integrationKey: "finance",
    operationType: "payroll-journal",
    status: operationStatus,
    sourceReference: "August 2026",
    attemptCount: 0,
    externalReference: externalReference || null,
    createdAt: "2026-08-16T13:00:00Z",
  });
  await page.route("**/api/hrm/integrations**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith("/finance-postings")) {
      const body = route.request().postDataJSON() as { sourceId: string };
      expect(body.sourceId).toBe(runId);
      operationCreated = true;
    } else if (path.endsWith(`/operations/${operationId}/reconcile`)) {
      const body = route.request().postDataJSON() as { outcome: string; externalReference: string };
      operationStatus = body.outcome === "matched" ? "reconciled" : "delivered";
      externalReference = body.externalReference;
    }
    const body = path.endsWith("/integrations")
      ? {
          contracts: [
            {
              key: "finance",
              name: "ERP finance journal",
              direction: "outbound",
              contractVersion: "1.0",
              transport: "NATS/JetStream",
              owner: "Finance operations",
              retryStrategy: "Replay with idempotency key",
              reconciliationProcess: "Match journal totals",
              status: "available",
            },
          ],
          operations: operationCreated ? [operation()] : [],
          ready: operationCreated && operationStatus === "ready" ? 1 : 0,
          delivered: 0,
          failed: 0,
          reconciled: operationStatus === "reconciled" ? 1 : 0,
          activeWorkers: 10,
          linkedWorkers: 9,
          unlinkedWorkers: 1,
          documentStorageMode: "object-storage",
        }
      : operation();
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(body),
    });
  });
  await page.route("**/api/hrm/payroll/runs", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        items: [{ id: runId, periodLabel: "August 2026", status: "released" }],
        totalCount: 1,
      }),
    });
  });
  await page.route("**/api/hrm/payroll/pay-groups", async (route) => {
    await route.fulfill({ status: 200, contentType: "application/json", body: "[]" });
  });

  await page.goto("/hrm/configuration/integrations");
  const workspace = page.getByTestId("integration-operations");
  await expect(workspace).toContainText("ERP finance journal");
  await expect(workspace).toContainText("9 of 10 active workers linked");
  await workspace.getByLabel("Released payroll run").selectOption(runId);
  await workspace.getByRole("button", { name: "Finance journal" }).click();
  await expect(workspace).toContainText("payroll-journal");
  await workspace.getByRole("button", { name: "Reconcile" }).click();
  await page.getByLabel("External reference").fill("FIN-JV-2026-08-001");
  await page.getByRole("button", { name: "Save outcome" }).click();
  await expect(workspace).toContainText("reconciled");
  await expect(workspace).toContainText("FIN-JV-2026-08-001");
});

test("HR administrator can review and evidence the M34 security controls", async ({ page }) => {
  const holdId = "019d0000-0000-7000-8000-000000000341";
  let holdPlaced = false;
  let evidenceRecorded = false;
  await page.addInitScript(() => {
    const payload = btoa(
      JSON.stringify({
        sub: "playwright-security-admin",
        tenant: "mightyfin-erp",
        preferred_username: "playwright.security.admin",
        realm_access: { roles: ["hr_admin"] },
      }),
    );
    const token = `test.${payload}.signature`;
    localStorage.setItem(
      "erp.oidc.session",
      JSON.stringify({ accessToken: token, idToken: token, expiresAt: Date.now() + 3_600_000 }),
    );
  });
  const dashboard = () => ({
    tenantId: "mightyfin-erp",
    controls: [
      {
        key: "tenant-query-filter",
        name: "Tenant data isolation",
        status: "passed",
        detail: "Every entity is tenant-filtered.",
        evidenceReference: "automated:M34TenantIsolationTests",
      },
      {
        key: "append-only-audit",
        name: "Privileged and entity audit",
        status: "passed",
        detail: "Audit evidence is immutable.",
      },
      {
        key: "backup-restore",
        name: "Backup and restore rehearsal",
        status: evidenceRecorded ? "passed" : "action-required",
        detail: "Restore into an isolated database.",
        evidenceReference: evidenceRecorded ? "RESTORE-2026-08" : null,
      },
    ],
    roleMatrix: [
      {
        capability: "security-admin",
        description: "Review audit and compliance",
        roles: ["hr_admin"],
        dataScope: "tenant",
        sensitive: true,
        control: "Append-only evidence",
      },
    ],
    privilegedActions: [
      {
        id: "evt-1",
        actorSubjectId: "payroll-admin",
        actorRoles: ["payroll"],
        method: "POST",
        path: "/api/hrm/payroll/runs/1/release",
        outcome: "succeeded",
        statusCode: 200,
        requestId: "req-m34",
        createdAt: "2026-08-16T13:30:00Z",
      },
    ],
    entityAudit: [
      {
        id: "audit-1",
        entityType: "PayrollRun",
        entityId: "run-1",
        action: "update",
        actorSubjectId: "payroll-admin",
        correlationId: "req-m34",
        createdAt: "2026-08-16T13:30:00Z",
      },
    ],
    retentionRules: [
      {
        recordType: "Payroll and statutory snapshots",
        retentionMonths: 120,
        legalBasis: "Tax and employment evidence",
        disposition: "Archive then dispose",
        legalHoldOverrides: true,
      },
    ],
    evidence: evidenceRecorded
      ? [
          {
            id: "evidence-1",
            controlKey: "backup-restore",
            status: "passed",
            evidenceReference: "RESTORE-2026-08",
            executedAt: "2026-08-16T13:35:00Z",
            executedBySubjectId: "playwright-security-admin",
          },
        ]
      : [],
    legalHolds: holdPlaced
      ? [
          {
            id: holdId,
            reference: "CASE-2026-034",
            scope: "worker:EMP-0034",
            reason: "Active investigation",
            status: "active",
            placedAt: "2026-08-16T13:34:00Z",
            placedBySubjectId: "playwright-security-admin",
          },
        ]
      : [],
    openFindings: evidenceRecorded ? 0 : 1,
    activeLegalHolds: holdPlaced ? 1 : 0,
  });
  await page.route("**/api/hrm/security**", async (route) => {
    const url = new URL(route.request().url());
    if (route.request().method() === "POST" && url.pathname.endsWith("/legal-holds")) {
      const body = route.request().postDataJSON() as {
        reference: string;
        scope: string;
        reason: string;
      };
      expect(body).toEqual({
        reference: "CASE-2026-034",
        scope: "worker:EMP-0034",
        reason: "Active investigation",
      });
      holdPlaced = true;
    }
    if (route.request().method() === "POST" && url.pathname.endsWith("/evidence")) {
      const body = route.request().postDataJSON() as {
        controlKey: string;
        evidenceReference: string;
      };
      expect(body.controlKey).toBe("backup-restore");
      expect(body.evidenceReference).toBe("RESTORE-2026-08");
      evidenceRecorded = true;
    }
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(url.pathname.endsWith("/security") ? dashboard() : {}),
    });
  });

  await page.goto("/hrm/configuration/compliance");
  const workspace = page.getByTestId("security-compliance");
  await expect(workspace).toContainText("Tenant data isolation");
  await expect(workspace).toContainText("action-required");
  await page.getByRole("tab", { name: "Enforced role matrix" }).click();
  await expect(workspace).toContainText("security-admin");
  await expect(workspace).toContainText("hr_admin");
  await page.getByRole("tab", { name: "Privileged audit" }).click();
  await expect(workspace).toContainText("POST /api/hrm/payroll/runs/1/release");
  await expect(workspace).toContainText("req-m34");
  await page.getByRole("tab", { name: "Retention and holds" }).click();
  await page.getByLabel("Legal hold reference").fill("CASE-2026-034");
  await page.getByLabel("Legal hold scope").fill("worker:EMP-0034");
  await page.getByLabel("Legal hold reason").fill("Active investigation");
  await page.getByRole("button", { name: "Place hold" }).click();
  await expect(workspace).toContainText("CASE-2026-034");
  await page.getByRole("tab", { name: "Control evidence" }).click();
  await page.getByLabel("Evidence reference").fill("RESTORE-2026-08");
  await page.getByRole("button", { name: "Record passed control" }).click();
  await expect(workspace).toContainText("RESTORE-2026-08");
  expect(holdPlaced).toBe(true);
  expect(evidenceRecorded).toBe(true);
});

test("HR leadership can filter and export the M35 management dashboard", async ({ page }) => {
  await page.addInitScript(() => {
    const payload = btoa(
      JSON.stringify({
        sub: "playwright-hr-leadership",
        tenant: "mightyfin-erp",
        preferred_username: "playwright.hr.leadership",
        realm_access: { roles: ["hr_admin", "payroll"] },
      }),
    );
    const token = `test.${payload}.signature`;
    localStorage.setItem(
      "erp.oidc.session",
      JSON.stringify({ accessToken: token, idToken: token, expiresAt: Date.now() + 3_600_000 }),
    );
  });
  await page.route("**/api/hrm/me", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        linked: true,
        subject: "playwright-hr-leadership",
        worker: {
          id: "worker-m35",
          employeeNo: "EMP-M35",
          fullName: "HR Leadership",
          status: "active",
        },
      }),
    });
  });
  const departmentId = "019d0000-0000-7000-8000-000000000350";
  let filtered = false;
  await page.route("**/api/hrm/reports/management**", async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname.includes("/export/")) {
      await route.fulfill({
        status: 200,
        contentType: "text/csv",
        body: "department,gross,net\nFinance,17348.35,16050.00\n",
      });
      return;
    }
    filtered = url.searchParams.get("orgUnitId") === departmentId;
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        generatedAt: "2026-08-16T14:00:00Z",
        dataThrough: "2026-08-16",
        filters: {
          fromDate: "2026-01-01",
          toDate: "2026-08-16",
          legalEntities: [{ id: "entity-1", code: "MF", name: "Mightyfin" }],
          orgUnits: [{ id: departmentId, code: "FIN", name: "Finance" }],
          locations: [{ id: "location-1", code: "LUS", name: "Lusaka" }],
        },
        kpis: [
          {
            code: "headcount",
            label: "Active headcount",
            value: 6,
            unit: "people",
            definition: "Workers active on the reporting end date.",
            source: "Workers",
          },
          {
            code: "turnover",
            label: "Turnover",
            value: 0,
            unit: "percent",
            definition: "Leavers divided by average headcount.",
            source: "Workers",
          },
          {
            code: "employer-cost",
            label: "Employer cost",
            value: 18215.76,
            unit: "ZMW",
            definition: "Gross plus employer contributions.",
            source: "Released payroll",
          },
          {
            code: "net-pay",
            label: "Net pay",
            value: 16050,
            unit: "ZMW",
            definition: "Released net pay.",
            source: "Released payroll",
          },
        ],
        trend: [
          {
            period: "2026-06",
            headcount: 6,
            hires: 0,
            leavers: 0,
            grossPay: 17348.35,
            employerCost: 18215.76,
          },
        ],
        departments: [
          {
            orgUnitId: departmentId,
            department: "Finance",
            headcount: 6,
            payrollWorkers: 6,
            grossPay: 17348.35,
            deductions: 1298.35,
            netPay: 16050,
            employerContributions: 867.41,
            employerCost: 18215.76,
          },
        ],
        leave: [{ leaveType: "annual", requests: 2, approvedDays: 10, pendingDays: 2 }],
        attendance: [
          {
            status: "present",
            records: 42,
            scheduledHours: 336,
            workedHours: 344,
            overtimeHours: 8,
          },
        ],
        recruitment: [{ stage: "shortlisted", candidates: 3, percentage: 60 }],
        movements: [{ movementType: "promotion", movements: 1 }],
        statutoryLiability: {
          paye: 430.94,
          napsaEmployee: 867.41,
          napsaEmployer: 867.41,
          nhimaEmployee: 0,
          nhimaEmployer: 0,
          total: 2165.76,
        },
        catalogue: [
          {
            code: "payroll-department",
            name: "Payroll by department",
            category: "Payroll and cost",
            description: "Released gross-to-net controls by department.",
            owner: "Payroll",
            certified: true,
            payrollRestricted: true,
            source: "Released payroll run lines",
          },
          {
            code: "workforce-summary",
            name: "Headcount and workforce movements",
            category: "Workforce",
            description: "Point-in-time workforce controls.",
            owner: "HR operations",
            certified: true,
            payrollRestricted: false,
            source: "Workers",
          },
        ],
        reconciliationNotes: ["Payroll totals include released and closed runs only."],
      }),
    });
  });

  await page.goto("/hrm/reports");
  const dashboard = page.getByTestId("management-reporting");
  await expect(
    page.getByRole("heading", { name: "Workforce and payroll intelligence" }),
  ).toBeVisible();
  await expect(page.getByText(/K\s*18,215\.76/, { exact: true }).first()).toBeVisible();
  await expect(page.getByRole("cell", { name: "Finance" })).toBeVisible();
  await expect(page.getByText("PAYE", { exact: true })).toBeVisible();
  await dashboard.getByLabel("Department").click();
  await page.getByRole("option", { name: "Finance" }).click();
  await dashboard.getByRole("button", { name: "Apply filters" }).click();
  await expect.poll(() => filtered).toBe(true);
  const download = page.waitForEvent("download");
  await page.getByTitle("Download Payroll by department").click();
  expect((await download).suggestedFilename()).toContain("payroll-department");
});

test("M36 coordinator records rehearsal evidence and a role-enforced go-live sign-off", async ({
  page,
}) => {
  await page.addInitScript(() => {
    const payload = btoa(
      JSON.stringify({
        sub: "playwright-go-live-admin",
        tenant: "mightyfin-erp",
        preferred_username: "go.live.admin",
        realm_access: { roles: ["hr_admin", "payroll", "finance_approver"] },
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
  let evidenceRecorded = false;
  let signoffRecorded = false;
  const dashboard = () => ({
    decision: signoffRecorded ? "approved" : evidenceRecorded ? "ready-for-signoff" : "blocked",
    canGoLive: signoffRecorded,
    evaluatedAt: "2026-08-16T15:00:00Z",
    passedGates: evidenceRecorded ? 2 : 1,
    totalGates: 2,
    blockers: evidenceRecorded ? [] : ["Production migration rehearsal"],
    gates: [
      {
        key: "database-migrations",
        category: "technical",
        name: "Database migrations",
        status: "passed",
        detail: "No pending migrations.",
        evidenceReference: "automated:ef-migrations",
        verifiedAt: "2026-08-16T15:00:00Z",
      },
      {
        key: "migration-rehearsal",
        category: "evidence",
        name: "Production migration rehearsal",
        status: evidenceRecorded ? "passed" : "blocked",
        detail: evidenceRecorded ? "Current evidence is passed." : "No evidence has been recorded.",
        evidenceReference: evidenceRecorded ? "M36-REHEARSAL-001" : null,
        verifiedAt: evidenceRecorded ? "2026-08-16T15:01:00Z" : null,
      },
    ],
    signoffs: [
      {
        id: signoffRecorded ? "signoff-1" : "00000000-0000-0000-0000-000000000000",
        roleKey: "hr-owner",
        roleName: "HR owner",
        decision: signoffRecorded ? "approved" : "pending",
        notes: signoffRecorded ? "Reviewed." : null,
        actorSubjectId: signoffRecorded ? "playwright-go-live-admin" : "",
        signedAt: signoffRecorded ? "2026-08-16T15:02:00Z" : "0001-01-01T00:00:00Z",
      },
    ],
  });
  await page.route("**/api/**", async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname.includes("/hrm/me")) {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          linked: true,
          worker: {
            id: "worker-m36",
            employeeNo: "EMP-M36",
            fullName: "Go-live Admin",
            status: "active",
          },
        }),
      });
      return;
    }
    if (route.request().method() === "POST" && url.pathname.endsWith("/evidence")) {
      const body = route.request().postDataJSON() as {
        controlKey: string;
        evidenceReference: string;
      };
      expect(body.controlKey).toBe("migration-rehearsal");
      expect(body.evidenceReference).toBe("M36-REHEARSAL-001");
      evidenceRecorded = true;
    }
    if (route.request().method() === "POST" && url.pathname.endsWith("/signoffs/hr-owner"))
      signoffRecorded = true;
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(route.request().method() === "GET" ? dashboard() : {}),
    });
  });

  await page.goto("/hrm/configuration/go-live");
  const workspace = page.getByTestId("go-live-readiness");
  await expect(workspace).toContainText("Production migration rehearsal");
  await expect(page.getByTestId("release-decision")).toHaveText("blocked");
  await page.getByRole("tab", { name: "Evidence" }).click();
  await page.getByLabel("Evidence reference").fill("M36-REHEARSAL-001");
  await page.getByRole("button", { name: "Record passed evidence" }).click();
  await expect(page.getByTestId("release-decision")).toHaveText("ready for signoff");
  await page.getByRole("tab", { name: "Formal sign-off" }).click();
  await page.getByRole("button", { name: "Approve" }).click();
  await expect(page.getByTestId("release-decision")).toHaveText("approved");
  expect(evidenceRecorded).toBe(true);
  expect(signoffRecorded).toBe(true);
});
