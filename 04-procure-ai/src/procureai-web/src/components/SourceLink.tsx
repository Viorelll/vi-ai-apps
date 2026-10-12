import { ExternalLink } from "lucide-react";
import type { Source } from "../models";
export function SourceLink({ source }: { source: Source }) {
  return (
    <a
      className="source"
      target="_blank"
      rel="noreferrer"
      href={`/api/documents/${source.documentId}/content${source.pageNumber ? `#page=${source.pageNumber}` : ""}`}
    >
      <span>
        {source.fileName}
        {source.pageNumber ? ` · p. ${source.pageNumber}` : ""}
      </span>
      <ExternalLink size={14} />
    </a>
  );
}
