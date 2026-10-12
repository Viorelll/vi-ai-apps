export type Evidence = {
  documentId: string;
  sourceId: string;
  pageNumber: number | null;
  text: string;
  regionJson: string | null;
};
export type Field = {
  path: string;
  rawText: string;
  value: unknown;
  confidence: number | null;
  evidence: Evidence[];
  correctedValue: unknown;
};
export type Line = {
  ordinal: number;
  description: string | null;
  quantity: number | null;
  unitPrice: number | null;
  amount: number | null;
  currency: string | null;
  pageNumber: number | null;
};
export type Issue = {
  code: string;
  severity: string;
  path: string;
  detailsJson: string;
};
export type Invoice = {
  id: string;
  documentId: string;
  kind: string;
  state: string;
  revision: number;
  vendorName: string | null;
  invoiceNumber: string | null;
  date: string | null;
  currency: string | null;
  subtotal: number | null;
  tax: number | null;
  total: number | null;
  explanation: string | null;
  suggestedCategory: string | null;
  errorCode: string | null;
  inputTokens: number;
  outputTokens: number;
  fields: Field[];
  lines: Line[];
  corrections: Record<string, unknown>;
  issues: Issue[];
};
