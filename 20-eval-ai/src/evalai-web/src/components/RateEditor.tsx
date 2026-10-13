import { useState } from "react";
import { api } from "../services/api";
export default function RateEditor({
  deployments,
  onError,
}: {
  deployments: string[];
  onError: (m: string) => void;
}) {
  const [message, setMessage] = useState("");
  return (
    <details className="panel">
      <summary>Tarife versionate · cost text cunoscut</summary>
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          const d = new FormData(e.currentTarget);
          try {
            await api.rate({
              deploymentAlias: d.get("deployment"),
              rateVersion: d.get("version"),
              currency: d.get("currency"),
              inputPerMillion: Number(d.get("input")),
              outputPerMillion: Number(d.get("output")),
            });
            setMessage(
              "Tarif salvat. Va fi fixat în snapshot-ul următoarei rulări.",
            );
          } catch (err) {
            onError(err instanceof Error ? err.message : "Tarif invalid");
          }
        }}
      >
        <div className="form-row">
          <label>
            Deployment
            <select name="deployment">
              {deployments.map((d) => (
                <option key={d}>{d}</option>
              ))}
            </select>
          </label>
          <label>
            Versiune
            <input name="version" required placeholder="2026-10-v1" />
          </label>
          <label>
            Monedă
            <input
              name="currency"
              defaultValue="USD"
              pattern="[A-Z]{3}"
              required
            />
          </label>
        </div>
        <div className="form-row">
          <label>
            Input / milion tokeni
            <input
              name="input"
              type="number"
              min="0"
              step="0.00000001"
              required
            />
          </label>
          <label>
            Output / milion tokeni
            <input
              name="output"
              type="number"
              min="0"
              step="0.00000001"
              required
            />
          </label>
        </div>
        <button>Salvează tarif</button>
        <p role="status">{message}</p>
      </form>
    </details>
  );
}
