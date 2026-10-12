export interface Finding {
  id?: string;
  code: string;
  severity: string;
  explanation: string;
  evidenceJson?: string;
  evidence?: { clauseId: string; quote: string }[];
}
