import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "./App";

const mockDatasets = [
  {
    id: "dataset-1",
    name: "Sample sales · EUR",
    rowCount: 3,
    revision: 1,
    currency: "EUR",
  },
];
let currentDatasets: typeof mockDatasets = [];
const mockAnalysis = {
  runId: "run-1",
  metric: "revenue",
  groupBy: "category",
  currency: "EUR",
  rows: [
    { label: "Hardware", value: 25 },
    { label: "Software", value: 21 },
  ],
  total: 46,
  resultHash: "1234567890abcdef",
  datasetRevision: 1,
  explanation: "Revenue totals 46 EUR.",
  usedFoundry: false,
};

describe("DataAI workspace", () => {
  afterEach(cleanup);
  beforeEach(() => {
    vi.restoreAllMocks();
    currentDatasets = [...mockDatasets];
    vi.spyOn(globalThis, "fetch").mockImplementation(async (input, init) => {
      const url = String(input);
      let value: unknown = {};
      if (url === "/api/datasets") value = currentDatasets;
      if (url === "/api/analyze") value = mockAnalysis;
      if (url === "/api/datasets/import" && init?.method === "POST") {
        const imported = {
          id: "dataset-2",
          name: "regional-sales",
          rowCount: 1,
          revision: 1,
          currency: "EUR",
        };
        currentDatasets = [...currentDatasets, imported];
        value = {
          id: imported.id,
          name: imported.name,
          rowsImported: 1,
          revision: 1,
        };
      }
      return { ok: true, json: async () => value } as Response;
    });
  });

  it("shows totals and matching table values from the same analysis result", async () => {
    render(<App />);
    expect((await screen.findAllByText(/46,00/)).length).toBeGreaterThan(0);
    expect(screen.getAllByText("Hardware").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Software").length).toBeGreaterThan(0);
    expect(screen.getAllByText(/25,00/).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/21,00/).length).toBeGreaterThan(0);
  });

  it("sends the selected question to the analysis API", async () => {
    render(<App />);
    await screen.findAllByText("Hardware");
    fireEvent.change(screen.getByRole("textbox", { name: "Întrebarea ta" }), {
      target: { value: "Cantități vândute pe zi" },
    });
    fireEvent.click(screen.getByRole("button", { name: /Analizează/ }));
    await waitFor(() =>
      expect(globalThis.fetch).toHaveBeenCalledWith(
        "/api/analyze",
        expect.objectContaining({
          method: "POST",
          body: JSON.stringify({
            datasetId: "dataset-1",
            question: "Cantități vândute pe zi",
          }),
        }),
      ),
    );
  });

  it("selects the dataset immediately after CSV import", async () => {
    const { container } = render(<App />);
    await screen.findAllByText("Hardware");
    const fileInput = container.querySelector('input[type="file"]');
    expect(fileInput).not.toBeNull();
    fireEvent.change(fileInput!, {
      target: {
        files: [new File(["csv"], "regional-sales.csv", { type: "text/csv" })],
      },
    });
    await waitFor(() =>
      expect((screen.getByRole("combobox") as HTMLSelectElement).value).toBe(
        "dataset-2",
      ),
    );
  });
});
