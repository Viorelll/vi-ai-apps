import { expect, test } from "@playwright/test";
test("creates a case, ingests evidence, cites sources, confirms once and restores on reload", async ({
  page,
  request,
}) => {
  expect((await (await request.get("/api/config")).json()).aiMode).toBe("Demo");
  await page.goto("/");
  await expect(
    page.getByRole("heading", { name: "Hai să rezolvăm problema." }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Creează cazul", exact: true })
    .click();
  await expect(page.getByText(/Caz #/)).toBeVisible();
  const fileName = `browser-evidence-${Date.now()}.txt`;
  await page
    .getByLabel("Încarcă dovadă")
    .setInputFiles({
      name: fileName,
      mimeType: "text/plain",
      buffer: Buffer.from("Synthetic monitor damage evidence"),
    });
  const row = page.locator(".file-list article").filter({ hasText: fileName });
  await expect(row).toContainText("Ready · atașat", { timeout: 30000 });
  await page
    .getByLabel("Descrie problema")
    .fill("Ecranul este fisurat. Pregătește un tichet.");
  await page.getByRole("button", { name: "Trimite mesajul" }).click();
  await expect(page.locator(".message.assistant")).toContainText("LAB-1001", {
    timeout: 30000,
  });
  await expect(page.locator(".source")).toHaveCount(2);
  await expect(page.locator(".proposal")).toContainText("Draft");
  await page.getByRole("button", { name: "Confirmă tichetul local" }).click();
  await expect(page.locator(".proposal")).toContainText(
    "Executată o singură dată",
  );
  await page.reload();
  await expect(page.locator(".proposal")).toContainText(
    "Executată o singură dată",
  );
  await expect(
    page.getByRole("button", { name: "Confirmă tichetul local" }),
  ).toHaveCount(0);
  await page.screenshot({
    path: "../../output/supportai-desktop.png",
    fullPage: true,
  });
  await page.setViewportSize({ width: 390, height: 844 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBeTruthy();
  await row.getByRole("button", { name: "Șterge" }).click();
  await expect(row).toHaveCount(0);
});
