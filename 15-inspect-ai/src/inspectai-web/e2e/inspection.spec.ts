import { test, expect } from "@playwright/test";
test("creates a confirmed equipment context and exposes upload controls", async ({
  page,
}) => {
  await page.route("**/api/inspections", (route) =>
    route.fulfill({ status: 201, json: { id: "inspection-1" } }),
  );
  await page.route("**/api/inspections/inspection-1", (route) =>
    route.fulfill({
      json: {
        id: "inspection-1",
        equipmentLabel: "Pompă",
        equipmentModel: "P-100",
        manualVersion: "1.0",
        revision: 1,
        state: "Created",
        sources: [],
        findings: [],
        checklist: [],
        questions: [],
        report: null,
        limitations: "Nu certifică siguranța.",
      },
    }),
  );
  await page.goto("/");
  await page.getByLabel("Echipament", { exact: true }).fill("Pompă");
  await page.getByLabel("Model confirmat").fill("P-100");
  await page.getByLabel("Versiunea manualului").fill("1.0");
  await page.getByRole("button", { name: "Creează inspecția" }).click();
  await expect(page.getByRole("heading", { name: "Pompă" })).toBeVisible();
  await expect(page.getByLabel("Adaugă fotografii")).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Analizează echipamentul" }),
  ).toBeDisabled();
  await expect(page.getByText("Nu certifică siguranța.")).toBeVisible();
});

test("renders separate findings, dual evidence, uncertainty and local checklist status", async ({
  page,
}) => {
  const photo = "11111111-1111-1111-1111-111111111111",
    manual = "22222222-2222-2222-2222-222222222222",
    chunk = "33333333-3333-3333-3333-333333333333",
    finding = "44444444-4444-4444-4444-444444444444";
  const data = {
    id: "report-1",
    equipmentLabel: "Pompă P-100",
    equipmentModel: "P-100",
    manualVersion: "1.0",
    state: "NeedsReview",
    revision: 5,
    errorCode: null,
    sources: [
      {
        documentId: photo,
        role: "photo",
        fileName: "inspection.png",
        status: "Ready",
        contentUrl: "/api/documents/" + photo + "/preview",
      },
      {
        documentId: manual,
        role: "manual",
        fileName: "manual.txt",
        status: "Ready",
        contentUrl: "/api/documents/" + manual + "/content",
      },
    ],
    findings: [
      {
        id: finding,
        documentId: photo,
        kind: "visible",
        description: "Pată brună pe carcasă.",
        reason: null,
        regionJson: null,
      },
    ],
    checklist: [
      {
        id: "check-1",
        findingId: finding,
        text: "Revizuiește procedura de curățare.",
        evidenceJson: JSON.stringify([
          {
            chunkId: chunk,
            quote: "Consultă tehnicianul înainte de curățare.",
          },
        ]),
        status: "proposed",
      },
    ],
    report: {
      resultJson: JSON.stringify({
        summary: "O pată este vizibilă.",
        unknowns: ["Starea internă este neobservabilă."],
      }),
      sourcesJson: JSON.stringify({
        sources: [
          {
            chunkId: chunk,
            documentId: manual,
            pageNumber: 3,
            text: "Consultă tehnicianul înainte de curățare.",
          },
        ],
      }),
    },
    questions: [],
    limitations:
      "Raportul nu certifică funcționarea ori siguranța echipamentului.",
  };
  await page.addInitScript(() =>
    localStorage.setItem("inspectai-current", "report-1"),
  );
  await page.route("**/api/inspections/report-1", (route) =>
    route.fulfill({ json: data }),
  );
  await page.route("**/api/inspections/report-1/checklist/check-1", (route) => {
    data.revision++;
    data.checklist[0].status = "noted";
    return route.fulfill({ json: data });
  });
  await page.route("**/api/documents/**/preview", (route) =>
    route.fulfill({
      status: 200,
      contentType: "image/png",
      body: Buffer.from(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Zk8sAAAAASUVORK5CYII=",
        "base64",
      ),
    }),
  );
  await page.goto("/");
  await expect(page.getByText("Pată brună pe carcasă.")).toBeVisible();
  await expect(
    page.getByText("Starea internă este neobservabilă."),
  ).toBeVisible();
  await expect(
    page.getByRole("link", { name: "Manual · pagina 3" }),
  ).toHaveAttribute("href", `/api/documents/${manual}/content#page=3`);
  await expect(
    page.getByRole("link", { name: "Observația din fotografie" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Notează local verificarea" }).click();
  await expect(
    page.getByText("Notat local · nicio intervenție executată"),
  ).toBeVisible();
  await page.screenshot({
    path: "../../artifacts/screenshots/inspectai-report.png",
    fullPage: true,
  });
});
