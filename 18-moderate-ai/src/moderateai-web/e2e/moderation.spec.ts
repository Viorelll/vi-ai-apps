import { test, expect } from "@playwright/test";
test("synthetic review preserves policy and supports a confirmed override", async ({
  page,
}) => {
  await page.goto("/");
  await expect(page.getByText("DEMO · fără AI real")).toBeVisible();
  await page.getByRole("button", { name: "2 · Review", exact: true }).click();
  await page.getByRole("button", { name: "Analizează conținutul →" }).click();
  await expect(page.getByText("Decizie .NET: REVIEW")).toBeVisible({
    timeout: 30000,
  });
  await expect(
    page.locator(".result-panel").getByText("[demo:review]", { exact: true }),
  ).toHaveCount(0);
  await page.getByRole("button", { name: "Arată inputul explicit" }).click();
  await expect(page.locator("pre")).toHaveText("[demo:review]");
  await page.getByRole("button", { name: "Ascunde inputul" }).click();
  await page
    .getByLabel("Notă de review (opțional)")
    .fill("Verificare locală sintetică");
  await page.getByRole("button", { name: "Allow local", exact: true }).click();
  await page
    .getByRole("button", { name: "Confirmă allow", exact: true })
    .click();
  await expect(page.locator(".result-title")).toContainText("Allow");
  await expect(page.getByText("Decizie .NET: REVIEW")).toBeVisible();
  await expect(page.locator(".review-history")).toContainText(
    "Verificare locală sintetică",
  );
  await page.reload();
  await page.locator(".history .item").first().click();
  await expect(page.locator(".review-history")).toContainText(
    "Verificare locală sintetică",
  );
});

for (const [fixture, decision] of [
  ["0 · Allow", "ALLOW"],
  ["4 · Block", "BLOCK"],
] as const) {
  test(`synthetic ${decision} shows the deterministic policy`, async ({
    page,
  }) => {
    await page.goto("/");
    await expect(page.getByText("DEMO · fără AI real")).toBeVisible();
    await page.getByRole("button", { name: fixture, exact: true }).click();
    await page.getByRole("button", { name: "Analizează conținutul →" }).click();
    await expect(
      page
        .locator(".policy strong")
        .filter({ hasText: `Decizie .NET: ${decision}` }),
    ).toBeVisible({
      timeout: 20000,
    });
    await expect(
      page.getByText("FourSeverityLevels", { exact: true }),
    ).toBeVisible();
  });
}
test("image preview is explicit and deletion invalidates the analysis", async ({
  page,
}) => {
  await page.goto("/");
  await expect(page.getByText("DEMO · fără AI real")).toBeVisible();
  await page
    .getByLabel("Încarcă imagine")
    .setInputFiles("e2e/fixtures/sample.png");
  await expect(page.locator(".file-chip")).toContainText("sample.png");
  await page.getByRole("button", { name: "Analizează conținutul →" }).click();
  await expect(
    page.locator(".policy strong").filter({ hasText: "Decizie .NET: REVIEW" }),
  ).toBeVisible({
    timeout: 20000,
  });
  await expect(
    page.getByAltText("Imagine trimisă pentru moderare"),
  ).toHaveCount(0);
  await page.getByRole("button", { name: "Arată inputul explicit" }).click();
  await expect(
    page.getByAltText("Imagine trimisă pentru moderare"),
  ).toBeVisible();
  page.once("dialog", (dialog) => dialog.accept());
  await page.getByRole("button", { name: "Șterge sursa locală" }).click();
  await expect(
    page.getByText(
      "Sursa a fost ștearsă. Acest rezultat este istoric și nu mai este actual.",
    ),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Allow local", exact: true }),
  ).toHaveCount(0);
});
