export type Finding = {
  id: string;
  documentId: string;
  kind: "visible" | "inferred" | "unknown";
  description: string;
  reason: string | null;
  regionJson: string | null;
};
