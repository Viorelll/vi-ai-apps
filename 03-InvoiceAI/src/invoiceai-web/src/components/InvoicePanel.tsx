import type { Invoice } from "../models/Invoice";
import FieldEditor from "./FieldEditor";
import StatusBadge from "./StatusBadge";
const labels: Record<string, string> = {
  vendorName: "Furnizor",
  invoiceNumber: "Număr factură",
  date: "Data",
  currency: "Moneda",
  subtotal: "Subtotal",
  tax: "Taxă",
  total: "Total",
};
export default function InvoicePanel({
  invoice,
  busy,
  onCorrect,
  onAnalyze,
  onValidate,
  onExport,
  onRetry,
}: {
  invoice: Invoice;
  busy: boolean;
  onCorrect: (path: string, value: unknown) => Promise<void>;
  onAnalyze: () => void;
  onValidate: () => void;
  onExport: () => void;
  onRetry: () => void;
}) {
  const editable = !["Uploaded", "Extracting", "Failed", "Stale"].includes(
    invoice.state,
  );
  const blocked = invoice.issues.some((x) => x.severity === "error");
  return (
    <section className="invoice-panel">
      <div className="panel-heading">
        <div>
          <p className="eyebrow">
            {invoice.kind === "receipt" ? "BON FISCAL" : "FACTURĂ"} · REVIZIA{" "}
            {invoice.revision}
          </p>
          <h2>{invoice.vendorName ?? "Document nou"}</h2>
        </div>
        <StatusBadge state={invoice.state} />
      </div>
      <div className="actions">
        {invoice.state === "Uploaded" && (
          <button disabled={busy} onClick={onAnalyze}>
            Procesează documentul
          </button>
        )}
        {invoice.state === "Failed" && (
          <button disabled={busy} onClick={onRetry}>
            Reîncearcă
          </button>
        )}
        <a
          className="button secondary"
          href={`/api/documents/${invoice.documentId}/content`}
          target="_blank"
          rel="noreferrer"
        >
          Deschide originalul ↗
        </a>
        <button
          className="secondary"
          disabled={busy || !editable}
          onClick={onValidate}
        >
          Revalidează
        </button>
        <button
          disabled={
            busy ||
            blocked ||
            !["Validated", "Exported"].includes(invoice.state)
          }
          onClick={onExport}
        >
          Descarcă JSON
        </button>
      </div>
      {invoice.state === "Extracting" && (
        <p role="status" className="notice">
          Extragere și verificare în curs. Rezultatul se actualizează automat.
        </p>
      )}
      {invoice.errorCode && (
        <p role="alert" className="error">
          {invoice.errorCode}
        </p>
      )}
      <div className="field-grid">
        {Object.entries(labels).map(([path, label]) => (
          <FieldEditor
            key={`${invoice.id}-${path}`}
            path={path}
            label={label}
            value={invoice[path as keyof Invoice]}
            field={invoice.fields.find((f) => f.path === path)}
            corrected={path in invoice.corrections}
            disabled={busy || !editable}
            onSave={onCorrect}
          />
        ))}
      </div>
      <h3>Linii extrase</h3>
      {invoice.lines.length === 0 ? (
        <p className="muted">Nicio linie disponibilă.</p>
      ) : (
        invoice.lines.map((l) => (
          <article key={l.ordinal} className="line">
            <h4>
              Linia {l.ordinal + 1} · pagina {l.pageNumber ?? "?"}
            </h4>
            <div className="field-grid">
              {(
                [
                  "description",
                  "quantity",
                  "unitPrice",
                  "amount",
                  "currency",
                ] as const
              ).map((p) => (
                <FieldEditor
                  key={p}
                  path={`lines.${l.ordinal}.${p}`}
                  label={
                    {
                      description: "Descriere",
                      quantity: "Cantitate",
                      unitPrice: "Preț unitar",
                      amount: "Sumă",
                      currency: "Moneda",
                    }[p]
                  }
                  value={l[p]}
                  field={invoice.fields.find(
                    (f) => f.path === `lines.${l.ordinal}.${p}`,
                  )}
                  corrected={`lines.${l.ordinal}.${p}` in invoice.corrections}
                  disabled={busy || !editable}
                  onSave={onCorrect}
                />
              ))}
            </div>
          </article>
        ))
      )}
      <h3>Verificări deterministe</h3>
      {invoice.issues.length ? (
        invoice.issues.map((x, n) => (
          <article className={`issue ${x.severity}`} key={n}>
            <strong>{x.code}</strong>
            <span>{x.path}</span>
            {x.detailsJson !== "{}" && <code>{x.detailsJson}</code>}
          </article>
        ))
      ) : (
        <p className="muted">
          {editable
            ? "Nu există probleme de validare."
            : "Verificările apar după procesare."}
        </p>
      )}
      {invoice.explanation && (
        <section className="explanation">
          <h3>Explicație AI</h3>
          <p>{invoice.explanation}</p>
          {invoice.suggestedCategory && (
            <p>Categorie sugerată: {invoice.suggestedCategory}</p>
          )}
          <small className="muted">
            {invoice.inputTokens} tokeni input · {invoice.outputTokens} tokeni
            output
          </small>
        </section>
      )}
    </section>
  );
}
