import type { Answer, Citation } from "../models/schemas";
import { timestamp } from "../services/time";
export function SearchResults({
  answer,
  onCitation,
}: {
  answer: Answer | undefined;
  onCitation: (c: Citation) => void;
}) {
  if (!answer)
    return (
      <section className="results empty">
        <p>Găsește răspunsul, cu dovezi</p>
        <small>
          Rezultatele includ transcriptul și intervalul din clipul original.
        </small>
      </section>
    );
  return (
    <section className="results" aria-live="polite">
      <div className="panel-heading">
        <h2>Răspuns</h2>
        <span>
          {answer.deployment === "demo-fixture" ? "Demo artificial" : "Foundry"}
        </span>
      </div>
      <p className="answer-text">{answer.answer}</p>
      {answer.citations.length > 0 && <h3>Momente relevante</h3>}
      {answer.citations.map((c) => (
        <article className="citation" key={c.label}>
          <div className="citation-heading">
            <span className="citation-label">{c.label}</span>
            <button onClick={() => onCitation(c)}>
              ▶ {timestamp(c.startMs)} – {timestamp(c.endMs)}
            </button>
            <small>
              {c.evidenceKind === "visual" ? "Dovadă vizuală" : "Transcript"}
            </small>
          </div>
          <p>{c.excerpt}</p>
        </article>
      ))}
    </section>
  );
}
