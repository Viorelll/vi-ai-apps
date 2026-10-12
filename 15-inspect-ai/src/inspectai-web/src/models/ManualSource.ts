export type ManualSource = {
  chunkId: string;
  documentId: string;
  pageNumber: number | null;
  text: string;
  sha256: string;
  generation: number;
};
