import { test, expect } from "@playwright/test";
import path from "node:path";
test("upload, polling, candidate sources, grounded question and export", async ({
  page,
}) => {
  await page.goto("/");
  await expect(
    page.getByRole("heading", { name: "Începe cu o imagine" }),
  ).toBeVisible();
  await page
    .locator("#photo")
    .setInputFiles(path.resolve("../../fixtures/black-usb-adapter.png"));
  await page.getByRole("button", { name: "Găsește candidați" }).click();
  await expect(
    page.getByRole("heading", { name: "Analiză finalizată" }),
  ).toBeVisible({ timeout: 30000 });
  await expect(page.locator(".candidate")).toHaveCount(2);
  await expect(page.locator(".demo")).toBeVisible();
  await expect(page.locator(".candidate").first()).toContainText("USB-BLK-01");
  await page
    .getByRole("button", { name: "Vezi fișa și sursele" })
    .first()
    .click();
  await expect(page.locator(".sheet").first()).toContainText(
    "Date artificiale",
  );
  await page.locator("#followup").fill("Ce stoc a fost consultat?");
  await page.getByRole("button", { name: "Trimite" }).click();
  await expect(page.locator(".followup")).toContainText("stoc snapshot");
  const download = page.waitForEvent("download");
  await page.getByRole("button", { name: "Export JSON" }).click();
  expect((await download).suggestedFilename()).toMatch(/^shopvision-.*\.json$/);
  await page.screenshot({
    path: "/tmp/shopvision-desktop.png",
    fullPage: true,
  });
});
test("mobile layout and invalid-file error", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/");
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
  await page.locator("#photo").setInputFiles({
    name: "spoof.png",
    mimeType: "image/png",
    buffer: Buffer.from("not an image"),
  });
  await page.getByRole("button", { name: "Găsește candidați" }).click();
  await expect(page.getByRole("alert")).toContainText("invalid_request");
  await page.screenshot({ path: "/tmp/shopvision-mobile.png", fullPage: true });
});
