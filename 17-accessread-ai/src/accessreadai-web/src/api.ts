import { z } from "zod";
import { documentSchema, readingSchema, answerSchema } from "./models/reading";
async function request(path: string, init?: RequestInit) {
  const response = await fetch(path, init);
  if (!response.ok) {
    const body = await response
      .json()
      .catch(() => ({ code: `http_${response.status}` }));
    throw new Error(
      `${body.code ?? "request_failed"}${body.traceId ? ` · ${body.traceId}` : ""}`,
    );
  }
  return response.status === 202 &&
    response.headers.get("content-length") === "0"
    ? null
    : response.json().catch(() => null);
}
const json = (body: unknown, method = "POST"): RequestInit => ({
  method,
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify(body),
});
export const api = {
  list: async (signal?: AbortSignal) =>
    z.array(documentSchema).parse(await request("/api/documents", { signal })),
  read: async (id: string, signal?: AbortSignal) =>
    readingSchema.parse(await request(`/api/readings/${id}`, { signal })),
  upload: async (file: File) => {
    const body = new FormData();
    body.append("file", file);
    return z
      .object({ documentId: z.string().uuid() })
      .parse(await request("/api/documents", { method: "POST", body }));
  },
  create: async (
    documentId: string,
    targetLanguage: string,
    sourceLanguage: string,
  ) =>
    readingSchema.parse(
      await request(
        "/api/readings",
        json({ documentId, targetLanguage, sourceLanguage }),
      ),
    ),
  generate: (id: string, revision: number) =>
    request(
      `/api/readings/${id}/generate`,
      json({ revision, simplify: true, translate: true, narrate: true }),
    ),
  correct: (id: string, revision: number, variantId: string, text: string) =>
    request(
      `/api/readings/${id}/variants`,
      json({ revision, variantId, text }, "PATCH"),
    ),
  retry: (id: string) =>
    request(`/api/documents/${id}/retry`, { method: "POST" }),
  delete: (id: string) => request(`/api/documents/${id}`, { method: "DELETE" }),
  ask: async (
    question: string,
    documentIds: string[],
    conversationId?: string,
  ) =>
    answerSchema.parse(
      await request(
        "/api/chat",
        json({
          question,
          documentIds,
          conversationId: conversationId ?? null,
          requestId: crypto.randomUUID(),
        }),
      ),
    ),
};
