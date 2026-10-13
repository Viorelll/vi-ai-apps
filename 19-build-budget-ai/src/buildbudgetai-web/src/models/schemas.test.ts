import { describe, it, expect } from "vitest";
import { budgetSchema } from "./schemas";
describe("financial contract", () => {
  it("rejects floating point money and malformed payloads", () => {
    expect(budgetSchema.safeParse({ totalDecimal: 570 }).success).toBe(false);
  });
  it("accepts decimal strings and preserves incomplete status", () => {
    expect(
      budgetSchema.parse({
        id: "00000000-0000-0000-0000-000000000001",
        revision: 1,
        totalDecimal: "570.00",
        currency: "EUR",
        incomplete: true,
        stale: false,
        lines: [],
      }).incomplete,
    ).toBe(true);
  });
});
