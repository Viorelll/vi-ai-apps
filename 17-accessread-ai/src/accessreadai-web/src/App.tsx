import { useEffect, useRef, useState } from "react";
import { api } from "./api";
import type { DocumentItem, Reading } from "./models/reading";
import { ReadingResult } from "./components/ReadingResult";
import { SourceChat } from "./components/SourceChat";
const languages = [
  ["ro", "Română"],
  ["en", "English"],
  ["fr", "Français"],
  ["de", "Deutsch"],
  ["es", "Español"],
];
const terminal = ["Ready", "NeedsReview", "Failed", "Stale"];
export default function App() {
  const [documents, setDocuments] = useState<DocumentItem[]>([]);
  const [selected, setSelected] = useState("");
  const [target, setTarget] = useState("en");
  const [source, setSource] = useState("ro");
  const [reading, setReading] = useState<Reading>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [resumeId, setResumeId] = useState(
    localStorage.getItem("accessread-reading") ?? "",
  );
  const fileRef = useRef<HTMLInputElement>(null);
  useEffect(() => {
    const controller = new AbortController();
    let active = true;
    async function refresh() {
      try {
        const docs = await api.list(controller.signal);
        if (active) setDocuments(docs);
      } catch (e) {
        if (active) setError(e instanceof Error ? e.message : "load_failed");
      }
    }
    void refresh();
    const timer = setInterval(refresh, 2000);
    return () => {
      active = false;
      controller.abort();
      clearInterval(timer);
    };
  }, []);
  useEffect(() => {
    if (!resumeId) return;
    const controller = new AbortController();
    let active = true;
    const poll = async () => {
      try {
        const result = await api.read(resumeId, controller.signal);
        if (active) {
          setReading(result);
          setSelected(result.documentId);
          setMessage(result.state);
          if (result.state === "Failed")
            setError(result.errorCode ?? "processing_failed");
        }
      } catch (e) {
        if (active) setError(e instanceof Error ? e.message : "load_failed");
      }
    };
    void poll();
    const timer = setInterval(poll, 1500);
    return () => {
      active = false;
      controller.abort();
      clearInterval(timer);
    };
  }, [resumeId]);
  async function run(action: () => Promise<void>) {
    setBusy(true);
    setError("");
    try {
      await action();
    } catch (e) {
      setError(e instanceof Error ? e.message : "request_failed");
    } finally {
      setBusy(false);
    }
  }
  const processing =
    reading &&
    !terminal.includes(reading.state) &&
    reading.state !== "Uploaded";
  const chosen = documents.find((x) => x.id === selected);
  return (
    <>
      <a className="skip-link" href="#workspace">
        Sari la documente
      </a>
      <header className="topbar">
        <a
          href="/"
          className="brand"
          aria-label="AccessReadAI, pagina principală"
        >
          <span className="brand-mark" aria-hidden="true">
            Aa
          </span>
          <span>
            AccessRead<span className="brand-ai">AI</span>
          </span>
        </a>
        <span className="tag">CITITOR ACCESIBIL</span>
      </header>
      <main id="workspace">
        <section className="intro">
          <p className="eyebrow">MAI CLAR. ÎN LIMBA TA. ÎN RITMUL TĂU.</p>
          <h1>
            Documentele devin
            <br />
            mai ușor de înțeles.
          </h1>
          <p>
            Original, text simplificat, traducere și audio — împreună,
            <br className="desktop-break" /> cu referințe către fiecare pagină.
          </p>
        </section>
        <div className="workspace">
          <section className="upload-card" aria-labelledby="upload-title">
            <span className="step">01 / DOCUMENT</span>
            <h2 id="upload-title">Alege sursa de lectură</h2>
            <p>PDF, PNG, JPEG, TXT sau Markdown · maximum 50 MiB.</p>
            <label className="upload-label" htmlFor="document-file">
              <span aria-hidden="true" className="upload-icon">
                ↑
              </span>
              <strong>Încarcă un document</strong>
              <span>Originalul rămâne păstrat separat</span>
            </label>
            <input
              ref={fileRef}
              id="document-file"
              type="file"
              accept=".pdf,.png,.jpg,.jpeg,.txt,.md"
              disabled={busy || !!processing}
              onChange={(e) => {
                const file = e.target.files?.[0];
                if (file)
                  void run(async () => {
                    const result = await api.upload(file);
                    setSelected(result.documentId);
                    setReading(undefined);
                    setResumeId("");
                    localStorage.removeItem("accessread-reading");
                    setDocuments(await api.list());
                    setMessage("Document încărcat. Așteaptă extracția.");
                  });
              }}
            />
            <button
              type="button"
              className="quiet demo-button"
              disabled={busy || !!processing}
              onClick={() =>
                void run(async () => {
                  const result = await api.upload(
                    new File(
                      [
                        "Nu depășiți 10 unități.\n\nApăsați butonul pentru a porni.\n\nPăstrați distanța de 2 metri.",
                      ],
                      "exemplu.txt",
                      { type: "text/plain" },
                    ),
                  );
                  setSelected(result.documentId);
                  setResumeId("");
                  setReading(undefined);
                  setDocuments(await api.list());
                  setMessage("Exemplul a fost încărcat.");
                })
              }
            >
              Încearcă documentul demonstrativ
            </button>
            <p className="hint">
              Cu setările demo, traducerea folosește numai exemplul artificial.
              Scanurile necesită configurarea Azure.
            </p>
            {documents.length > 0 && (
              <div className="document-picker">
                <label htmlFor="selected-document">Documente disponibile</label>
                <select
                  id="selected-document"
                  value={selected}
                  disabled={!!processing}
                  onChange={(e) => {
                    setSelected(e.target.value);
                    setReading(undefined);
                    setResumeId("");
                  }}
                >
                  <option value="">Selectează un document</option>
                  {documents.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.fileName} · {d.state}
                    </option>
                  ))}
                </select>
                {chosen && (
                  <div className="document-actions">
                    <a href={`/api/documents/${chosen.id}/content`}>
                      Descarcă originalul
                    </a>
                    {chosen.state === "Failed" && (
                      <button
                        className="quiet"
                        onClick={() =>
                          void run(async () => {
                            await api.retry(chosen.id);
                            setDocuments(await api.list());
                          })
                        }
                      >
                        Reîncearcă extracția
                      </button>
                    )}
                    <button
                      className="quiet"
                      disabled={busy || !!processing}
                      onClick={() =>
                        void run(async () => {
                          await api.delete(chosen.id);
                          setSelected("");
                          setReading(undefined);
                          setResumeId("");
                          localStorage.removeItem("accessread-reading");
                          setDocuments(await api.list());
                        })
                      }
                    >
                      Șterge sursa
                    </button>
                  </div>
                )}
              </div>
            )}
          </section>
          <section className="settings-card" aria-labelledby="settings-title">
            <span className="step">02 / PREFERINȚE</span>
            <h2 id="settings-title">Cum vrei să citești?</h2>
            <label htmlFor="source-language">Limba originalului</label>
            <select
              id="source-language"
              value={source}
              onChange={(e) => setSource(e.target.value)}
              disabled={!!processing}
            >
              {languages.map(([code, label]) => (
                <option key={code} value={code}>
                  {label}
                </option>
              ))}
            </select>
            <label htmlFor="target-language">Limba traducerii</label>
            <select
              id="target-language"
              value={target}
              onChange={(e) => setTarget(e.target.value)}
              disabled={!!processing}
            >
              {languages.map(([code, label]) => (
                <option key={code} value={code}>
                  {label}
                </option>
              ))}
            </select>
            <ul className="benefits">
              <li>Textul original rămâne vizibil</li>
              <li>Numerele și negațiile sunt verificate</li>
              <li>Audio pornește doar când alegi tu</li>
            </ul>
            <button
              className="generate"
              disabled={busy || !!processing || chosen?.state !== "Ready"}
              onClick={() =>
                void run(async () => {
                  let current =
                    reading?.state === "Failed"
                      ? reading
                      : await api.create(selected, target, source);
                  await api.generate(current.id, current.revision);
                  current = await api.read(current.id);
                  setReading(current);
                  setResumeId(current.id);
                  localStorage.setItem("accessread-reading", current.id);
                })
              }
            >
              {processing
                ? "Se pregătește lectura…"
                : reading?.state === "Failed"
                  ? "Reîncearcă lectura"
                  : "Generează lectura"}
              <span aria-hidden="true">→</span>
            </button>
            <p className="status" role="status" aria-live="polite">
              {chosen?.state === "Failed"
                ? chosen.errorCode
                : message || "Alege un document pentru a începe."}
            </p>
          </section>
        </div>
        {error && (
          <p className="notice error" role="alert">
            {error}
          </p>
        )}
        {reading && (
          <ReadingResult
            reading={reading}
            busy={busy || !!processing}
            onCorrect={(variantId, text) =>
              run(async () => {
                await api.correct(
                  reading.id,
                  reading.revision,
                  variantId,
                  text,
                );
                setReading(await api.read(reading.id));
              })
            }
          />
        )}{" "}
        {chosen?.state === "Ready" && <SourceChat documentId={selected} />}
        <footer>
          AccessReadAI{" "}
          <span>
            Conținutul neclar este marcat pentru verificare. Păstrează aplicația
            într-o rețea privată.
          </span>
        </footer>
      </main>
    </>
  );
}
