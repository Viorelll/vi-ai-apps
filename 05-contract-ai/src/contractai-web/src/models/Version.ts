export interface Version {
  id: string;
  documentId: string;
  contractKey: string;
  versionLabel: string;
  parentVersionId: string | null;
  state: string;
  revision: number;
  summary: string;
  deployment: string | null;
  inputTokens: number;
  outputTokens: number;
}
