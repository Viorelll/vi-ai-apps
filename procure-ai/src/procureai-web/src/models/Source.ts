export type Source = {
  documentId: string;
  sourceId: string;
  fileName: string;
  generation: number;
  text: string;
  pageNumber: number | null;
};
