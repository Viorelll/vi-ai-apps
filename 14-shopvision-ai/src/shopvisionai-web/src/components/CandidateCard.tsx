import { useState } from "react";
import { z } from "zod";
import type { Candidate } from "../models/search";
import { request } from "../services/api";
export function CandidateCard({ candidate: c }: { candidate: Candidate }) {
  const [sheet, setSheet] = useState("");
  const [error, setError] = useState("");
  async function openSheet() {
    try {
      const data = z
        .object({ sheet: z.string() })
        .parse(await request(c.sheetUrl));
      setSheet(data.sheet);
      setError("");
    } catch (e) {
      setError(String(e));
    }
  }
  const stock =
    c.stockLabel === "stale"
      ? "Snapshot expirat"
      : c.quantity > 0
        ? "În stoc la consultare"
        : "Fără stoc la consultare";
  return (
    <article className="candidate">
      <div className="card-top">
        <span className="sku">{c.sku}</span>
        <span className="tag">Candidat</span>
      </div>
      <h3>{c.name}</h3>
      <p>{c.reason}</p>
      <div className="price">
        {c.price} <small>{c.currency}</small>
      </div>
      <div className="stock">
        <span className={c.quantity > 0 ? "dot" : "dot empty"} />
        {stock} · {c.quantity} buc.
      </div>
      <p className="timestamp">
        Consultat: {new Date(c.capturedAtUtc).toLocaleString("ro-RO")}
      </p>
      <p className="score">
        Scor retrieval: {c.retrievalScore.toFixed(2)} · nu reprezintă
        probabilitatea identității
      </p>
      <button className="text-button" onClick={openSheet}>
        Vezi fișa și sursele ↗
      </button>
      {sheet && (
        <div className="sheet">
          {sheet}
          <p>{c.sourceIds.join(" · ")}</p>
        </div>
      )}
      {error && <p role="alert">{error}</p>}
    </article>
  );
}
