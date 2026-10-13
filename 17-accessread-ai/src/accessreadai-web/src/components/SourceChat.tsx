import { useState } from "react";
import { api } from "../api";
import type { Answer } from "../models/reading";
export function SourceChat({ documentId }: { documentId: string }) {
  const [question, setQuestion] = useState("");
  const [answer, setAnswer] = useState<Answer>();
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  return (
    <section className="chat" aria-labelledby="chat-title">
      <h2 id="chat-title">Întreabă despre sursă</h2>
      <p>
        Răspunsurile folosesc numai documentul selectat și includ fragmentele
        citate.
      </p>
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          setError("");
          try {
            setAnswer(
              await api.ask(question, [documentId], answer?.conversationId),
            );
          } catch (error) {
            setError(error instanceof Error ? error.message : "request_failed");
          } finally {
            setBusy(false);
          }
        }}
      >
        <label htmlFor="question">Întrebarea ta</label>
        <div className="question-row">
          <input
            id="question"
            value={question}
            maxLength={2000}
            onChange={(e) => setQuestion(e.target.value)}
            required
          />
          <button disabled={busy}>{busy ? "Caută…" : "Întreabă"}</button>
        </div>
      </form>
      {error && <p role="alert">{error}</p>}
      {answer && (
        <div aria-live="polite">
          <p>{answer.answer}</p>
          {answer.citations.map((source) => (
            <blockquote key={source.label}>
              <p>{source.excerpt}</p>
              {source.contentUrl && (
                <a href={source.contentUrl}>
                  {source.label} · Pagina {source.pageNumber}
                </a>
              )}
            </blockquote>
          ))}
        </div>
      )}
    </section>
  );
}
