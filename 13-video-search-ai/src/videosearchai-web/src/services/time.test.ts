import { describe, it, expect } from "vitest";
import { seek, timestamp } from "./time";
import { citationSchema } from "../models/schemas";
describe("original video timestamps", () => {
  it("seeks at 70 seconds", () => {
    const player = { currentTime: 0 } as HTMLVideoElement;
    seek(player, 70000);
    expect(player.currentTime).toBe(70);
    expect(timestamp(95000)).toBe("01:35");
  });
  it("ignores invalid times", () => {
    const player = { currentTime: 4 } as HTMLVideoElement;
    seek(player, -1);
    seek(player, NaN);
    expect(player.currentTime).toBe(4);
  });
  it("rejects external citation URLs", () => {
    expect(
      citationSchema.safeParse({
        label: "S1",
        videoId: crypto.randomUUID(),
        segmentId: crypto.randomUUID(),
        documentId: crypto.randomUUID(),
        generation: 1,
        startMs: 70000,
        endMs: 95000,
        excerpt: "Index",
        contentUrl: "https://evil.example",
        evidenceKind: "transcript",
      }).success,
    ).toBe(false);
  });
});
