export type Event = {
  sourceId: string;
  timestampUtc: string;
  severity: string;
  traceId?: string | null;
  message: string;
};
export type Hypothesis = {
  claim: string;
  confidenceLabel: string;
  evidenceIds: string[];
  contradictions: string[];
};
export type Diagnosis = {
  id?: string;
  state: string;
  summary: string;
  hypotheses: Hypothesis[];
  nextChecks: string[];
  provider: string;
  inputTokens: number;
  outputTokens: number;
  durationMs: number;
};
export type Incident = {
  incident: {
    id: string;
    service: string;
    fromUtc: string;
    toUtc: string;
    events: Event[];
    diagnoses: Diagnosis[];
  };
  releases: { label: string; deployedAtUtc?: string }[];
  logSources: {
    id: string;
    fileName: string;
    sha256: string;
    sizeBytes: number;
    createdAtUtc: string;
    contentUrl: string;
  }[];
};
async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, init);
  if (!response.ok) {
    const detail = (await response.json().catch(() => null)) as {
      title?: string;
      detail?: string;
    } | null;
    throw new Error(
      detail?.detail ?? detail?.title ?? `Cererea a eșuat (${response.status})`,
    );
  }
  return response.json() as Promise<T>;
}
export const loadIncident = (id: string) =>
  request<Incident>(`/api/incidents/${id}`);
export const createDemo = () =>
  request<{ id: string }>("/api/incidents/demo", { method: "POST" });
export const createIncident = (
  service: string,
  fromUtc: string,
  toUtc: string,
) =>
  request<{ id: string }>("/api/incidents", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ service, fromUtc, toUtc }),
  });
export async function uploadLogs(id: string, file: File) {
  const response = await fetch(
    `/api/incidents/${id}/logs?fileName=${encodeURIComponent(file.name)}`,
    {
      method: "POST",
      headers: { "Content-Type": "application/x-ndjson" },
      body: file,
    },
  );
  if (!response.ok)
    throw new Error(`Importul logurilor a eșuat (${response.status})`);
}
export const diagnose = (id: string) =>
  request<Diagnosis>(`/api/incidents/${id}/diagnose`, { method: "POST" });
