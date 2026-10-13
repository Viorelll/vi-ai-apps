import { z } from "zod";
export async function request<T>(
  path: string,
  schema: z.ZodType<T>,
  method = "GET",
  body?: unknown,
): Promise<T> {
  const response = await fetch(path, {
    method,
    headers:
      body instanceof FormData
        ? undefined
        : body
          ? { "Content-Type": "application/json" }
          : undefined,
    body:
      body instanceof FormData ? body : body ? JSON.stringify(body) : undefined,
  });
  if (!response.ok) {
    let code = `HTTP ${response.status}`;
    try {
      const error = await response.json();
      code = error.code ?? error.title ?? code;
      if (error.traceId) code += ` · ${error.traceId}`;
    } catch {
      /* HTTP status retained */
    }
    throw new Error(code);
  }
  return schema.parse(await response.json());
}
export async function action(path: string, method: string) {
  const r = await fetch(path, { method });
  if (!r.ok) throw new Error(`HTTP ${r.status}`);
}
