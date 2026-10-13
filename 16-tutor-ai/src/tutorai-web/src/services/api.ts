import { z } from "zod";
export async function request<T>(
  path: string,
  schema: z.ZodType<T>,
  body?: unknown,
  signal?: AbortSignal,
): Promise<T> {
  const r = await fetch(path, {
    method: body === undefined ? "GET" : "POST",
    signal,
    headers: body === undefined ? {} : { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!r.ok) {
    const p = await r.json().catch(() => ({}));
    throw new Error(
      `${p.code ?? "request_failed"}: ${p.detail ?? r.status} ${p.traceId ? `(trace: ${p.traceId})` : ""}`,
    );
  }
  return schema.parse(await r.json());
}
