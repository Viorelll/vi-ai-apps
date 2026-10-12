export type Order = {
  reference: string;
  status: string;
  product: string;
  returnEligible: boolean;
};
export type Document = {
  id: string;
  fileName: string;
  status: string;
  isPolicy: boolean;
  errorCode: string | null;
  sizeBytes: number;
};
export type Citation = {
  label: string;
  documentId: string | null;
  fileName: string;
  excerpt: string;
  contentUrl: string | null;
  pageNumber: number | null;
  startMs: number | null;
};
export type Message = {
  id: string;
  role: string;
  text: string;
  stale: boolean;
  citations: Citation[];
  inputTokens: number;
  outputTokens: number;
  durationMs: number;
};
export type Proposal = {
  id: string;
  kind: string;
  argumentsJson: string;
  payloadHash: string;
  state: string;
  revision: number;
  expiresAtUtc: string;
};
export type Execution = { id: string; proposalId: string; resultJson: string };
export type Job = { id: string; status: string; errorCode: string | null };
export type SupportCase = {
  id: string;
  state: string;
  revision: number;
  orderReference: string | null;
  order: Order | null;
  messages: Message[];
  attachments: Document[];
  proposals: Proposal[];
  executions: Execution[];
  jobs: Job[];
};
export type UploadResult = {
  documentId: string;
  jobId: string | null;
  status: string;
  deduplicated: boolean;
};
