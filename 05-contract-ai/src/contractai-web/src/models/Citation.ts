export interface Citation {
  clauseId: string;
  versionId: string;
  documentId: string;
  generation: number;
  pageNumber: number | null;
  excerpt: string;
  url: string;
}
