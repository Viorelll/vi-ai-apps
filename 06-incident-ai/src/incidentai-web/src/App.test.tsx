import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import App from "./App";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("IncidentAI panel", () => {
  it("loads demo incident, diagnoses it and renders cited timeline sources", async () => {
    const id = "incident-1";
    const diagnosis = {
      state: "completed",
      summary: "Three timeout errors share trace T1.",
      hypotheses: [
        {
          claim: "Dependency timeouts",
          confidenceLabel: "hypothesis",
          evidenceIds: ["E-001"],
          contradictions: [],
        },
      ],
      nextChecks: ["Check dependency latency."],
      provider: "offline",
      inputTokens: 0,
      outputTokens: 0,
      durationMs: 1,
    };
    const incident = {
      incident: {
        id,
        service: "checkout-api",
        fromUtc: "2026-10-11T10:00:00Z",
        toUtc: "2026-10-11T11:00:00Z",
        events: [
          {
            sourceId: "E-001",
            timestampUtc: "2026-10-11T10:02:00Z",
            severity: "Error",
            traceId: "T1",
            message: "dependency timeout",
          },
        ],
        diagnoses: [diagnosis],
      },
      releases: [{ label: "R2", deployedAtUtc: "2026-10-11T10:00:00Z" }],
      logSources: [],
    };
    const fetchMock = vi.fn().mockImplementation(async (url: string) => {
      if (String(url) === "/api/incidents/demo")
        return Response.json({ id }, { status: 201 });
      if (String(url) === `/api/incidents/${id}/diagnose`)
        return Response.json(diagnosis);
      if (String(url) === `/api/incidents/${id}`)
        return Response.json(incident);
      throw new Error(`Unexpected URL: ${url}`);
    });
    vi.stubGlobal("fetch", fetchMock);
    render(<App />);
    fireEvent.click(
      screen.getByRole("button", { name: /scenariul demonstrativ/i }),
    );
    await screen.findByText(/Three timeout errors share trace T1/);
    fireEvent.click(
      screen.getByRole("button", { name: /analizează incidentul/i }),
    );
    await screen.findByText("Dependency timeouts");
    expect(screen.getAllByText(/E-001/).length).toBeGreaterThan(0);
    expect(screen.getByText("Check dependency latency.")).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith(
      `/api/incidents/${id}/diagnose`,
      expect.objectContaining({ method: "POST" }),
    );
  });
});
