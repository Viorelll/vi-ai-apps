import { useCallback, useEffect, useState } from "react";
import * as api from "./api";
import type { Invoice } from "./models/Invoice";
import type { Document } from "./models/Document";
import UploadPanel from "./components/UploadPanel";
import InvoicePanel from "./components/InvoicePanel";
import ChatPanel from "./components/ChatPanel";
import StatusBadge from "./components/StatusBadge";
export default function App() {
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [documents, setDocuments] = useState<Document[]>([]);
  const [selected, setSelected] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [mode, setMode] = useState("");
  const invoice = invoices.find((i) => i.id === selected);
  const refresh = useCallback(async (signal?: AbortSignal) => {
    const [ii, dd] = await Promise.all([
      api.listInvoices(signal),
      api.listDocuments(signal),
    ]);
    setInvoices(ii);
    setDocuments(dd);
    setSelected((old) => (ii.some((i) => i.id === old) ? old : ii[0]?.id));
  }, []);
  useEffect(() => {
    const controller = new AbortController();
    let active = true;
    let timer: ReturnType<typeof setTimeout>;
    async function poll() {
      try {
        await refresh(controller.signal);
      } catch (e) {
        if (active)
          setError(e instanceof Error ? e.message : "Eroare de conectare");
      }
      if (active) timer = setTimeout(poll, 1500);
    }
    void poll();
    void api
      .request<{ mode: string }>("/api/config", { signal: controller.signal })
      .then((x) => {
        if (active) setMode(x.mode);
      })
      .catch(() => {});
    return () => {
      active = false;
      clearTimeout(timer);
      controller.abort();
    };
  }, [refresh]);
  async function act(action: () => Promise<unknown>) {
    setBusy(true);
    setError("");
    try {
      await action();
      await refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Operațiune eșuată");
      throw e;
    } finally {
      setBusy(false);
    }
  }
  function run(action: () => Promise<unknown>) {
    void act(action).catch(() => {});
  }
  return (
    <main>
      <header>
        <a className="brand" href="/">
          ▧ <span>InvoiceAI</span>
        </a>
        <span className="muted">Din document, în date verificate.</span>
      </header>
      {mode === "Demo" && (
        <div className="demo-banner">
          <strong>Mod demonstrativ</strong> · Acceptă numai fișierele seed din
          proiect. Pentru documente proprii configurează Azure și setează
          AI_MODE=Azure.
        </div>
      )}
      <div className="layout">
        <aside>
          <h2>Documente</h2>
          <UploadPanel
            busy={busy}
            onUpload={async (file, kind) => {
              try {
                await act(async () => {
                  const d = await api.upload(file);
                  const i = await api.createInvoice(d.documentId, kind);
                  setSelected(i.id);
                });
              } catch {
                /* global error */
              }
            }}
          />
          <nav aria-label="Facturi">
            {invoices.map((i) => (
              <button
                className={`document-card ${selected === i.id ? "selected" : ""}`}
                key={i.id}
                onClick={() => {
                  setSelected(i.id);
                  setError("");
                }}
              >
                <strong>
                  {documents.find((d) => d.id === i.documentId)?.fileName ??
                    i.invoiceNumber ??
                    "Document"}
                </strong>
                <StatusBadge state={i.state} />
                <small>{i.vendorName ?? "În așteptarea procesării"}</small>
              </button>
            ))}
          </nav>
          {documents
            .filter((d) => !invoices.some((i) => i.documentId === d.id))
            .map((d) => (
              <article className="orphan" key={d.id}>
                <p>
                  {d.fileName} · {d.status}
                </p>
                <button
                  disabled={busy}
                  onClick={() =>
                    run(async () => {
                      const i = await api.createInvoice(d.id, "invoice");
                      setSelected(i.id);
                    })
                  }
                >
                  Atașează ca factură
                </button>
                <button
                  className="secondary"
                  onClick={() => run(() => api.remove(d.id))}
                >
                  Șterge
                </button>
              </article>
            ))}
          <p className="muted">
            Exportul păstrează corecțiile și dovezile. Fără postare în ERP.
          </p>
        </aside>
        <div className="workspace">
          {error && (
            <p role="alert" className="error">
              {error}
            </p>
          )}
          {invoice ? (
            <>
              <InvoicePanel
                invoice={invoice}
                busy={busy}
                onCorrect={async (path, value) => {
                  await act(() =>
                    api.correct(invoice.id, invoice.revision, {
                      [path]: value,
                    }),
                  );
                }}
                onAnalyze={() => run(() => api.analyze(invoice.id))}
                onValidate={() => run(() => api.validate(invoice.id))}
                onExport={() => run(() => api.download(invoice.id))}
                onRetry={() => run(() => api.retry(invoice.documentId))}
              />
              <ChatPanel
                key={invoice.id}
                invoiceId={invoice.id}
                disabled={[
                  "Uploaded",
                  "Extracting",
                  "Failed",
                  "Stale",
                ].includes(invoice.state)}
              />
              <button
                className="danger"
                disabled={busy}
                onClick={() => {
                  if (
                    window.confirm(
                      "Ștergi originalul și îl excluzi din analize?",
                    )
                  )
                    run(() => api.remove(invoice.documentId));
                }}
              >
                Șterge documentul
              </button>
            </>
          ) : (
            <section className="empty">
              <span className="empty-icon">▧</span>
              <h1>
                Facturi clare.
                <br />
                Cifre verificate.
              </h1>
              <p>
                Încarcă o factură sau un bon, pornește procesarea și verifică
                rezultatul înainte de export.
              </p>
              <ol>
                <li>Încarcă documentul</li>
                <li>Verifică sumele și proveniența</li>
                <li>Corectează și descarcă JSON</li>
              </ol>
            </section>
          )}
        </div>
      </div>
    </main>
  );
}
