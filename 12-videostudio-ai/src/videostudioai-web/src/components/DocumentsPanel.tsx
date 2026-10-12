import { useEffect, useState } from "react";
import { api } from "../services/api";
import type { Answer, Document } from "../models/schemas";
export function DocumentsPanel() {
  const [docs, setDocs] = useState<Document[]>([]),
    [selected, setSelected] = useState<string[]>([]),
    [question, setQuestion] = useState(""),
    [answer, setAnswer] = useState<Answer>(),
    [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  useEffect(() => {
    const c = new AbortController();
    let running = false;
    async function refresh() {
      if (running) return;
      running = true;
      try {
        setDocs(await api.documents(c.signal));
      } catch (e) {
        if (!c.signal.aborted) setError(String(e));
      } finally {
        running = false;
      }
    }
    void refresh();
    const t = setInterval(() => void refresh(), 3000);
    return () => {
      c.abort();
      clearInterval(t);
    };
  }, []);
  async function run(action: () => Promise<void>) {
    setBusy(true);
    setError("");
    try {
      await action();
      setDocs(await api.documents());
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <details className="documents">
      <summary>
        Surse și referințe <span className="muted">· opțional</span>
      </summary>
      <p className="muted">
        Text și PDF pentru întrebări cu surse. Imaginile necesită Foundry
        vision; analiza video și OCR sunt extensii opționale.
      </p>
      <label className="upload">
        + Adaugă referință
        <input
          type="file"
          accept=".txt,.pdf,.png,.jpg,.jpeg,.mp4"
          disabled={busy}
          onChange={(e) => {
            const f = e.target.files?.[0];
            if (f)
              void run(async () => {
                await api.upload(f);
              });
            e.target.value = "";
          }}
        />
      </label>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      <div className="document-list">
        {docs.map((d) => (
          <div className="document" key={d.id}>
            <label>
              <input
                type="checkbox"
                disabled={d.status !== "Ready"}
                checked={selected.includes(d.id)}
                onChange={(e) =>
                  setSelected(
                    e.target.checked
                      ? [...selected, d.id]
                      : selected.filter((x) => x !== d.id),
                  )
                }
              />
              <a href={d.contentUrl}>{d.fileName}</a>
            </label>
            <span className="pill">{d.status}</span>
            {d.errorCode && <span>{d.errorCode}</span>}
            {d.status === "Failed" && (
              <button
                disabled={busy}
                onClick={() =>
                  void run(async () => {
                    await api.retryDocument(d.id);
                  })
                }
              >
                Reîncearcă
              </button>
            )}
            <button
              className="text-button"
              disabled={busy || ["Uploaded", "Processing"].includes(d.status)}
              onClick={() =>
                void run(async () => {
                  await api.deleteDocument(d.id);
                  setSelected(selected.filter((x) => x !== d.id));
                  setAnswer(undefined);
                })
              }
            >
              Șterge
            </button>
          </div>
        ))}
      </div>
      <label>
        Întrebare despre sursele selectate
        <input
          value={question}
          maxLength={2000}
          onChange={(e) => setQuestion(e.target.value)}
          placeholder="Ce elemente trebuie să apară în videoclip?"
        />
      </label>
      <button
        className="secondary"
        disabled={busy || !question.trim() || !selected.length}
        onClick={() =>
          void run(async () => {
            setAnswer(
              await api.chat(question, selected, answer?.conversationId),
            );
          })
        }
      >
        Consultă sursele
      </button>
      {answer && (
        <article className="answer">
          <p>{answer.answer}</p>
          {answer.citations.map((c) => (
            <blockquote key={c.chunkId}>
              <a href={c.contentUrl}>
                {c.label} · {c.fileName}
              </a>
              <p>{c.excerpt}</p>
            </blockquote>
          ))}
        </article>
      )}
    </details>
  );
}
