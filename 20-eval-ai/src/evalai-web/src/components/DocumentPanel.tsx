import { useEffect, useState } from "react";
import { api } from "../services/api";
import type { Document, Chunk, Chat } from "../models/schemas";
export default function DocumentPanel({
  onError,
}: {
  onError: (m: string) => void;
}) {
  const [docs, setDocs] = useState<Document[]>([]);
  const [selected, setSelected] = useState<string[]>([]);
  const [chunks, setChunks] = useState<Chunk[]>([]);
  const [question, setQuestion] = useState("");
  const [chat, setChat] = useState<Chat>();
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    const abort = new AbortController();
    let timer: ReturnType<typeof setTimeout>;
    const load = async () => {
      try {
        setDocs(await api.documents(abort.signal));
      } catch (e) {
        if (!abort.signal.aborted)
          onError(e instanceof Error ? e.message : "Documente indisponibile");
      }
      if (!abort.signal.aborted) timer = setTimeout(load, 2000);
    };
    void load();
    return () => {
      abort.abort();
      clearTimeout(timer);
    };
  }, [onError]);
  async function action(task: () => Promise<unknown>) {
    setBusy(true);
    try {
      await task();
      setDocs(await api.documents());
    } catch (e) {
      onError(e instanceof Error ? e.message : "Operație eșuată");
    } finally {
      setBusy(false);
    }
  }
  return (
    <details className="panel">
      <summary>Dovezi și documente · upload, surse, întrebări</summary>
      <p className="muted">
        TXT/DOCX se extrag local. PDF și imagini necesită Content Understanding
        configurat; nu simulăm extracția.
      </p>
      <label>
        Încarcă document
        <input
          aria-label="Încarcă document"
          type="file"
          accept=".txt,.md,.pdf,.docx,.png,.jpg,.jpeg"
          disabled={busy}
          onChange={(e) => {
            const f = e.target.files?.[0];
            if (f) void action(() => api.upload(f));
            e.target.value = "";
          }}
        />
      </label>
      <ul className="documents">
        {docs.map((d) => (
          <li key={d.id}>
            <label>
              <input
                type="checkbox"
                disabled={d.status !== "Ready"}
                checked={selected.includes(d.id)}
                onChange={(e) =>
                  setSelected(
                    e.target.checked
                      ? [...selected, d.id]
                      : selected.filter((id) => id !== d.id),
                  )
                }
              />
              {d.fileName}
            </label>
            <span className="badge">{d.status}</span>
            {d.errorCode && <small className="fail">{d.errorCode}</small>}
            <a href={`/api/documents/${d.id}/content`}>Original</a>
            <button
              className="ghost"
              disabled={d.status !== "Ready"}
              onClick={() =>
                void action(async () => setChunks(await api.chunks(d.id)))
              }
            >
              Chunk-uri
            </button>
            {d.status === "Failed" && (
              <button
                className="ghost"
                onClick={() => void action(() => api.retryDocument(d.id))}
              >
                Retry
              </button>
            )}
            <button
              className="ghost"
              onClick={() =>
                void action(async () => {
                  await api.deleteDocument(d.id);
                  setSelected((s) => s.filter((id) => id !== d.id));
                  setChunks([]);
                })
              }
            >
              Șterge
            </button>
          </li>
        ))}
      </ul>
      {chunks.map((c) => (
        <article className="chunk" key={c.id}>
          <code>{c.id}</code>
          <p>{c.text}</p>
          <a
            href={`${c.contentUrl}${c.pageNumber ? `#page=${c.pageNumber}` : ""}`}
          >
            Sursă {c.pageNumber ? `· pagina ${c.pageNumber}` : ""}
          </a>
        </article>
      ))}
      <form
        onSubmit={(e) => {
          e.preventDefault();
          void action(async () =>
            setChat(await api.chat(question, selected, chat?.conversationId)),
          );
        }}
      >
        <label>
          Întrebare pe sursele selectate
          <input
            value={question}
            onChange={(e) => setQuestion(e.target.value)}
            maxLength={2000}
          />
        </label>
        <button disabled={busy || selected.length === 0 || !question.trim()}>
          Întreabă
        </button>
      </form>
      {chat && (
        <article>
          <p>{chat.answer}</p>
          {chat.citations.map((c) => (
            <blockquote key={c.chunkId}>
              <a
                href={`/api/documents/${c.documentId}/content${c.pageNumber ? `#page=${c.pageNumber}` : ""}`}
              >
                {c.fileName}
              </a>
              <p>{c.text}</p>
            </blockquote>
          ))}
        </article>
      )}
    </details>
  );
}
