import { Check, Circle, LoaderCircle } from "lucide-react";
import type { Run } from "../models";
const roles = [
  ["needs", "Necesar"],
  ["suppliers", "Oferte"],
  ["compare", "Comparație"],
  ["recommend", "Recomandare"],
];
export function AgentProgress({ run }: { run?: Run }) {
  const active =
    run &&
    !["Created", "Completed", "Failed", "NeedsInput"].includes(run.state) &&
    !run.stale;
  return (
    <ol className="agents">
      {roles.map(([role, name], index) => {
        const step = run?.steps.find((s) => s.role === role);
        const working = active && !step && index === run!.steps.length;
        return (
          <li key={role} className={step ? "done" : working ? "working" : ""}>
            <span>
              {step ? (
                <Check size={18} />
              ) : working ? (
                <LoaderCircle className="spin" size={18} />
              ) : (
                <Circle size={18} />
              )}
            </span>
            <div>
              <strong>{name}</strong>
              <small>
                {step
                  ? `${step.inputTokens + step.outputTokens} tokeni`
                  : working
                    ? "În lucru"
                    : "În așteptare"}
              </small>
            </div>
          </li>
        );
      })}
    </ol>
  );
}
