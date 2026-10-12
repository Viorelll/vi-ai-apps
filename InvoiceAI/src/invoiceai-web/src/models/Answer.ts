export type Citation = {
  sourceId: string;
  documentId: string;
  fileName: string;
  pageNumber: number | null;
  excerpt: string;
  contentUrl: string;
};
export type Answer = {
  conversationId: string;
  answer: string;
  citations: Citation[];
  inputTokens: number;
  outputTokens: number;
};
