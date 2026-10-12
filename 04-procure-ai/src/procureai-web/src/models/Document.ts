export type Document = {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  state: string;
  errorCode: string | null;
};
