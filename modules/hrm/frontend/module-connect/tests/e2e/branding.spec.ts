import { expect, test } from "@playwright/test";

test("uses Mightyfin HRMS browser branding and the checked-in favicon", async ({ page }) => {
  await page.goto("/");

  await expect(page).toHaveTitle("Mightyfin HRMS");
  await expect(page.getByText("Mightyfin HRMS", { exact: true })).toBeVisible();
  await expect(page.locator('link[rel="icon"][href="/mightyfin-mark.png"]')).toHaveCount(1);

  const favicon = await page.request.get("/favicon.ico");
  expect(favicon.status()).toBe(200);
  expect(favicon.headers()["content-type"]).toMatch(/image\/x-icon|image\/vnd\.microsoft\.icon/);
  expect((await favicon.body()).byteLength).toBeGreaterThan(0);
});
