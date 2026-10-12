import { test, expect } from "@playwright/test";
import { readFile } from "node:fs/promises";
test("generate, paint original-resolution mask, edit, export and restore history", async ({
  page,
}) => {
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await page.goto("/");
  await expect(
    page.getByText("Mod demonstrativ:", { exact: false }),
  ).toBeVisible();
  const brief = `Studio perfume advertising, warm lighting ${Date.now()}`;
  await page.getByLabel("Descrie imaginea").fill(brief);
  await page.getByRole("button", { name: "Generează imaginea" }).click();
  await expect(page.locator(".results .status")).toHaveText("Ready", {
    timeout: 30_000,
  });
  await expect(page.locator(".asset-card")).toHaveCount(1);
  const canvas = page.getByLabel("Mască de editare");
  await canvas.scrollIntoViewIfNeeded();
  const rect = await canvas.boundingBox();
  if (!rect) throw new Error("canvas not rendered");
  await page.mouse.move(rect.x + 20, rect.y + 20);
  await page.mouse.down();
  await page.mouse.move(rect.x + 100, rect.y + 45, { steps: 8 });
  await page.mouse.up();
  const submitted = page.waitForRequest(
    (r) => r.url().endsWith("/edits") && r.method() === "POST",
  );
  await page.getByRole("button", { name: "Aplică editarea" }).click();
  const body = (await submitted).postDataJSON();
  expect(body.inputAssetId).toBeTruthy();
  expect(body.maskDocumentId).toBeTruthy();
  await expect(page.locator(".asset-card")).toHaveCount(2, { timeout: 30_000 });
  await expect(page.locator(".results .status")).toHaveText("Ready");
  await page.getByText("Compară cu originalul").click();
  await expect(page.getByAltText("Înainte", { exact: true })).toBeVisible();
  await expect(page.getByAltText("După", { exact: true })).toBeVisible();
  await page.getByLabel("Text exact pentru banner").fill("Ofertă de laborator");
  const downloadEvent = page.waitForEvent("download");
  await page.getByRole("button", { name: /Descarcă cu banner/ }).click();
  const download = await downloadEvent;
  const path = await download.path();
  if (!path) throw new Error("download missing");
  const bytes = await readFile(path);
  expect(bytes.subarray(0, 8)).toEqual(
    Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]),
  );
  expect(bytes.readUInt32BE(16)).toBe(1024);
  expect(bytes.readUInt32BE(20)).toBe(1024);
  await page.screenshot({ path: "test-results/studio.png", fullPage: true });
  await page.reload();
  const history = page.getByLabel("Proiecte salvate");
  await expect(history).toBeVisible();
  const value = await history
    .locator("option")
    .filter({ hasText: brief.slice(0, 45) })
    .first()
    .getAttribute("value");
  if (!value) throw new Error("persisted project missing");
  await history.selectOption(value);
  await expect(page.locator(".asset-card")).toHaveCount(2);
  expect(errors).toEqual([]);
});
test("short brief is disabled and layout fits mobile", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/");
  await page.getByLabel("Descrie imaginea").fill("abc");
  await expect(
    page.getByRole("button", { name: "Generează imaginea" }),
  ).toBeDisabled();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
});
