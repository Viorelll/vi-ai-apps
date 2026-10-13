import { test, expect } from "@playwright/test";
import path from "node:path";
test("upload, index, search and seek the original at 70 seconds", async ({
  page,
}) => {
  await page.goto("/");
  await expect(
    page.getByRole("heading", { name: "Răspunsul e într-un clip." }),
  ).toBeVisible();
  await page
    .getByLabel("Încarcă video", { exact: true })
    .setInputFiles(path.resolve("../../seeds/postgresql-course.mp4"));
  const row = page
    .locator(".video-list li")
    .filter({ hasText: "postgresql-course" });
  await expect(row).toBeVisible();
  const button = row.getByRole("button", {
    name: /^(Procesează|Reindexează)$/,
  });
  await button.click();
  await expect(row.locator(".status")).toHaveText("Pregătit", {
    timeout: 30000,
  });
  await row.getByRole("checkbox").check();
  await page.getByLabel("Ce vrei să găsești?").fill("index pentru query");
  await page.getByRole("button", { name: "Caută în clipuri" }).click();
  await page.getByRole("button", { name: "01:10 – 01:35" }).click();
  await expect
    .poll(() =>
      page
        .locator("video")
        .evaluate((v) => (v as HTMLVideoElement).currentTime),
    )
    .toBeCloseTo(70, 1);
  await expect(page.locator(".citation")).toContainText("CREATE INDEX");
  await expect(page.locator("video")).toHaveAttribute(
    "src",
    /\/api\/video\/library\/.+\/content/,
  );
});
test("small viewport and accessible empty search", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/");
  await expect(
    page.getByRole("button", { name: "Caută în clipuri" }),
  ).toBeDisabled();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
});
