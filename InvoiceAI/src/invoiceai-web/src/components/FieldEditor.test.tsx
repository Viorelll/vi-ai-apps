import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import FieldEditor, { parseCorrection } from "./FieldEditor";
describe("field corrections", () => {
  it.each([
    ["120.00", 120],
    ["0", 0],
    ["-1.25", -1.25],
    ["", null],
  ])("parses %s explicitly", (text, value) =>
    expect(parseCorrection("total", String(text))).toBe(value),
  );
  it.each(["1,234", "1O0,00", "NaN", "1e2"])(
    "rejects ambiguous numeric text %s",
    (text) => expect(() => parseCorrection("total", text)).toThrow(),
  );
  it("preserves strings such as invoice numbers", () =>
    expect(parseCorrection("invoiceNumber", "00123")).toBe("00123"));
  it("saves numeric correction through form", async () => {
    const save = vi.fn().mockResolvedValue(undefined);
    render(
      <FieldEditor
        path="total"
        label="Total"
        value={119}
        disabled={false}
        corrected={false}
        onSave={save}
      />,
    );
    fireEvent.click(screen.getByText("Corectează"));
    fireEvent.change(screen.getByLabelText("Total"), {
      target: { value: "120.00" },
    });
    fireEvent.click(screen.getByText("Salvează"));
    await waitFor(() => expect(save).toHaveBeenCalledWith("total", 120));
  });
  it("does not render untrusted markup", () => {
    render(
      <FieldEditor
        path="vendorName"
        label="Furnizor"
        value="<script>alert(1)</script>"
        disabled={false}
        corrected={false}
        onSave={vi.fn()}
      />,
    );
    expect(screen.getByText("<script>alert(1)</script>")).toBeInTheDocument();
    expect(document.querySelector("script")).toBeNull();
  });
  it("reports concurrent correction error", async () => {
    render(
      <FieldEditor
        path="total"
        label="Total"
        value={119}
        disabled={false}
        corrected={false}
        onSave={vi.fn().mockRejectedValue(new Error("REVISION_CONFLICT"))}
      />,
    );
    fireEvent.click(screen.getByText("Corectează"));
    fireEvent.click(screen.getByText("Salvează"));
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "REVISION_CONFLICT",
    );
  });
});
