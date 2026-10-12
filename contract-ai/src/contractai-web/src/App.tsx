import { useEffect, useState } from "react";
import type { Version } from "./models/Version";
import type { VersionDetail } from "./models/VersionDetail";
import type { Comparison } from "./models/Comparison";
import type { Answer } from "./models/Answer";
import { ApiError, request, post, poll } from "./services/api";
import Sources from "./components/Sources";
export default function App() {
  const [versions, setVersions] = useState<Version[]>([]);
  const [selected, setSelected] = useState("");
  const [detail, setDetail] = useState<VersionDetail | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [contractKey, setContractKey] = useState("contract-demo");
  const [label, setLabel] = useState("V1");
  const [parent, setParent] = useState("");
  const [left, setLeft] = useState("");
  const [right, setRight] = useState("");
  const [comparison, setComparison] = useState<Comparison | null>(null);
  const [question, setQuestion] = useState("");
  const [answers, setAnswers] = useState<Answer[]>([]);
  const [busy, setBusy] = useState(false);
  const [progress, setProgress] = useState("");
  const [error, setError] = useState("");
  const refresh = async () =>
    setVersions(await request<Version[]>("/api/contracts/versions"));
  const load = async (id: string) => {
    setSelected(id);
    setAnswers([]);
    setDetail(await request<VersionDetail>(`/api/contracts/versions/${id}`));
  };
  const run = async (work: () => Promise<void>) => {
    setBusy(true);
    setError("");
    try {
      await work();
    } catch (e) {
      setError(
        e instanceof ApiError
          ? `${e.message} · ${e.code}${e.traceId ? ` · Trace: ${e.traceId}` : ""}`
          : String(e),
      );
    } finally {
      setBusy(false);
      setProgress("");
    }
  };
  useEffect(() => {
    void refresh().catch((e) => setError(String(e)));
  }, []);
  async function upload() {
    if (!file) return;
    if (file.size > 50 * 1024 * 1024)
      throw new Error("Fișierul depășește 50 MiB.");
    const data = new FormData();
    data.append("file", file);
    setProgress("Încărcare și extragere…");
    const uploaded = await request<{
      documentId: string;
      jobId: string | null;
    }>("/api/documents", { method: "POST", body: data });
    const version = await post<Version>("/api/contracts/versions", {
      documentId: uploaded.documentId,
      contractKey,
      versionLabel: label,
      parentVersionId: parent || null,
    });
    await refresh();
    await load(version.id);
    await poll<{ status: string; errorCode: string | null }>(
      `/api/documents/${uploaded.documentId}`,
      (d) => d.status,
      (d) => {
        if (d.errorCode) setError(d.errorCode);
      },
    );
    setProgress("Document extras. Poți porni analiza.");
    await load(version.id);
  }
  async function analyze() {
    if (!detail) return;
    setProgress("Analiza clauzelor și obligațiilor…");
    const job = await post<{ jobId: string }>(
      `/api/contracts/versions/${selected}/analyze`,
      { revision: detail.version.revision },
    );
    await poll<{ status: string; errorCode: string | null }>(
      `/api/jobs/${job.jobId}`,
      (j) => j.status,
      (j) => {
        if (j.errorCode) setError(j.errorCode);
      },
    );
    await load(selected);
    await refresh();
  }
  async function compare() {
    setProgress("Compararea versiunilor…");
    const result = await post<{ comparisonId: string }>(
      "/api/contracts/compare",
      { leftVersionId: left, rightVersionId: right },
    );
    const value = await poll<Comparison>(
      `/api/contracts/comparisons/${result.comparisonId}`,
      (c) => c.state,
    );
    setComparison(value);
  }
  async function ask() {
    const answer = await post<Answer>(
      `/api/contracts/versions/${selected}/questions`,
      {
        requestId: crypto.randomUUID(),
        question,
        conversationId: answers.at(-1)?.conversationId ?? null,
      },
    );
    setAnswers((a) => [...a, answer]);
    setQuestion("");
  }
  function download() {
    const url = URL.createObjectURL(
      new Blob([JSON.stringify({ detail, comparison, answers }, null, 2)], {
        type: "application/json",
      }),
    );
    const a = document.createElement("a");
    a.href = url;
    a.download = "contractai-report.json";
    a.click();
    URL.revokeObjectURL(url);
  }
  const ready =
    detail && ["Ready", "NeedsReview"].includes(detail.version.state);
  return (
    <>
      <header>
        <div className="brand">
          C<span>ContractAI</span>
        </div>
        <span className="header-note">
          ANALIZĂ CU DOVEZI · FĂRĂ VERDICT JURIDIC
        </span>
      </header>
      <main>
        <div className="intro">
          <p className="eyebrow">ASISTENT PENTRU CONTRACTE</p>
          <h1>
            Înțelege clauzele.
            <br />
            <em>Observă ce s-a schimbat.</em>
          </h1>
          <p>
            Încarcă versiuni, verifică obligațiile și compară modificările.
            Fiecare observație rămâne legată de textul original.
          </p>
        </div>
        {error && (
          <div role="alert" className="error">
            {error}
          </div>
        )}
        {busy && (
          <div role="status" className="status">
            {progress || "Se procesează…"}
          </div>
        )}
        <div className="workspace">
          <aside>
            <section>
              <p className="eyebrow">01 / DOCUMENTE</p>
              <h2>O versiune nouă</h2>
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  void run(upload);
                }}
              >
                <label>
                  Contract
                  <input
                    required
                    value={contractKey}
                    onChange={(e) => setContractKey(e.target.value)}
                    maxLength={100}
                  />
                </label>
                <div className="row">
                  <label>
                    Versiune
                    <input
                      required
                      value={label}
                      onChange={(e) => setLabel(e.target.value)}
                      maxLength={80}
                    />
                  </label>
                  <label>
                    Versiune anterioară
                    <select
                      value={parent}
                      onChange={(e) => setParent(e.target.value)}
                    >
                      <option value="">Fără</option>
                      {versions
                        .filter((v) => v.contractKey === contractKey)
                        .map((v) => (
                          <option key={v.id} value={v.id}>
                            {v.versionLabel}
                          </option>
                        ))}
                    </select>
                  </label>
                </div>
                <label className="upload">
                  Alege contractul
                  <input
                    type="file"
                    accept=".pdf,.docx,.png,.jpg,.jpeg,.txt,.md"
                    required
                    onChange={(e) => setFile(e.target.files?.[0] ?? null)}
                  />
                  <small>PDF, DOCX, scan sau text · maximum 50 MiB</small>
                </label>
                <button disabled={busy}>Încarcă versiunea ↗</button>
              </form>
            </section>
            <section>
              <p className="eyebrow">BIBLIOTECA LOCALĂ</p>
              <h2>Versiunile tale</h2>
              {versions.length === 0 && (
                <p className="muted">
                  Încarcă primul contract pentru a începe.
                </p>
              )}
              <div className="version-list">
                {versions.map((v) => (
                  <button
                    className={`version ${selected === v.id ? "active" : ""}`}
                    key={v.id}
                    disabled={busy}
                    onClick={() => void run(() => load(v.id))}
                  >
                    <span>
                      {v.contractKey}
                      <strong>{v.versionLabel}</strong>
                    </span>
                    <small>{v.state}</small>
                  </button>
                ))}
              </div>
            </section>
          </aside>
          <div className="results">
            <section>
              <div className="section-head">
                <div>
                  <p className="eyebrow">02 / ANALIZĂ</p>
                  <h2>
                    {detail
                      ? `${detail.version.contractKey} · ${detail.version.versionLabel}`
                      : "Clauze și obligații"}
                  </h2>
                </div>
                {detail && (
                  <span className="badge">{detail.version.state}</span>
                )}
              </div>
              {!detail ? (
                <div className="empty">
                  <span>§</span>
                  <h3>Contractul tău, în context.</h3>
                  <p>
                    Selectează o versiune pentru a vedea sursele și a porni
                    analiza.
                  </p>
                </div>
              ) : (
                <>
                  <div className="actions">
                    <button
                      disabled={
                        busy ||
                        ["Stale", "Analyzing"].includes(detail.version.state)
                      }
                      onClick={() => void run(analyze)}
                    >
                      Analizează contractul
                    </button>
                    <button
                      className="secondary"
                      disabled={busy}
                      onClick={() =>
                        void run(async () => {
                          await post(
                            `/api/documents/${detail.version.documentId}/retry`,
                            {},
                          );
                          await poll<{ status: string }>(
                            `/api/documents/${detail.version.documentId}`,
                            (d) => d.status,
                          );
                          await load(selected);
                        })
                      }
                    >
                      Reîncearcă extragerea
                    </button>
                    <button className="secondary" onClick={download}>
                      Export JSON
                    </button>
                    <button
                      className="danger"
                      disabled={busy || detail.version.state === "Stale"}
                      onClick={() =>
                        void run(async () => {
                          await request(
                            `/api/documents/${detail.version.documentId}`,
                            { method: "DELETE" },
                          );
                          setComparison(null);
                          await load(selected);
                          await refresh();
                        })
                      }
                    >
                      Șterge sursa
                    </button>
                  </div>
                  {detail.version.state === "Stale" && (
                    <div className="error">
                      Sursa a fost ștearsă. Acest rezultat este istoric și nu
                      mai reprezintă o analiză activă.
                    </div>
                  )}
                  {detail.version.summary && (
                    <p className="summary">{detail.version.summary}</p>
                  )}
                  <h3>Obligații și termene</h3>
                  {detail.obligations.length === 0 ? (
                    <p className="muted">Nicio obligație extrasă încă.</p>
                  ) : (
                    detail.obligations.map((o) => (
                      <article className="obligation" key={o.id}>
                        <strong>{o.partyText}</strong>
                        <p>{o.actionText}</p>
                        <span className="badge">
                          {o.relativeTerm ?? o.dueDate ?? "Termen necunoscut"}
                        </span>
                        {o.relativeTerm && (
                          <small>
                            Data absolută rămâne necunoscută fără evenimentul de
                            referință.
                          </small>
                        )}
                        <Sources
                          sources={JSON.parse(o.evidenceJson).flatMap(
                            (e: { clauseId: string; quote: string }) => {
                              const c = detail.citations.find(
                                (s) => s.clauseId === e.clauseId,
                              );
                              return c ? [{ ...c, excerpt: e.quote }] : [];
                            },
                          )}
                        />
                      </article>
                    ))
                  )}
                  {detail.findings.map((f) => (
                    <article className="finding" key={f.id}>
                      <span className="eyebrow">DE VERIFICAT · {f.code}</span>
                      <p>{f.explanation}</p>
                      <Sources
                        sources={JSON.parse(f.evidenceJson ?? "[]").flatMap(
                          (e: { clauseId: string; quote: string }) => {
                            const c = detail.citations.find(
                              (s) => s.clauseId === e.clauseId,
                            );
                            return c ? [{ ...c, excerpt: e.quote }] : [];
                          },
                        )}
                      />
                    </article>
                  ))}
                  {detail.unknowns.length > 0 && (
                    <details>
                      <summary>
                        Informații necunoscute ({detail.unknowns.length})
                      </summary>
                      <ul>
                        {detail.unknowns.map((u) => (
                          <li key={u}>{u}</li>
                        ))}
                      </ul>
                    </details>
                  )}
                  <details>
                    <summary>Clauze și surse ({detail.clauses.length})</summary>
                    <Sources sources={detail.citations} />
                  </details>
                </>
              )}
            </section>
            <section>
              <p className="eyebrow">03 / COMPARAȚIE</p>
              <h2>Două versiuni, diferențe clare</h2>
              <div className="row">
                <label>
                  Înainte
                  <select
                    value={left}
                    onChange={(e) => setLeft(e.target.value)}
                  >
                    <option value="">Selectează</option>
                    {versions.map((v) => (
                      <option key={v.id} value={v.id}>
                        {v.contractKey} · {v.versionLabel}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  După
                  <select
                    value={right}
                    onChange={(e) => setRight(e.target.value)}
                  >
                    <option value="">Selectează</option>
                    {versions.map((v) => (
                      <option key={v.id} value={v.id}>
                        {v.contractKey} · {v.versionLabel}
                      </option>
                    ))}
                  </select>
                </label>
              </div>
              <button
                disabled={busy || !left || !right || left === right}
                onClick={() => void run(compare)}
              >
                Compară versiunile
              </button>
              {comparison && (
                <>
                  <p className="summary">{comparison.result.summary}</p>
                  {comparison.result.pairs
                    ?.filter((p) => p.kind !== "unchanged")
                    .map((p, i) => (
                      <article className="diff" key={i}>
                        <span className="badge">{p.kind}</span>
                        <div className="row">
                          <blockquote className="before">
                            {p.beforeText ?? "—"}
                          </blockquote>
                          <blockquote className="after">
                            {p.afterText ?? "—"}
                          </blockquote>
                        </div>
                        <Sources
                          sources={
                            comparison.result.citations?.filter(
                              (c) =>
                                c.clauseId === p.leftClauseId ||
                                c.clauseId === p.rightClauseId,
                            ) ?? []
                          }
                        />
                      </article>
                    ))}
                  {comparison.result.findings?.map((f, i) => (
                    <article className="finding" key={i}>
                      <strong>{f.code}</strong>
                      <p>{f.explanation}</p>
                    </article>
                  ))}
                </>
              )}
            </section>
            <section>
              <p className="eyebrow">04 / ÎNTREBĂRI</p>
              <h2>Întreabă despre această versiune</h2>
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  void run(ask);
                }}
              >
                <label className="sr-only" htmlFor="question">
                  Întrebarea ta
                </label>
                <textarea
                  id="question"
                  value={question}
                  onChange={(e) => setQuestion(e.target.value)}
                  required
                  maxLength={4000}
                  placeholder="Care este termenul de plată?"
                />
                <button disabled={busy || !ready || !question.trim()}>
                  Trimite întrebarea ↗
                </button>
              </form>
              {answers.map((a) => (
                <article className="answer" key={a.messageId}>
                  <p>{a.answer}</p>
                  <Sources sources={a.citations} />
                  <small>
                    {a.deployment} · {a.inputTokens + a.outputTokens} tokeni
                  </small>
                </article>
              ))}
            </section>
          </div>
        </div>
        <footer>
          Observațiile necesită verificare umană. ContractAI nu oferă
          consultanță juridică. Originalele și credențialele rămân pe server.
        </footer>
      </main>
    </>
  );
}
