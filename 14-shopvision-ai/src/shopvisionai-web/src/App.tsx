import { useEffect, useRef, useState } from "react";
import type { Search } from "./models/search";
import { ask, createSearch, loadSearch, post, upload } from "./services/api";
import { CandidateCard } from "./components/CandidateCard";
const labels: Record<string, string> = {
  Created: "Pregătit",
  Inspecting: "Analizăm fotografia",
  Searching: "Căutăm în catalog",
  Completed: "Analiză finalizată",
  NeedsInput: "Sunt necesare mai multe detalii",
  Failed: "Analiza a eșuat",
  Stale: "Sursa a fost ștearsă",
};
export default function App() {
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState("");
  const [question, setQuestion] = useState("");
  const [search, setSearch] = useState<Search | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [followup, setFollowup] = useState("");
  const [answer, setAnswer] = useState("");
  const [asking, setAsking] = useState(false);
  const abort = useRef<AbortController | null>(null);
  useEffect(() => {
    if (!file) {
      setPreview("");
      return;
    }
    const url = URL.createObjectURL(file);
    setPreview(url);
    return () => URL.revokeObjectURL(url);
  }, [file]);
  useEffect(() => {
    if (!search || !["Inspecting", "Searching"].includes(search.state)) return;
    const controller = new AbortController();
    abort.current = controller;
    let timer: ReturnType<typeof setTimeout>;
    const poll = async () => {
      try {
        const next = await loadSearch(search.id, controller.signal);
        setSearch(next);
        if (["Inspecting", "Searching"].includes(next.state))
          timer = setTimeout(poll, 1000);
      } catch (e) {
        if (!controller.signal.aborted) setError(String(e));
      }
    };
    timer = setTimeout(poll, 500);
    return () => {
      controller.abort();
      clearTimeout(timer);
    };
  }, [search?.id, search?.state]);
  async function analyze() {
    if (!file) return;
    setBusy(true);
    setError("");
    setAnswer("");
    setSearch(null);
    try {
      const doc = await upload(file);
      const s = await createSearch(doc.documentId, question);
      await post(`/api/visual/searches/${s.id}/analyze`, {
        revision: s.revision,
        requestId: crypto.randomUUID(),
      });
      setSearch(await loadSearch(s.id));
    } catch (e) {
      setError(String(e));
    } finally {
      setBusy(false);
    }
  }
  async function retry() {
    if (!search) return;
    setBusy(true);
    setError("");
    try {
      await post(`/api/visual/searches/${search.id}/analyze`, {
        revision: search.revision,
        requestId: crypto.randomUUID(),
      });
      setSearch(await loadSearch(search.id));
    } catch (e) {
      setError(String(e));
    } finally {
      setBusy(false);
    }
  }
  async function questionCandidate() {
    if (!search || !followup.trim()) return;
    setAsking(true);
    try {
      setAnswer((await ask(search.id, followup)).answer);
    } catch (e) {
      setError(String(e));
    } finally {
      setAsking(false);
    }
  }
  function exportJson() {
    if (!search) return;
    const url = URL.createObjectURL(
      new Blob([JSON.stringify(search, null, 2)], { type: "application/json" }),
    );
    const a = document.createElement("a");
    a.href = url;
    a.download = `shopvision-${search.id}.json`;
    a.click();
    URL.revokeObjectURL(url);
  }
  const processing =
    busy || ["Inspecting", "Searching"].includes(search?.state ?? "");
  return (
    <>
      <header>
        <a className="brand" href="/">
          ◈{" "}
          <span>
            ShopVision<span className="brand-ai">AI</span>
          </span>
        </a>
        <span className="header-note">
          CATALOG DEMONSTRATIV · FĂRĂ ACHIZIȚII
        </span>
      </header>
      <main>
        <div className="eyebrow">ASISTENT VIZUAL PENTRU PRODUSE</div>
        <h1>
          O fotografie.
          <br />
          <span>Mai multe posibilități.</span>
        </h1>
        <p className="intro">
          Descoperă produse similare din catalog. Compară caracteristicile
          vizibile, consultă fișele și verifică stocul disponibil la momentul
          consultării.
        </p>
        <div className="workspace">
          <section className="input-panel">
            <div className="section-label">
              <span>01</span> Fotografia produsului
            </div>
            <label
              className={`upload ${preview ? "has-image" : ""}`}
              htmlFor="photo"
            >
              {preview ? (
                <img src={preview} alt="Fotografia selectată" />
              ) : (
                <>
                  <span className="upload-icon">＋</span>
                  <strong>Alege o fotografie</strong>
                  <span>PNG, JPG sau WebP · maximum 50 MiB</span>
                </>
              )}
              <input
                id="photo"
                type="file"
                accept="image/png,image/jpeg,image/webp"
                disabled={processing}
                onChange={(e) => {
                  const f = e.target.files?.[0];
                  setError("");
                  if (f && f.size > 50 * 1024 * 1024) {
                    setError("Fișierul depășește 50 MiB.");
                    return;
                  }
                  setFile(f ?? null);
                  setSearch(null);
                }}
              />
            </label>
            {file && (
              <p className="filename">
                {file.name} · {(file.size / 1024).toFixed(0)} KB
              </p>
            )}
            <label className="field-label" htmlFor="question">
              Ce ai vrea să afli? <span>opțional</span>
            </label>
            <textarea
              id="question"
              value={question}
              maxLength={2000}
              onChange={(e) => setQuestion(e.target.value)}
              placeholder="De exemplu: Ce adaptoare similare sunt în catalog?"
            />
            <button
              className="primary"
              disabled={!file || processing}
              onClick={analyze}
            >
              {processing ? "Se procesează…" : "Găsește candidați"}{" "}
              <span>↗</span>
            </button>
            <p className="privacy">
              Stocul și prețul sunt citite din catalog. Fotografia nu confirmă
              SKU-ul, materialul sau autenticitatea.
            </p>
          </section>
          <section className="results-panel" aria-live="polite">
            <div className="section-label">
              <span>02</span> Observații și candidați
            </div>
            {error && (
              <div className="error" role="alert">
                {error}
              </div>
            )}
            {!search && !processing && (
              <div className="empty-state">
                <div className="empty-orbit">◈</div>
                <h2>Începe cu o imagine</h2>
                <p>
                  Observațiile vizibile și candidații vor apărea aici.
                  <br />
                  Rezultatele includ surse și limitele identificării.
                </p>
                <div className="steps">
                  <span>Observă</span>
                  <span>→</span>
                  <span>Compară</span>
                  <span>→</span>
                  <span>Verifică</span>
                </div>
              </div>
            )}
            {processing && (
              <div className="processing">
                <span className="spinner" /> Analizăm imaginea și consultăm
                catalogul…
              </div>
            )}
            {search && (
              <>
                <div className="result-heading">
                  <h2>{labels[search.state]}</h2>
                  {search.state === "Completed" && (
                    <button className="text-button" onClick={exportJson}>
                      Export JSON ↓
                    </button>
                  )}
                </div>
                {search.errorCode && (
                  <div className="error">{search.errorCode}</div>
                )}
                {["Failed", "NeedsInput"].includes(search.state) && (
                  <button onClick={retry} disabled={busy}>
                    Reanalizează
                  </button>
                )}
                {search.result && search.state !== "Stale" && (
                  <>
                    {search.result.demonstration && (
                      <div className="demo">
                        MOD DEMONSTRATIV — observații fixe pentru fixture-ul
                        adaptor USB negru, fără inferență AI reală.
                      </div>
                    )}
                    <div className="observations">
                      {search.result.observations.map((o, i) => (
                        <div className="observation" key={i}>
                          <span>
                            {o.kind === "visible"
                              ? "Vizibil"
                              : o.kind === "inferred"
                                ? "Dedus"
                                : "Necunoscut"}
                          </span>
                          <strong>
                            {o.attribute}: {o.value}
                          </strong>
                          <a
                            href={search.imageUrl}
                            target="_blank"
                            rel="noreferrer"
                          >
                            Sursa fotografie ↗
                          </a>
                        </div>
                      ))}
                    </div>
                    <p className="warning">{search.result.warning}</p>
                    <div className="candidate-grid">
                      {search.result.candidates.map((c) => (
                        <CandidateCard candidate={c} key={c.sku} />
                      ))}
                    </div>
                    {search.result.candidates.length === 0 && (
                      <p>
                        Niciun candidat susținut de dovezi. Încarcă o fotografie
                        mai clară.
                      </p>
                    )}
                    {search.state === "Completed" && (
                      <div className="followup">
                        <label htmlFor="followup">
                          Întreabă despre datele candidaților
                        </label>
                        <div>
                          <input
                            id="followup"
                            value={followup}
                            maxLength={2000}
                            onChange={(e) => setFollowup(e.target.value)}
                            placeholder="Ce informații sunt disponibile?"
                          />
                          <button
                            onClick={questionCandidate}
                            disabled={asking || !followup.trim()}
                          >
                            {asking ? "…" : "Trimite"}
                          </button>
                        </div>
                        {answer && <p>{answer}</p>}
                      </div>
                    )}
                  </>
                )}
              </>
            )}
          </section>
        </div>
        <footer>
          <span>ShopVisionAI · seed-v1</span>
          <span>
            Identificare orientativă. Date artificiale, fără tranzacții.
          </span>
        </footer>
      </main>
    </>
  );
}
