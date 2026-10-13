import {
  render,
  screen,
  fireEvent,
  waitFor,
  cleanup,
} from "@testing-library/react";
import { afterEach, describe, it, expect, vi } from "vitest";
import App from "./App";
vi.mock("./api", () => ({
  getHealth: vi.fn(async () => ({
    status: "ok",
    mode: "Demo",
    policyVersion: "v1",
  })),
  getHistory: vi.fn(async () => ({ items: [] })),
  getQueue: vi.fn(async () => ({ items: [], total: 0, page: 1, pageSize: 20 })),
}));
afterEach(cleanup);
describe("Moderation screen", () => {
  it("labels demo and requires input before analysis", async () => {
    render(<App />);
    await screen.findByText("DEMO · fără AI real");
    expect(
      screen.getByRole("button", { name: "Analizează conținutul →" }),
    ).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: "2 · Review" }));
    expect(screen.getByLabelText("Text")).toHaveValue("[demo:review]");
    await waitFor(() =>
      expect(
        screen.getByRole("button", { name: "Analizează conținutul →" }),
      ).toBeEnabled(),
    );
  });
});
