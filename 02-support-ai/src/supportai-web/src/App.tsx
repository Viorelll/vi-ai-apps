import { useCallback, useEffect, useRef, useState } from "react";
import {
  Headphones,
  Plus,
  Send,
  Upload,
  FileText,
  ShieldCheck,
  MessageSquare,
} from "lucide-react";
import { api } from "./api";
import type { Document, Order, Proposal, SupportCase } from "./models/types";
import SourceCard from "./components/SourceCard";
import ProposalCard from "./components/ProposalCard";
const stateNames: Record<string, string> = {
  Open: "Deschis",
  WaitingForEvidence: "Așteaptă dovezi",
  Answered: "Răspuns primit",
  ActionProposed: "Acțiune propusă",
  Closed: "Închis",
};
export default function App() {
  const [orders, setOrders] = useState<Order[]>([]),
    [selected, setSelected] = useState("LAB-1001"),
    [current, setCurrent] = useState<SupportCase | null>(null),
    [documents, setDocuments] = useState<Document[]>([]),
    [text, setText] = useState(""),
    [busy, setBusy] = useState(false),
    [error, setError] = useState(""),
    [mode, setMode] = useState(""),
    [notice, setNotice] = useState("");
  const activeId = useRef<string | null>(null),
    pendingAttachment = useRef(new Map<string, string>()),
    requestId = useRef<string | null>(null);
  const processing =
    current?.jobs.some((x) =>
      ["Queued", "Running", "RetryWaiting"].includes(x.status),
    ) ?? false;
  const perform = async (action: () => Promise<void>) => {
    setBusy(true);
    setError("");
    try {
      await action();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Cererea a eșuat");
    } finally {
      setBusy(false);
    }
  };
  const refresh = useCallback(async (signal?: AbortSignal) => {
    const docs = await api.documents(signal);
    setDocuments(docs);
    for (const [id, caseId] of pendingAttachment.current) {
      const doc = docs.find((d) => d.id === id);
      if (doc?.status === "Ready") {
        await api.attach(caseId, id);
        pendingAttachment.current.delete(id);
      } else if (doc?.status === "Failed") {
        pendingAttachment.current.delete(id);
        setError(
          `Extragerea a eșuat: ${doc.errorCode}. Configurează providerul și folosește Reîncearcă.`,
        );
      }
    }
    const id = activeId.current;
    if (id) {
      const next = await api.load(id, signal);
      if (activeId.current === id) setCurrent(next);
    }
  }, []);
  useEffect(() => {
    const controller = new AbortController();
    api
      .orders()
      .then(setOrders)
      .catch((e) => setError(String(e)));
    api
      .config()
      .then((c) => setMode(c.aiMode))
      .catch(() => {});
    const saved = localStorage.getItem("supportai-case");
    if (saved) activeId.current = saved;
    let running = false;
    const tick = async () => {
      if (running) return;
      running = true;
      try {
        await refresh(controller.signal);
      } catch (e) {
        if (!controller.signal.aborted)
          setError(e instanceof Error ? e.message : "Conexiune indisponibilă");
      } finally {
        running = false;
      }
    };
    void tick();
    const timer = setInterval(() => void tick(), 1500);
    return () => {
      controller.abort();
      clearInterval(timer);
    };
  }, [refresh]);
  async function ensureCase() {
    if (activeId.current) return activeId.current;
    const item = await api.create(selected || null);
    activeId.current = item.id;
    localStorage.setItem("supportai-case", item.id);
    await refresh();
    return item.id;
  }
  async function newCase() {
    await perform(async () => {
      const item = await api.create(selected || null);
      activeId.current = item.id;
      localStorage.setItem("supportai-case", item.id);
      pendingAttachment.current.clear();
      requestId.current = null;
      setText("");
      setNotice("");
      await refresh();
    });
  }
  async function submit() {
    if (!text.trim()) return;
    await perform(async () => {
      const id = await ensureCase();
      requestId.current ??= crypto.randomUUID();
      await api.send(id, text, requestId.current);
      requestId.current = null;
      setText("");
      await refresh();
    });
  }
  async function upload(file: File) {
    await perform(async () => {
      if (file.size > 50 * 1024 * 1024)
        throw new Error("Fișierul depășește limita de 50 MiB.");
      const id = await ensureCase();
      const result = await api.upload(file);
      if (result.status === "Ready") await api.attach(id, result.documentId);
      else pendingAttachment.current.set(result.documentId, id);
      setNotice("Dovada va fi atașată cazului după extragere.");
      await refresh();
    });
  }
  async function confirm(p: Proposal, key: string) {
    await perform(async () => {
      await api.confirm(p, key);
      await refresh();
    });
  }
  const order = current?.order || orders.find((o) => o.reference === selected);
  return (
    <>
      <header className="topbar">
        <div className="brand">
          <span className="brand-icon">
            <Headphones size={23} />
          </span>
          <div>
            SupportAI<small>ASISTENT DE SUPORT</small>
          </div>
        </div>
        <span className="privacy">
          <ShieldCheck size={15} /> Laborator privat · fără conturi
        </span>
      </header>
      <main>
        <div className="page-heading">
          <div>
            <span className="eyebrow">UN CAZ. TOATE INFORMAȚIILE.</span>
            <h1>Hai să rezolvăm problema.</h1>
            <p>
              Descrie situația, adaugă dovezi și verifică răspunsul în sursele
              sale.
            </p>
          </div>
          <span className="mode badge">
            {mode === "Demo"
              ? "Mod demo · fără Azure"
              : mode === "Foundry"
                ? "Microsoft Foundry"
                : "Se conectează…"}
          </span>
        </div>
        {error && (
          <div role="alert" className="error">
            <strong>Nu am putut finaliza operația.</strong>
            <span>{error}</span>
            <button className="text-button" onClick={() => setError("")}>
              Închide
            </button>
          </div>
        )}
        <div className="workspace">
          <aside>
            <section className="panel">
              <div className="section-title">
                <h2>01 · Cazul tău</h2>
                <span className="badge">
                  {current ? stateNames[current.state] || current.state : "Nou"}
                </span>
              </div>
              <label htmlFor="order">Comandă de laborator</label>
              <select
                id="order"
                value={selected}
                onChange={(e) => setSelected(e.target.value)}
                disabled={busy}
              >
                <option value="">Fără comandă</option>
                {orders.map((o) => (
                  <option key={o.reference} value={o.reference}>
                    {o.reference} · {o.product}
                  </option>
                ))}
              </select>
              {order && (
                <div className="order-info">
                  <span>{order.reference}</span>
                  <strong>{order.product}</strong>
                  <span>{order.status}</span>
                  <small>
                    {order.returnEligible
                      ? "În intervalul demonstrativ de retur"
                      : "Returul nu este eligibil în acest moment"}
                  </small>
                </div>
              )}
              <button
                className="secondary full"
                disabled={busy}
                onClick={() => void newCase()}
              >
                <Plus size={16} />
                {current ? "Începe un caz nou" : "Creează cazul"}
              </button>
              {current && (
                <small className="case-id">
                  Caz #{current.id.slice(0, 8)} · comanda se fixează la creare
                </small>
              )}
            </section>
            <section className="panel">
              <div className="section-title">
                <h2>02 · Dovezi</h2>
                <FileText size={18} />
              </div>
              <label className="dropzone">
                <Upload size={25} />
                <strong>Adaugă un fișier</strong>
                <span>Text, PDF, imagini, audio sau video</span>
                <small>Maximum 50 MiB · media necesită Azure</small>
                <input
                  aria-label="Încarcă dovadă"
                  type="file"
                  accept=".txt,.md,.markdown,.pdf,.png,.jpg,.jpeg,.wav,.mp3,.mp4"
                  disabled={busy || processing}
                  onChange={(e) => {
                    const file = e.target.files?.[0];
                    if (file) void upload(file);
                    e.target.value = "";
                  }}
                />
              </label>
              {notice && (
                <p role="status" className="hint">
                  {notice}
                </p>
              )}
              <div className="file-list">
                {documents
                  .filter((d) => !d.isPolicy)
                  .map((doc) => (
                    <article key={doc.id}>
                      <strong>{doc.fileName}</strong>
                      <span
                        className={`file-status ${doc.status.toLowerCase()}`}
                      >
                        {doc.status}
                        {current?.attachments.some((d) => d.id === doc.id) &&
                          " · atașat"}
                      </span>
                      <div className="file-actions">
                        {doc.status === "Ready" && (
                          <>
                            <a
                              href={`/api/documents/${doc.id}/content`}
                              target="_blank"
                              rel="noreferrer"
                            >
                              Original ↗
                            </a>
                            {!current?.attachments.some(
                              (d) => d.id === doc.id,
                            ) && (
                              <button
                                disabled={busy || processing}
                                onClick={() =>
                                  void perform(async () => {
                                    const id = await ensureCase();
                                    await api.attach(id, doc.id);
                                    await refresh();
                                  })
                                }
                              >
                                Atașează
                              </button>
                            )}
                          </>
                        )}
                        {doc.status === "Failed" && (
                          <button
                            disabled={busy}
                            onClick={() =>
                              void perform(async () => {
                                await api.retry(doc.id);
                                if (activeId.current)
                                  pendingAttachment.current.set(
                                    doc.id,
                                    activeId.current,
                                  );
                                await refresh();
                              })
                            }
                          >
                            Reîncearcă
                          </button>
                        )}
                        <button
                          disabled={busy}
                          onClick={() =>
                            void perform(async () => {
                              await api.remove(doc.id);
                              pendingAttachment.current.delete(doc.id);
                              await refresh();
                            })
                          }
                        >
                          Șterge
                        </button>
                      </div>
                    </article>
                  ))}
              </div>
            </section>
            <p className="privacy-note">
              <ShieldCheck size={16} />
              Date artificiale. Cheile rămân pe server. Acțiunile se execută
              numai local.
            </p>
          </aside>
          <section className="panel conversation">
            <div className="section-title">
              <h2>03 · Conversația</h2>
              <span className="live-dot" />
              Politici și surse verificate
            </div>
            <div className="messages" aria-live="polite">
              {!current?.messages.length && (
                <div className="empty">
                  <span>
                    <MessageSquare size={32} />
                  </span>
                  <h3>Cu ce te putem ajuta?</h3>
                  <p>
                    Alege o comandă și spune-ne ce s-a întâmplat. Vom consulta
                    politica și dovezile atașate.
                  </p>
                  <button
                    className="suggestion"
                    onClick={() =>
                      setText(
                        "Ecranul este fisurat. Vreau să pregătesc un tichet de suport.",
                      )
                    }
                  >
                    „Ecranul este fisurat. Ce pot face?” ↗
                  </button>
                </div>
              )}
              {current?.messages.map((message) => (
                <article key={message.id} className={`message ${message.role}`}>
                  <div className="message-role">
                    {message.role === "assistant" ? "SupportAI" : "Tu"}
                    {message.stale && (
                      <span className="badge">
                        Surse modificate · răspuns vechi
                      </span>
                    )}
                  </div>
                  <p>{message.text}</p>
                  {message.citations.map((source) => (
                    <SourceCard key={source.label} source={source} />
                  ))}
                  {message.role === "assistant" && (
                    <small className="usage">
                      {message.inputTokens + message.outputTokens} tokeni ·{" "}
                      {message.durationMs} ms
                    </small>
                  )}
                </article>
              ))}
              {processing && (
                <p role="status" className="processing">
                  <span className="spinner" />
                  Consultăm comanda, politica și dovezile…
                </p>
              )}
              {current?.jobs
                .filter((j) => j.status === "Failed")
                .map((j) => (
                  <p key={j.id} className="error">
                    Procesarea a eșuat: {j.errorCode}. Corectează configurația
                    și trimite din nou mesajul.
                  </p>
                ))}
              {current?.proposals.map((p) => (
                <ProposalCard
                  key={p.id}
                  proposal={p}
                  execution={current.executions.find(
                    (e) => e.proposalId === p.id,
                  )}
                  busy={busy}
                  onConfirm={(proposal, key) => void confirm(proposal, key)}
                  onReject={(proposal) =>
                    void perform(async () => {
                      await api.reject(proposal.id);
                      await refresh();
                    })
                  }
                />
              ))}
            </div>
            <form
              className="composer"
              onSubmit={(e) => {
                e.preventDefault();
                void submit();
              }}
            >
              <label htmlFor="message">Descrie problema</label>
              <textarea
                id="message"
                rows={3}
                maxLength={8000}
                placeholder="De exemplu: produsul a sosit cu ecranul fisurat…"
                value={text}
                disabled={busy || processing}
                onChange={(e) => {
                  setText(e.target.value);
                  requestId.current = null;
                }}
              />
              <div>
                <small>
                  {text.length}/8000 · Verifică sursele înainte de confirmare.
                </small>
                <button
                  type="submit"
                  disabled={busy || processing || !text.trim()}
                >
                  <Send size={16} />
                  {processing ? "Se procesează" : "Trimite mesajul"}
                </button>
              </div>
            </form>
          </section>
        </div>
        <footer>
          SupportAI · Laborator de suport asistat de AI{" "}
          <span>Acțiuni locale, confirmate explicit.</span>
        </footer>
      </main>
    </>
  );
}
