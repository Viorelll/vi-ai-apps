import type { Finding, Source, Region } from "../models/Inspection";
const names = {
  visible: "Observat",
  inferred: "Ipoteză",
  unknown: "Neobservabil",
};
export default function Findings({
  findings,
  sources,
}: {
  findings: Finding[];
  sources: Source[];
}) {
  return (
    <div className="findings">
      {findings.map((f) => {
        const source = sources.find((s) => s.documentId === f.documentId);
        let region: Region | null = null;
        try {
          region = f.regionJson ? JSON.parse(f.regionJson) : null;
        } catch {
          /* no overlay for invalid geometry */
        }
        return (
          <article className="finding" key={f.id}>
            {source && (
              <div className="photo-frame">
                <img src={source.contentUrl} alt={source.fileName} />
                {region?.coordinateSpace === "normalized" && (
                  <span
                    className="region"
                    style={{
                      left: `${region.x * 100}%`,
                      top: `${region.y * 100}%`,
                      width: `${region.width * 100}%`,
                      height: `${region.height * 100}%`,
                    }}
                  />
                )}
              </div>
            )}
            <div>
              <span className={`badge ${f.kind}`}>{names[f.kind]}</span>
              <p>{f.description}</p>
              {f.reason && <small>{f.reason}</small>}
              {source && (
                <a href={source.contentUrl} target="_blank" rel="noreferrer">
                  Fotografia sursă ↗
                </a>
              )}
            </div>
          </article>
        );
      })}
    </div>
  );
}
