import { useEffect, useRef, useState } from "react";
import { ask } from "../api";
import type { Answer } from "../models/Answer";
export default function ChatPanel({
  invoiceId,
  disabled,
}: {
  invoiceId: string;
  disabled: boolean;
}) {
  const [question, setQuestion] = useState("");
  const [answer, setAnswer] = useState<Answer>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const abort = useRef<AbortController | null>(null);
  useEffect(() => () => abort.current?.abort(), []);
  return (
    <section className="chat">
      <h3>Întreabă despre acest document</h3>
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          if (busy || !question.trim()) return;
          setBusy(true);
          setError("");
          const ctrl = new AbortController();
          abort.current = ctrl;
          try {
            setAnswer(
              await ask(
                invoiceId,
                question,
                answer?.conversationId,
                ctrl.signal,
              ),
            );
            setQuestion("");
          } catch (e) {
            if (!ctrl.signal.aborted)
              setError(e instanceof Error ? e.message : "Eroare");
          } finally {
            if (!ctrl.signal.aborted) setBusy(false);
          }
        }}
      >
        <label htmlFor="question">Întrebare</label>
        <textarea
          id="question"
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          maxLength={2000}
          disabled={disabled || busy}
          placeholder="Ce sumă necesită verificare?"
        />
        <button disabled={disabled || busy || !question.trim()}>
          {busy ? "Se generează…" : "Întreabă"}
        </button>
      </form>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      {answer && (
        <div className="answer">
          <p>{answer.answer}</p>
          {answer.citations.map((c) => (
            <article key={c.sourceId}>
              <a href={c.contentUrl} target="_blank" rel="noreferrer">
                {c.fileName} · pagina {c.pageNumber ?? "?"}
              </a>
              <blockquote>{c.excerpt}</blockquote>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}
