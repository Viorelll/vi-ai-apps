import { describe, it, expect } from "vitest";
import { canvasPoint, wrapText } from "./canvas";
import { projectSchema } from "../models/schemas";
describe("canvas coordinates", () => {
  it("maps CSS-scaled drawing to original pixels", () => {
    expect(
      canvasPoint(
        170,
        210,
        { left: 10, top: 10, width: 320, height: 400 },
        1024,
        1536,
      ),
    ).toEqual({ x: 512, y: 768 });
  });
  it("clamps pointers outside canvas", () => {
    expect(
      canvasPoint(
        -20,
        500,
        { left: 0, top: 0, width: 100, height: 100 },
        1024,
        1024,
      ),
    ).toEqual({ x: 0, y: 1024 });
  });
});
describe("deterministic banner", () => {
  const ctx = { measureText: (s: string) => ({ width: s.length * 10 }) };
  it("retains exact Unicode copy and line breaks", () => {
    const text = "Ofertă de laborator\nPreț 100 €";
    expect(wrapText(ctx, text, 500).join("\n")).toBe(text);
  });
  it("wraps words without hallucinating text", () => {
    expect(wrapText(ctx, "unu doi trei", 70)).toEqual(["unu doi", "trei"]);
  });
});
it("rejects malformed API results before rendering", () => {
  expect(() =>
    projectSchema.parse({ id: "bad", assets: [{ width: -1 }] }),
  ).toThrow();
});
