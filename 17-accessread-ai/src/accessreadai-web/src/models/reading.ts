import { z } from "zod";
export const documentSchema = z.object({
  id: z.string().uuid(),
  fileName: z.string(),
  state: z.string(),
  errorCode: z.string().nullable().optional(),
});
export type DocumentItem = z.infer<typeof documentSchema>;
const narrationSchema = z.object({
  id: z.string().uuid(),
  voice: z.string(),
  audioUrl: z.string().startsWith("/api/"),
  contentType: z.string(),
});
const variantSchema = z.object({
  id: z.string().uuid(),
  kind: z.string(),
  language: z.string(),
  text: z.string(),
  narration: z.array(narrationSchema),
});
export const readingSchema = z.object({
  id: z.string().uuid(),
  documentId: z.string().uuid(),
  state: z.string(),
  revision: z.number(),
  sourceLanguage: z.string(),
  targetLanguage: z.string(),
  errorCode: z.string().nullable().optional(),
  blocks: z.array(
    z.object({
      id: z.string().uuid(),
      sourceId: z.string(),
      pageNumber: z.number(),
      ordinal: z.number(),
      kind: z.string(),
      original: z.string(),
      needsReview: z.boolean(),
      warning: z.string().nullable(),
      contentUrl: z.string().startsWith("/api/"),
      imageUrl: z.string().startsWith("/api/").nullable(),
      imageDescriptions: z.array(z.string()),
      variants: z.array(variantSchema),
    }),
  ),
});
export type Reading = z.infer<typeof readingSchema>;
export type Block = Reading["blocks"][number];
export type Variant = Block["variants"][number];
export const answerSchema = z.object({
  conversationId: z.string().uuid(),
  answer: z.string(),
  citations: z.array(
    z.object({
      label: z.string(),
      excerpt: z.string(),
      pageNumber: z.number(),
      contentUrl: z.string().startsWith("/api/").nullable(),
    }),
  ),
});
export type Answer = z.infer<typeof answerSchema>;
