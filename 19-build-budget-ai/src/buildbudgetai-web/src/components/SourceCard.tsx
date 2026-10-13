import type { Evidence } from "../models/schemas";
export function SourceCard({ source }: { source: Evidence }) {
  return (
    <details className="source">
      <summary>
        Sursă {source.pageNumber ? `· pagina ${source.pageNumber}` : ""}
      </summary>
      <p>{source.text}</p>
      <a
        href={`/api/documents/${source.documentId}/content`}
        target="_blank"
        rel="noreferrer"
      >
        Deschide originalul ↗
      </a>
    </details>
  );
}
