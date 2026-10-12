import { useState } from "react";
import SourceRegion from "./SourceRegion";
import type { Field } from "../models/Invoice";
const numberPaths = new Set([
  "subtotal",
  "tax",
  "total",
  "amount",
  "quantity",
  "unitPrice",
]);
export function parseCorrection(path: string, text: string): unknown {
  if (text.trim() === "") return null;
  if (numberPaths.has(path.split(".").at(-1)!)) {
    if (!/^-?\d+(\.\d+)?$/.test(text.trim()))
      throw new Error("Folosește punct zecimal, de exemplu 120.00.");
    const n = Number(text);
    if (!Number.isFinite(n)) throw new Error("Valoare numerică invalidă");
    return n;
  }
  return text.trim();
}
export default function FieldEditor({
  path,
  label,
  value,
  field,
  disabled,
  corrected,
  onSave,
}: {
  path: string;
  label: string;
  value: unknown;
  field?: Field;
  disabled: boolean;
  corrected: boolean;
  onSave: (path: string, value: unknown) => Promise<void>;
}) {
  const [editing, setEditing] = useState(false);
  const [text, setText] = useState("");
  const [error, setError] = useState("");
  return (
    <article className="field">
      <div className="field-label">
        {label}
        {corrected && <span className="tag">corectat</span>}
      </div>
      {editing ? (
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            setError("");
            try {
              await onSave(path, parseCorrection(path, text));
              setEditing(false);
            } catch (e) {
              setError(e instanceof Error ? e.message : "Eroare");
            }
          }}
        >
          <input
            aria-label={label}
            value={text}
            onChange={(e) => setText(e.target.value)}
            autoFocus
          />
          <button disabled={disabled}>Salvează</button>
          <button
            type="button"
            className="secondary"
            onClick={() => setEditing(false)}
          >
            Anulează
          </button>
          {error && <p role="alert">{error}</p>}
        </form>
      ) : (
        <div className="field-value">
          <strong>{value == null ? "Necunoscut" : String(value)}</strong>
          <button
            className="text-button"
            disabled={disabled}
            onClick={() => {
              setText(value == null ? "" : String(value));
              setEditing(true);
            }}
          >
            Corectează
          </button>
        </div>
      )}
      <small className="muted">
        Încredere:{" "}
        {field?.confidence == null
          ? "necunoscută"
          : `${Math.round(field.confidence * 100)}%`}
      </small>
      {field && (
        <details>
          <summary>Text extras și proveniență</summary>
          <p>{field.rawText || "Lipsă"}</p>
          {field.evidence.map((s) => (
            <div key={s.sourceId}>
              <a
                href={`/api/documents/${s.documentId}/content#page=${s.pageNumber ?? 1}`}
                target="_blank"
                rel="noreferrer"
              >
                Pagina {s.pageNumber ?? "?"}
              </a>
              <p>{s.text}</p>
              {s.regionJson && <SourceRegion json={s.regionJson} />}
            </div>
          ))}
        </details>
      )}
    </article>
  );
}
