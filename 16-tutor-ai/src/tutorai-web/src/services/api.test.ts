import { describe, it, expect, vi, afterEach } from "vitest";
import { z } from "zod";
import { request } from "./api";
import { sessionSchema } from "../models/contracts";
afterEach(() => vi.unstubAllGlobals());
describe("API boundary", () => {
  it("validates runtime responses", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 42 }) }),
    );
    await expect(
      request("/api/test", z.object({ id: z.string() })),
    ).rejects.toThrow();
  });
  it("retains safe error code and trace", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 409,
        json: async () => ({
          code: "revision_conflict",
          detail: "Reload",
          traceId: "trace-1",
        }),
      }),
    );
    await expect(request("/api/test", z.unknown(), {})).rejects.toThrow(
      "revision_conflict",
    );
  });
  it("rejects unsupported session states", () => {
    expect(sessionSchema.safeParse({ state: "MadeUp" }).success).toBe(false);
  });
  it("sends JSON to the same origin API", async () => {
    const fetch = vi
      .fn()
      .mockResolvedValue({ ok: true, json: async () => ({ ok: true }) });
    vi.stubGlobal("fetch", fetch);
    await request("/api/test", z.object({ ok: z.boolean() }), {
      answer: "Retriever",
    });
    expect(fetch.mock.calls[0][1].body).toBe('{"answer":"Retriever"}');
  });
});
