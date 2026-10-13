import { z } from "zod";
import { videoSchema, runSchema } from "../models/schemas";
export async function request<T>(
  path: string,
  schema: z.ZodType<T>,
  init: RequestInit = {},
  signal?: AbortSignal,
): Promise<T> {
  const response = await fetch(path, { ...init, signal });
  if (!response.ok) {
    let detail: unknown;
    try {
      detail = await response.json();
    } catch {
      detail = null;
    }
    const parsed = z
      .object({ code: z.string().optional(), traceId: z.string().optional() })
      .safeParse(detail);
    throw new Error(
      parsed.success
        ? `${parsed.data.code ?? "request_failed"}${parsed.data.traceId ? ` · ${parsed.data.traceId}` : ""}`
        : `request_failed (${response.status})`,
    );
  }
  return schema.parse(await response.json());
}
export const list = (signal?: AbortSignal) =>
  request("/api/video/library", z.array(videoSchema), {}, signal);
export async function upload(file: File, signal?: AbortSignal) {
  const data = new FormData();
  data.append("file", file);
  const doc = await request(
    "/api/documents",
    z.object({ documentId: z.uuid(), duplicate: z.boolean() }),
    { method: "POST", body: data },
    signal,
  );
  return request(
    "/api/video/library",
    videoSchema,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        documentId: doc.documentId,
        title: file.name.replace(/\.[^.]+$/, ""),
      }),
    },
    signal,
  );
}
export const index = (id: string, revision: number, signal?: AbortSignal) =>
  request(
    `/api/video/library/${id}/index`,
    z.object({ jobId: z.uuid() }),
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ revision }),
    },
    signal,
  );
export async function remove(id: string, signal?: AbortSignal) {
  const response = await fetch(`/api/documents/${id}`, {
    method: "DELETE",
    signal,
  });
  if (!response.ok) throw new Error(`delete_failed (${response.status})`);
}
export const search = (
  question: string,
  videoIds: string[],
  signal?: AbortSignal,
) =>
  request(
    "/api/video/library/search",
    z.object({ searchRunId: z.uuid() }),
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        question,
        videoIds,
        requestId: crypto.randomUUID(),
      }),
    },
    signal,
  );
export const loadRun = (id: string, signal?: AbortSignal) =>
  request(`/api/video/library/search/${id}`, runSchema, {}, signal);
export const config = (signal?: AbortSignal) =>
  request(
    "/api/config",
    z.object({
      mode: z.enum(["Demo", "Foundry"]),
      maxUploadBytes: z.number(),
      maxDurationMs: z.number(),
    }),
    {},
    signal,
  );
