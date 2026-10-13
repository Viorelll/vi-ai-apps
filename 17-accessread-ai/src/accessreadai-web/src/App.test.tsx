import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { vi, describe, it, expect } from "vitest";
import App from "./App";
import { readingSchema } from "./models/reading";
vi.mock("./api", () => ({
  api: {
    list: vi.fn().mockResolvedValue([]),
    upload: vi.fn().mockRejectedValue(new Error("unsupported_file")),
  },
}));
describe("Accessible reader", () => {
  it("labels all input and keeps generation disabled without a ready document", () => {
    render(<App />);
    expect(screen.getByLabelText("Limba originalului")).toBeInTheDocument();
    expect(screen.getByLabelText("Limba traducerii")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: /Generează lectura/ }),
    ).toBeDisabled();
    expect(screen.getByLabelText(/Încarcă un document/)).toBeInTheDocument();
  });
  it("shows safe upload errors", async () => {
    render(<App />);
    fireEvent.change(screen.getByLabelText(/Încarcă un document/), {
      target: { files: [new File(["bad"], "bad.pdf")] },
    });
    await waitFor(() =>
      expect(screen.getByRole("alert")).toHaveTextContent("unsupported_file"),
    );
  });
  it("rejects malformed or external source URLs", () => {
    expect(() => readingSchema.parse({ id: "bad", blocks: [] })).toThrow();
  });
});
