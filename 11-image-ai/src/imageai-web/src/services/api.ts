import { z } from "zod";
import { projectSchema, documentSchema } from "../models/schemas";
async function request(path: string, init?: RequestInit) {
  const response = await fetch(path, init);
  if (!response.ok) {
    const p = await response.json().catch(() => ({}));
    throw new Error(
      `${p.code || p.title || "request_failed"}${p.traceId ? ` · ${p.traceId}` : ""}`,
    );
  }
  return response.json();
}
export const api = {
  list: async () =>
    z.array(projectSchema).parse(await request("/api/images/projects")),
  get: async (id: string, signal?: AbortSignal) =>
    projectSchema.parse(
      await request(`/api/images/projects/${id}`, { signal }),
    ),
  create: async (brief: string) =>
    projectSchema.parse(
      await request("/api/images/projects", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ brief }),
      }),
    ),
  action: async (
    id: string,
    kind: "generate" | "edits" | "variants",
    body: unknown,
  ) =>
    request(`/api/images/projects/${id}/${kind}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    }),
  upload: async (file: File | Blob, name = "image.png") => {
    const body = new FormData();
    body.append("file", file, name);
    return documentSchema.parse(
      await request("/api/documents", { method: "POST", body }),
    );
  },
  config: async () =>
    z.object({ providerMode: z.string() }).parse(await request("/api/config")),
  content: (id: string) => `/api/images/assets/${id}/content`,
};
