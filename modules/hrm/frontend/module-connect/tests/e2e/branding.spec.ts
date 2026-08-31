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

test("login wordmark keeps its native aspect ratio", async ({ page }) => {
  await page.goto("/sign-in");

  const logo = page.getByTestId("signin-brand-logo").first();
  const container = page.getByTestId("signin-brand-logo-container").first();
  await expect(logo).toBeVisible();
  await expect(container).toBeVisible();
  await expect(logo).not.toHaveClass(/(?:^|\s)(?:h|w)-/);
  const dimensions = await logo.evaluate((element: HTMLImageElement) => {
    const box = element.getBoundingClientRect();
    return {
      naturalWidth: element.naturalWidth,
      naturalHeight: element.naturalHeight,
      renderedWidth: box.width,
      renderedHeight: box.height,
    };
  });

  expect(dimensions.naturalWidth).toBeGreaterThan(0);
  expect(dimensions.naturalHeight).toBeGreaterThan(0);
  expect(dimensions.renderedWidth).toBeGreaterThanOrEqual(120);
  const containerBox = await container.boundingBox();
  expect(containerBox).not.toBeNull();
  expect(dimensions.renderedWidth).toBeLessThanOrEqual(containerBox!.width);
  expect(dimensions.renderedHeight).toBeLessThanOrEqual(containerBox!.height);
  expect(
    Math.abs(
      dimensions.renderedWidth / dimensions.renderedHeight
        - dimensions.naturalWidth / dimensions.naturalHeight,
    ),
  ).toBeLessThan(0.02);
});

test("mobile login keeps the Mightyfin logo visible", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/sign-in");

  const container = page.getByTestId("signin-mobile-brand-logo-container");
  const logo = page.getByTestId("signin-mobile-brand-logo");
  await expect(container).toBeVisible();
  await expect(logo).toBeVisible();
  await expect(logo).toHaveAttribute("src", "/mightyfin-logo-color.png");
});
