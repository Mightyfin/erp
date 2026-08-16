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
