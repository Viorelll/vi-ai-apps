import { useState } from "react";
import type { Proposal, Execution } from "../models/types";
export default function ProposalCard({
  proposal,
  execution,
  busy,
  onConfirm,
  onReject,
}: {
  proposal: Proposal;
  execution?: Execution;
  busy: boolean;
  onConfirm: (p: Proposal, key: string) => void;
  onReject: (p: Proposal) => void;
}) {
  const [key] = useState(() => crypto.randomUUID());
  const args = JSON.parse(proposal.argumentsJson) as {
    Summary?: string;
    summary?: string;
    EvidenceLabels?: string[];
    evidenceLabels?: string[];
  };
  const expired = new Date(proposal.expiresAtUtc).getTime() <= Date.now();
  return (
    <article className="proposal">
      <div className="proposal-head">
        <strong>
          {proposal.kind === "return"
            ? "Propunere de retur"
            : "Propunere de tichet"}
        </strong>
        <span className="badge">{proposal.state}</span>
      </div>
      <p>{args.summary || args.Summary}</p>
      <small>
        Dovezi:{" "}
        {(args.evidenceLabels || args.EvidenceLabels || []).join(", ") ||
          "fără dovezi"}{" "}
        · Expiră: {new Date(proposal.expiresAtUtc).toLocaleTimeString("ro-RO")}
      </small>
      <p className="local-hint">
        Acțiune demonstrativă locală. Nu trimite date către un CRM și nu emite
        rambursări.
      </p>
      {execution ? (
        <div role="status" className="success">
          Executată o singură dată · ID {execution.id.slice(0, 8)}
          <details>
            <summary>Rezultat local</summary>
            <pre>
              {JSON.stringify(JSON.parse(execution.resultJson), null, 2)}
            </pre>
          </details>
        </div>
      ) : proposal.state === "Draft" ? (
        <div className="action-row">
          <button
            disabled={busy || expired}
            onClick={() => onConfirm(proposal, key)}
          >
            Confirmă {proposal.kind === "return" ? "returul" : "tichetul"} local
          </button>
          <button
            className="secondary"
            disabled={busy}
            onClick={() => onReject(proposal)}
          >
            Respinge
          </button>
          {expired && <span>Propunerea a expirat.</span>}
        </div>
      ) : null}
    </article>
  );
}
