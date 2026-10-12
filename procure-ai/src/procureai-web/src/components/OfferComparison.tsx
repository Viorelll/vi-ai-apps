import { Trophy } from "lucide-react";
import type { Run } from "../models";
export function OfferComparison({ run }: { run: Run }) {
  return (
    <div className="table-scroll">
      <table>
        <thead>
          <tr>
            <th>Furnizor</th>
            <th>Net</th>
            <th>Transport</th>
            <th>Total</th>
            <th>Termen</th>
            <th>Scor</th>
          </tr>
        </thead>
        <tbody>
          {run.offers.map((offer) => {
            const score = run.scores.find((s) => s.id === offer.id);
            const winner =
              !run.stale && run.recommendation?.recommendedOfferId === offer.id;
            return (
              <tr key={offer.id} className={winner ? "winner" : ""}>
                <td>
                  <strong>
                    {winner && <Trophy size={14} />} {offer.supplierLabel}
                  </strong>
                  <small>
                    {offer.currency ?? "Monedă lipsă"} · {offer.quantity ?? "?"}{" "}
                    {offer.unit ?? "unități"}
                  </small>
                </td>
                <td>{offer.net ?? "—"}</td>
                <td>{offer.shipping ?? "—"}</td>
                <td>{score?.cost ?? "—"}</td>
                <td>
                  {offer.leadDays === null ? "—" : `${offer.leadDays} zile`}
                </td>
                <td>
                  {score ? score.score.toFixed(4) : "Neeligibil / incomplet"}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
