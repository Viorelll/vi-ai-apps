import { useCallback, useEffect, useState } from "react";
import {
  FlaskConical,
  Play,
  Download,
  ArrowRight,
  RefreshCw,
} from "lucide-react";
import { api } from "./services/api";
import type { Dataset, Variant, Report } from "./models/schemas";
import ResultTable from "./components/ResultTable";
import DatasetEditor from "./components/DatasetEditor";
import DocumentPanel from "./components/DocumentPanel";
import RateEditor from "./components/RateEditor";
const terminal = ["Completed", "Partial", "Failed", "Canceled"];
export default function App() {
  const [datasets, setDatasets] = useState<Dataset[]>([]);
  const [variants, setVariants] = useState<Variant[]>([]);
  const [mode, setMode] = useState("");
  const [datasetId, setDatasetId] = useState("");
  const [selected, setSelected] = useState<string[]>([]);
  const [budget, setBudget] = useState(15000);
  const [baseline, setBaseline] = useState("");
  const [runs, setRuns] = useState<
    { id: string; datasetId: string; state: string; stale: boolean }[]
  >([]);
  const [runId, setRunId] = useState("");
  const [report, setReport] = useState<Report>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const onError = useCallback((m: string) => setError(m), []);
  const refresh = useCallback(async (signal?: AbortSignal) => {
    const [d, v, r] = await Promise.all([
      api.datasets(signal),
      api.variants(signal),
      api.runs(signal),
    ]);
    setDatasets(d);
    setVariants(v.variants);
    setMode(v.mode);
    setRuns(r);
    setDatasetId((current) => current || d[0]?.id || "");
    setSelected((current) =>
      current.length ? current : v.variants.map((x) => x.id),
    );
  }, []);
  useEffect(() => {
    const a = new AbortController();
    void refresh(a.signal).catch((e) => {
      if (!a.signal.aborted) setError(e.message);
    });
    return () => a.abort();
  }, [refresh]);
  useEffect(() => {
    if (!runId) return;
    const a = new AbortController();
    let timer: ReturnType<typeof setTimeout>;
    const poll = async () => {
      try {
        const r = await api.report(runId, a.signal);
        setReport(r);
        if (!terminal.includes(r.state)) timer = setTimeout(poll, 800);
        else await refresh(a.signal);
      } catch (e) {
        if (!a.signal.aborted)
          setError(e instanceof Error ? e.message : "Rulare indisponibilă");
      }
    };
    void poll();
    return () => {
      a.abort();
      clearTimeout(timer);
    };
  }, [runId, refresh]);
  const running = !!report && !terminal.includes(report.state);
  async function run() {
    setBusy(true);
    setError("");
    try {
      const r = await api.run(datasetId, selected, baseline || null, budget);
      setReport(undefined);
      setRunId(r.id);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Rulare eșuată");
    } finally {
      setBusy(false);
    }
  }
  async function action(kind: "retry" | "cancel") {
    if (!report) return;
    setBusy(true);
    try {
      await api.action(report.id, kind, report.revision);
      const r = await api.report(report.id);
      setReport(r);
      setRunId("");
      setTimeout(() => setRunId(r.id), 0);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Operație eșuată");
    } finally {
      setBusy(false);
    }
  }
  return (
    <main>
      <header>
        <div className="brand">
          <span className="logo">
            <FlaskConical size={24} />
          </span>
          <b>EvalAI</b>
          <span className="muted">/ laborator de evaluare</span>
        </div>
        <span className="badge">{mode || "Conectare…"}</span>
      </header>
      <section className="intro">
        <span className="eyebrow">CALITATE MĂSURABILĂ</span>
        <h1>
          Aceleași cazuri.
          <br />
          <span>Rezultate comparabile.</span>
        </h1>
        <p>
          Compară variantele AI pe un snapshot fix. Checks .NET, judge, latență
          și usage — fiecare cu propria dovadă.
        </p>
      </section>
      {mode === "Demo" && (
        <aside className="notice">
          Mod demonstrativ explicit. Candidații, judge și tokenii sunt
          sintetici. Configurează Foundry pentru evaluări reale.
        </aside>
      )}
      {error && (
        <div className="error" role="alert">
          {error}
          <button className="ghost" onClick={() => setError("")}>
            Închide
          </button>
        </div>
      )}
      <section className="panel">
        <div className="section-heading">
          <h2>Configurează comparația</h2>
          <span className="muted">01 / Snapshot</span>
        </div>
        <div className="form-row">
          <label>
            Dataset
            <select
              aria-label="Dataset"
              value={datasetId}
              onChange={(e) => {
                setDatasetId(e.target.value);
                setBaseline("");
              }}
            >
              {datasets.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name} · {d.version} · {d.caseCount} cazuri
                </option>
              ))}
            </select>
          </label>
          <label>
            Baseline compatibil
            <select
              aria-label="Baseline"
              value={baseline}
              onChange={(e) => setBaseline(e.target.value)}
            >
              <option value="">Fără baseline</option>
              {runs
                .filter(
                  (r) =>
                    r.datasetId === datasetId &&
                    r.state === "Completed" &&
                    !r.stale,
                )
                .map((r) => (
                  <option key={r.id} value={r.id}>
                    {r.id.slice(0, 8)} · Completed
                  </option>
                ))}
            </select>
          </label>
          <label>
            Buget total tokeni
            <input
              aria-label="Buget tokeni"
              type="number"
              min="256"
              max="100000"
              value={budget}
              onChange={(e) => setBudget(Number(e.target.value))}
            />
          </label>
        </div>
        <div className="variant-grid">
          {variants.map((v) => (
            <label
              className={`variant ${selected.includes(v.id) ? "selected" : ""}`}
              key={v.id}
            >
              <input
                type="checkbox"
                checked={selected.includes(v.id)}
                onChange={(e) =>
                  setSelected(
                    e.target.checked
                      ? [...selected, v.id]
                      : selected.filter((id) => id !== v.id),
                  )
                }
              />
              <span>
                <b>Varianta {v.name}</b>
                <small>
                  {v.deploymentAlias} · {v.promptVersion}
                </small>
              </span>
            </label>
          ))}
        </div>
        <div className="run-bar">
          <span className="muted">
            Fără deploy automat. Lipsa tarifului înseamnă cost necunoscut.
          </span>
          <button
            className="primary"
            disabled={busy || running || !datasetId || selected.length === 0}
            onClick={() => void run()}
          >
            <Play size={16} />
            {busy ? "Se trimite…" : "Rulează comparația"}
            <ArrowRight size={16} />
          </button>
        </div>
      </section>
      <section className="panel">
        <div className="section-heading">
          <h2>Rezultate</h2>
          <label className="history">
            Rulări
            <select
              aria-label="Istoric rulări"
              value={runId}
              onChange={(e) => {
                setReport(undefined);
                setRunId(e.target.value);
              }}
            >
              <option value="">Selectează o rulare</option>
              {runs.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.id.slice(0, 8)} · {r.state}
                </option>
              ))}
            </select>
          </label>
        </div>
        {!report ? (
          <div className="empty">
            <FlaskConical size={32} />
            <h3>Pregătit pentru primul experiment</h3>
            <p>
              Dataset-ul seed are 3 cazuri. În Demo, A trece 3/3 și B trece 2/3.
            </p>
          </div>
        ) : (
          <>
            <div className="result-top">
              <span className="badge">
                {report.state}
                {report.stale ? " · STALE" : ""}
              </span>
              <span>
                {report.progress.completed}/{report.progress.total} cazuri ·{" "}
                {report.usedTokens}/{report.budgetTokens} tokeni
              </span>
              <div className="exports">
                {["json", "csv", "jsonl"].map((f) => (
                  <a
                    key={f}
                    href={`/api/evaluations/runs/${report.id}/export?format=${f}`}
                  >
                    <Download size={14} />
                    {f.toUpperCase()}
                  </a>
                ))}
              </div>
            </div>
            <progress
              value={report.progress.completed}
              max={report.progress.total}
            />
            {report.stale && (
              <p className="fail">
                Dovezile s-au schimbat. Acest rezultat nu mai reprezintă sursele
                active.
              </p>
            )}
            {report.errorCode && <p className="fail">{report.errorCode}</p>}
            <div className="summary-grid">
              {report.summaries.map((s) => (
                <article key={s.variantId}>
                  <span className="eyebrow">VARIANTA {s.variant}</span>
                  <h3>
                    {s.passed}
                    <span>/{s.total} checks</span>
                  </h3>
                  <p>
                    Judge {s.judgeMean === null ? "—" : s.judgeMean.toFixed(2)}{" "}
                    / 5 · {s.failed} erori
                  </p>
                  <small>
                    p50 {s.p50Ms} ms · p95 {s.p95Ms} ms
                  </small>
                  <p className={s.regression ? "fail" : "muted"}>
                    {s.regression === null
                      ? "Regresie: neevaluată"
                      : s.regression
                        ? "Regresie detectată"
                        : "Fără regresie deterministă"}
                    {s.checksDelta !== null ? ` · Δ ${s.checksDelta}` : ""}
                  </p>
                </article>
              ))}
            </div>
            <ResultTable report={report} />
            <p className="muted">{report.costNote}</p>
            {report.possibleRepeatedCloudCost && (
              <p className="notice">
                Un apel eșuat sau repetat poate avea usage neconfirmat și cost
                suplimentar.
              </p>
            )}
            <div className="actions">
              {["Failed", "Partial"].includes(report.state) && (
                <button disabled={busy} onClick={() => void action("retry")}>
                  <RefreshCw size={14} />
                  Retry cazuri eșuate
                </button>
              )}
              {running && (
                <button disabled={busy} onClick={() => void action("cancel")}>
                  Anulează
                </button>
              )}
            </div>
          </>
        )}
      </section>
      <DatasetEditor
        onSaved={(id) => {
          setDatasetId(id);
          void refresh().catch((e) => setError(e.message));
        }}
        onError={onError}
      />
      <DocumentPanel onError={onError} />
      <RateEditor
        deployments={[
          ...variants.map((v) => v.deploymentAlias),
          "evalai-judge",
        ]}
        onError={onError}
      />
      <footer>
        EvalAI · snapshot-uri versionate, rezultate explicite.
        <span>Seed mic ≠ diferență statistică demonstrată.</span>
      </footer>
    </main>
  );
}
