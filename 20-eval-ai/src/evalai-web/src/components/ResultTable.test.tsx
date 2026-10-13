import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import ResultTable from "./ResultTable";
import { reportSchema } from "../models/schemas";
const report = reportSchema.parse({
  id: "11111111-1111-4111-8111-111111111111",
  datasetId: "22222222-2222-4222-8222-222222222222",
  state: "Completed",
  revision: 2,
  errorCode: null,
  stale: false,
  budgetTokens: 1000,
  usedTokens: 72,
  possibleRepeatedCloudCost: false,
  baselineCompatible: false,
  costNote: "Text token cost only.",
  progress: { completed: 1, total: 1 },
  summaries: [
    {
      variantId: "a",
      variant: "A",
      deployment: "test-a",
      total: 1,
      passed: 1,
      failed: 0,
      deterministicFailed: 0,
      completed: 1,
      judgeMean: 5,
      p50Ms: 20,
      p95Ms: 20,
      regression: null,
      checksDelta: null,
    },
  ],
  results: [
    {
      id: "r1",
      variant: "A",
      caseName: "golden case",
      state: "Completed",
      errorCode: null,
      output: { answer: 42 },
      checks: {
        passed: true,
        jsonFieldsPassed: true,
        citationsPassed: true,
        errors: [],
      },
      judge: {
        score: 5,
        reason: "Correct and grounded.",
        rubricVersion: "judge-v1",
      },
      candidateUsage: {
        input: 20,
        output: 12,
        deployment: "test-a",
        simulated: false,
      },
      judgeUsage: {
        input: 30,
        output: 10,
        deployment: "judge",
        simulated: false,
      },
      candidateLatencyMs: 20,
      candidateCost: {
        amount: null,
        currency: null,
        rateVersion: null,
        knownPartial: true,
      },
      judgeCost: {
        amount: null,
        currency: null,
        rateVersion: null,
        knownPartial: true,
      },
    },
  ],
});
describe("Evaluation result table", () => {
  it("renders deterministic checks and judge as separate scores", () => {
    render(<ResultTable report={report} />);
    expect(screen.getByText("PASS")).toBeInTheDocument();
    expect(screen.getByText("5/5")).toBeInTheDocument();
    expect(screen.getByText("Correct and grounded.")).toBeInTheDocument();
  });
  it("shows missing rates as unknown and preserves actual token usage", () => {
    render(<ResultTable report={report} />);
    expect(screen.getAllByText("Necunoscut")).toHaveLength(2);
    expect(screen.getAllByText("20 in / 12 out")).toHaveLength(2);
    expect(screen.getAllByText("Judge: 30 in / 10 out")).toHaveLength(2);
  });
  it("validates the API response shape at the frontend boundary", () => {
    expect(() => reportSchema.parse({ id: "wrong" })).toThrow();
  });
});
