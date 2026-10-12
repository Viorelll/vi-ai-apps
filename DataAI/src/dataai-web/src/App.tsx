import { useEffect, useMemo, useRef, useState } from "react";
import {
  Activity,
  ArrowDown,
  ArrowUpRight,
  BarChart3,
  ChevronDown,
  CircleHelp,
  Database,
  FileUp,
  LoaderCircle,
  Plus,
  Sparkles,
  Table2,
  UploadCloud,
} from "lucide-react";
import { api, type Analysis, type Dataset } from "./api";

const suggestions = [
  "Venituri pe categorie",
  "Cantități vândute pe zi",
  "Compară veniturile pe categorii",
];
const money = (value: number, currency: string) =>
  new Intl.NumberFormat("ro-RO", {
    style: "currency",
    currency,
    maximumFractionDigits: 2,
  }).format(value);

function App() {
  const [datasets, setDatasets] = useState<Dataset[]>([]);
  const [selected, setSelected] = useState("");
  const [question, setQuestion] = useState("Venituri pe categorie");
  const [analysis, setAnalysis] = useState<Analysis | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [importing, setImporting] = useState(false);
  const fileRef = useRef<HTMLInputElement>(null);

  const refresh = async () => {
    const items = await api.datasets();
    setDatasets(items);
    setSelected((value) => value || items[0]?.id || "");
  };
  useEffect(() => {
    refresh().catch(() =>
      setError("Nu s-a putut conecta la API. Verifică serviciile DataAI."),
    );
  }, []);
  useEffect(() => {
    if (selected) run("Venituri pe categorie");
  }, [selected]);

  async function run(value = question) {
    if (!selected || !value.trim()) return;
    setQuestion(value);
    setLoading(true);
    setError("");
    try {
      setAnalysis(await api.analyze(selected, value));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Analiza a eșuat.");
    } finally {
      setLoading(false);
    }
  }

  async function onImport(file?: File) {
    if (!file) return;
    setImporting(true);
    setError("");
    try {
      const imported = await api.importCsv(
        file,
        file.name.replace(/\.csv$/i, ""),
      );
      const items = await api.datasets();
      setDatasets(items);
      setSelected(imported.id);
      setAnalysis(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Importul a eșuat.");
    } finally {
      setImporting(false);
      if (fileRef.current) fileRef.current.value = "";
    }
  }

  const active = datasets.find((item) => item.id === selected);
  const max = Math.max(
    1,
    ...(analysis?.rows.map((row) => Number(row.value)) ?? []),
  );
  const chartPoints = useMemo(
    () =>
      analysis?.rows.map((row, index) => ({
        ...row,
        x: 54 + index * (480 / Math.max(analysis.rows.length - 1, 1)),
        y: 196 - (Number(row.value) / max) * 154,
      })) ?? [],
    [analysis, max],
  );

  return (
    <main className="shell">
      <aside className="rail">
        <div className="brand-mark">
          <Activity size={18} />
        </div>
        <div className="rail-separator" />
        <div className="rail-icon active">
          <BarChart3 size={18} />
        </div>
        <div className="rail-icon">
          <Database size={18} />
        </div>
        <div className="rail-bottom">
          <CircleHelp size={18} />
        </div>
      </aside>
      <section className="workspace">
        <header className="topbar">
          <div className="crumb">
            Spațiul de lucru <span>/</span> <strong>DataAI</strong>
          </div>
          <div className="top-actions">
            <span className="status-dot" /> Toate sistemele active{" "}
            <div className="avatar">V</div>
          </div>
        </header>
        <div className="content">
          <div className="page-heading">
            <div>
              <div className="eyebrow">
                <span>ANALIZĂ EXPLORATORIE</span>
                <span className="eyebrow-dot">·</span>
                <span>V1.0</span>
              </div>
              <h1>
                Înțelege datele
                <br />
                din companie.
              </h1>
              <p>
                Întreabă în limbaj natural. DataAI verifică fiecare rezultat
                direct în setul tău de date.
              </p>
            </div>
            <div className="heading-art">
              <div className="orbit orbit-one" />
              <div className="orbit orbit-two" />
              <div className="art-core">
                <Sparkles size={21} />
              </div>
              <span className="art-node node-a" />
              <span className="art-node node-b" />
              <span className="art-node node-c" />
            </div>
          </div>
          <section className="query-card">
            <div className="query-top">
              <div className="dataset-label">
                <div className="dataset-icon">
                  <Database size={17} />
                </div>
                <div>
                  <span>SET DE DATE ACTIV</span>
                  <div className="select-wrap">
                    <select
                      aria-label="Set de date"
                      value={selected}
                      onChange={(e) => {
                        setAnalysis(null);
                        setSelected(e.target.value);
                      }}
                    >
                      {datasets.map((d) => (
                        <option key={d.id} value={d.id}>
                          {d.name}
                        </option>
                      ))}
                    </select>
                    <ChevronDown size={14} />
                  </div>
                </div>
              </div>
              <div className="dataset-meta">
                <span>
                  <i className="green-dot" />
                  {active?.rowCount ?? "—"} rânduri
                </span>
                <span className="meta-divider" />
                <span>Revizia {active?.revision ?? "—"}</span>
                <button
                  className="text-button"
                  onClick={() => fileRef.current?.click()}
                  disabled={importing}
                >
                  {importing ? (
                    <LoaderCircle className="spin" size={15} />
                  ) : (
                    <Plus size={15} />
                  )}{" "}
                  Importă CSV
                </button>
                <input
                  ref={fileRef}
                  type="file"
                  accept=".csv,text/csv"
                  hidden
                  onChange={(e) => onImport(e.target.files?.[0])}
                />
              </div>
            </div>
            <div className="query-input-wrap">
              <Sparkles size={17} />
              <input
                aria-label="Întrebarea ta"
                value={question}
                onChange={(e) => setQuestion(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter") run();
                }}
                placeholder="Întreabă despre datele tale..."
              />
              <button
                className="run-button"
                onClick={() => run()}
                disabled={loading || !selected}
              >
                {loading ? (
                  <LoaderCircle className="spin" size={16} />
                ) : (
                  <>
                    Analizează <ArrowUpRight size={16} />
                  </>
                )}
              </button>
            </div>
            <div className="suggestions">
              <span>ÎNCEARCĂ</span>
              {suggestions.map((text) => (
                <button key={text} onClick={() => run(text)}>
                  {text}
                  <ArrowUpRight size={12} />
                </button>
              ))}
            </div>
          </section>
          {error && (
            <div className="error-banner" role="alert">
              {error}
            </div>
          )}
          <div className="results-heading">
            <div>
              <span className="section-kicker">REZULTATUL ANALIZEI</span>
              <h2>
                {loading
                  ? "Calculez rezultatele…"
                  : analysis
                    ? question
                    : "Rezultatele apar aici"}
              </h2>
            </div>
            {analysis && (
              <div className="result-meta">
                <span
                  className={
                    analysis.usedFoundry ? "mode foundry" : "mode demo"
                  }
                >
                  <i />
                  {analysis.usedFoundry ? "Microsoft Foundry" : "Mod demo"}
                </span>
                <span>Același rezultat verificat pentru tabel și grafic</span>
              </div>
            )}
          </div>
          {loading && (
            <div className="loading-card">
              <LoaderCircle className="spin" />
              <span>Interpretez întrebarea și verific datele...</span>
            </div>
          )}
          {analysis && !loading && (
            <>
              <div className="metric-strip">
                <div className="metric-card primary-metric">
                  <div className="metric-label">
                    TOTAL{" "}
                    {analysis.metric === "revenue" ? "VENITURI" : "CANTITATE"}
                    <span className="metric-icon">
                      <Activity size={16} />
                    </span>
                  </div>
                  <div className="metric-value">
                    {analysis.metric === "revenue"
                      ? money(analysis.total, analysis.currency)
                      : `${new Intl.NumberFormat("ro-RO").format(analysis.total)} buc.`}
                  </div>
                  <div className="metric-foot">
                    <span className="trend">
                      <ArrowUpRight size={14} /> Calculat determinist
                    </span>
                    <span>·</span>
                    <span>{analysis.rows.length} grupuri</span>
                  </div>
                </div>
                <div className="metric-card">
                  <div className="metric-label">CATEGORII ANALIZATE</div>
                  <div className="metric-value">
                    {analysis.rows.length.toString().padStart(2, "0")}
                  </div>
                  <div className="metric-foot">din setul de date activ</div>
                </div>
                <div className="metric-card">
                  <div className="metric-label">VERSIUNE DATE</div>
                  <div className="metric-value">
                    r{analysis.datasetRevision}
                  </div>
                  <div className="metric-foot">
                    Hash {analysis.resultHash.slice(0, 11)}
                  </div>
                </div>
              </div>
              <div className="visuals">
                <section className="panel chart-panel">
                  <div className="panel-heading">
                    <div>
                      <h3>Distribuția valorilor</h3>
                      <p>
                        {analysis.metric === "revenue"
                          ? `Valori în ${analysis.currency}`
                          : "Unități vândute"}{" "}
                        · grupare{" "}
                        {analysis.groupBy === "day" ? "pe zi" : "pe categorie"}
                      </p>
                    </div>
                    <button className="icon-button" aria-label="Detalii grafic">
                      <ChevronDown size={16} />
                    </button>
                  </div>
                  <div className="chart-wrap">
                    <div className="y-labels">
                      <span>{money(max, analysis.currency)}</span>
                      <span>{money(max * 0.5, analysis.currency)}</span>
                      <span>{money(0, analysis.currency)}</span>
                    </div>
                    <svg
                      className="chart"
                      viewBox="0 0 540 228"
                      role="img"
                      aria-label="Graficul valorilor calculate"
                    >
                      <defs>
                        <linearGradient id="fill" x1="0" x2="0" y1="0" y2="1">
                          <stop
                            offset="0%"
                            stopColor="#645bea"
                            stopOpacity=".19"
                          />
                          <stop
                            offset="100%"
                            stopColor="#645bea"
                            stopOpacity="0"
                          />
                        </linearGradient>
                      </defs>
                      {[42, 119, 196].map((y) => (
                        <line
                          key={y}
                          x1="45"
                          x2="530"
                          y1={y}
                          y2={y}
                          stroke="#eaedf1"
                          strokeDasharray="4 5"
                        />
                      ))}
                      {chartPoints.length > 1 && (
                        <polygon
                          points={`45,196 ${chartPoints.map((p) => `${p.x},${p.y}`).join(" ")} 530,196`}
                          fill="url(#fill)"
                        />
                      )}
                      {chartPoints.length > 1 && (
                        <polyline
                          points={chartPoints
                            .map((p) => `${p.x},${p.y}`)
                            .join(" ")}
                          fill="none"
                          stroke="#665ce8"
                          strokeWidth="2.5"
                          strokeLinejoin="round"
                          strokeLinecap="round"
                        />
                      )}
                      {chartPoints.map((p, i) => (
                        <g key={p.label}>
                          <circle
                            cx={p.x}
                            cy={p.y}
                            r="5"
                            fill="#fff"
                            stroke="#665ce8"
                            strokeWidth="2.5"
                          />
                          <text
                            x={p.x}
                            y="218"
                            textAnchor={
                              i === 0
                                ? "start"
                                : i === chartPoints.length - 1
                                  ? "end"
                                  : "middle"
                            }
                            className="chart-label"
                          >
                            {p.label.length > 12
                              ? `${p.label.slice(0, 10)}…`
                              : p.label}
                          </text>
                        </g>
                      ))}
                    </svg>
                  </div>
                </section>
                <section className="panel table-panel">
                  <div className="panel-heading">
                    <div>
                      <h3>
                        <Table2 size={16} /> Datele calculate
                      </h3>
                      <p>Rezultat determinist din aplicație</p>
                    </div>
                    <button className="icon-button" aria-label="Exportă tabel">
                      <ArrowDown size={16} />
                    </button>
                  </div>
                  <div className="table-scroll">
                    <table>
                      <thead>
                        <tr>
                          <th>
                            {analysis.groupBy === "day" ? "DATA" : "CATEGORIE"}
                          </th>
                          <th className="align-right">
                            {analysis.metric === "revenue"
                              ? `VALOARE (${analysis.currency})`
                              : "CANTITATE"}
                          </th>
                        </tr>
                      </thead>
                      <tbody>
                        {analysis.rows.map((row, i) => (
                          <tr key={row.label}>
                            <td>
                              <span className={`row-dot dot-${i % 4}`} />
                              {row.label}
                            </td>
                            <td className="align-right amount">
                              {analysis.metric === "revenue"
                                ? money(row.value, analysis.currency)
                                : new Intl.NumberFormat("ro-RO").format(
                                    row.value,
                                  )}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                      <tfoot>
                        <tr>
                          <td>Total</td>
                          <td className="align-right amount">
                            {analysis.metric === "revenue"
                              ? money(analysis.total, analysis.currency)
                              : new Intl.NumberFormat("ro-RO").format(
                                  analysis.total,
                                )}
                          </td>
                        </tr>
                      </tfoot>
                    </table>
                  </div>
                </section>
              </div>
              <div className="explanation">
                <div className="explanation-icon">
                  <Sparkles size={16} />
                </div>
                <div>
                  <span>INTERPRETARE DATAAI</span>
                  <p>{analysis.explanation}</p>
                </div>
                <span className="verified">✓ Rezultat verificat</span>
              </div>
            </>
          )}
          {!analysis && !loading && (
            <div className="empty-card">
              <div className="empty-icon">
                <UploadCloud size={22} />
              </div>
              <h3>Începe cu o întrebare</h3>
              <p>
                Alege un set de date, importă propriul CSV sau pornește cu
                datele demonstrative incluse.
              </p>
              <button onClick={() => fileRef.current?.click()}>
                <FileUp size={15} /> Încarcă un CSV
              </button>
            </div>
          )}
          <footer>
            <span>
              DATAAI <span className="footer-dot">·</span> ANALITICĂ PENTRU
              COMPANII
            </span>
            <span>
              Datele rămân în mediul tău securizat{" "}
              <span className="lock">⌑</span>
            </span>
          </footer>
        </div>
      </section>
    </main>
  );
}

export default App;
