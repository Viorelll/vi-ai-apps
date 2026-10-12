import { useEffect, useState } from "react";
import {
  ScanLine,
  Plus,
  Camera,
  BookOpen,
  ArrowRight,
  Download,
  ShieldCheck,
  LoaderCircle,
  MessageSquare,
  FileText,
} from "lucide-react";
import type {
  Inspection,
  ManualSource,
  Checklist as Item,
} from "./models/Inspection";
import { load, post, request, upload } from "./services/api";
import Findings from "./components/Findings";
import Checklist from "./components/Checklist";
const stateLabels: Record<string, string> = {
  Created: "Nouă",
  Inspecting: "În analiză",
  Retrieving: "Consultăm manualul",
  Ready: "Pregătit",
  NeedsReview: "De revizuit",
  Failed: "Analiză eșuată",
  Stale: "Raport învechit",
};
const documentLabels: Record<string, string> = {
  Uploaded: "În așteptare",
  Processing: "În procesare",
  Ready: "Pregătit",
  Failed: "Procesare eșuată",
  Deleting: "Sursă ștearsă",
};
export default function App() {
  const [inspection, setInspection] = useState<Inspection | null>(null);
  const [label, setLabel] = useState("");
  const [model, setModel] = useState("");
  const [version, setVersion] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [question, setQuestion] = useState("");
  useEffect(() => {
    const id = localStorage.getItem("inspectai-current");
    if (id)
      load(id)
        .then(setInspection)
        .catch((e) => setError(e.message));
  }, []);
  useEffect(() => {
    if (!inspection) return;
    const id = inspection.id;
    const timer = setInterval(() => {
      load(id)
        .then(setInspection)
        .catch((e) => setError(e.message));
    }, 2000);
    return () => clearInterval(timer);
  }, [inspection?.id]);
  async function action(fn: () => Promise<void>) {
    setBusy(true);
    setError("");
    try {
      await fn();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Operația a eșuat.");
    } finally {
      setBusy(false);
    }
  }
  async function create() {
    await action(async () => {
      const i = await post<{ id: string }>("/api/inspections", {
        equipmentLabel: label,
        equipmentModel: model,
        manualVersion: version,
      });
      localStorage.setItem("inspectai-current", i.id);
      setInspection(await load(i.id));
    });
  }
  async function attach(files: FileList | null, role: "photo" | "manual") {
    if (!files || !inspection) return;
    const id = inspection.id;
    await action(async () => {
      for (const file of Array.from(files)) {
        const d = await upload(file);
        const current = await load(id);
        await post(`/api/inspections/${id}/sources`, {
          documentId: d.documentId,
          role,
          revision: current.revision,
          manualModel: current.equipmentModel,
          manualVersion: current.manualVersion,
        });
      }
      setInspection(await load(id));
    });
  }
  async function analyze() {
    if (!inspection) return;
    const current = inspection;
    await action(async () => {
      await post(`/api/inspections/${current.id}/analyze`, {
        revision: current.revision,
        requestId: crypto.randomUUID(),
      });
      setInspection(await load(current.id));
    });
  }
  async function note(c: Item) {
    if (!inspection) return;
    const i = inspection;
    await action(async () => {
      await request(`/api/inspections/${i.id}/checklist/${c.id}`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          status: c.status === "noted" ? "proposed" : "noted",
          revision: i.revision,
        }),
      });
      setInspection(await load(i.id));
    });
  }
  async function ask() {
    if (!inspection || !question.trim()) return;
    const i = inspection;
    await action(async () => {
      await post(`/api/inspections/${i.id}/questions`, {
        question,
        requestId: crypto.randomUUID(),
        revision: i.revision,
      });
      setQuestion("");
      setInspection(await load(i.id));
    });
  }
  async function exportReport() {
    if (!inspection) return;
    await action(async () => {
      const data = await request(`/api/inspections/${inspection.id}/export`);
      const url = URL.createObjectURL(
        new Blob([JSON.stringify(data, null, 2)], { type: "application/json" }),
      );
      const a = document.createElement("a");
      a.href = url;
      a.download = "inspectai-report.json";
      a.click();
      URL.revokeObjectURL(url);
    });
  }
  const processing =
    inspection?.state === "Inspecting" || inspection?.state === "Retrieving";
  const ready =
    inspection?.state === "Ready" || inspection?.state === "NeedsReview";
  let summary = "";
  let unknowns: string[] = [];
  let manualSources: ManualSource[] = [];
  if (inspection?.report) {
    try {
      const r = JSON.parse(inspection.report.resultJson);
      summary = r.summary;
      unknowns = r.unknowns;
      manualSources = JSON.parse(inspection.report.sourcesJson).sources;
    } catch {
      /* invalid report stays visible as missing content */
    }
  }
  return (
    <>
      <header>
        <a className="brand" href="/">
          <span>
            <ScanLine size={24} />
          </span>
          Inspect<span className="brand-ai">AI</span>
        </a>
        <span className="header-label">ASISTENT DE INSPECȚIE</span>
        <button
          className="ghost"
          onClick={() => {
            localStorage.removeItem("inspectai-current");
            setInspection(null);
            setError("");
          }}
        >
          <Plus size={16} />
          Inspecție nouă
        </button>
      </header>
      <main>
        <div className="eyebrow">
          <span />
          FOTOGRAFII + MANUAL + DOVEZI
        </div>
        <div className="intro">
          <div>
            <h1>
              Privește atent.
              <br />
              <em>Verifică informat.</em>
            </h1>
            <p>
              Transformă fotografiile echipamentului într-o listă de verificări,
              <br className="desktop" /> cu observații clare și trimiteri la
              manual.
            </p>
          </div>
          <div className="intro-mark">
            <ScanLine size={70} strokeWidth={1} />
            <small>OBSERVĂ. CITEAZĂ. REVIZUIEȘTE.</small>
          </div>
        </div>
        {error && (
          <div className="error" role="alert">
            {error}
          </div>
        )}
        <div className="workspace">
          <aside>
            <div className="panel-title">
              <span className="number">01</span>
              <h2>Contextul inspecției</h2>
            </div>
            {!inspection ? (
              <div className="context-form">
                <label>
                  Echipament
                  <input
                    value={label}
                    onChange={(e) => setLabel(e.target.value)}
                    placeholder="ex. Pompă de circulație"
                  />
                </label>
                <label>
                  Model confirmat
                  <input
                    value={model}
                    onChange={(e) => setModel(e.target.value)}
                    placeholder="ex. P-100"
                  />
                </label>
                <label>
                  Versiunea manualului
                  <input
                    value={version}
                    onChange={(e) => setVersion(e.target.value)}
                    placeholder="ex. Revizia 1.0"
                  />
                </label>
                <button
                  className="primary"
                  disabled={
                    busy || !label.trim() || !model.trim() || !version.trim()
                  }
                  onClick={create}
                >
                  Creează inspecția
                  <ArrowRight size={17} />
                </button>
              </div>
            ) : (
              <>
                <div className="context-card">
                  <h3>{inspection.equipmentLabel}</h3>
                  <dl>
                    <dt>MODEL</dt>
                    <dd>{inspection.equipmentModel}</dd>
                    <dt>MANUAL</dt>
                    <dd>{inspection.manualVersion}</dd>
                  </dl>
                  <span className="badge">
                    {stateLabels[inspection.state] ?? inspection.state}
                  </span>
                </div>
                <label className={`upload ${busy ? "disabled" : ""}`}>
                  <Camera size={26} />
                  <strong>Adaugă fotografii</strong>
                  <small>PNG, JPEG · maximum 8 fotografii</small>
                  <input
                    aria-label="Adaugă fotografii"
                    type="file"
                    accept="image/png,image/jpeg"
                    multiple
                    disabled={busy || processing}
                    onChange={(e) => {
                      void attach(e.target.files, "photo");
                      e.target.value = "";
                    }}
                  />
                </label>
                <label className={`upload ${busy ? "disabled" : ""}`}>
                  <BookOpen size={26} />
                  <strong>Adaugă manualul</strong>
                  <small>PDF, DOCX, TXT · model și versiune confirmate</small>
                  <input
                    aria-label="Adaugă manualul"
                    type="file"
                    accept=".pdf,.docx,.txt,.md"
                    disabled={
                      busy ||
                      processing ||
                      inspection.sources.some((s) => s.role === "manual")
                    }
                    onChange={(e) => {
                      void attach(e.target.files, "manual");
                      e.target.value = "";
                    }}
                  />
                </label>
                <div className="source-list">
                  {inspection.sources.map((s) => (
                    <div key={s.documentId}>
                      <FileText size={15} />
                      <div>
                        <a href={s.contentUrl} target="_blank" rel="noreferrer">
                          {s.fileName}
                        </a>
                        <small>
                          {documentLabels[s.status] ?? s.status}
                          {s.errorCode ? " · " + s.errorCode : ""}
                        </small>
                      </div>
                      <button
                        title="Șterge sursa și invalidează raportul"
                        disabled={busy}
                        onClick={() => {
                          if (
                            window.confirm(
                              "Ștergi sursa pentru toate inspecțiile care o folosesc? Rapoartele vor deveni învechite.",
                            )
                          )
                            void action(async () => {
                              await request(`/api/documents/${s.documentId}`, {
                                method: "DELETE",
                              });
                              setInspection(await load(inspection.id));
                            });
                        }}
                      >
                        ×
                      </button>
                      {s.status === "Failed" && (
                        <button
                          disabled={busy}
                          onClick={() =>
                            void action(async () => {
                              await post(
                                `/api/documents/${s.documentId}/retry`,
                                {},
                              );
                              setInspection(await load(inspection.id));
                            })
                          }
                        >
                          Reîncearcă
                        </button>
                      )}
                    </div>
                  ))}
                </div>
                <button
                  className="primary"
                  disabled={
                    busy ||
                    processing ||
                    !inspection.sources.some((s) => s.role === "photo") ||
                    !inspection.sources.some((s) => s.role === "manual") ||
                    inspection.sources.some((s) => s.status !== "Ready")
                  }
                  onClick={analyze}
                >
                  {processing ? (
                    <LoaderCircle className="spin" size={18} />
                  ) : (
                    <ScanLine size={18} />
                  )}{" "}
                  {processing ? "Analiză în curs…" : "Analizează echipamentul"}
                  <ArrowRight size={17} />
                </button>
                {inspection.errorCode && (
                  <p className="error">{inspection.errorCode}</p>
                )}
              </>
            )}
            <div className="aside-note">
              <ShieldCheck size={19} />
              <p>
                Doar dovezi din sursele tale.
                <br />
                Nicio comandă către echipament.
              </p>
            </div>
          </aside>
          <section className="report-panel">
            <div className="panel-title">
              <span className="number">02</span>
              <h2>Raport cu dovezi</h2>
              {ready && (
                <button
                  className="ghost"
                  disabled={busy}
                  onClick={exportReport}
                >
                  <Download size={16} />
                  Export JSON
                </button>
              )}
            </div>
            {!inspection?.report ? (
              <div className="empty">
                <div className="empty-icon">
                  <ScanLine size={48} strokeWidth={1} />
                </div>
                <h3>
                  {processing
                    ? "Construim raportul cu dovezi"
                    : "Fiecare verificare începe cu o sursă."}
                </h3>
                <p>
                  Adaugă fotografiile și manualul potrivit.
                  <br />
                  Raportul va separa ce se vede de ce trebuie verificat.
                </p>
                <div className="steps">
                  <span>1 · Fotografii</span>
                  <span>2 · Manual</span>
                  <span>3 · Revizuire</span>
                </div>
              </div>
            ) : (
              <>
                <div className={`report-state ${!ready ? "stale" : ""}`}>
                  {ready
                    ? "PROPUNERE PENTRU REVIZUIRE UMANĂ"
                    : "RAPORT ÎNVECHIT · REANALIZEAZĂ SURSELE ACTIVE"}
                </div>
                <h3 className="section-title">Ce observăm</h3>
                <p className="summary">{summary}</p>
                <Findings
                  findings={inspection.findings}
                  sources={inspection.sources}
                />
                <h3 className="section-title">Ce merită verificat</h3>
                <Checklist
                  items={inspection.checklist}
                  manualSources={manualSources}
                  findings={inspection.findings}
                  sources={inspection.sources}
                  onNote={(c) => void note(c)}
                  busy={busy || !ready}
                />
                {unknowns.length > 0 && (
                  <div className="unknowns">
                    <strong>Ce nu putem stabili</strong>
                    <ul>
                      {unknowns.map((u, i) => (
                        <li key={i}>{u}</li>
                      ))}
                    </ul>
                  </div>
                )}
                <div className="questions">
                  <h3>
                    <MessageSquare size={18} />
                    Întreabă despre această inspecție
                  </h3>
                  {inspection.questions.map((q, i) => (
                    <article key={i}>
                      <strong>{q.question}</strong>
                      <p>{JSON.parse(q.answerJson).answer}</p>
                      {JSON.parse(q.answerJson).evidence?.map(
                        (e: { chunkId: string; quote: string }, n: number) => {
                          const s = manualSources.find(
                            (s) => s.chunkId === e.chunkId,
                          );
                          return (
                            <blockquote key={n}>
                              {e.quote}
                              {s && (
                                <a
                                  href={`/api/documents/${s.documentId}/content${s.pageNumber ? "#page=" + s.pageNumber : ""}`}
                                  target="_blank"
                                  rel="noreferrer"
                                >
                                  Manual ↗
                                </a>
                              )}
                            </blockquote>
                          );
                        },
                      )}
                    </article>
                  ))}
                  <form
                    onSubmit={(e) => {
                      e.preventDefault();
                      void ask();
                    }}
                  >
                    <input
                      aria-label="Întrebare"
                      placeholder="Ce spune manualul despre această observație?"
                      value={question}
                      onChange={(e) => setQuestion(e.target.value)}
                      maxLength={2000}
                    />
                    <button
                      disabled={busy || !ready || !question.trim()}
                      aria-label="Trimite întrebarea"
                    >
                      <ArrowRight size={20} />
                    </button>
                  </form>
                </div>
              </>
            )}
            <div className="limitations">
              <ShieldCheck size={18} />
              <p>
                {inspection?.limitations ??
                  "Fotografiile nu pot dovedi defecte interne sau starea operațională. Raportul nu certifică funcționarea ori siguranța echipamentului."}
              </p>
            </div>
          </section>
        </div>
        <footer>
          <span>InspectAI</span>
          <span>Observații, ipoteze și necunoscute — păstrate separat.</span>
        </footer>
      </main>
    </>
  );
}
