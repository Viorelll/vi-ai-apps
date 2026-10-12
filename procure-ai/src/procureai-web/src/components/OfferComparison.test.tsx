// @vitest-environment jsdom
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { OfferComparison } from "./OfferComparison";
import type { Run } from "../models";
afterEach(cleanup);
const run = {
  stale: false,
  offers: [
    {
      id: "a",
      supplierLabel: "Oferta A",
      currency: "EUR",
      quantity: 10,
      unit: "unități",
      net: 1000,
      shipping: 50,
      leadDays: 7,
    },
  ],
  scores: [{ id: "a", cost: 1050, score: 0.79 }],
  recommendation: { recommendedOfferId: "a" },
} as Run;
describe("offer matrix", () => {
  it("shows server cost and exact score", () => {
    render(<OfferComparison run={run} />);
    expect(screen.getByText("1050")).toBeTruthy();
    expect(screen.getByText("0.7900")).toBeTruthy();
    expect(screen.getByText("Oferta A").closest("tr")?.className).toBe(
      "winner",
    );
  });
  it("stale recommendations do not highlight a current winner", () => {
    render(<OfferComparison run={{ ...run, stale: true }} />);
    expect(screen.getByText("Oferta A").closest("tr")?.className).toBe("");
  });
});
