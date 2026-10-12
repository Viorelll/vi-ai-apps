import type { Citation } from "../models/types";
export default function SourceCard({ source }: { source: Citation }) {
  return (
    <details className="source">
      <summary>
        <span>{source.label}</span>
        {source.fileName}
        {source.pageNumber !== null && ` · p. ${source.pageNumber}`}
        {source.startMs !== null && ` · ${Math.round(source.startMs / 1000)}s`}
      </summary>
      <p>{source.excerpt}</p>
      {source.contentUrl ? (
        <a href={source.contentUrl} target="_blank" rel="noreferrer">
          Deschide originalul ↗
        </a>
      ) : (
        <small>Sursa a fost ștearsă; citarea păstrează textul original.</small>
      )}
    </details>
  );
}
