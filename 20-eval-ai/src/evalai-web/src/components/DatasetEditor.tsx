import { useState } from "react";
import { api } from "../services/api";
export default function DatasetEditor({
  onSaved,
  onError,
}: {
  onSaved: (id: string) => void;
  onError: (message: string) => void;
}) {
  const [name, setName] = useState("Dataset nou");
  const [version, setVersion] = useState("v1");
  const [busy, setBusy] = useState(false);
  const [cases, setCases] = useState(
    JSON.stringify(
      [
        {
          name: "Exemplu",
          input: { operation: "sum", values: [1, 2] },
          expected: { answer: 3 },
          evidenceIds: [],
        },
      ],
      null,
      2,
    ),
  );
  return (
    <details className="panel">
      <summary>Dataset nou · snapshot imutabil</summary>
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          try {
            const r = await api.createDataset({
              name,
              version,
              cases: JSON.parse(cases),
            });
            onSaved(r.id);
          } catch (err) {
            onError(err instanceof Error ? err.message : "Dataset invalid");
          } finally {
            setBusy(false);
          }
        }}
      >
        <div className="form-row">
          <label>
            Nume
            <input
              required
              value={name}
              onChange={(e) => setName(e.target.value)}
            />
          </label>
          <label>
            Versiune
            <input
              required
              value={version}
              onChange={(e) => setVersion(e.target.value)}
            />
          </label>
        </div>
        <label>
          Cazuri JSON · input / expected / evidenceIds
          <textarea
            className="code"
            rows={10}
            value={cases}
            onChange={(e) => setCases(e.target.value)}
          />
        </label>
        <p className="muted">
          evidenceIds sunt ID-urile chunk-urilor Ready. Expected rămâne ascuns
          candidatului. O versiune salvată nu se modifică.
        </p>
        <button disabled={busy}>Salvează dataset</button>
      </form>
    </details>
  );
}
