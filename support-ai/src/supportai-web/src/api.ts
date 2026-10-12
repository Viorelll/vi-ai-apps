import type {
  Document,
  Order,
  SupportCase,
  UploadResult,
  Proposal,
  Execution,
} from "./models/types";
export async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, init);
  if (!response.ok) {
    const error = await response.json().catch(() => ({}));
    throw new Error(
      `${error.code || error.title || "Cererea a eșuat"}${error.traceId ? ` · ${error.traceId}` : ""}`,
    );
  }
  if (
    response.status === 204 ||
    (response.status === 202 && !response.headers.get("content-type"))
  )
    return undefined as T;
  return response.json();
}
function post<T>(path: string, body: unknown): Promise<T> {
  return request(path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
}
export const api = {
  config: () => request<{ aiMode: string }>("/api/config"),
  orders: () => request<Order[]>("/api/support/orders"),
  documents: (signal?: AbortSignal) =>
    request<Document[]>("/api/documents", { signal }),
  create: (orderReference: string | null) =>
    post<SupportCase>("/api/support/cases", { orderReference }),
  load: (id: string, signal?: AbortSignal) =>
    request<SupportCase>(`/api/support/cases/${id}`, { signal }),
  send: (id: string, text: string, requestId: string) =>
    post<{ jobId: string }>(`/api/support/cases/${id}/messages`, {
      text,
      requestId,
    }),
  attach: (id: string, documentId: string) =>
    post<void>(`/api/support/cases/${id}/attachments`, { documentId }),
  upload: (file: File) => {
    const body = new FormData();
    body.append("file", file);
    return request<UploadResult>("/api/documents", { method: "POST", body });
  },
  confirm: (proposal: Proposal, idempotencyKey: string) =>
    post<Execution>(`/api/support/actions/${proposal.id}/confirm`, {
      revision: proposal.revision,
      payloadHash: proposal.payloadHash,
      idempotencyKey,
    }),
  reject: (id: string) => post<void>(`/api/support/actions/${id}/reject`, {}),
  retry: (id: string) => post<void>(`/api/documents/${id}/retry`, {}),
  remove: (id: string) =>
    request<void>(`/api/documents/${id}`, { method: "DELETE" }),
};
