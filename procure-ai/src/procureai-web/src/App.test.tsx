// @vitest-environment jsdom
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import App from "./App";
import { api } from "./api";
import type { Run } from "./models";
vi.mock("./api", () => ({
  errorHint: (s: string) => s,
  api: {
    health: vi.fn(),
    runs: vi.fn(),
    documents: vi.fn(),
    run: vi.fn(),
    create: vi.fn(),
    attach: vi.fn(),
    upload: vi.fn(),
    start: vi.fn(),
    ask: vi.fn(),
  },
}));
const empty: Run = {
  id: "run-1",
  brief: "10 unități",
  criteria: { currency: "EUR", quantity: 10, unit: "unități", maxDays: 10 },
  state: "Created",
  revision: 0,
  stale: false,
  errorCode: null,
  snapshotHash: "",
  budgetTokens: 15000,
  usedTokens: 0,
  documents: [],
  sources: [],
  steps: [],
  offers: [],
  scores: [],
  recommendation: null,
};
beforeEach(() => {
  localStorage.clear();
  vi.resetAllMocks();
  vi.mocked(api.health).mockResolvedValue({ mode: "Demo" });
  vi.mocked(api.runs).mockResolvedValue([]);
  vi.mocked(api.documents).mockResolvedValue([]);
  vi.mocked(api.create).mockResolvedValue({ id: empty.id, revision: 0 });
  vi.mocked(api.run).mockResolvedValue(empty);
});
afterEach(cleanup);
describe("single-screen procurement workflow", () => {
  it("creates a persisted run from the brief and fixed criteria", async () => {
    render(<App />);
    fireEvent.click(screen.getByRole("button", { name: /Creează analiza/ }));
    await waitFor(() => expect(api.create).toHaveBeenCalledOnce());
    expect(vi.mocked(api.create).mock.calls[0][1]).toEqual(empty.criteria);
    await waitFor(() =>
      expect(localStorage.getItem("procureai-run")).toBe(empty.id),
    );
    expect(
      screen
        .getByRole("button", { name: /Rulează agenții/ })
        .hasAttribute("disabled"),
    ).toBe(true);
  });
  it("uploads and attaches each demo source with the newest revision", async () => {
    vi.mocked(api.upload)
      .mockResolvedValueOnce({ documentId: "a" })
      .mockResolvedValueOnce({ documentId: "b" });
    vi.mocked(api.attach)
      .mockResolvedValueOnce({ ...empty, revision: 1 })
      .mockResolvedValueOnce({ ...empty, revision: 2 });
    render(<App />);
    fireEvent.click(screen.getByRole("button", { name: /Încarcă exemplul/ }));
    await waitFor(() => expect(api.attach).toHaveBeenCalledTimes(2));
    expect(api.attach).toHaveBeenNthCalledWith(1, empty.id, "a", 0, "offer");
    expect(api.attach).toHaveBeenNthCalledWith(2, empty.id, "b", 1, "offer");
  });
  it("does not start while document processing is unfinished", async () => {
    localStorage.setItem("procureai-run", empty.id);
    vi.mocked(api.run).mockResolvedValue({
      ...empty,
      documents: [
        {
          id: "d",
          fileName: "a.pdf",
          contentType: "application/pdf",
          sizeBytes: 2,
          state: "Uploaded",
          errorCode: null,
        },
      ],
    });
    render(<App />);
    await screen.findByText("a.pdf");
    expect(
      screen
        .getByRole("button", { name: /Rulează agenții/ })
        .hasAttribute("disabled"),
    ).toBe(true);
    expect(api.start).not.toHaveBeenCalled();
  });
  it("shows failed requests and does not mark them complete", async () => {
    vi.mocked(api.create).mockRejectedValue(new Error("invalid_requirements"));
    render(<App />);
    fireEvent.click(screen.getByRole("button", { name: /Creează analiza/ }));
    expect((await screen.findByRole("alert")).textContent).toContain(
      "invalid_requirements",
    );
  });
});
