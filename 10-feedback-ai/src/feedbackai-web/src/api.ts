export type Item = {
  id: string;
  ordinal: number;
  redactedText: string;
  topic: string;
  suggestedTopic: string;
  sentiment: string;
  urgency: string;
  reason: string;
  provider: string;
};
export type Batch = {
  id: string;
  state: string;
  revision: number;
  total: number;
  topics: Record<string, number>;
  items: Item[];
};
async function json<T>(url: string, init?: RequestInit): Promise<T> {
  const r = await fetch(url, init);
  if (!r.ok)
    throw new Error((await r.text()).slice(0, 240) || `HTTP ${r.status}`);
  return r.json();
}
export const createBatch = (messages: string[]) =>
  json<Batch>("/api/feedback/batches", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ messages }),
  });
export async function importFile(file: File): Promise<Batch> {
  const body = new FormData();
  body.append("file", file);
  const response = await fetch("/api/feedback/batches/import", {
    method: "POST",
    body,
  });
  if (!response.ok) throw new Error((await response.text()).slice(0, 240));
  return response.json();
}
export const analyze = (id: string) =>
  json<Batch>(`/api/feedback/batches/${id}/analyze`, { method: "POST" });
export const getBatch = (id: string) =>
  json<Batch>(`/api/feedback/batches/${id}`);
export async function correct(
  id: string,
  revision: number,
  field: "topic" | "urgency",
  value: string,
) {
  const r = await fetch(`/api/feedback/items/${id}/labels`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ revision, field, value }),
  });
  if (!r.ok)
    throw new Error(
      r.status === 409
        ? "Datele s-au schimbat. Reîncarcă analiza."
        : `HTTP ${r.status}`,
    );
}
