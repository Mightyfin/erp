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
