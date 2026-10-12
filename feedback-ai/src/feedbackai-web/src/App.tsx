import { useMemo, useState } from "react";
import {
  Activity,
  ArrowDownToLine,
  ArrowRight,
  Check,
  ChevronDown,
  CircleHelp,
  Clock3,
  FileText,
  MessageSquareText,
  ShieldCheck,
  Sparkles,
  UploadCloud,
} from "lucide-react";
import {
  analyze,
  correct,
  createBatch,
  getBatch,
  importFile,
  type Batch,
} from "./api";
const seed = `Comanda mea întârzie de trei zile și curierul nu răspunde. Sunați-mă la +373 69123456.
Produsul a ajuns defect, sunt foarte dezamăgit.
Bună ziua, îmi puteți trimite mai multe informații despre garanție?`;
export default function App() {
  const [text, setText] = useState(seed);
  const [batch, setBatch] = useState<Batch>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [filter, setFilter] = useState("all");
  const [notice, setNotice] = useState("");
  const visible = useMemo(
    () =>
      batch?.items.filter((x) => filter === "all" || x.topic === filter) ?? [],
    [batch, filter],
  );
  async function run() {
    setBusy(true);
    setError("");
    try {
      let b = await createBatch(text.split(/\n+/).filter((x) => x.trim()));
      b = await analyze(b.id);
      setBatch(b);
    } catch (e) {
      setError(e instanceof Error ? e.message : "A apărut o eroare");
    } finally {
      setBusy(false);
    }
  }
  async function relabel(
    itemId: string,
    field: "topic" | "urgency",
    value: string,
  ) {
    if (!batch) return;
    try {
      await correct(itemId, batch.revision, field, value);
      setBatch(await getBatch(batch.id));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Nu s-a putut salva eticheta");
    }
  }

  async function upload(file: File) {
    setBusy(true);
    setError("");
    setNotice("");
    try {
      const imported = await importFile(file);
      setBatch(await analyze(imported.id));
      setNotice(`${file.name} a fost importat și analizat.`);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Importul a eșuat");
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="shell">
      <aside className="rail">
        <div className="brandmark">
          f<span>.</span>
        </div>
        <div className="rail-icons">
          <div className="rail-icon active">
            <Activity size={19} />
          </div>
          <div className="rail-icon">
            <FileText size={19} />
          </div>
          <div className="rail-icon">
            <MessageSquareText size={19} />
          </div>
        </div>
        <div className="rail-bottom">
          <CircleHelp size={19} />
          <div className="avatar">V</div>
        </div>
      </aside>
      <main className="main">
        <header className="topbar">
          <div className="crumb">
            Spațiu de lucru <span>/</span> <b>Analiza feedbackului</b>
          </div>
          <div className="top-actions">
            <span className="live">
              <i /> Mediu demo
            </span>
            <div className="avatar small">V</div>
          </div>
        </header>
        <section className="content">
          <div className="title-row">
            <div>
              <div className="eyebrow">
                <span className="eyebrow-line" /> INTELIGENȚĂ PENTRU EXPERIENȚA
                CLIENTULUI
              </div>
              <h1>Feedback, înțeles.</h1>
              <p className="subtitle">
                Transformă mesajele clienților în semnale clare și grupări
                verificabile.
              </p>
            </div>
            <button className="help-button">
              <CircleHelp size={16} /> Ghid rapid
            </button>
          </div>
          <div className="stat-grid">
            <div className="stat-card">
              <div className="stat-label">
                MESAJE ANALIZATE <MessageSquareText size={16} />
              </div>
              <div className="stat-value">
                {batch?.total ?? "—"}
                <small>{batch ? " în acest lot" : " Începe o analiză"}</small>
              </div>
              <div className="stat-foot">
                <span className="dot green" /> Date păstrate local
              </div>
            </div>
            <div className="stat-card">
              <div className="stat-label">
                SUBIECTE IDENTIFICATE <Activity size={16} />
              </div>
              <div className="stat-value">
                {batch ? Object.keys(batch.topics).length : "—"}
                <small> categorii active</small>
              </div>
              <div className="stat-foot">
                <span className="dot blue" /> Taxonomie fixă
              </div>
            </div>
            <div className="stat-card">
              <div className="stat-label">
                PROTECȚIA DATELOR <ShieldCheck size={16} />
              </div>
              <div className="stat-value">
                PII<small> mascat automat</small>
              </div>
              <div className="stat-foot">
                <span className="dot green" /> Înainte de procesarea AI
              </div>
            </div>
            <div className="stat-card accent">
              <div className="stat-label">
                STAREA ANALIZEI <Sparkles size={16} />
              </div>
              <div className="stat-value">
                {batch?.state === "Analyzed" ? "Finalizată" : "Pregătit"}
                <small />
              </div>
              <div className="stat-foot">
                <span className="dot green" />{" "}
                {batch?.items[0]?.provider ?? "Așteaptă mesajele tale"}
              </div>
            </div>
          </div>
          <div className="workspace-grid">
            <section className="panel import-panel">
              <div className="panel-heading">
                <div>
                  <div className="section-kicker">01 / INTRARE</div>
                  <h2>Adaugă feedback</h2>
                </div>
                <label className="pill upload-pill">
                  <UploadCloud size={12} />
                  IMPORTĂ CSV / JSON / TXT
                  <input
                    type="file"
                    accept=".csv,.json,.txt,text/csv,application/json,text/plain"
                    disabled={busy}
                    onChange={(event) => {
                      const file = event.currentTarget.files?.[0];
                      if (file) void upload(file);
                      event.currentTarget.value = "";
                    }}
                  />
                </label>
              </div>
              <p className="panel-copy">
                Lipește mesaje, câte unul pe rând. Numere de telefon și adrese
                de email sunt mascate înainte de clasificare.
              </p>
              <label className="field-label" htmlFor="feedback">
                MESAJ ELE CLIENTULUI
              </label>
              <textarea
                id="feedback"
                value={text}
                onChange={(e) => setText(e.target.value)}
                placeholder="Lipește feedback aici, un mesaj pe linie…"
              />
              <div className="input-meta">
                <span>
                  {text.split(/\n+/).filter((x) => x.trim()).length} mesaje
                  detectate
                </span>
                <button onClick={() => setText("")} className="text-button">
                  Șterge tot
                </button>
              </div>
              <button
                className="primary"
                disabled={busy || !text.trim()}
                onClick={run}
              >
                {busy ? (
                  <>
                    <span className="spinner" /> Se analizează…
                  </>
                ) : (
                  <>
                    Analizează feedback <ArrowRight size={16} />
                  </>
                )}
              </button>
              {error && (
                <div role="alert" className="error">
                  {error}
                </div>
              )}
              {notice && <div className="notice">{notice}</div>}
              <div className="privacy-note">
                <ShieldCheck size={16} />
                <span>
                  <b>Confidențialitate integrată</b>
                  <small>Textul original nu este trimis modelului AI.</small>
                </span>
                <Check size={16} className="check" />
              </div>
            </section>
            <section className="panel insight-panel">
              <div className="panel-heading">
                <div>
                  <div className="section-kicker">02 / VIZIUNE DE ANSAMBLU</div>
                  <h2>Semnalele din lot</h2>
                </div>
                <button
                  className="icon-button"
                  onClick={() =>
                    batch &&
                    window.open(
                      `/api/feedback/batches/${batch.id}/export`,
                      "_blank",
                    )
                  }
                  disabled={!batch}
                >
                  <ArrowDownToLine size={17} />
                </button>
              </div>
              <p className="panel-copy">
                Distribuția se calculează din toate mesajele analizate,
                indiferent de filtrul tabelului.
              </p>
              {batch ? (
                <div className="topic-list">
                  {Object.entries(batch.topics).map(([name, count]) => (
                    <div className="topic-row" key={name}>
                      <span className={`topic-dot ${name}`} />
                      <span className="topic-name">{name}</span>
                      <div className="bar-bg">
                        <div
                          className={`bar ${name}`}
                          style={{
                            width: `${batch.total ? (count / batch.total) * 100 : 0}%`,
                          }}
                        />
                      </div>
                      <b>{count}</b>
                      <small>{Math.round((count / batch.total) * 100)}%</small>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="empty-insight">
                  <div className="empty-chart">
                    <span />
                    <span />
                    <span />
                    <span />
                    <span />
                  </div>
                  <b>Imaginea de ansamblu apare aici</b>
                  <span>
                    Analizează primul lot pentru a vedea subiectele recurente.
                  </span>
                </div>
              )}
              <div className="insight-foot">
                <Clock3 size={14} />
                {batch ? "Ultima analiză · acum" : "Actualizare în timp real"}
                <span className="foot-right">Sursă: etichete active</span>
              </div>
            </section>
          </div>
          <section className="panel results-panel">
            <div className="results-heading">
              <div>
                <div className="section-kicker">03 / EXPLORARE</div>
                <h2>
                  Mesajele analizate{" "}
                  <span className="count">{batch?.total ?? 0}</span>
                </h2>
              </div>
              <div className="table-actions">
                <div className="filter-wrap">
                  <span>Subiect:</span>
                  <select
                    value={filter}
                    onChange={(e) => setFilter(e.target.value)}
                  >
                    <option value="all">Toate categoriile</option>
                    {[
                      "delivery",
                      "product",
                      "information",
                      "other",
                      "unknown",
                    ].map((x) => (
                      <option key={x}>{x}</option>
                    ))}
                  </select>
                  <ChevronDown size={13} />
                </div>
                <button
                  className="icon-button export"
                  disabled={!batch}
                  onClick={() =>
                    batch &&
                    window.open(
                      `/api/feedback/batches/${batch.id}/export`,
                      "_blank",
                    )
                  }
                >
                  <ArrowDownToLine size={16} />
                  <span>Exportă CSV</span>
                </button>
              </div>
            </div>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>MESAJ REDACTAT</th>
                    <th>SUBIECT</th>
                    <th>TON</th>
                    <th>URGENȚĂ</th>
                    <th>PROVENIENȚĂ</th>
                  </tr>
                </thead>
                <tbody>
                  {visible.map((item) => (
                    <tr key={item.id}>
                      <td>
                        <div className="message-cell">
                          <span className="row-id">
                            {String(item.ordinal + 1).padStart(2, "0")}
                          </span>
                          <span>{item.redactedText}</span>
                        </div>
                      </td>
                      <td>
                        <select
                          className={`tag topic-tag ${item.topic}`}
                          value={item.topic}
                          onChange={(e) =>
                            relabel(item.id, "topic", e.target.value)
                          }
                        >
                          {[
                            "delivery",
                            "product",
                            "information",
                            "other",
                            "unknown",
                          ].map((x) => (
                            <option key={x}>{x}</option>
                          ))}
                        </select>
                      </td>
                      <td>
                        <span className={`sentiment ${item.sentiment}`}>
                          {item.sentiment === "negative"
                            ? "Negativ"
                            : item.sentiment === "positive"
                              ? "Pozitiv"
                              : "Neutru"}
                        </span>
                      </td>
                      <td>
                        <select
                          className={`urgency ${item.urgency}`}
                          value={item.urgency}
                          onChange={(e) =>
                            relabel(item.id, "urgency", e.target.value)
                          }
                        >
                          {["normal", "high", "unknown"].map((x) => (
                            <option key={x}>{x}</option>
                          ))}
                        </select>
                      </td>
                      <td>
                        <span className="source-pill">
                          <Sparkles size={12} />
                          {item.provider}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {!batch && (
                <div className="table-empty">
                  <div className="empty-file">
                    <UploadCloud size={21} />
                  </div>
                  <b>Încă nu există rezultate</b>
                  <span>Adaugă câteva mesaje și pornește analiza.</span>
                </div>
              )}
              {batch && visible.length === 0 && (
                <div className="table-empty">
                  <b>Niciun mesaj în această categorie</b>
                </div>
              )}
            </div>
            <footer className="table-footer">
              <span>
                Afișează <b>{visible.length}</b> din <b>{batch?.total ?? 0}</b>{" "}
                mesaje
              </span>
              <span className="footer-safe">
                <ShieldCheck size={14} /> Numai text redactat · Fără contactare
                externă
              </span>
            </footer>
          </section>
          <footer className="page-footer">
            <span>
              FEEDBACKAI <i>·</i> LABORATOR DE ANALIZĂ
            </span>
            <span>
              Taxonomie v1.0 <i>·</i>{" "}
              <a
                href="https://learn.microsoft.com/azure/ai-foundry/"
                target="_blank"
                rel="noreferrer"
              >
                Microsoft Foundry
              </a>
            </span>
          </footer>
        </section>
      </main>
    </div>
  );
}
