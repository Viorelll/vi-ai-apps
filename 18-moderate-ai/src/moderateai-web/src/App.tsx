import { useEffect, useState } from "react";
import * as api from "./api";
import { ItemResult, ItemSummary } from "./models";
export default function App() {
  const [mode, setMode] = useState<string>("Se conectează…"),
    [text, setText] = useState(""),
    [image, setImage] = useState<{ id: string; fileName: string } | null>(null),
    [result, setResult] = useState<ItemResult | null>(null),
    [history, setHistory] = useState<ItemSummary[]>([]),
    [queue, setQueue] = useState<ItemSummary[]>([]),
    [total, setTotal] = useState(0),
    [page, setPage] = useState(1),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false),
    [reveal, setReveal] = useState(false),
    [note, setNote] = useState(""),
    [confirm, setConfirm] = useState<"allow" | "block" | null>(null);
  async function refresh() {
    const [h, q] = await Promise.all([api.getHistory(), api.getQueue(page)]);
    setHistory(h.items);
    setQueue(q.items);
    setTotal(q.total);
  }
  useEffect(() => {
    let active = true;
    api
      .getHealth()
      .then((h) => {
        if (active) setMode(h.mode);
      })
      .catch((e) => {
        if (active) setError(String(e.message));
      });
    return () => {
      active = false;
    };
  }, []);
  useEffect(() => {
    refresh().catch((e) => setError(e.message));
  }, [page]);
  useEffect(() => {
    if (!result || result.state !== "Analyzing") return;
    let active = true;
    const timer = setInterval(() => {
      api
        .getItem(result.id)
        .then((r) => {
          if (active) {
            setResult(r);
            if (r.state !== "Analyzing")
              refresh().catch((e) => setError(e.message));
          }
        })
        .catch((e) => {
          if (active) setError(e.message);
        });
    }, 1000);
    return () => {
      active = false;
      clearInterval(timer);
    };
  }, [result?.id, result?.state]);
  async function action(fn: () => Promise<void>) {
    setBusy(true);
    setError("");
    try {
      await fn();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Operația a eșuat");
    } finally {
      setBusy(false);
    }
  }
  async function select(id: string) {
    setReveal(false);
    setConfirm(null);
    setNote("");
    setResult(await api.getItem(id));
  }
  async function submit() {
    const id = await api.createItem(text, image?.id ?? null);
    await api.analyze(id);
    await select(id);
    await refresh();
  }
  const stateLabel: Record<string, string> = {
    Submitted: "Trimis",
    Analyzing: "În analiză",
    Allowed: "Allow",
    Review: "Review",
    Blocked: "Block",
    Failed: "Eroare",
  };
  return (
    <div className="shell">
      <header>
        <div className="brand">
          <span className="brand-icon">M</span>
          <div>
            <h1>ModerateAI</h1>
            <p>Conținut analizat. Decizii explicabile.</p>
          </div>
        </div>
        <span className={mode === "Demo" ? "mode demo" : "mode"}>
          {mode === "Demo" ? "DEMO · fără AI real" : mode}
        </span>
      </header>
      <section className="notice">
        Politica locală decide. Modelul explică. Review-ul nu publică, nu șterge
        și nu sancționează conținut extern.
      </section>
      {mode === "Demo" && (
        <div className="demo-banner">
          Rezultatele sunt artificiale. Doar cele trei fixtures produc scoruri
          demonstrative; orice alt text sau imagine intră în review.
        </div>
      )}
      {error && (
        <div role="alert" className="error">
          {error}
          <button onClick={() => setError("")} aria-label="Închide eroarea">
            ×
          </button>
        </div>
      )}
      <main>
        <section className="panel input-panel">
          <div className="section-heading">
            <span className="eyebrow">01 / INTRARE</span>
            <h2>Trimite pentru analiză</h2>
          </div>
          <label htmlFor="text">Text</label>
          <textarea
            id="text"
            value={text}
            onChange={(e) => setText(e.target.value)}
            placeholder="Introdu textul de verificat…"
            rows={7}
            disabled={busy}
          />
          <small>
            {Array.from(text).length.toLocaleString()} / 10.000 caractere
            Unicode
          </small>
          {mode === "Demo" && (
            <div className="fixtures">
              {(
                [
                  ["allow", "0 · Allow"],
                  ["review", "2 · Review"],
                  ["block", "4 · Block"],
                ] as const
              ).map(([value, label]) => (
                <button
                  key={value}
                  disabled={busy}
                  onClick={() => {
                    setText(`[demo:${value}]`);
                    setImage(null);
                  }}
                >
                  {label}
                </button>
              ))}
            </div>
          )}
          <label className="upload">
            Imagine PNG / JPEG <span>Maximum 4 MiB · 50–7200 px</span>
            <input
              aria-label="Încarcă imagine"
              type="file"
              accept="image/png,image/jpeg"
              disabled={busy}
              onChange={(e) => {
                const file = e.target.files?.[0];
                if (file)
                  action(async () => {
                    if (file.size > 4 * 1024 * 1024)
                      throw new Error("Imaginea depășește 4 MiB");
                    setImage(await api.upload(file));
                  });
                e.target.value = "";
              }}
            />
          </label>
          {image && (
            <div className="file-chip">
              {image.fileName}
              <button
                disabled={busy}
                onClick={() => setImage(null)}
                aria-label="Detașează imaginea"
              >
                ×
              </button>
            </div>
          )}
          <button
            className="primary wide"
            disabled={
              busy ||
              (!text.trim() && !image) ||
              Array.from(text).length > 10000
            }
            onClick={() => action(submit)}
          >
            {busy ? "Se procesează…" : "Analizează conținutul →"}
          </button>
          <p className="fine-print">
            Inputul rămâne în laboratorul local. În modul Foundry, textul și
            imaginea sunt trimise către Azure Content Safety.
          </p>
        </section>
        <section className="panel result-panel">
          <div className="section-heading">
            <span className="eyebrow">02 / REZULTAT</span>
            <h2>Analiză și politică</h2>
          </div>
          {!result ? (
            <div className="empty">
              <span>◇</span>
              <h3>Începe cu un conținut</h3>
              <p>
                Vei vedea categoriile, scala providerului și decizia politicii.
              </p>
            </div>
          ) : (
            <>
              <div className="result-title">
                <span className={`status ${result.state.toLowerCase()}`}>
                  {stateLabel[result.state] ?? result.state}
                </span>
                <small>revizia {result.revision}</small>
              </div>
              <small className="mono">{result.id}</small>
              {result.stale && (
                <p role="alert" className="error">
                  Sursa a fost ștearsă. Acest rezultat este istoric și nu mai
                  este actual.
                </p>
              )}
              {result.state === "Analyzing" && (
                <p role="status" className="processing">
                  Analiza rulează în fundal. Rezultatul se actualizează automat.
                </p>
              )}
              {result.errorCode && (
                <div className="error">{result.errorCode}</div>
              )}
              {result.state === "Failed" && !result.stale && (
                <button
                  disabled={busy}
                  onClick={() =>
                    action(async () => {
                      await api.analyze(result.id);
                      await select(result.id);
                    })
                  }
                >
                  Reîncearcă analiza
                </button>
              )}
              {result.analysis && (
                <>
                  <div className="metadata">
                    <span>{result.analysis.provider}</span>
                    <span>API {result.analysis.apiVersion}</span>
                    <span>{result.analysis.outputScale}</span>
                  </div>
                  {!result.analysis.complete && (
                    <p className="warning">
                      Analiză incompletă. Un scor lipsă nu înseamnă zero.
                    </p>
                  )}
                  <div className="scores">
                    {result.analysis.scores.map((s) => (
                      <div key={s.category} className="score">
                        <span>{s.category}</span>
                        <div className="score-track">
                          <i
                            style={{
                              width: `${Math.max(0, Math.min(100, (s.severity / 6) * 100))}%`,
                            }}
                          />
                        </div>
                        <strong>{s.severity}</strong>
                      </div>
                    ))}
                  </div>
                </>
              )}
              {result.policy && (
                <div className="policy">
                  <strong>
                    Decizie .NET: {result.policy.decision.toUpperCase()}
                  </strong>
                  <small>{result.policy.policyVersion}</small>
                  <ul>
                    {result.policy.reasons.map((r) => (
                      <li key={r}>{r}</li>
                    ))}
                  </ul>
                </div>
              )}
              {result.explanation && (
                <div className="explanation">
                  <span className="eyebrow">EXPLICAȚIE</span>
                  <p>{result.explanation}</p>
                </div>
              )}
              {result.reviews.length > 0 && (
                <div className="review-history">
                  <h3>Review local</h3>
                  {result.reviews.map((r) => (
                    <p key={r.id}>
                      <strong>{r.decision.toUpperCase()}</strong> · revizia{" "}
                      {r.expectedRevision} ·{" "}
                      {new Date(r.createdAtUtc).toLocaleString()}
                      <br />
                      {r.note}
                    </p>
                  ))}
                </div>
              )}
              {!result.stale && (
                <button
                  className="text-button"
                  disabled={busy}
                  onClick={() =>
                    action(async () => {
                      if (!reveal)
                        setResult(await api.getItem(result.id, true));
                      setReveal(!reveal);
                    })
                  }
                >
                  {reveal ? "Ascunde inputul" : "Arată inputul explicit"}
                </button>
              )}
              {reveal && (
                <div className="original">
                  {result.text && <pre>{result.text}</pre>}
                  {result.imageDocumentId && (
                    <>
                      <img
                        src={`/api/documents/${result.imageDocumentId}/content`}
                        alt="Imagine trimisă pentru moderare"
                      />
                      <button
                        disabled={busy}
                        onClick={() =>
                          action(async () => {
                            if (
                              !window.confirm(
                                "Ștergi sursa? Analizele asociate vor fi marcate ca istorice.",
                              )
                            )
                              return;
                            await api.request(
                              `/api/documents/${result.imageDocumentId}`,
                              { method: "DELETE" },
                            );
                            await select(result.id);
                            await refresh();
                          })
                        }
                      >
                        Șterge sursa locală
                      </button>
                    </>
                  )}
                </div>
              )}
              {result.state === "Review" && !result.stale && (
                <div className="review-actions">
                  <label htmlFor="note">Notă de review (opțional)</label>
                  <textarea
                    id="note"
                    rows={2}
                    maxLength={1000}
                    value={note}
                    onChange={(e) => setNote(e.target.value)}
                  />
                  <div className="button-row">
                    <button disabled={busy} onClick={() => setConfirm("allow")}>
                      Allow local
                    </button>
                    <button
                      disabled={busy}
                      className="danger"
                      onClick={() => setConfirm("block")}
                    >
                      Block local
                    </button>
                  </div>
                  {confirm && (
                    <div role="dialog" aria-label="Confirmă review">
                      <p>
                        Confirmi decizia locală{" "}
                        <strong>{confirm.toUpperCase()}</strong> pentru revizia{" "}
                        {result.revision}?
                      </p>
                      <div className="button-row">
                        <button
                          className="primary"
                          disabled={busy}
                          onClick={() =>
                            action(async () => {
                              setResult(
                                await api.review(
                                  result.id,
                                  result.revision,
                                  confirm,
                                  note,
                                ),
                              );
                              setConfirm(null);
                              await refresh();
                            })
                          }
                        >
                          Confirmă {confirm}
                        </button>
                        <button onClick={() => setConfirm(null)}>
                          Anulează
                        </button>
                      </div>
                    </div>
                  )}
                </div>
              )}
              {result.analysis && (
                <div className="links">
                  <a href={`/api/moderation/items/${result.id}/export`}>
                    Export JSON ↓
                  </a>
                  <a href={`/api/moderation/items/${result.id}/raw`}>
                    Rezultat brut ↓
                  </a>
                  <small>
                    Tokens:{" "}
                    {result.analysis.inputTokens + result.analysis.outputTokens}
                  </small>
                </div>
              )}
            </>
          )}
        </section>
        <aside>
          <section className="panel">
            <span className="eyebrow">03 / REVIEW LOCAL</span>
            <h2>
              Coada de verificare <span className="count">{total}</span>
            </h2>
            {queue.length === 0 ? (
              <p className="muted">Niciun caz în așteptare.</p>
            ) : (
              queue.map((i) => (
                <button
                  className="item"
                  key={i.id}
                  onClick={() => action(() => select(i.id))}
                  disabled={busy}
                >
                  <span className="status review">Review</span>
                  <span className="mono">{i.id.slice(0, 8)}</span>
                  <small>rev. {i.revision}</small>
                </button>
              ))
            )}
            <div className="button-row">
              <button
                disabled={page === 1 || busy}
                onClick={() => setPage((p) => p - 1)}
              >
                ←
              </button>
              <small>Pagina {page}</small>
              <button
                disabled={page * 20 >= total || busy}
                onClick={() => setPage((p) => p + 1)}
              >
                →
              </button>
            </div>
          </section>
          <section className="panel history">
            <div className="section-heading">
              <span className="eyebrow">ISTORIC RECENT</span>
              <button
                aria-label="Actualizează istoricul"
                onClick={() => action(refresh)}
                disabled={busy}
              >
                ↻
              </button>
            </div>
            {history.length === 0 ? (
              <p className="muted">Analizele tale vor apărea aici.</p>
            ) : (
              history.map((i) => (
                <button
                  className="item"
                  key={i.id}
                  disabled={busy}
                  onClick={() => action(() => select(i.id))}
                >
                  <span className={`status ${i.state.toLowerCase()}`}>
                    {stateLabel[i.state] ?? i.state}
                  </span>
                  <span className="mono">{i.id.slice(0, 8)}</span>
                  {i.stale && <small>istoric</small>}
                </button>
              ))
            )}
          </section>
        </aside>
      </main>
      <footer>ModerateAI · Politică versionată · Fără acțiuni externe</footer>
    </div>
  );
}
