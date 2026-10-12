import type { ChatResult, Criteria, Document, Run, RunSummary } from "./models";
const hints: Record<string, string> = {
  foundry_not_configured:
    "Completează endpoint-ul, cheia și deployment-ul Microsoft Foundry în configurația serverului.",
  content_understanding_not_configured:
    "Completează configurația Content Understanding pentru PDF și imagini.",
  documents_not_ready:
    "Așteaptă procesarea tuturor documentelor înainte de pornire.",
  incomplete_offers:
    "Ofertele au câmpuri lipsă. Completează sursele și creează o analiză nouă.",
  incomparable_offers:
    "Ofertele trebuie să aibă aceeași monedă, cantitate și unitate.",
  revision_or_state_conflict:
    "Analiza s-a modificat. Reîncarcă și încearcă din nou.",
  revision_conflict: "Analiza s-a modificat. Reîncarcă și încearcă din nou.",
  invalid_requirements:
    "Verifică necesarul, moneda, cantitatea și termenul de livrare.",
  file_signature_mismatch: "Conținutul fișierului nu corespunde extensiei.",
  sources_changed: "O sursă a fost ștearsă. Creează o analiză nouă.",
  token_budget_exceeded: "Bugetul de tokeni a fost epuizat.",
};
export function errorHint(code: string) {
  return hints[code] ?? code;
}
export async function request<T>(
  path: string,
  init?: RequestInit,
  signal?: AbortSignal,
): Promise<T> {
  const response = await fetch(path, { ...init, signal });
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(
      `${errorHint(body.code ?? body.detail ?? `Cererea a eșuat (${response.status}).`)}${body.traceId ? ` · ${body.traceId}` : ""}`,
    );
  }
  if (response.status === 204 || response.headers.get("Content-Length") === "0")
    return undefined as T;
  const text = await response.text();
  return text ? JSON.parse(text) : (undefined as T);
}
function post<T>(path: string, body: unknown): Promise<T> {
  return request<T>(path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
}
export const api = {
  health: (signal?: AbortSignal) =>
    request<{ mode: string }>("/api/health", undefined, signal),
  runs: (signal?: AbortSignal) =>
    request<RunSummary[]>("/api/procurement/runs", undefined, signal),
  run: (id: string, signal?: AbortSignal) =>
    request<Run>(`/api/procurement/runs/${id}`, undefined, signal),
  create: (brief: string, criteria: Criteria) =>
    post<{ id: string; revision: number }>("/api/procurement/runs", {
      brief,
      criteria,
    }),
  attach: (id: string, documentId: string, revision: number, role: string) =>
    post<Run>(`/api/procurement/runs/${id}/documents`, {
      documentId,
      role,
      revision,
    }),
  start: (id: string, revision: number) =>
    post(`/api/procurement/runs/${id}/start`, { revision }),
  retry: (id: string, revision: number) =>
    post(`/api/procurement/runs/${id}/retry`, { revision }),
  documents: (signal?: AbortSignal) =>
    request<Document[]>("/api/documents", undefined, signal),
  upload: (file: File) => {
    const body = new FormData();
    body.append("file", file);
    return request<{ documentId: string }>("/api/documents", {
      method: "POST",
      body,
    });
  },
  remove: (id: string) => request(`/api/documents/${id}`, { method: "DELETE" }),
  retryDocument: (id: string) =>
    request(`/api/documents/${id}/retry`, { method: "POST" }),
  ask: (runId: string, question: string, conversationId?: string) =>
    post<ChatResult>("/api/chat", {
      runId,
      question,
      conversationId: conversationId ?? null,
    }),
};
