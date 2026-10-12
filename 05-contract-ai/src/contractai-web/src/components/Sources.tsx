import type { Citation } from "../models/Citation";
export default function Sources({ sources }: { sources: Citation[] }) {
  return (
    <div className="sources">
      {sources.map((s, i) => (
        <details key={`${s.clauseId}-${i}`}>
          <summary>
            Sursa {i + 1} ·{" "}
            {s.pageNumber ? `pagina ${s.pageNumber}` : "pagină necunoscută"}
          </summary>
          <blockquote>{s.excerpt}</blockquote>
          <a href={s.url} target="_blank" rel="noreferrer">
            Deschide originalul ↗
          </a>
        </details>
      ))}
    </div>
  );
}
