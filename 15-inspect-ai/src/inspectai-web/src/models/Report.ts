export type Report = {
  resultJson: string;
  sourcesJson: string;
  inputHash: string;
  promptVersion: string;
  deployment: string;
  inputTokens: number;
  outputTokens: number;
};
