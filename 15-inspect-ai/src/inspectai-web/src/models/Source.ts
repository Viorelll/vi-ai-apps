export type Source = {
  documentId: string;
  role: "photo" | "manual";
  fileName: string;
  status: string;
  errorCode: string | null;
  contentUrl: string;
  sha256: string;
};
