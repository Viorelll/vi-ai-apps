import type { Reason } from "./Reason";
export type Recommendation = {
  recommendedOfferId: string;
  snapshotHash: string;
  reasons: Reason[];
  unknowns: string[];
};
