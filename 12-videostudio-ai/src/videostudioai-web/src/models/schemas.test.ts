import { describe, it, expect } from "vitest";
import { isActive, projectSchema, clipSchema } from "./schemas";
describe("API runtime contracts", () => {
  it("rejects malformed provider data before rendering", () => {
    expect(() => projectSchema.parse({ id: "invalid", clips: [] })).toThrow();
  });
  it("rejects content URLs outside the API", () => {
    expect(() =>
      clipSchema.parse({
        id: crypto.randomUUID(),
        jobId: crypto.randomUUID(),
        contentUrl: "https://evil.example/video",
        durationMs: 8000,
        width: 1280,
        height: 720,
        sha256: "hash",
        prompt: "test",
        deployment: "model",
        sourceClipId: null,
      }),
    ).toThrow();
  });
  it("recognizes terminal and active jobs", () => {
    for (const s of ["Ready", "Failed", "Uncertain", "NeedsReview", "Canceled"])
      expect(isActive(s)).toBe(false);
    for (const s of ["Queued", "InProgress", "Downloading"])
      expect(isActive(s)).toBe(true);
  });
});
