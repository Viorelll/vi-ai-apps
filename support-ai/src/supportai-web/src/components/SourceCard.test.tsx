import { afterEach, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import SourceCard from "./SourceCard";
afterEach(cleanup);
it("renders provider text safely and opens only server-generated original links", () => {
  render(
    <SourceCard
      source={{
        label: "S1",
        documentId: "d1",
        fileName: "policy.pdf",
        excerpt: "<script>alert(1)</script>",
        contentUrl: "/api/documents/d1/content",
        pageNumber: 2,
        startMs: null,
      }}
    />,
  );
  expect(screen.getByText("<script>alert(1)</script>")).toBeTruthy();
  expect(document.querySelector("script")).toBeNull();
  expect(screen.getByText("Deschide originalul ↗").getAttribute("href")).toBe(
    "/api/documents/d1/content",
  );
});
it("keeps citation snapshots after deletion", () => {
  render(
    <SourceCard
      source={{
        label: "S1",
        documentId: null,
        fileName: "removed.txt",
        excerpt: "Original evidence",
        contentUrl: null,
        pageNumber: null,
        startMs: null,
      }}
    />,
  );
  expect(screen.getByText("Original evidence")).toBeTruthy();
  expect(screen.queryByRole("link")).toBeNull();
});
