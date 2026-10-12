export class ApiError extends Error {
  constructor(
    public code: string,
    public traceId: string,
    message: string,
  ) {
    super(message);
  }
}
export async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const r = await fetch(path, init);
  if (!r.ok) {
    const p = (await r.json().catch(() => ({}))) as {
      code?: string;
      traceId?: string;
      detail?: string;
      title?: string;
    };
    throw new ApiError(
      p.code ?? `http_${r.status}`,
      p.traceId ?? "",
      p.detail ?? p.title ?? `Cererea a eșuat (${r.status}).`,
    );
  }
  return r.json() as Promise<T>;
}
export function post<T>(path: string, body: unknown): Promise<T> {
  return request(path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
}
export async function poll<T>(
  path: string,
  status: (value: T) => string,
  onProgress?: (value: T) => void,
): Promise<T> {
  for (let attempt = 0; attempt < 180; attempt++) {
    const value = await request<T>(path);
    onProgress?.(value);
    const state = status(value);
    if (["Ready", "NeedsReview", "Succeeded"].includes(state)) return value;
    if (["Failed", "Stale", "Canceled", "Deleting"].includes(state))
      throw new ApiError(
        `state_${state}`,
        "",
        "Procesarea nu s-a finalizat. Verifică starea și configurația serverului.",
      );
    await new Promise((resolve) => setTimeout(resolve, 1000));
  }
  throw new ApiError(
    "poll_timeout",
    "",
    "Procesarea continuă pe server. Reîncarcă versiunea pentru a verifica rezultatul.",
  );
}
