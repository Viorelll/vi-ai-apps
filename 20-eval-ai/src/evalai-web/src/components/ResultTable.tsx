import type { Report } from "../models/schemas";
export default function ResultTable({ report }: { report: Report }) {
  return (
    <>
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th>Caz / variantă</th>
              <th>Checks .NET</th>
              <th>Judge · 1–5</th>
              <th>Latență</th>
              <th>Usage candidat / judge</th>
              <th>Cost cunoscut</th>
              <th>Stare</th>
            </tr>
          </thead>
          <tbody>
            {report.results.map((r) => (
              <tr key={r.id}>
                <td>
                  <b>{r.caseName}</b>
                  <small>Varianta {r.variant}</small>
                </td>
                <td>
                  {r.checks ? (
                    <span className={r.checks.passed ? "pass" : "fail"}>
                      {r.checks.passed ? "PASS" : "FAIL"}
                    </span>
                  ) : (
                    "—"
                  )}
                  {r.checks?.errors.map((e) => (
                    <small key={e}>{e}</small>
                  ))}
                </td>
                <td>
                  {r.judge ? (
                    <>
                      <b>{r.judge.score}/5</b>
                      <small>{r.judge.reason}</small>
                    </>
                  ) : (
                    "—"
                  )}
                </td>
                <td>{r.candidateUsage ? `${r.candidateLatencyMs} ms` : "—"}</td>
                <td>
                  {r.candidateUsage
                    ? `${r.candidateUsage.input} in / ${r.candidateUsage.output} out`
                    : "—"}
                  <small>
                    {r.judgeUsage
                      ? `Judge: ${r.judgeUsage.input} in / ${r.judgeUsage.output} out`
                      : "Judge: —"}
                  </small>
                  {r.candidateUsage?.simulated && <small>Simulat</small>}
                </td>
                <td>
                  {r.candidateCost.amount === null
                    ? "Necunoscut"
                    : `${r.candidateCost.amount} ${r.candidateCost.currency}`}
                  <small>
                    Judge: {r.judgeCost.amount ?? "necunoscut"}{" "}
                    {r.judgeCost.currency}
                  </small>
                  {r.candidateCost.rateVersion && (
                    <small>Tarif {r.candidateCost.rateVersion}</small>
                  )}
                </td>
                <td>
                  <span className="badge">{r.state}</span>
                  {r.errorCode && <small className="fail">{r.errorCode}</small>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <details>
        <summary>Output-uri pe caz · JSON</summary>
        <pre>
          {JSON.stringify(
            report.results.map((r) => ({
              case: r.caseName,
              variant: r.variant,
              output: r.output,
            })),
            null,
            2,
          )}
        </pre>
      </details>
    </>
  );
}
