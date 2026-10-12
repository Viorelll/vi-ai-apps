export type Slot = {
  id: string;
  startUtc: string;
  endUtc: string;
  resourceLabel: string;
  revision: number;
};
export type Proposal = {
  id: string;
  state: string;
  revision: number;
  expiresAtUtc: string;
  slotId: string;
  startUtc: string;
  endUtc: string;
  resourceLabel: string;
  slotRevision: number;
};
export type TurnResult = {
  text: string;
  transcript: string;
  proposalId: string | null;
  audioBase64: string | null;
  audioContentType: string | null;
};
