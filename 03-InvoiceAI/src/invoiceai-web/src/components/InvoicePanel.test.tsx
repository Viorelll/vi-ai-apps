import { it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import InvoicePanel from "./InvoicePanel";
import type { Invoice } from "../models/Invoice";
const base: Invoice = {
  id: "i",
  documentId: "d",
  kind: "invoice",
  state: "NeedsReview",
  revision: 1,
  vendorName: "Seed",
  invoiceNumber: "1",
  date: "2026-10-11",
  currency: "MDL",
  subtotal: 100,
  tax: 20,
  total: 119,
  explanation: null,
  suggestedCategory: null,
  errorCode: null,
  inputTokens: 0,
  outputTokens: 0,
  fields: [],
  lines: [],
  corrections: {},
  issues: [
    {
      code: "TOTAL_MISMATCH",
      severity: "error",
      path: "total",
      detailsJson: "{}",
    },
  ],
};
const callbacks = {
  onCorrect: vi.fn(),
  onAnalyze: vi.fn(),
  onValidate: vi.fn(),
  onExport: vi.fn(),
  onRetry: vi.fn(),
};
it("blocks export when deterministic rules fail", () => {
  render(<InvoicePanel invoice={base} busy={false} {...callbacks} />);
  expect(screen.getByText("Descarcă JSON")).toBeDisabled();
  expect(screen.getByText("TOTAL_MISMATCH")).toBeVisible();
});
it("enables export after validation", () => {
  render(
    <InvoicePanel
      invoice={{ ...base, state: "Validated", total: 120, issues: [] }}
      busy={false}
      {...callbacks}
    />,
  );
  expect(screen.getByText("Descarcă JSON")).toBeEnabled();
});
it("disables actions during extraction", () => {
  render(
    <InvoicePanel
      invoice={{ ...base, state: "Extracting", issues: [] }}
      busy={false}
      {...callbacks}
    />,
  );
  expect(screen.getByText("Descarcă JSON")).toBeDisabled();
  expect(screen.getByRole("status")).toHaveTextContent("în curs");
});
