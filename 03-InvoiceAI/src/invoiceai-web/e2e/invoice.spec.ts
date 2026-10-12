import { test, expect } from "@playwright/test";
import path from "node:path";
test("upload, detect mismatch, correct, export and display sources", async ({
  page,
}) => {
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await page.goto("/");
  await expect(
    page.getByText("Mod demonstrativ", { exact: true }),
  ).toBeVisible();
  await page
    .locator("#file")
    .setInputFiles(path.resolve("../../samples/invoice.pdf"));
  await page.getByRole("button", { name: "Procesează documentul" }).click();
  await expect(page.getByText("TOTAL_MISMATCH", { exact: true })).toBeVisible({
    timeout: 30000,
  });
  await expect(
    page.getByRole("button", { name: "Descarcă JSON" }),
  ).toBeDisabled();
  const total = page
    .locator(".field")
    .filter({ has: page.locator(".field-label", { hasText: /^Total/ }) });
  await total.getByRole("button", { name: "Corectează" }).click();
  await total.getByLabel("Total", { exact: true }).fill("120.00");
  await total.getByRole("button", { name: "Salvează" }).click();
  await expect(page.getByText("TOTAL_MISMATCH", { exact: true })).toHaveCount(
    0,
  );
  await expect(
    page.getByRole("button", { name: "Descarcă JSON" }),
  ).toBeEnabled();
  const [download] = await Promise.all([
    page.waitForEvent("download"),
    page.getByRole("button", { name: "Descarcă JSON" }).click(),
  ]);
  const fs = await import("node:fs/promises");
  const file = await download.path();
  const payload = JSON.parse(await fs.readFile(file!, "utf8"));
  expect(payload.amounts.total).toBe(120);
  expect(payload.corrections.total).toBe(120);
  expect(
    payload.evidence.find((x: { path: string }) => x.path === "total").value,
  ).toBe(119);
  await total.getByText("Text extras și proveniență").click();
  await expect(
    total.getByText("119.00 MDL", { exact: true }).first(),
  ).toBeVisible();
  await expect(total.getByRole("link", { name: "Pagina 1" })).toHaveAttribute(
    "href",
    /#page=1$/,
  );
  await page
    .getByLabel("Întrebare", { exact: true })
    .fill("Care este totalul?");
  await page.getByRole("button", { name: "Întreabă", exact: true }).click();
  await expect(page.locator(".answer")).toBeVisible();
  expect(errors).toEqual([]);
  await page.screenshot({ path: "/tmp/invoiceai-desktop.png", fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({ path: "/tmp/invoiceai-mobile.png", fullPage: true });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBeTruthy();
  const cleanup = await page.request.delete(
    `/api/documents/${payload.evidence[0].evidence[0].documentId}`,
  );
  const job = await cleanup.json();
  await expect
    .poll(
      async () =>
        (await (await page.request.get(`/api/jobs/${job.jobId}`)).json())
          .status,
    )
    .toBe("Completed");
});
