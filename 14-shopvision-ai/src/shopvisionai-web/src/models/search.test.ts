import { describe, it, expect } from "vitest";
import { candidateSchema, searchSchema } from "./search";
describe("API contracts", () => {
  it("rejects numeric money and remote source links", () => {
    expect(
      candidateSchema.safeParse({ price: 12, sheetUrl: "https://example.com" })
        .success,
    ).toBe(false);
  });
  it("rejects unsupported state", () => {
    expect(searchSchema.safeParse({ state: "MadeUp" }).success).toBe(false);
  });
  it("accepts a grounded candidate", () => {
    expect(
      candidateSchema.parse({
        sku: "USB-BLK-01",
        name: "USB",
        category: "adapter",
        retrievalScore: 0.8,
        reason: "candidate",
        price: "149.90",
        currency: "MDL",
        quantity: 8,
        stockLabel: "stale",
        capturedAtUtc: "2026-10-11T12:00:00Z",
        sourceIds: ["catalog:USB-BLK-01"],
        sheetUrl: "/api/visual/catalog/USB-BLK-01",
      }).price,
    ).toBe("149.90");
  });
});
