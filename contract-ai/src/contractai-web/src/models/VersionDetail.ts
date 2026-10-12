import type { Version } from "./Version";
import type { Clause } from "./Clause";
import type { Obligation } from "./Obligation";
import type { Finding } from "./Finding";
import type { Citation } from "./Citation";
export interface VersionDetail {
  version: Version;
  clauses: Clause[];
  obligations: Obligation[];
  findings: Finding[];
  unknowns: string[];
  citations: Citation[];
}
