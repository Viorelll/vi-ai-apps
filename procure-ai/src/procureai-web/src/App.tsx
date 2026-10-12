import { useCallback, useEffect, useState } from "react";
import {
  ArrowRight,
  CheckCheck,
  Download,
  FilePlus2,
  FileText,
  Layers3,
  LoaderCircle,
  Play,
  Plus,
  RotateCcw,
  ShieldCheck,
  Sparkles,
  Trash2,
} from "lucide-react";
import { api, errorHint } from "./api";
import { AgentProgress } from "./components/AgentProgress";
import { OfferComparison } from "./components/OfferComparison";
import { SourceLink } from "./components/SourceLink";
import type { ChatResult, Criteria, Document, Run, RunSummary } from "./models";

const initial: Criteria = {
  currency: "EUR",
  quantity: 10,
  unit: "unități",
  maxDays: 10,
};
const stateLabel: Record<string, string> = {
  Created: "Pregătire",
  Planning: "Planificare",
  Evaluating: "Evaluare",
  Recommending: "Recomandare",
  Completed: "Finalizată",
  NeedsInput: "Informații necesare",
  Failed: "Eroare",
};
export default function App() {
  const [brief, setBrief] = useState(
    "Avem nevoie de 10 unități, cu livrare în maximum 10 zile. Compară costul net și transportul pentru ofertele conforme.",
  );
  const [criteria, setCriteria] = useState<Criteria>(initial);
  const [runId, setRunId] = useState<string>(
    () => localStorage.getItem("procureai-run") ?? "",
  );
  const [run, setRun] = useState<Run>();
  const [runs, setRuns] = useState<RunSummary[]>([]);
  const [documents, setDocuments] = useState<Document[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [mode, setMode] = useState("Se verifică conexiunea");
  const [role, setRole] = useState("offer");
  const [question, setQuestion] = useState("");
  const [chat, setChat] = useState<ChatResult>();
  const refresh = useCallback(
    async (signal?: AbortSignal) => {
      const [list, docs] = await Promise.all([
        api.runs(signal),
        api.documents(signal),
      ]);
      setRuns(list);
      setDocuments(docs);
      if (runId) setRun(await api.run(runId, signal));
    },
    [runId],
  );
  useEffect(() => {
    const controller = new AbortController();
    const poll = () => {
      void refresh(controller.signal).catch((e) => {
        if (!controller.signal.aborted) setError(e.message);
      });
    };
    poll();
    const timer = setInterval(poll, 2000);
    void api
      .health(controller.signal)
      .then((h) =>
        setMode(
          h.mode === "Demo"
            ? "Mod demonstrativ · fără apeluri cloud"
            : "Microsoft Foundry",
        ),
      )
      .catch(() => {
        if (!controller.signal.aborted) setMode("Serviciile nu sunt pregătite");
      });
    return () => {
      controller.abort();
      clearInterval(timer);
    };
  }, [refresh]);
  function select(id: string) {
    setRunId(id);
    localStorage.setItem("procureai-run", id);
    setRun(undefined);
    setChat(undefined);
  }
  async function perform(action: () => Promise<void>) {
    setBusy(true);
    setError("");
    try {
      await action();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Operația a eșuat.");
    } finally {
      setBusy(false);
    }
  }
  async function create() {
    const result = await api.create(brief, criteria);
    select(result.id);
    const created = await api.run(result.id);
    setRun(created);
    return created;
  }
  async function uploadFiles(files: File[]) {
    let current = run ?? (await create());
    if (current.state !== "Created")
      throw new Error("Creează o analiză nouă pentru a adăuga surse.");
    for (const file of files) {
      const result = await api.upload(file);
      if (!current.documents.some((d) => d.id === result.documentId))
        current = await api.attach(
          current.id,
          result.documentId,
          current.revision,
          role,
        );
    }
    setRun(current);
    setDocuments(await api.documents());
  }
  async function example() {
    let current = await create();
    for (const [supplierLabel, net, shipping, leadDays] of [
      ["Oferta A", 1000, 50, 7],
      ["Oferta B", 980, 120, 9],
    ] as const) {
      const content = JSON.stringify(
        {
          supplierLabel,
          currency: criteria.currency,
          quantity: criteria.quantity,
          unit: criteria.unit,
          net,
          shipping,
          leadDays,
          meetsRequirements: true,
        },
        null,
        2,
      );
      const result = await api.upload(
        new File([content], `${supplierLabel.replace(" ", "-")}.json`, {
          type: "application/json",
        }),
      );
      current = await api.attach(
        current.id,
        result.documentId,
        current.revision,
        "offer",
      );
    }
    setRun(current);
  }
  async function exportReport() {
    if (!run) return;
    const report = await api.run(run.id);
    const url = URL.createObjectURL(
      new Blob([JSON.stringify(report, null, 2)], { type: "application/json" }),
    );
    const link = document.createElement("a");
    link.href = url;
    link.download = `ProcureAI-${run.id}.json`;
    link.click();
    URL.revokeObjectURL(url);
  }
  const active =
    !!run &&
    !["Created", "Completed", "NeedsInput", "Failed"].includes(run.state) &&
    !run.stale;
  const ready =
    run?.documents.length && run.documents.every((d) => d.state === "Ready");
  return (
    <div className="app">
      <header className="header">
        <a className="brand" href="#">
          <span className="brand-icon">
            <Layers3 size={23} />
          </span>
          Procure<span>AI</span>
          <small>ACHIZIȚII CU DOVEZI</small>
        </a>
        <span className="private">
          <ShieldCheck size={15} /> Spațiu privat
        </span>
      </header>
      <main>
        <div className="intro">
          <div>
            <p className="eyebrow">DE LA OFERTE LA O DECIZIE CLARĂ</p>
            <h1>
              Compară corect.
              <br />
              <span>Alege informat.</span>
            </h1>
            <p>
              Patru roluri distincte. O grilă transparentă.
              <br />
              Fiecare recomandare se întoarce la sursă.
            </p>
          </div>
          <div className="intro-note">
            <span className="mode-dot" />
            {mode}
            <p>
              Nicio comandă nu este plasată.
              <br />
              Tu păstrezi decizia finală.
            </p>
          </div>
        </div>
        {error && (
          <div className="error" role="alert">
            {error}
            <button aria-label="Închide eroarea" onClick={() => setError("")}>
              ×
            </button>
          </div>
        )}
        <div className="analysis-bar">
          <div>
            <span className="eyebrow">ANALIZA CURENTĂ</span>
            <select
              aria-label="Selectează analiza"
              value={runId}
              onChange={(e) => select(e.target.value)}
            >
              <option value="">Analiză nouă</option>
              {runs.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.brief.slice(0, 65)} · {stateLabel[r.state] ?? r.state}
                </option>
              ))}
            </select>
          </div>
          <button
            className="secondary"
            disabled={busy}
            onClick={() => {
              select("");
              setError("");
            }}
          >
            <Plus size={16} /> Analiză nouă
          </button>
        </div>
        <div className="workspace">
          <section className="panel inputs">
            <div className="section-title">
              <span>01</span>
              <div>
                <h2>Definește necesarul</h2>
                <p>Condiții fixe pentru o comparație echitabilă.</p>
              </div>
            </div>
            <label>
              Descriere
              <textarea
                value={run?.brief ?? brief}
                onChange={(e) => setBrief(e.target.value)}
                disabled={!!run}
                rows={5}
                maxLength={8000}
              />
            </label>
            <div className="form-grid">
              <label>
                Cantitate
                <input
                  type="number"
                  min="1"
                  max="1000000"
                  value={run?.criteria.quantity ?? criteria.quantity}
                  disabled={!!run}
                  onChange={(e) =>
                    setCriteria({
                      ...criteria,
                      quantity: Number(e.target.value),
                    })
                  }
                />
              </label>
              <label>
                Unitate
                <input
                  value={run?.criteria.unit ?? criteria.unit}
                  disabled={!!run}
                  maxLength={40}
                  onChange={(e) =>
                    setCriteria({ ...criteria, unit: e.target.value })
                  }
                />
              </label>
              <label>
                Monedă
                <select
                  value={run?.criteria.currency ?? criteria.currency}
                  disabled={!!run}
                  onChange={(e) =>
                    setCriteria({ ...criteria, currency: e.target.value })
                  }
                >
                  {["EUR", "RON", "MDL", "USD"].map((c) => (
                    <option key={c}>{c}</option>
                  ))}
                </select>
              </label>
              <label>
                Livrare maximă (zile)
                <input
                  type="number"
                  min="1"
                  max="3650"
                  disabled={!!run}
                  value={run?.criteria.maxDays ?? criteria.maxDays}
                  onChange={(e) =>
                    setCriteria({
                      ...criteria,
                      maxDays: Number(e.target.value),
                    })
                  }
                />
              </label>
            </div>
            <div className="weights">
              <span>GRILA DE EVALUARE</span>
              <div>
                <b>70%</b> cost total <i /> <b>30%</b> termen
              </div>
              <small>
                Net + transport · aceeași monedă și cantitate · doar oferte
                conforme
              </small>
            </div>
            {!run && (
              <div className="buttons">
                <button
                  className="primary"
                  disabled={busy || !brief.trim()}
                  onClick={() =>
                    void perform(async () => {
                      await create();
                    })
                  }
                >
                  Creează analiza <ArrowRight size={16} />
                </button>
                <button
                  className="text-button"
                  disabled={busy}
                  onClick={() => void perform(example)}
                >
                  Încarcă exemplul A / B
                </button>
              </div>
            )}
            <div className="divider" />
            <div className="section-title">
              <span>02</span>
              <div>
                <h2>Adaugă ofertele</h2>
                <p>Documente originale, păstrate ca dovezi.</p>
              </div>
            </div>
            <label>
              Rolul fișierului
              <select
                value={role}
                onChange={(e) => setRole(e.target.value)}
                disabled={busy || (!!run && run.state !== "Created")}
              >
                <option value="offer">Ofertă comercială</option>
                <option value="specification">Specificație tehnică</option>
                <option value="policy">Politică / cerințe</option>
              </select>
            </label>
            <label className="upload-box">
              <FilePlus2 size={28} />
              <strong>Încarcă documente</strong>
              <span>JSON, TXT, MD, DOCX, PDF, PNG, JPEG · maximum 50 MiB</span>
              <input
                aria-label="Încarcă documente"
                type="file"
                multiple
                accept=".json,.txt,.md,.docx,.pdf,.png,.jpg,.jpeg"
                disabled={busy || (!!run && run.state !== "Created")}
                onChange={(e) => {
                  const files = Array.from(e.target.files ?? []);
                  e.target.value = "";
                  if (files.length) void perform(() => uploadFiles(files));
                }}
              />
            </label>
            <p className="fine">
              Modul demonstrativ normalizează ofertele JSON. PDF și imagini
              necesită Content Understanding; textul comercial liber necesită
              Foundry.
            </p>
            {run?.documents.map((d) => (
              <div className="document" key={d.id}>
                <FileText size={18} />
                <div>
                  <a
                    href={`/api/documents/${d.id}/content`}
                    target="_blank"
                    rel="noreferrer"
                  >
                    {d.fileName}
                  </a>
                  <small>
                    {d.state === "Ready" ? "Pregătit" : d.state}
                    {d.errorCode && ` · ${d.errorCode}`}
                  </small>
                </div>
                {d.state === "Failed" && (
                  <button
                    title="Reprocesează fișierul"
                    disabled={busy}
                    onClick={() =>
                      void perform(async () => {
                        await api.retryDocument(d.id);
                      })
                    }
                  >
                    <RotateCcw size={15} />
                  </button>
                )}
                <button
                  title="Șterge sursa"
                  disabled={busy}
                  onClick={() => {
                    if (
                      confirm(
                        "Ștergi sursa? Analizele asociate vor fi marcate ca neactuale.",
                      )
                    )
                      void perform(async () => {
                        await api.remove(d.id);
                        await refresh();
                      });
                  }}
                >
                  <Trash2 size={15} />
                </button>
              </div>
            ))}
            {run?.state === "Created" &&
              documents.filter((d) => !run.documents.some((x) => x.id === d.id))
                .length > 0 && (
                <label>
                  Adaugă o sursă existentă
                  <select
                    value=""
                    disabled={busy}
                    onChange={(e) => {
                      if (e.target.value)
                        void perform(async () => {
                          setRun(
                            await api.attach(
                              run.id,
                              e.target.value,
                              run.revision,
                              role,
                            ),
                          );
                        });
                    }}
                  >
                    <option value="">Alege din bibliotecă…</option>
                    {documents
                      .filter((d) => !run.documents.some((x) => x.id === d.id))
                      .map((d) => (
                        <option key={d.id} value={d.id}>
                          {d.fileName} · {d.state}
                        </option>
                      ))}
                  </select>
                </label>
              )}
          </section>
          <section className="panel results">
            <div className="section-title">
              <span>03</span>
              <div>
                <h2>Rulează analiza</h2>
                <p>Scorurile sunt calculate în .NET, explicate cu surse.</p>
              </div>
              <span
                className={`badge ${run?.state === "Completed" && !run.stale ? "green" : ""}`}
              >
                {run?.stale
                  ? "Surse modificate"
                  : run
                    ? stateLabel[run.state]
                    : "Nepornită"}
              </span>
            </div>
            <AgentProgress run={run} />
            <div className="run-actions">
              <button
                className="primary"
                disabled={
                  busy || !run || run.state !== "Created" || !ready || run.stale
                }
                onClick={() =>
                  void perform(async () => {
                    await api.start(run!.id, run!.revision);
                    await refresh();
                  })
                }
              >
                {busy || active ? (
                  <LoaderCircle className="spin" size={16} />
                ) : (
                  <Play size={16} />
                )}{" "}
                {active ? "Analiză în desfășurare" : "Rulează agenții"}
              </button>
              <span>
                {run
                  ? `${run.usedTokens.toLocaleString()} / ${run.budgetTokens.toLocaleString()} tokeni`
                  : "Buget: 15.000 tokeni / analiză"}
              </span>
            </div>
            {run?.errorCode && (
              <div className="warning">
                <strong>{stateLabel[run.state]}</strong>
                <p>{errorHint(run.errorCode)}</p>
                {run.state === "Failed" && !run.stale && (
                  <button
                    disabled={busy}
                    className="secondary"
                    onClick={() =>
                      void perform(async () => {
                        await api.retry(run.id, run.revision);
                        await refresh();
                      })
                    }
                  >
                    <RotateCcw size={15} /> Reia de la checkpoint
                  </button>
                )}
              </div>
            )}
            {run?.stale && (
              <div className="warning">
                O sursă a fost ștearsă. Rezultatul este istoric; creează o
                analiză nouă cu sursele actuale.
              </div>
            )}
            {!run?.offers.length && (
              <div className="empty">
                <Sparkles size={32} />
                <h3>O decizie începe cu dovezi.</h3>
                <p>
                  Definește necesarul, adaugă cel puțin o ofertă
                  <br />
                  și pornește analiza după procesarea fișierelor.
                </p>
                <div className="empty-grid">
                  <span>01 NECESAR</span>
                  <span>02 OFERTE</span>
                  <span>03 RECOMANDARE</span>
                </div>
              </div>
            )}
            {run && run.offers.length > 0 && (
              <>
                <div className="result-heading">
                  <h3>Matrice de comparație</h3>
                  <span>{run.offers.length} oferte</span>
                </div>
                <OfferComparison run={run} />
              </>
            )}
            {run?.recommendation && (
              <div className={`recommendation ${run.stale ? "stale" : ""}`}>
                <div className="recommendation-title">
                  <CheckCheck size={20} />
                  <span>
                    {run.stale
                      ? "RECOMANDARE ISTORICĂ"
                      : "RECOMANDARE CU SURSE"}
                  </span>
                </div>
                {run.recommendation.reasons.map((reason, i) => (
                  <div key={i}>
                    <p>{reason.text}</p>
                    <div className="sources">
                      {reason.sourceIds.map((id) => {
                        const source = run.sources.find(
                          (s) => s.sourceId === id,
                        );
                        return (
                          source && <SourceLink key={id} source={source} />
                        );
                      })}
                    </div>
                  </div>
                ))}
                <button
                  className="secondary"
                  disabled={busy || run.stale || run.state !== "Completed"}
                  onClick={() => void perform(exportReport)}
                >
                  <Download size={15} /> Exportă raportul JSON
                </button>
              </div>
            )}
            {run && run.sources.length > 0 && !run.stale && (
              <div className="chat">
                <h3>Întreabă sursele acestei analize</h3>
                <form
                  onSubmit={(e) => {
                    e.preventDefault();
                    void perform(async () => {
                      setChat(
                        await api.ask(run.id, question, chat?.conversationId),
                      );
                      setQuestion("");
                    });
                  }}
                >
                  <input
                    aria-label="Întrebare"
                    placeholder="Ce termene de livrare au ofertele?"
                    value={question}
                    maxLength={4000}
                    onChange={(e) => setQuestion(e.target.value)}
                  />
                  <button
                    aria-label="Trimite întrebarea"
                    className="primary"
                    disabled={busy || !question.trim()}
                  >
                    <ArrowRight size={18} />
                  </button>
                </form>
                {chat && (
                  <div className="chat-answer">
                    <p>{chat.answer}</p>
                    <div className="sources">
                      {chat.citations.map((s) => (
                        <SourceLink key={s.sourceId} source={s} />
                      ))}
                    </div>
                  </div>
                )}
              </div>
            )}
          </section>
        </div>
        <footer>
          <span>
            <ShieldCheck size={14} /> Fără conturi. Fără achiziții automate.
          </span>
          <span>ProcureAI · recomandări verificabile</span>
        </footer>
      </main>
    </div>
  );
}
