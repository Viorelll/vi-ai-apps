import { z } from "zod";
import {
  datasetSchema,
  variantsSchema,
  reportSchema,
  documentSchema,
  chunkSchema,
  chatSchema,
} from "../models/schemas";
export async function request<T>(
  path: string,
  schema: z.ZodType<T>,
  init: RequestInit = {},
): Promise<T> {
  const response = await fetch("/api" + path, init);
  if (!response.ok) {
    let message = `HTTP ${response.status}`;
    try {
      const p = await response.json();
      message = `${p.code ?? p.title ?? message}: ${p.detail ?? ""}${p.traceId ? ` (trace ${p.traceId})` : ""}`;
    } catch {}
    throw new Error(message);
  }
  try {
    return schema.parse(await response.json());
  } catch {
    throw new Error("Răspuns API invalid. Verifică versiunea serverului.");
  }
}
const post = (body: unknown): RequestInit => ({
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify(body),
});
export const api = {
  datasets: (signal?: AbortSignal) =>
    request("/evaluations/datasets", z.array(datasetSchema), { signal }),
  variants: (signal?: AbortSignal) =>
    request("/evaluations/variants", variantsSchema, { signal }),
  runs: (signal?: AbortSignal) =>
    request(
      "/evaluations/runs",
      z.array(
        z.object({
          id: z.string(),
          datasetId: z.string(),
          state: z.string(),
          stale: z.boolean(),
        }),
      ),
      { signal },
    ),
  report: (id: string, signal?: AbortSignal) =>
    request(`/evaluations/runs/${id}`, reportSchema, { signal }),
  run: (
    datasetId: string,
    variantIds: string[],
    baselineRunId: string | null,
    budgetTokens: number,
  ) =>
    request(
      "/evaluations/runs",
      z.object({ id: z.string() }),
      post({ datasetId, variantIds, baselineRunId, budgetTokens }),
    ),
  createDataset: (body: unknown) =>
    request("/evaluations/datasets", z.object({ id: z.string() }), post(body)),
  async action(id: string, action: "retry" | "cancel", revision: number) {
    const r = await fetch(
      `/api/evaluations/runs/${id}/${action}`,
      post({ revision }),
    );
    if (!r.ok) {
      const p = await r.json();
      throw new Error(`${p.code}: ${p.detail}`);
    }
  },
  documents: (signal?: AbortSignal) =>
    request("/documents", z.array(documentSchema), { signal }),
  chunks: (id: string) =>
    request(`/documents/${id}/chunks`, z.array(chunkSchema)),
  upload: (file: File) => {
    const body = new FormData();
    body.append("file", file);
    return request("/documents", z.object({ documentId: z.string() }), {
      method: "POST",
      body,
    });
  },
  async deleteDocument(id: string) {
    const r = await fetch(`/api/documents/${id}`, { method: "DELETE" });
    if (!r.ok) throw new Error("Ștergerea documentului a eșuat.");
  },
  retryDocument: (id: string) =>
    request(`/documents/${id}/retry`, z.object({ jobId: z.string() }), {
      method: "POST",
    }),
  chat: (question: string, documentIds: string[], conversationId?: string) =>
    request(
      "/chat",
      chatSchema,
      post({
        requestId: crypto.randomUUID(),
        question,
        documentIds,
        conversationId,
      }),
    ),
  rate: (body: unknown) =>
    request("/evaluations/rates", z.object({ id: z.string() }), post(body)),
};
