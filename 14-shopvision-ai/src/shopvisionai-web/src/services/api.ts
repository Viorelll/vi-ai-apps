import { z } from "zod";
import { searchSchema } from "../models/search";
export async function request(
  path: string,
  init?: RequestInit,
): Promise<unknown> {
  const response = await fetch(path, init);
  if (!response.ok) {
    let error: { code?: string; traceId?: string } = {};
    try {
      error = await response.json();
    } catch {
      /* non-JSON proxy failure */
    }
    throw new Error(
      `${error.code ?? "request_failed"} (${response.status})${error.traceId ? ` · trace ${error.traceId}` : ""}`,
    );
  }
  if (response.status === 204 || response.headers.get("content-length") === "0")
    return null;
  return response.json();
}
export function post(path: string, body: unknown) {
  return request(path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
}
export async function upload(file: File) {
  const data = new FormData();
  data.append("file", file);
  return z
    .object({ documentId: z.uuid() })
    .parse(await request("/api/documents", { method: "POST", body: data }));
}
export async function loadSearch(id: string, signal?: AbortSignal) {
  return searchSchema.parse(
    await request(`/api/visual/searches/${id}`, { signal }),
  );
}
export async function createSearch(imageDocumentId: string, question: string) {
  return z.object({ id: z.uuid(), revision: z.number() }).parse(
    await post("/api/visual/searches", {
      imageDocumentId,
      question,
      catalogVersion: "seed-v1",
    }),
  );
}
export async function ask(id: string, question: string) {
  return z.object({ answer: z.string(), sourceIds: z.array(z.string()) }).parse(
    await post(`/api/visual/searches/${id}/questions`, {
      question,
      requestId: crypto.randomUUID(),
    }),
  );
}
