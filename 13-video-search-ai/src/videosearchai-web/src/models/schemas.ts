import { z } from "zod";
export const videoSchema = z.object({
  id: z.uuid(),
  documentId: z.uuid(),
  title: z.string(),
  durationMs: z.number().positive(),
  state: z.enum([
    "Uploaded",
    "Analyzing",
    "Indexing",
    "Ready",
    "Failed",
    "Stale",
  ]),
  revision: z.number().int(),
  errorCode: z.string().nullable(),
});
export const citationSchema = z
  .object({
    label: z.string(),
    videoId: z.uuid(),
    segmentId: z.uuid(),
    documentId: z.uuid(),
    generation: z.number().int(),
    startMs: z.number().nonnegative(),
    endMs: z.number().positive(),
    excerpt: z.string(),
    contentUrl: z
      .string()
      .regex(/^\/api\/video\/library\/[0-9a-f-]+\/content$/i),
    evidenceKind: z.enum(["transcript", "visual"]),
  })
  .refine((c) => c.endMs > c.startMs, "Invalid temporal range");
export const answerSchema = z.object({
  answer: z.string(),
  citations: z.array(citationSchema),
  inputTokens: z.number().nonnegative(),
  outputTokens: z.number().nonnegative(),
  deployment: z.string(),
});
export const runSchema = z.object({
  id: z.uuid(),
  state: z.enum(["Queued", "Answered", "Failed", "Stale"]),
  result: answerSchema.nullable(),
  errorCode: z.string().nullable(),
});
export type Video = z.infer<typeof videoSchema>;
export type Citation = z.infer<typeof citationSchema>;
export type Answer = z.infer<typeof answerSchema>;
