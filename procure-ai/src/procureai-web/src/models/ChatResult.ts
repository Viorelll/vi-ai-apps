import type { Source } from "./Source";
export type ChatResult = {
  conversationId: string;
  answer: string;
  citations: Source[];
};
