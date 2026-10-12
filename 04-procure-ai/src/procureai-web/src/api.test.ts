import { afterEach, describe, expect, it, vi } from "vitest";
import { api } from "./api";
afterEach(() => vi.unstubAllGlobals());
describe("ProcureAI API", () => {
  it("includes optimistic revision when attaching an offer", async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValue(new Response("{}", { status: 200 }));
    vi.stubGlobal("fetch", fetcher);
    await api.attach("run", "document", 3, "offer");
    const [url, init] = fetcher.mock.calls[0];
    expect(url).toBe("/api/procurement/runs/run/documents");
    expect(JSON.parse(init.body)).toEqual({
      documentId: "document",
      revision: 3,
      role: "offer",
    });
  });
  it("starts the selected run with its current revision", async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValue(new Response('{"jobId":"job"}', { status: 202 }));
    vi.stubGlobal("fetch", fetcher);
    await api.start("run", 7);
    expect(fetcher.mock.calls[0][0]).toBe("/api/procurement/runs/run/start");
    expect(JSON.parse(fetcher.mock.calls[0][1].body)).toEqual({ revision: 7 });
  });
  it("scope is explicit for chat", async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValue(new Response("{}", { status: 200 }));
    vi.stubGlobal("fetch", fetcher);
    await api.ask("run", "Lead time?", "conversation");
    expect(JSON.parse(fetcher.mock.calls[0][1].body)).toEqual({
      runId: "run",
      question: "Lead time?",
      conversationId: "conversation",
    });
  });
  it("uploads real multipart files", async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValue(new Response('{"documentId":"d"}', { status: 202 }));
    vi.stubGlobal("fetch", fetcher);
    const file = new File(["{}"], "offer.json", { type: "application/json" });
    await api.upload(file);
    expect(fetcher.mock.calls[0][1].body.get("file")).toBe(file);
  });
  it("translates safe error codes and retains trace id", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response('{"code":"documents_not_ready","traceId":"trace-123"}', {
          status: 409,
        }),
      ),
    );
    await expect(api.start("run", 1)).rejects.toThrow(
      "Așteaptă procesarea tuturor documentelor înainte de pornire. · trace-123",
    );
  });
  it("handles empty accepted deletion responses", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response(null, { status: 202 })),
    );
    await expect(api.remove("d")).resolves.toBeUndefined();
  });
});
