import type { Source } from "./Source";
import type { Recommendation } from "./Recommendation";
import type { Score } from "./Score";
import type { Offer } from "./Offer";
import type { Document } from "./Document";
import type { Criteria } from "./Criteria";
import type { RunSummary } from "./RunSummary";
export type Run = RunSummary & {
  criteria: Criteria;
  errorCode: string | null;
  snapshotHash: string;
  budgetTokens: number;
  usedTokens: number;
  documents: Document[];
  steps: {
    role: string;
    state: string;
    deployment: string;
    inputTokens: number;
    outputTokens: number;
    durationMs: number;
  }[];
  offers: Offer[];
  scores: Score[];
  recommendation: Recommendation | null;
  sources: Source[];
};
