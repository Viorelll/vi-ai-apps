import { test, expect } from "@playwright/test";
const version = {
  id: "version-2",
  documentId: "doc-2",
  contractKey: "contract-demo",
  versionLabel: "V2",
  state: "Ready",
  revision: 2,
  summary: "Contract demonstrativ.",
  deployment: "test-only",
  inputTokens: 10,
  outputTokens: 5,
};
const citation = {
  clauseId: "clause-2",
  versionId: version.id,
  documentId: "doc-2",
  generation: 1,
  pageNumber: 2,
  excerpt: "Beneficiarul plătește în 15 zile de la recepție.",
  url: "/api/documents/doc-2/content#page=2",
};
test("loads obligations, relative dates, correct citations and version-scoped questions", async ({
  page,
}) => {
  await page.route("**/api/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    let body: unknown;
    if (path.endsWith("/questions")) {
      const input = route.request().postDataJSON();
      expect(input.question).toBe("Care este termenul de plată?");
      expect(path).toContain(version.id);
      body = {
        conversationId: "conv",
        messageId: "msg",
        answer: "Termenul este 15 zile de la recepție.",
        citations: [citation],
        deployment: "test-only",
        inputTokens: 10,
        outputTokens: 5,
      };
    } else if (path.endsWith("/versions")) body = [version];
    else
      body = {
        version,
        clauses: [
          { id: "clause-2", text: citation.excerpt, ordinal: 0, pageNumber: 2 },
        ],
        obligations: [
          {
            id: "obligation",
            partyText: "Beneficiarul",
            actionText: "Plată",
            dueDate: null,
            relativeTerm: "15 zile de la recepție",
            evidenceJson: JSON.stringify([
              { clauseId: "clause-2", quote: citation.excerpt },
            ]),
          },
        ],
        findings: [],
        unknowns: ["Data recepției"],
        citations: [citation],
      };
    await route.fulfill({ json: body });
  });
  await page.goto("/");
  await page.getByRole("button", { name: /contract-demo.*V2/ }).click();
  await expect(
    page.getByText("Data absolută rămâne necunoscută", { exact: false }),
  ).toBeVisible();
  await page.getByText("Sursa 1 · pagina 2").first().click();
  await expect(
    page.getByRole("link", { name: "Deschide originalul ↗" }).first(),
  ).toHaveAttribute("href", "/api/documents/doc-2/content#page=2");
  await page.getByLabel("Întrebarea ta").fill("Care este termenul de plată?");
  await page.getByRole("button", { name: "Trimite întrebarea ↗" }).click();
  await expect(
    page.getByText("Termenul este 15 zile de la recepție.", { exact: true }),
  ).toBeVisible();
});
test("shows missing Foundry configuration and trace instead of a successful analysis", async ({
  page,
}) => {
  await page.route("**/api/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith("/analyze"))
      return route.fulfill({
        status: 503,
        json: {
          code: "foundry_not_configured",
          traceId: "trace-demo",
          detail: "Configure Foundry on the server.",
        },
      });
    await route.fulfill({
      json: path.endsWith("/versions")
        ? [version]
        : {
            version,
            clauses: [],
            obligations: [],
            findings: [],
            unknowns: [],
            citations: [],
          },
    });
  });
  await page.goto("/");
  await page.getByRole("button", { name: /contract-demo.*V2/ }).click();
  await page.getByRole("button", { name: "Analizează contractul" }).click();
  await expect(page.getByRole("alert")).toContainText("foundry_not_configured");
  await expect(page.getByRole("alert")).toContainText("trace-demo");
});
