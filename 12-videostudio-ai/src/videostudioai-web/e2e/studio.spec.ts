import { test, expect } from "@playwright/test";
test("brief → confirmed storyboard → playable clip → immutable remix", async ({
  page,
}) => {
  await page.goto("/");
  await expect(
    page.getByRole("heading", { name: "Povestea ta. În mișcare." }),
  ).toBeVisible();
  await page
    .getByLabel("Ce poveste vrei să spui?")
    .fill("O ceașcă de cafea în lumina dimineții.");
  await page.getByRole("button", { name: "Creează scenariul →" }).click();
  await expect(page.getByLabel("Prompt scena 1")).toBeVisible();
  await page
    .getByLabel("Prompt scena 1")
    .fill("Close-up of a ceramic coffee cup, warm daylight, slow dolly.");
  await expect(
    page.getByRole("button", { name: "Generează clip ✧" }),
  ).toBeDisabled();
  await page
    .getByRole("button", { name: "Confirmă scenariul", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Generează clip ✧" }),
  ).toBeEnabled();
  await page.getByRole("button", { name: "Generează clip ✧" }).click();
  await expect(page.locator("video")).toHaveCount(1, { timeout: 90000 });
  const original = await page.locator("video").first().getAttribute("src");
  await page
    .locator("video")
    .first()
    .evaluate(async (video: HTMLVideoElement) => {
      await video.play();
    });
  await expect
    .poll(() =>
      page
        .locator("video")
        .first()
        .evaluate((v: HTMLVideoElement) => v.duration),
    )
    .toBeGreaterThan(7);
  await page.getByRole("button", { name: "Creează remix" }).click();
  await expect(page.locator("video")).toHaveCount(2, { timeout: 90000 });
  await expect(page.locator("video").first()).toHaveAttribute("src", original!);
  await page.reload();
  await expect(page.locator("video")).toHaveCount(2);
});
