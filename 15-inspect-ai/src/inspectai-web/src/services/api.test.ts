import { describe, it, expect, vi, afterEach } from "vitest";
import { parseInspection, request } from "./api";
afterEach(() => vi.unstubAllGlobals());
describe("API contract", () => {
  it("rejects malformed data", () => {
    expect(() =>
      parseInspection({
        id: "x",
        revision: 1,
        state: "Ready",
        sources: [],
        findings: [{ kind: "certified" }],
        checklist: [],
        questions: [],
      }),
    ).toThrow();
  });
  it("preserves provider error and trace ID", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 503,
        json: async () => ({
          detail: "Configure Foundry",
          code: "foundry_not_configured",
          traceId: "trace-1",
        }),
      }),
    );
    await expect(request("/api/inspections")).rejects.toThrow("trace-1");
  });
});
