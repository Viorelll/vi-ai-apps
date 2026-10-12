import type { Inspection } from "../models/Inspection";
export async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, init);
  if (!response.ok) {
    let code = `HTTP ${response.status}`;
    try {
      const p = await response.json();
      code = `${p.detail ?? p.title ?? code}${p.code ? " · " + p.code : ""}${p.traceId ? " · Trace " + p.traceId : ""}`;
    } catch {
      /* non-JSON proxy error */
    }
    throw new Error(code);
  }
  return response.json() as Promise<T>;
}
export function post<T>(path: string, body: unknown) {
  return request<T>(path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
}
export function parseInspection(value: unknown): Inspection {
  const x = value as Inspection;
  if (
    !x ||
    typeof x.id !== "string" ||
    typeof x.revision !== "number" ||
    typeof x.state !== "string" ||
    !Array.isArray(x.sources) ||
    !Array.isArray(x.findings) ||
    !Array.isArray(x.checklist) ||
    !Array.isArray(x.questions)
  )
    throw new Error("Răspuns API incompatibil.");
  for (const f of x.findings)
    if (
      !["visible", "inferred", "unknown"].includes(f.kind) ||
      typeof f.description !== "string" ||
      typeof f.documentId !== "string"
    )
      throw new Error("Observație API invalidă.");
  return x;
}
export async function load(id: string) {
  return parseInspection(await request(`/api/inspections/${id}`));
}
export async function upload(file: File) {
  const body = new FormData();
  body.append("file", file);
  return request<{
    documentId: string;
    jobId: string | null;
    existing: boolean;
  }>("/api/documents", { method: "POST", body });
}
