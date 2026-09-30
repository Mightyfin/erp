import { expect, test, type Page } from "@playwright/test";

const local = { mode: "local", localUsersEnabled: true, identityConfigured: false };
const hybrid = { mode: "hybrid", localUsersEnabled: true, identityConfigured: true };

async function configure(page: Page, policy: () => unknown, failed = false) {
  const idpRequests: string[] = [];
  const bearerHeaders: string[] = [];
  await page.route("https://auth.mightyfinance.co.zm/**", async (route) => {
    idpRequests.push(route.request().url());
    await route.fulfill({ contentType: "text/html", body: "<h1>Identity provider</h1>" });
  });
  await page.route("**/api/hrm/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    const authorization = route.request().headers().authorization;
    if (authorization) bearerHeaders.push(authorization);
    const body = path.endsWith("/auth/capabilities") ? policy()
      : path.endsWith("/auth/me") ? { authenticated: false, user: null }
      : path.endsWith("/branding") ? {} : [];
    await route.fulfill({ status: failed && path.endsWith("/auth/capabilities") ? 503 : 200, json: body });
  });
  return { idpRequests, bearerHeaders };
}

for (const expiresAt of [1, Date.now() + 3_600_000]) {
  test(`local mode ignores stored OIDC tokens (${expiresAt === 1 ? "expired" : "valid"}) and callbacks`, async ({ page }) => {
    const observed = await configure(page, () => local);
    await page.addInitScript((expiry) => {
      localStorage.setItem("erp.oidc.session", JSON.stringify({ accessToken: "old-access", idToken: "old-id", refreshToken: "old-refresh", expiresAt: expiry }));
      localStorage.setItem("erp.oidc.state", JSON.stringify({ state: "old-state" }));
    }, expiresAt);
    await page.goto("/sign-in?code=old-code&state=old-state");
    await expect(page.getByRole("button", { name: "Sign in with HRMS local account" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Continue with organisation account" })).toHaveCount(0);
    expect(await page.evaluate(() => localStorage.getItem("erp.oidc.session"))).toBeNull();
    expect(observed.idpRequests).toEqual([]);
    expect(observed.bearerHeaders).toEqual([]);
  });
}

test("hybrid uses runtime policy without a frontend build flag and supports interactive PKCE", async ({ page }) => {
  const observed = await configure(page, () => hybrid);
  await page.goto("/sign-in?error=login_required");
  await expect(page.getByRole("button", { name: "Sign in with HRMS local account" })).toBeVisible();
  await page.getByRole("button", { name: "Continue with organisation account" }).click();
  await expect(page.getByRole("heading", { name: "Identity provider" })).toBeVisible();
  const request = new URL(observed.idpRequests[0]);
  expect(request.pathname).toContain("/realms/mightyfin-erp/");
  expect(request.searchParams.get("client_id")).toBe("erp-web");
  expect(request.searchParams.get("code_challenge_method")).toBe("S256");
  expect(request.searchParams.get("prompt")).toBe("login");
});

test("OIDC-only mode hides the local form", async ({ page }) => {
  await configure(page, () => ({ mode: "oidc", identityConfigured: true, localUsersEnabled: false }));
  await page.goto("/sign-in?error=login_required");
  await expect(page.getByRole("button", { name: "Continue with organisation account" })).toBeVisible();
  await expect(page.getByLabel("Work email")).toHaveCount(0);
});

test("silent SSO runs only after runtime policy enables it", async ({ page }) => {
  const observed = await configure(page, () => hybrid);
  await page.goto("/sign-in");
  await expect(page.getByRole("heading", { name: "Identity provider" })).toBeVisible();
  expect(new URL(observed.idpRequests[0]).searchParams.get("prompt")).toBe("none");
});

for (const policy of [{}, { mode: "unknown", identityConfigured: true, localUsersEnabled: true }, { mode: "oidc", identityConfigured: "true", localUsersEnabled: false }]) {
  test(`incomplete or invalid policy does not enable sign-in: ${JSON.stringify(policy)}`, async ({ page }) => {
    const observed = await configure(page, () => policy);
    await page.goto("/sign-in");
    await expect(page.getByRole("alert")).toContainText("Sign-in is temporarily unavailable");
    await expect(page.getByRole("button", { name: "Continue with organisation account" })).toHaveCount(0);
    expect(observed.idpRequests).toEqual([]);
  });
}

test("configuration failure does not enable OIDC", async ({ page }) => {
  const observed = await configure(page, () => hybrid, true);
  await page.goto("/sign-in");
  await expect(page.getByRole("alert")).toContainText("Sign-in is temporarily unavailable");
  expect(observed.idpRequests).toEqual([]);
});

test("changing server mode takes effect on reload without rebuilding", async ({ page }) => {
  let policy = local;
  const observed = await configure(page, () => policy);
  await page.goto("/sign-in?error=login_required");
  await expect(page.getByRole("button", { name: "Sign in with HRMS local account" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Continue with organisation account" })).toHaveCount(0);
  policy = hybrid;
  await page.reload();
  await expect(page.getByRole("button", { name: "Continue with organisation account" })).toBeVisible();
  policy = local;
  await page.reload();
  await expect(page.getByRole("button", { name: "Sign in with HRMS local account" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Continue with organisation account" })).toHaveCount(0);
  expect(observed.idpRequests).toEqual([]);
});
