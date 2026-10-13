import { test, expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
test("keyboard-accessible document workflow, aligned variants and audio", async ({
  page,
}) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { level: 1 })).toHaveText(
    /Documentele devin/,
  );
  let accessibility = await new AxeBuilder({ page }).analyze();
  expect(accessibility.violations).toEqual([]);
  await page.keyboard.press("Tab");
  await expect(
    page.getByRole("link", { name: "Sari la documente" }),
  ).toBeFocused();
  await page
    .getByRole("button", { name: "Încearcă documentul demonstrativ" })
    .click();
  await expect(
    page.getByRole("button", { name: /Generează lectura/ }),
  ).toBeEnabled({ timeout: 30000 });
  await page.getByRole("button", { name: /Generează lectura/ }).click();
  await expect(page.getByRole("status")).toHaveText("Ready", {
    timeout: 60000,
  });
  await expect(
    page.getByText("Do not exceed 10 units.", { exact: true }),
  ).toBeVisible();
  await expect(page.locator(".reading-block")).toHaveCount(3);
  const audio = page.locator("audio").first();
  await audio.evaluate(async (element: HTMLAudioElement) => {
    element.load();
    await new Promise<void>((resolve, reject) => {
      element.addEventListener("loadedmetadata", () => resolve(), {
        once: true,
      });
      element.addEventListener(
        "error",
        () => reject(new Error("audio failed")),
        { once: true },
      );
    });
  });
  expect(
    await audio.evaluate((element: HTMLAudioElement) => element.duration),
  ).toBeGreaterThan(0);
  expect(
    await audio.evaluate((element: HTMLAudioElement) => element.autoplay),
  ).toBe(false);
  accessibility = await new AxeBuilder({ page }).analyze();
  expect(accessibility.violations).toEqual([]);
  await page.reload();
  await expect(
    page.getByText("Do not exceed 10 units.", { exact: true }),
  ).toBeVisible();
  await page.screenshot({
    path: "../../artifacts/reader-desktop.png",
    fullPage: true,
  });
});
test("responsive reading screen at mobile width", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/");
  await expect(page.getByLabel("Limba traducerii")).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
  const accessibility = await new AxeBuilder({ page }).analyze();
  expect(accessibility.violations).toEqual([]);
  await page.screenshot({
    path: "../../artifacts/reader-mobile.png",
    fullPage: true,
  });
});
