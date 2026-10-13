import { expect, test } from "@playwright/test";
test("seed A/B comparison, result history and exports work in the actual application", async ({
  page,
  request,
}) => {
  const seed = await request.get("/api/evaluations/datasets");
  expect(seed.ok()).toBeTruthy();
  const datasets = await seed.json();
  expect(
    datasets.some((d: { name: string }) => d.name === "Synthetic arithmetic"),
  ).toBeTruthy();
  const variantsResponse = await request.get("/api/evaluations/variants");
  const { variants, mode } = await variantsResponse.json();
  expect(mode).toBe("Demo");
  expect(variants).toHaveLength(2);
  await page.goto("/");
  await expect(
    page.getByRole("heading", { name: /Aceleași cazuri/i }),
  ).toBeVisible();
  await expect(page.getByText("Mod demonstrativ explicit")).toBeVisible();
  await page.getByRole("button", { name: /Rulează comparația/i }).click();
  await expect(
    page.getByText("Completed", { exact: true }).first(),
  ).toBeVisible({ timeout: 30000 });
  await expect(page.getByText("3/3 checks")).toBeVisible();
  await expect(page.getByText("2/3 checks")).toBeVisible();
  await expect(
    page.getByText("Judge 5.00 / 5", { exact: false }).first(),
  ).toBeVisible();
  const jsonLink = await page
    .locator("a")
    .filter({ hasText: "JSON" })
    .first()
    .getAttribute("href");
  expect(jsonLink).toBeTruthy();
  const exportResponse = await page.evaluate(async (href) => {
    const response = await fetch(href);
    return { ok: response.ok, body: await response.json() };
  }, jsonLink);
  expect(exportResponse.ok).toBeTruthy();
  expect(exportResponse.body.results).toHaveLength(6);
});
test("dataset form rejects invalid JSON and reports an actionable error", async ({
  page,
}) => {
  await page.goto("/");
  await page.getByText("Dataset nou · snapshot imutabil").click();
  await page
    .getByLabel("Cazuri JSON · input / expected / evidenceIds")
    .fill("{invalid");
  await page.getByRole("button", { name: "Salvează dataset" }).click();
  await expect(page.getByRole("alert")).toContainText(/JSON at position 1/);
});
