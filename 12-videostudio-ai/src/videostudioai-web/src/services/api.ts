import { z } from "zod";
import {
  projectSchema,
  capabilitiesSchema,
  documentSchema,
  answerSchema,
  type Scene,
} from "../models/schemas";
export async function request<T>(
  path: string,
  schema: z.ZodType<T>,
  init?: RequestInit,
): Promise<T> {
  const r = await fetch(path, init);
  if (!r.ok) {
    const p = await r.json().catch(() => ({}));
    throw new Error(
      `${p.title ?? "Cererea a eșuat"} (${p.code ?? r.status})${p.traceId ? ` · trace: ${p.traceId}` : ""}`,
    );
  }
  return schema.parse(await r.json());
}
const json = (method: string, body: unknown): RequestInit => ({
  method,
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify(body),
});
const accepted = z.object({ jobId: z.string().uuid() });
export const api = {
  capabilities: (signal?: AbortSignal) =>
    request("/api/video/capabilities", capabilitiesSchema, { signal }),
  list: (signal?: AbortSignal) =>
    request("/api/video/projects", z.array(projectSchema), { signal }),
  get: (id: string, signal?: AbortSignal) =>
    request(`/api/video/projects/${id}`, projectSchema, { signal }),
  create: (brief: string) =>
    request("/api/video/projects", projectSchema, json("POST", { brief })),
  storyboard: (id: string, revision: number) =>
    request(
      `/api/video/projects/${id}/storyboard`,
      accepted,
      json("POST", { revision, requestId: crypto.randomUUID() }),
    ),
  confirm: (id: string, revision: number, scenes: Scene[]) =>
    request(
      `/api/video/projects/${id}/storyboard`,
      projectSchema,
      json("PATCH", { revision, scenes }),
    ),
  generate: (
    id: string,
    revision: number,
    sceneId: string,
    seconds: number,
    size: string,
  ) =>
    request(
      `/api/video/projects/${id}/generate`,
      accepted,
      json("POST", {
        revision,
        requestId: crypto.randomUUID(),
        sceneId,
        seconds,
        size,
      }),
    ),
  remix: (id: string, revision: number, prompt: string) =>
    request(
      `/api/video/clips/${id}/remix`,
      accepted,
      json("POST", { revision, requestId: crypto.randomUUID(), prompt }),
    ),
  retry: (id: string, revision: number, acknowledgeCost: boolean) =>
    request(
      `/api/video/jobs/${id}/retry`,
      accepted,
      json("POST", {
        revision,
        requestId: crypto.randomUUID(),
        acknowledgeCost,
      }),
    ),
  documents: (signal?: AbortSignal) =>
    request("/api/documents", z.array(documentSchema), { signal }),
  upload: (file: File) => {
    const body = new FormData();
    body.append("file", file);
    return request("/api/documents", documentSchema, { method: "POST", body });
  },
  deleteDocument: async (id: string) => {
    const r = await fetch(`/api/documents/${id}`, { method: "DELETE" });
    if (!r.ok) throw new Error("Ștergerea documentului a eșuat.");
  },
  retryDocument: async (id: string) => {
    const r = await fetch(`/api/documents/${id}/retry`, { method: "POST" });
    if (!r.ok) throw new Error("Reluarea extracției a eșuat.");
  },
  chat: (question: string, documentIds: string[], conversationId?: string) =>
    request(
      "/api/chat",
      answerSchema,
      json("POST", { question, documentIds, conversationId }),
    ),
};
