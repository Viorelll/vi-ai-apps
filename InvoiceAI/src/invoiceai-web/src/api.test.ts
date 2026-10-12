import { afterEach, describe, it, expect, vi } from "vitest";
import { request, upload, correct } from "./api";
afterEach(() => vi.unstubAllGlobals());
describe("API contracts", () => {
  it("exposes safe problem code and traceId", async () => {
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValue(
          new Response(
            JSON.stringify({ code: "REVISION_CONFLICT", traceId: "trace-123" }),
            { status: 409 },
          ),
        ),
    );
    await expect(request("/api/invoices")).rejects.toMatchObject({
      code: "REVISION_CONFLICT",
      status: 409,
      traceId: "trace-123",
    });
  });
  it("uploads multipart without overriding browser boundary", async () => {
    const fetch = vi
      .fn()
      .mockResolvedValue(
        new Response('{"documentId":"doc","duplicate":false}'),
      );
    vi.stubGlobal("fetch", fetch);
    await upload(
      new File(["%PDF-1.4"], "invoice.pdf", { type: "application/pdf" }),
    );
    const init = fetch.mock.calls[0][1];
    expect(init.body).toBeInstanceOf(FormData);
    expect(init.headers).toBeUndefined();
    expect(init.body.get("file").name).toBe("invoice.pdf");
  });
  it("sends expected revision and typed correction", async () => {
    const fetch = vi.fn().mockResolvedValue(new Response("{}"));
    vi.stubGlobal("fetch", fetch);
    await correct("id", 7, { total: 120 });
    expect(JSON.parse(fetch.mock.calls[0][1].body)).toEqual({
      revision: 7,
      corrections: { total: 120 },
    });
  });
  it("propagates cancellation", async () => {
    const fetch = vi
      .fn()
      .mockRejectedValue(new DOMException("Aborted", "AbortError"));
    vi.stubGlobal("fetch", fetch);
    const c = new AbortController();
    c.abort();
    await expect(
      request("/api/invoices", { signal: c.signal }),
    ).rejects.toHaveProperty("name", "AbortError");
  });
});
