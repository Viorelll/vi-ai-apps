import { describe, it, expect } from "vitest";
import { resultSchema, healthSchema } from "./models";
describe("API contracts", () => {
  it("rejects malformed results instead of rendering unknown data", () => {
    expect(
      resultSchema.safeParse({ id: "invalid", state: "Allowed" }).success,
    ).toBe(false);
  });
  it("accepts only explicit operating modes", () => {
    expect(
      healthSchema.safeParse({
        status: "ok",
        mode: "unknown",
        policyVersion: "v1",
      }).success,
    ).toBe(false);
    expect(
      healthSchema.safeParse({
        status: "ok",
        mode: "Demo",
        policyVersion: "v1",
      }).success,
    ).toBe(true);
  });
});
