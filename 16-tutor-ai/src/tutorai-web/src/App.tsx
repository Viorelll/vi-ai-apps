import { useEffect, useState } from "react";
import { z } from "zod";
import {
  sessionSchema,
  sourceSchema,
  type Session,
  type Source,
} from "./models/contracts";
import { request } from "./services/api";
import Evidence from "./components/Evidence";
export default function App() {
  const [sources, setSources] = useState<Source[]>([]),
    [session, setSession] = useState<Session | null>(null),
    [topic, setTopic] = useState("RAG"),
    [level, setLevel] = useState(1),
    [question, setQuestion] = useState(
      "Explică acest concept și oferă un exemplu .NET.",
    ),
    [answer, setAnswer] = useState(""),
    [kind, setKind] = useState("single_choice"),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false),
    [requestId, setRequestId] = useState(() => crypto.randomUUID()),
    [uploadStatus, setUploadStatus] = useState(""),
    [documentId, setDocumentId] = useState(""),
    [officialUrl, setOfficialUrl] = useState(""),
    [sourceTitle, setSourceTitle] = useState("");
  const working =
    busy || session?.state === "Explaining" || session?.state === "Assessing";
  async function run(action: () => Promise<void>) {
    setBusy(true);
    setError("");
    try {
      await action();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Eroare necunoscută");
    } finally {
      setBusy(false);
    }
  }
  function receiveSession(updated: Session) {
    setSession((current) =>
      current?.id === updated.id && current.revision > updated.revision
        ? current
        : updated,
    );
  }
  useEffect(() => {
    const abort = new AbortController();
    request(
      "/api/lessons/sources",
      z.array(sourceSchema),
      undefined,
      abort.signal,
    )
      .then(setSources)
      .catch((e) => {
        if (!abort.signal.aborted) setError(String(e));
      });
    const id = localStorage.getItem("tutorai-session");
    if (id)
      request(
        `/api/tutor/sessions/${id}`,
        sessionSchema,
        undefined,
        abort.signal,
      )
        .then(receiveSession)
        .catch(() => localStorage.removeItem("tutorai-session"));
    return () => abort.abort();
  }, []);
  useEffect(() => {
    if (!session || !["Explaining", "Assessing"].includes(session.state))
      return;
    const abort = new AbortController();
    const sessionId = session.id;
    let timer: ReturnType<typeof setTimeout>;
    async function poll() {
      try {
        const updated = await request(
          `/api/tutor/sessions/${sessionId}`,
          sessionSchema,
          undefined,
          abort.signal,
        );
        if (!abort.signal.aborted) receiveSession(updated);
      } catch (e) {
        if (!abort.signal.aborted)
          setError(e instanceof Error ? e.message : String(e));
      }
      if (!abort.signal.aborted) timer = setTimeout(poll, 700);
    }
    timer = setTimeout(poll, 700);
    return () => {
      clearTimeout(timer);
      abort.abort();
    };
  }, [session?.id, session?.state]);
  useEffect(() => {
    setAnswer("");
    setRequestId(crypto.randomUUID());
  }, [session?.exercise?.id]);
  async function refresh(id: string) {
    receiveSession(await request(`/api/tutor/sessions/${id}`, sessionSchema));
  }
  async function start() {
    const s = await request("/api/tutor/sessions", sessionSchema, {
      topic,
      difficulty: level,
    });
    localStorage.setItem("tutorai-session", s.id);
    setSession(s);
  }
  async function queue(step: string, body: unknown) {
    if (!session) return;
    await request(
      `/api/tutor/sessions/${session.id}/${step}`,
      z.object({ jobId: z.string().uuid() }),
      body,
    );
    await refresh(session.id);
  }
  async function upload(file: File) {
    const body = new FormData();
    body.append("file", file);
    const r = await fetch("/api/documents", { method: "POST", body });
    const data = await r.json();
    if (!r.ok) throw new Error(data.detail ?? "Upload eșuat");
    setDocumentId(data.documentId);
    setUploadStatus("Extracție în curs…");
    for (let i = 0; i < 100; i++) {
      const metadata = await request(
        `/api/documents/${data.documentId}`,
        z.object({ status: z.string(), errorCode: z.string().nullable() }),
      );
      if (metadata.status === "Ready") {
        setUploadStatus(
          "Document extras. Adaugă URL-ul oficial pentru a-l utiliza ca sursă.",
        );
        return;
      }
      if (metadata.status === "Failed")
        throw new Error(metadata.errorCode ?? "Extracție eșuată");
      await new Promise((resolve) => setTimeout(resolve, 1000));
    }
    throw new Error("Procesarea continuă; verifică documentul mai târziu.");
  }
  return (
    <main>
      <header>
        <div className="logo">
          T<span>AI</span>
        </div>
        <div>
          <p className="eyebrow">LEARN / PRACTICE / UNDERSTAND</p>
          <h1>TutorAI</h1>
          <p className="subtitle">
            AI-103 și .NET, explicate cu surse oficiale.
          </p>
        </div>
        <span className="badge">Sesiune anonimă</span>
      </header>
      {session?.demoMode && (
        <p className="notice" role="status">
          Mod demonstrativ — fără inferență Azure. Configurează Foundry pentru
          tutorul AI real.
        </p>
      )}
      {error && (
        <p className="error" role="alert">
          {error}{" "}
          <button onClick={() => session && run(() => refresh(session.id))}>
            Reîncarcă sesiunea
          </button>
        </p>
      )}
      <div className="layout">
        <aside className="card">
          <span className="step">01 / ALEGE DIRECȚIA</span>
          <h2>Ce învățăm azi?</h2>
          <label>
            Topic
            <select
              value={topic}
              onChange={(e) => setTopic(e.target.value)}
              disabled={working}
            >
              {[...new Set(sources.map((s) => s.objective))].map((t) => (
                <option key={t}>{t}</option>
              ))}
            </select>
          </label>
          <label>
            Nivel inițial
            <select
              value={level}
              onChange={(e) => setLevel(Number(e.target.value))}
              disabled={working}
            >
              <option value={1}>1 · Fundamente</option>
              <option value={2}>2 · Aplicare</option>
              <option value={3}>3 · Aprofundare</option>
            </select>
          </label>
          <button
            className="primary"
            disabled={working || !sources.length}
            onClick={() => run(start)}
          >
            Începe o sesiune nouă →
          </button>
          <p className="muted">
            Nivelul se adaptează după ultimele două răspunsuri. Nu reprezintă o
            certificare.
          </p>
          <details>
            <summary>Adaugă material oficial</summary>
            <label>
              PDF / DOCX / imagine / text
              <input
                type="file"
                accept=".pdf,.docx,.png,.jpg,.jpeg,.txt,.md"
                disabled={working}
                onChange={(e) => {
                  const f = e.target.files?.[0];
                  if (f) run(() => upload(f));
                }}
              />
            </label>
            <p role="status">{uploadStatus}</p>
            <label>
              Titlu
              <input
                value={sourceTitle}
                onChange={(e) => setSourceTitle(e.target.value)}
              />
            </label>
            <label>
              URL Microsoft Learn
              <input
                type="url"
                value={officialUrl}
                onChange={(e) => setOfficialUrl(e.target.value)}
                placeholder="https://learn.microsoft.com/…"
              />
            </label>
            <button
              disabled={working || !documentId || !officialUrl || !sourceTitle}
              onClick={() =>
                run(async () => {
                  await request(
                    "/api/lessons/sources",
                    z.object({ id: z.string().uuid() }),
                    {
                      documentId,
                      officialUrl,
                      objective: topic,
                      title: sourceTitle,
                    },
                  );
                  setSources(
                    await request(
                      "/api/lessons/sources",
                      z.array(sourceSchema),
                    ),
                  );
                  setUploadStatus("Sursă adăugată.");
                })
              }
            >
              Înregistrează sursa
            </button>
            <p className="muted">
              URL-ul este proveniență declarată local, nu verificarea
              autenticității documentului. PDF/imaginile necesită Content
              Understanding.
            </p>
          </details>
        </aside>
        <section className="workspace">
          <div className="card">
            <div className="section-top">
              <span className="step">02 / ÎNȚELEGE CONCEPTUL</span>
              {session && (
                <span className="pill">
                  Nivel {session.difficulty} · {session.state}
                </span>
              )}
            </div>
            <h2>{session?.topic ?? "O idee clară. Un pas înainte."}</h2>
            <label>
              Întrebarea ta
              <textarea
                value={question}
                onChange={(e) => setQuestion(e.target.value)}
                maxLength={2000}
              />
            </label>
            <button
              disabled={!session || working || session.state === "Stale"}
              onClick={() =>
                run(() =>
                  queue("explain", { question, revision: session!.revision }),
                )
              }
            >
              Explică din surse
            </button>
            {working && (
              <p className="processing" role="status">
                Se procesează… rezultatul se salvează în sesiune.
              </p>
            )}
            {session?.state === "Failed" && (
              <button
                disabled={working}
                onClick={() =>
                  run(() => queue("retry", { revision: session.revision }))
                }
              >
                Reîncearcă operația
              </button>
            )}
            {session?.errorCode && <p className="error">{session.errorCode}</p>}
            {session?.explanation && (
              <article>
                <p className="explanation">{session.explanation.explanation}</p>
                <Evidence output={session.explanation} sources={sources} />
              </article>
            )}
          </div>
          <div className="card">
            <span className="step">03 / EXERSEAZĂ</span>
            <h2>Verifică ce ai înțeles</h2>
            <div className="inline">
              <select
                aria-label="Tip exercițiu"
                value={kind}
                onChange={(e) => setKind(e.target.value)}
              >
                <option value="single_choice">Alegere unică</option>
                <option value="open_answer">Răspuns deschis</option>
              </select>
              <button
                disabled={
                  !session ||
                  working ||
                  !["Open", "FeedbackReady", "Failed"].includes(session.state)
                }
                onClick={() =>
                  run(() =>
                    queue("exercises", { kind, revision: session!.revision }),
                  )
                }
              >
                Generează exercițiu
              </button>
            </div>
            {session?.exercise && (
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  run(async () => {
                    await request(
                      `/api/tutor/exercises/${session.exercise!.id}/attempts`,
                      z.object({ id: z.string().uuid() }),
                      { answer, requestId, revision: session.revision },
                    );
                    await refresh(session.id);
                  });
                }}
              >
                <h3>{session.exercise.prompt}</h3>
                {session.exercise.kind === "single_choice" ? (
                  <fieldset>
                    <legend>Alege răspunsul</legend>
                    {session.exercise.options.map((option) => (
                      <label className="option" key={option}>
                        <input
                          type="radio"
                          name="answer"
                          value={option}
                          checked={answer === option}
                          onChange={() => setAnswer(option)}
                          disabled={
                            working || session.state !== "ExerciseReady"
                          }
                        />
                        {option}
                      </label>
                    ))}
                  </fieldset>
                ) : (
                  <label>
                    Răspuns
                    <textarea
                      value={answer}
                      maxLength={8000}
                      onChange={(e) => setAnswer(e.target.value)}
                      disabled={working || session.state !== "ExerciseReady"}
                    />
                  </label>
                )}
                <button
                  className="primary"
                  disabled={
                    working ||
                    !answer.trim() ||
                    session.state !== "ExerciseReady"
                  }
                >
                  Trimite răspunsul
                </button>
              </form>
            )}
            {session?.feedback?.feedback && (
              <div className="feedback" role="status">
                <h3>
                  Feedback · {Math.round((session.feedback.score ?? 0) * 100)}%
                </h3>
                <p>{session.feedback.feedback.explanation}</p>
                {session.feedback.feedback.criteria.map((c) => (
                  <p key={c.criterion}>
                    <strong>{c.criterion}</strong> — {c.feedback}
                  </p>
                ))}
                <Evidence
                  output={session.feedback.feedback}
                  sources={sources}
                />
                <p>
                  Nivelul următor: <strong>{session.difficulty}</strong>
                </p>
              </div>
            )}
          </div>
        </section>
      </div>
      <footer>
        Surse oficiale, exemple de învățare. Fără exam dumps, conturi sau
        progres global.
      </footer>
    </main>
  );
}
