import type {
  Checklist as Item,
  Evidence,
  Finding,
  ManualSource,
  Source,
} from "../models/Inspection";
export default function Checklist({
  items,
  manualSources,
  findings,
  sources,
  onNote,
  busy,
}: {
  items: Item[];
  manualSources: ManualSource[];
  findings: Finding[];
  sources: Source[];
  onNote: (item: Item) => void;
  busy: boolean;
}) {
  return (
    <div>
      {items.map((c, index) => {
        const finding = findings.find((f) => f.id === c.findingId);
        const photo = sources.find((s) => s.documentId === finding?.documentId);
        const evidence: Evidence[] = JSON.parse(c.evidenceJson);
        return (
          <article className="check" key={c.id}>
            <button
              className={`check-toggle ${c.status === "noted" ? "checked" : ""}`}
              disabled={busy}
              onClick={() => onNote(c)}
              aria-label={
                c.status === "noted"
                  ? "Revino la propunere"
                  : "Notează local verificarea"
              }
            >
              {c.status === "noted" ? "✓" : index + 1}
            </button>
            <div>
              <strong>{c.text}</strong>
              {photo && (
                <a href={photo.contentUrl} target="_blank" rel="noreferrer">
                  Observația din fotografie ↗
                </a>
              )}
              {evidence.map((e, n) => {
                const s = manualSources.find((x) => x.chunkId === e.chunkId);
                return (
                  <blockquote key={n}>
                    „{e.quote}”
                    {s && (
                      <a
                        href={`/api/documents/${s.documentId}/content${s.pageNumber ? "#page=" + s.pageNumber : ""}`}
                        target="_blank"
                        rel="noreferrer"
                      >
                        Manual ·{" "}
                        {s.pageNumber
                          ? "pagina " + s.pageNumber
                          : "pagină necunoscută"}{" "}
                        ↗
                      </a>
                    )}
                  </blockquote>
                );
              })}
              <small>
                {c.status === "noted"
                  ? "Notat local · nicio intervenție executată"
                  : "Propunere pentru revizuire"}
              </small>
            </div>
          </article>
        );
      })}
    </div>
  );
}
