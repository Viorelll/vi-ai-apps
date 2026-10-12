import type { Invoice } from "./models/Invoice";
import type { Document } from "./models/Document";
import type { Answer } from "./models/Answer";
export class ApiError extends Error {
  constructor(
    public code: string,
    public status: number,
    public traceId?: string,
  ) {
    super(code + (traceId ? " · " + traceId : ""));
  }
}
export async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const r = await fetch(url, init);
  if (!r.ok) {
    const p = await r.json().catch(() => ({}));
    throw new ApiError(
      p.code ?? p.title ?? `HTTP_${r.status}`,
      r.status,
      p.traceId,
    );
  }
  if (r.status === 204 || r.headers.get("content-length") === "0")
    return undefined as T;
  return r.json();
}
const json = (method: string, body?: unknown): RequestInit => ({
  method,
  headers: { "Content-Type": "application/json" },
  body: body === undefined ? undefined : JSON.stringify(body),
});
export const listInvoices = (signal?: AbortSignal) =>
  request<Invoice[]>("/api/invoices", { signal });
export const listDocuments = (signal?: AbortSignal) =>
  request<Document[]>("/api/documents", { signal });
export async function upload(file: File) {
  const body = new FormData();
  body.append("file", file);
  return request<{ documentId: string; duplicate: boolean }>("/api/documents", {
    method: "POST",
    body,
  });
}
export const createInvoice = (documentId: string, kind: string) =>
  request<Invoice>("/api/invoices", json("POST", { documentId, kind }));
export const analyze = (id: string) =>
  request<{ jobId: string }>(`/api/invoices/${id}/analyze`, json("POST"));
export const correct = (
  id: string,
  revision: number,
  corrections: Record<string, unknown>,
) =>
  request<Invoice>(
    `/api/invoices/${id}/fields`,
    json("PATCH", { revision, corrections }),
  );
export const validate = (id: string) =>
  request<Invoice>(`/api/invoices/${id}/validate`, json("POST"));
export const remove = (id: string) =>
  request<{ jobId: string }>(`/api/documents/${id}`, { method: "DELETE" });
export async function retry(id: string) {
  const r = await fetch(`/api/documents/${id}/retry`, { method: "POST" });
  if (!r.ok) {
    const p = await r.json();
    throw new ApiError(p.code, r.status, p.traceId);
  }
}
export const ask = (
  invoiceId: string,
  question: string,
  conversationId?: string,
  signal?: AbortSignal,
) =>
  request<Answer>("/api/chat", {
    ...json("POST", { invoiceId, question, conversationId }),
    signal,
  });
export async function download(id: string) {
  const r = await fetch(`/api/invoices/${id}/export`);
  if (!r.ok) {
    const p = await r.json();
    throw new ApiError(p.code, r.status, p.traceId);
  }
  const url = URL.createObjectURL(await r.blob());
  const a = document.createElement("a");
  a.href = url;
  a.download = `invoice-${id}.json`;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
