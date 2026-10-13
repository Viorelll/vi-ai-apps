import { z } from "zod";
import { resultSchema, summarySchema, healthSchema } from "./models";
export async function request(path: string, init: RequestInit = {}) {
  const response = await fetch(path, init);
  if (!response.ok) {
    let detail: unknown;
    try {
      detail = await response.json();
    } catch {
      detail = null;
    }
    const problem = z
      .object({ code: z.string().optional(), traceId: z.string().optional() })
      .safeParse(detail);
    throw new Error(
      `${response.status}: ${problem.success ? (problem.data.code ?? "request_failed") : "request_failed"}${problem.success && problem.data.traceId ? ` · trace ${problem.data.traceId}` : ""}`,
    );
  }
  return response.json() as Promise<unknown>;
}
export const getItem = async (id: string, includeInput = false) =>
  resultSchema.parse(
    await request(`/api/moderation/items/${id}?includeInput=${includeInput}`),
  );
export const getHealth = async () =>
  healthSchema.parse(await request("/api/health"));
export const getHistory = async () =>
  z
    .object({ items: z.array(summarySchema) })
    .parse(await request("/api/moderation/items"));
export const getQueue = async (page = 1) =>
  z
    .object({
      total: z.number(),
      page: z.number(),
      pageSize: z.number(),
      items: z.array(summarySchema),
    })
    .parse(await request(`/api/moderation/review?page=${page}`));
export async function createItem(text: string, imageDocumentId: string | null) {
  const result = z.object({ id: z.string().uuid() }).parse(
    await request("/api/moderation/items", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        requestId: crypto.randomUUID(),
        text: text.trim() ? text : null,
        imageDocumentId,
      }),
    }),
  );
  return result.id;
}
export const analyze = async (id: string) =>
  request(`/api/moderation/items/${id}/analyze`, { method: "POST" });
export const review = async (
  id: string,
  revision: number,
  decision: "allow" | "block",
  note: string,
) =>
  resultSchema.parse(
    await request(`/api/moderation/items/${id}/review`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ revision, decision, note: note || null }),
    }),
  );
export async function upload(file: File) {
  const data = new FormData();
  data.append("file", file);
  return z
    .object({
      id: z.string().uuid(),
      fileName: z.string(),
      sha256: z.string(),
      status: z.literal("Ready"),
    })
    .parse(await request("/api/documents", { method: "POST", body: data }));
}
