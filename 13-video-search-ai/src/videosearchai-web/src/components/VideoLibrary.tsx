import type { Video } from "../models/schemas";
import { timestamp } from "../services/time";
const states: Record<string, string> = {
  Uploaded: "Încărcat",
  Analyzing: "Analiză în curs",
  Indexing: "Indexare",
  Ready: "Pregătit",
  Failed: "Eșuat",
  Stale: "Depășit",
};
export function VideoLibrary({
  videos,
  selected,
  busy,
  onSelect,
  onIndex,
  onDelete,
  onPlay,
}: {
  videos: Video[];
  selected: string[];
  busy: boolean;
  onSelect: (id: string) => void;
  onIndex: (v: Video) => void;
  onDelete: (v: Video) => void;
  onPlay: (v: Video) => void;
}) {
  return (
    <section className="library-panel">
      <div className="panel-heading">
        <h2>Biblioteca ta</h2>
        <span>{videos.length} clipuri</span>
      </div>
      {videos.length === 0 ? (
        <div className="empty">
          <p>Niciun clip încă</p>
          <small>Încarcă un tutorial pentru a începe.</small>
        </div>
      ) : (
        <ul className="video-list">
          {videos.map((v) => (
            <li key={v.id}>
              <div className="video-top">
                <input
                  type="checkbox"
                  aria-label={`Selectează ${v.title}`}
                  checked={selected.includes(v.id)}
                  disabled={busy || v.state !== "Ready"}
                  onChange={() => onSelect(v.id)}
                />
                <button className="video-title" onClick={() => onPlay(v)}>
                  {v.title}
                </button>
                <span className="duration">{timestamp(v.durationMs)}</span>
              </div>
              <div className="video-bottom">
                <span className={`status ${v.state.toLowerCase()}`}>
                  {states[v.state]}
                </span>
                <div className="row-actions">
                  <button
                    disabled={
                      busy || ["Analyzing", "Indexing"].includes(v.state)
                    }
                    onClick={() => onIndex(v)}
                  >
                    {v.state === "Failed"
                      ? "Reîncearcă"
                      : v.state === "Ready"
                        ? "Reindexează"
                        : "Procesează"}
                  </button>
                  <button
                    className="danger"
                    disabled={busy}
                    onClick={() => onDelete(v)}
                    aria-label={`Șterge ${v.title}`}
                  >
                    Șterge
                  </button>
                </div>
              </div>
              {v.errorCode && <p className="item-error">{v.errorCode}</p>}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
