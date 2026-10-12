import type { Citation } from "./Citation";
import type { Finding } from "./Finding";
export interface Comparison {
  id: string;
  state: string;
  result: {
    summary?: string;
    findings?: Finding[];
    pairs?: {
      kind: string;
      beforeText: string | null;
      afterText: string | null;
      leftClauseId: string | null;
      rightClauseId: string | null;
    }[];
    citations?: Citation[];
  };
}
