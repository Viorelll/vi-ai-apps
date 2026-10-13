import { test, expect } from "@playwright/test";
import path from "node:path";
test("570 EUR, document evidence, extraction review, chat and persisted plan", async ({
  page,
}) => {
  const failures: string[] = [];
  page.on("pageerror", (e) => failures.push(e.message));
  await page.goto("/");
  await expect(
    page.getByRole("heading", { name: /O casă începe/ }),
  ).toBeVisible();
  const planName = `E2E ${Date.now()}`;
  await page.getByLabel("Nume plan").fill(planName);
  await page.getByRole("button", { name: /Încarcă exemplul/ }).click();
  await expect(page.getByTestId("budget-total")).toContainText("570.00");
  await expect(
    page.getByText("110 m² × 5 + 20 = 570.00 EUR", { exact: true }),
  ).toBeVisible();
  await page
    .getByLabel("Încarcă document")
    .setInputFiles(path.resolve("../../samples/offer-demo.txt"));
  await expect(page.getByText("Ready", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: /Extrage câmpurile/ }).click();
  await expect(page.getByText(/demo-explicit-text-parser/)).toBeVisible();
  await page.getByRole("button", { name: "Verifică și completează" }).click();
  await expect(page.getByLabel("Confirm prețul")).not.toBeChecked();
  await expect(page.getByLabel("Preț / unitate")).toHaveValue("5");
  await expect(page.getByLabel("Transport", { exact: true })).toHaveValue("20");
  await page.getByRole("button", { name: "Întreabă →" }).click();
  await expect(page.getByText("demo-no-model", { exact: true })).toBeVisible();
  await page.reload();
  await page.getByLabel("Planuri salvate").selectOption({ label: planName });
  await expect(page.getByTestId("budget-total")).toContainText("570.00");
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(
    page.getByRole("heading", { name: /Necesar & oferte/ }),
  ).toBeVisible();
  expect(failures).toEqual([]);
});
