import { afterEach, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import ProposalCard from "./ProposalCard";
import type { Proposal } from "../models/types";
afterEach(cleanup);
const proposal: Proposal = {
  id: "p1",
  kind: "ticket",
  argumentsJson: JSON.stringify({
    summary: "Damaged monitor",
    evidenceLabels: ["S1"],
  }),
  payloadHash: "hash",
  state: "Draft",
  revision: 1,
  expiresAtUtc: new Date(Date.now() + 60000).toISOString(),
};
it("shows the exact arguments and uses a stable key for retries", () => {
  const confirm = vi.fn();
  render(
    <ProposalCard
      proposal={proposal}
      busy={false}
      onConfirm={confirm}
      onReject={vi.fn()}
    />,
  );
  expect(screen.getByText("Damaged monitor")).toBeTruthy();
  const button = screen.getByText("Confirmă tichetul local");
  fireEvent.click(button);
  fireEvent.click(button);
  expect(confirm).toHaveBeenCalledTimes(2);
  expect(confirm.mock.calls[0][1]).toBe(confirm.mock.calls[1][1]);
});
it("blocks expired confirmations", () => {
  const confirm = vi.fn();
  render(
    <ProposalCard
      proposal={{ ...proposal, expiresAtUtc: new Date(0).toISOString() }}
      busy={false}
      onConfirm={confirm}
      onReject={vi.fn()}
    />,
  );
  fireEvent.click(screen.getByText("Confirmă tichetul local"));
  expect(confirm).not.toHaveBeenCalled();
});
it("shows execution result rather than a confirm button", () => {
  render(
    <ProposalCard
      proposal={{ ...proposal, state: "Executed" }}
      execution={{
        id: "execution1",
        proposalId: "p1",
        resultJson: '{"scope":"local-demo"}',
      }}
      busy={false}
      onConfirm={vi.fn()}
      onReject={vi.fn()}
    />,
  );
  expect(screen.queryByText("Confirmă tichetul local")).toBeNull();
  expect(screen.getByRole("status")).toBeTruthy();
});
