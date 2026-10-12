import { afterEach, describe, expect, it, vi } from "vitest";
import { api, request } from "./api";
afterEach(() => vi.unstubAllGlobals());
describe("SupportAI API contracts", () => {
  it("sends the message request ID for deduplication", async () => {
    const fetcher = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ jobId: "j1" }), {
        status: 202,
        headers: { "Content-Type": "application/json" },
      }),
    );
    vi.stubGlobal("fetch", fetcher);
    await api.send("case-1", "Damaged", "request-1");
    expect(JSON.parse(fetcher.mock.calls[0][1].body)).toEqual({
      text: "Damaged",
      requestId: "request-1",
    });
  });
  it("confirms the exact proposal revision and hash", async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValue(new Response(JSON.stringify({ id: "e1" })));
    vi.stubGlobal("fetch", fetcher);
    await api.confirm(
      {
        id: "p1",
        revision: 3,
        payloadHash: "hash",
        kind: "ticket",
        argumentsJson: "{}",
        state: "Draft",
        expiresAtUtc: "",
      },
      "key-1",
    );
    expect(JSON.parse(fetcher.mock.calls[0][1].body)).toEqual({
      revision: 3,
      payloadHash: "hash",
      idempotencyKey: "key-1",
    });
  });
  it("uploads multipart file bytes", async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValue(new Response(JSON.stringify({ documentId: "d1" })));
    vi.stubGlobal("fetch", fetcher);
    const file = new File(["Evidence"], "note.txt");
    await api.upload(file);
    expect(fetcher.mock.calls[0][1].body.get("file")).toBe(file);
  });
  it("preserves safe server error code and trace", async () => {
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValue(
          new Response(
            JSON.stringify({ code: "proposal_conflict", traceId: "trace-1" }),
            { status: 409 },
          ),
        ),
    );
    await expect(request("/api/test")).rejects.toThrow(
      "proposal_conflict · trace-1",
    );
  });
  it("supports responses without bodies", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response(null, { status: 204 })),
    );
    await expect(api.attach("case", "doc")).resolves.toBeUndefined();
  });
  it("does not hide malformed success responses", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("not JSON")));
    await expect(api.orders()).rejects.toThrow();
  });
});
