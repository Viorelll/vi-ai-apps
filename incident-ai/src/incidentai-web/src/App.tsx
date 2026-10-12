import { useState } from "react";
import {
  createDemo,
  createIncident,
  diagnose,
  loadIncident,
  uploadLogs,
  type Diagnosis,
  type Incident,
} from "./api";
import "./App.css";

export default function App() {
  const [incident, setIncident] = useState<Incident>();
  const [result, setResult] = useState<Diagnosis>();
  const [service, setService] = useState("checkout-api");
  const [from, setFrom] = useState("2026-10-11T10:00");
  const [to, setTo] = useState("2026-10-11T11:00");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function run(action: () => Promise<void>) {
    setBusy(true);
    setError("");
    try {
      await action();
    } catch (e) {
      setError(e instanceof Error ? e.message : "A apărut o eroare.");
    } finally {
      setBusy(false);
    }
  }
  async function open(id: string) {
    const data = await loadIncident(id);
    setIncident(data);
    setResult(data.incident.diagnoses[0]);
  }
  async function startDemo() {
    const { id } = await createDemo();
    await open(id);
  }
  async function startIncident() {
    const { id } = await createIncident(
      service,
      new Date(from).toISOString(),
      new Date(to).toISOString(),
    );
    await open(id);
  }
  async function analyze() {
    if (!incident) return;
    const diagnosis = await diagnose(incident.incident.id);
    setResult(diagnosis);
    setIncident(await loadIncident(incident.incident.id));
  }
  const events = incident?.incident.events ?? [];
  const evidence = new Set(
    result?.hypotheses.flatMap((h) => h.evidenceIds) ?? [],
  );
  return (
    <main className="shell">
      <header className="topbar">
        <a className="brand" href="#">
          <span className="brand-mark">I</span>
          <span>
            incident<span className="brand-ai">ai</span>
          </span>
        </a>
        <span className="local-pill">
          <i /> Spațiu local · Date private
        </span>
      </header>
      <section className="hero">
        <div className="eyebrow">INCIDENT RESPONSE COPILOT</div>
        <h1>Înțelege ce s-a întâmplat.</h1>
        <p>
          Leagă logurile și schimbările de o cronologie. Primește ipoteze
          verificabile, cu sursele lor.
        </p>
      </section>
      <section className="workspace">
        <aside className="controls panel">
          <div className="section-title">
            <span className="step">01</span>
            <div>
              <h2>Configurează incidentul</h2>
              <p>Definește serviciul și fereastra de timp.</p>
            </div>
          </div>
          <label>
            Serviciu
            <input
              value={service}
              onChange={(e) => setService(e.target.value)}
              placeholder="ex. checkout-api"
            />
          </label>
          <div className="time-grid">
            <label>
              De la
              <input
                type="datetime-local"
                value={from}
                onChange={(e) => setFrom(e.target.value)}
              />
            </label>
            <label>
              Până la
              <input
                type="datetime-local"
                value={to}
                onChange={(e) => setTo(e.target.value)}
              />
            </label>
          </div>
          <button
            className="button secondary full"
            disabled={busy}
            onClick={() => void run(startIncident)}
          >
            Creează incident
          </button>
          <div className="divider">
            <span>sau începe instant</span>
          </div>
          <button
            className="button demo full"
            disabled={busy}
            onClick={() => void run(startDemo)}
          >
            <span>✦</span> Încarcă scenariul demonstrativ
          </button>
          {incident && (
            <div className="upload">
              <label htmlFor="logs">Importă loguri JSONL</label>
              <input
                id="logs"
                type="file"
                accept=".jsonl,.ndjson,.log,.txt"
                disabled={busy}
                onChange={(e) => {
                  const file = e.target.files?.[0];
                  if (file)
                    void run(async () => {
                      await uploadLogs(incident.incident.id, file);
                      await open(incident.incident.id);
                    });
                }}
              />
              <small>
                Maxim 5 MB · mesajele sunt redactate înainte de analiză
              </small>
            </div>
          )}
          {incident && (
            <button
              className="button primary full analyze"
              disabled={busy}
              onClick={() => void run(analyze)}
            >
              {busy ? (
                <>
                  <span className="spinner" /> Analizez dovezile…
                </>
              ) : (
                <>✦ &nbsp;Analizează incidentul</>
              )}
            </button>
          )}
          {error && (
            <p className="error" role="alert">
              {error}
            </p>
          )}
          <div className="safety-note">
            <span>◈</span>
            <p>
              Analiză read-only. IncidentAI nu rulează comenzi și nu modifică
              infrastructura.
            </p>
          </div>
        </aside>
        <section className="results panel">
          <div className="section-title">
            <span className="step">02</span>
            <div>
              <h2>Raport de diagnostic</h2>
              <p>
                {incident
                  ? `${incident.incident.service} · ${new Date(incident.incident.fromUtc).toLocaleString()}`
                  : "Raportul și dovezile apar aici."}
              </p>
            </div>
            {incident && (
              <a
                className="download"
                href={`/api/incidents/${incident.incident.id}/report`}
              >
                ↓ Export JSON
              </a>
            )}
          </div>
          {!incident ? (
            <div className="empty">
              <div className="empty-icon">⌁</div>
              <h3>Începe cu un incident</h3>
              <p>
                Încarcă scenariul demonstrativ sau configurează propriul
                interval de loguri.
              </p>
              <div className="empty-chips">
                <span>Timeline</span>
                <span>Trace correlation</span>
                <span>Source citations</span>
              </div>
            </div>
          ) : (
            <>
              {result && (
                <div className="summary">
                  <span className="status-dot" />
                  <div>
                    <span className="summary-label">
                      REZUMAT ·{" "}
                      {result.provider === "offline"
                        ? "ANALIZĂ LOCALĂ"
                        : "MICROSOFT FOUNDRY"}
                    </span>
                    <p>{result.summary}</p>
                  </div>
                </div>
              )}
              {result?.hypotheses.map((h, i) => (
                <article className="hypothesis" key={`${i}-${h.claim}`}>
                  <div className="hypothesis-head">
                    <span className="rank">H{i + 1}</span>
                    <span className="confidence">
                      Ipoteză · {h.confidenceLabel}
                    </span>
                  </div>
                  <h3>{h.claim}</h3>
                  <div className="citations">
                    {h.evidenceIds.map((id) => (
                      <button
                        key={id}
                        onClick={() =>
                          document.getElementById(id)?.scrollIntoView({
                            behavior: "smooth",
                            block: "center",
                          })
                        }
                      >
                        ↗ {id}
                      </button>
                    ))}
                  </div>
                  {h.contradictions.length > 0 && (
                    <p className="muted">
                      Dovezi contradictorii: {h.contradictions.join("; ")}
                    </p>
                  )}
                </article>
              ))}
              {result && (
                <div className="checks">
                  <h3>Pași de verificare</h3>
                  {result.nextChecks.map((check, i) => (
                    <p key={check}>
                      <span>{String(i + 1).padStart(2, "0")}</span>
                      {check}
                    </p>
                  ))}
                </div>
              )}
              <div className="timeline">
                <div className="timeline-head">
                  <div>
                    <h3>Cronologie</h3>
                    <p>{events.length} evenimente · ordonate după UTC</p>
                  </div>
                  <span>
                    {
                      events.filter((e) => /error|critical/i.test(e.severity))
                        .length
                    }{" "}
                    erori
                  </span>
                </div>
                {events.length === 0 ? (
                  <p className="muted">
                    Nu există loguri pentru acest incident. Importă un fișier
                    JSONL.
                  </p>
                ) : (
                  events.map((e) => (
                    <article
                      id={e.sourceId}
                      key={e.sourceId}
                      className={`event ${evidence.has(e.sourceId) ? "cited" : ""}`}
                    >
                      <time>
                        {new Date(e.timestampUtc).toLocaleTimeString([], {
                          hour12: false,
                        })}
                      </time>
                      <span className={`severity ${e.severity.toLowerCase()}`}>
                        {e.severity}
                      </span>
                      <div>
                        <p>{e.message}</p>
                        <small>
                          {e.traceId ? `trace ${e.traceId} · ` : ""}
                          {e.sourceId}
                        </small>
                      </div>
                    </article>
                  ))
                )}
              </div>
              {incident.logSources.length > 0 && (
                <div className="timeline sources">
                  <div className="timeline-head">
                    <div>
                      <h3>Fișiere sursă</h3>
                      <p>Originalele sunt păstrate în stocarea S3 privată.</p>
                    </div>
                  </div>
                  {incident.logSources.map((source) => (
                    <article className="event" key={source.id}>
                      <time>{(source.sizeBytes / 1024).toFixed(1)} KB</time>
                      <span className="severity">JSONL</span>
                      <div>
                        <a className="download" href={source.contentUrl}>
                          {source.fileName} ↗
                        </a>
                        <small>SHA-256 {source.sha256}</small>
                      </div>
                    </article>
                  ))}
                </div>
              )}
              {incident.releases.length > 0 && (
                <div className="release">
                  <span>↗</span>
                  <p>
                    <b>Release observat</b>
                    <br />
                    {incident.releases
                      .map(
                        (r) =>
                          `${r.label}${r.deployedAtUtc ? ` · ${new Date(r.deployedAtUtc).toLocaleTimeString()}` : ""}`,
                      )
                      .join(", ")}
                    <small>
                      {" "}
                      Corelația temporală nu confirmă cauzalitatea.
                    </small>
                  </p>
                </div>
              )}
            </>
          )}
        </section>
      </section>
      <footer>
        <span>
          INCIDENTAI <b>·</b> READ-ONLY INCIDENT ANALYSIS
        </span>
        <span>
          Datele demo sunt artificiale. Verifică independent fiecare ipoteză.
        </span>
      </footer>
    </main>
  );
}
