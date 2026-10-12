import { afterEach, describe, expect, it, vi } from "vitest";
import { sendText } from "./api";

afterEach(() => vi.unstubAllGlobals());

describe("voice API boundary", () => {
  it("submits a text turn with a deduplication request id", async () => {
    const fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        text: "Am pregătit o propunere.",
        transcript: "Mâine dimineață",
        proposalId: null,
        audioBase64: null,
        audioContentType: null,
      }),
    });
    vi.stubGlobal("fetch", fetch);
    await expect(
      sendText("session-1", "Mâine dimineață"),
    ).resolves.toMatchObject({ transcript: "Mâine dimineață" });
    const [url, options] = fetch.mock.calls[0] as [string, RequestInit];
    expect(url).toBe("/api/voice/sessions/session-1/turns");
    expect(JSON.parse(String(options.body))).toMatchObject({
      text: "Mâine dimineață",
    });
    expect(JSON.parse(String(options.body)).requestId).toMatch(
      /[0-9a-f-]{36}/i,
    );
  });

  it("surfaces a safe server error code when the AI provider is not configured", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
        status: 503,
        json: async () => ({ code: "ai_not_configured" }),
      }),
    );
    await expect(sendText("session-1", "Salut")).rejects.toThrow(
      "ai_not_configured",
    );
  });
});
