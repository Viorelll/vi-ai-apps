import type { Citation } from "./Citation";
export interface Answer {
  conversationId: string;
  messageId: string;
  answer: string;
  citations: Citation[];
  deployment: string;
  inputTokens: number;
  outputTokens: number;
}
