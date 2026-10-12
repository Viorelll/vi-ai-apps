export type Dataset = {
  id: string;
  name: string;
  rowCount: number;
  revision: number;
  currency: string;
};
export type Bucket = { label: string; value: number };
export type Analysis = {
  runId: string;
  metric: string;
  groupBy: string;
  currency: string;
  rows: Bucket[];
  total: number;
  resultHash: string;
  datasetRevision: number;
  explanation: string;
  usedFoundry: boolean;
};
export type ImportResult = {
  id: string;
  name: string;
  rowsImported: number;
  revision: number;
};

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, init);
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(
      body.code ?? body.title ?? `Request failed (${response.status})`,
    );
  }
  return response.json() as Promise<T>;
}

export const api = {
  datasets: () => request<Dataset[]>("/api/datasets"),
  analyze: (datasetId: string, question: string) =>
    request<Analysis>("/api/analyze", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ datasetId, question }),
    }),
  importCsv: (file: File, name: string) => {
    const body = new FormData();
    body.append("file", file);
    body.append("name", name);
    return request<ImportResult>("/api/datasets/import", {
      method: "POST",
      body,
    });
  },
};
