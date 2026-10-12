export type Document = {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  status: string;
  errorCode?: string | null;
  createdAtUtc: string;
};
export type Citation = {
  label: string;
  documentId: string;
  chunkId: string;
  fileName: string;
  pageNumber: number | null;
  startMs: number | null;
  endMs: number | null;
  excerpt: string;
  contentUrl: string;
};
export type Answer = {
  conversationId: string;
  answer: string;
  citations: Citation[];
  inputTokens: number;
  outputTokens: number;
  durationMs: number;
};
async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const r = await fetch(url, init);
  if (!r.ok) {
    let message = `Cererea a eșuat (${r.status}).`;
    try {
      const p = await r.json();
      message = p.detail || p.error || message;
    } catch {}
    throw new Error(message);
  }
  return r.status === 204 ? (undefined as T) : r.json();
}
export const listDocuments = () => request<Document[]>("/api/documents");
export async function upload(file: File) {
  const body = new FormData();
  body.append("file", file);
  return request<{ documentId: string; status: string }>("/api/documents", {
    method: "POST",
    body,
  });
}
export const removeDocument = (id: string) =>
  request<void>(`/api/documents/${id}`, { method: "DELETE" });
export const retryDocument = (id: string) =>
  request<void>(`/api/documents/${id}/retry`, { method: "POST" });
export const ask = (question: string, conversationId?: string) =>
  request<Answer>("/api/chat", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ question, conversationId: conversationId ?? null }),
  });
