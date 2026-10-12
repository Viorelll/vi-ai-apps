import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError, post, request, poll } from "./api";
afterEach(() => vi.unstubAllGlobals());
describe("API boundary", () => {
  it("preserves server error code and trace", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 503,
        json: async () => ({
          code: "foundry_not_configured",
          traceId: "trace-1",
          detail: "Configure Foundry",
        }),
      }),
    );
    await expect(request("/api/test")).rejects.toMatchObject({
      code: "foundry_not_configured",
      traceId: "trace-1",
    });
  });
  it("posts JSON without exposing infrastructure credentials", async () => {
    const fetch = vi
      .fn()
      .mockResolvedValue({ ok: true, json: async () => ({ jobId: "1" }) });
    vi.stubGlobal("fetch", fetch);
    await expect(post("/api/analyze", { revision: 2 })).resolves.toEqual({
      jobId: "1",
    });
    expect(fetch).toHaveBeenCalledWith(
      "/api/analyze",
      expect.objectContaining({
        body: '{"revision":2}',
        headers: { "Content-Type": "application/json" },
      }),
    );
  });
  it("stops polling a failed job", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({ status: "Failed" }),
      }),
    );
    await expect(
      poll<{ status: string }>("/api/jobs/1", (j) => j.status),
    ).rejects.toBeInstanceOf(ApiError);
  });
  it("accepts NeedsReview as a completed analysis", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({ status: "NeedsReview" }),
      }),
    );
    await expect(
      poll<{ status: string }>("/api/jobs/1", (j) => j.status),
    ).resolves.toEqual({ status: "NeedsReview" });
  });
});
