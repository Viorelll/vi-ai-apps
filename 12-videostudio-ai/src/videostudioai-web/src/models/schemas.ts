import { z } from "zod";
export const sceneSchema = z.object({
  id: z.string(),
  title: z.string(),
  prompt: z.string(),
});
export const clipSchema = z.object({
  id: z.string().uuid(),
  jobId: z.string().uuid(),
  contentUrl: z.string().startsWith("/api/"),
  durationMs: z.number(),
  width: z.number(),
  height: z.number(),
  sha256: z.string(),
  prompt: z.string(),
  deployment: z.string(),
  sourceClipId: z.string().uuid().nullable(),
});
export const jobSchema = z.object({
  id: z.string().uuid(),
  kind: z.string(),
  state: z.string(),
  providerId: z.string().nullable(),
  errorCode: z.string().nullable(),
  attempt: z.number(),
  inputTokens: z.number(),
  outputTokens: z.number(),
});
export const projectSchema = z.object({
  id: z.string().uuid(),
  brief: z.string(),
  state: z.string(),
  revision: z.number(),
  confirmed: z.boolean(),
  storyboard: z
    .object({ title: z.string(), scenes: z.array(sceneSchema) })
    .nullable(),
  jobs: z.array(jobSchema),
  clips: z.array(clipSchema),
});
export const capabilitiesSchema = z.object({
  mode: z.string(),
  deployment: z.string(),
  seconds: z.array(z.number()),
  sizes: z.array(z.string()),
  videoEnabled: z.boolean(),
  preview: z.boolean(),
});
export const documentSchema = z.object({
  id: z.string().uuid(),
  fileName: z.string(),
  contentType: z.string(),
  sizeBytes: z.number(),
  status: z.string(),
  errorCode: z.string().nullable(),
  contentUrl: z.string().startsWith("/api/"),
});
export const answerSchema = z.object({
  conversationId: z.string().uuid(),
  answer: z.string(),
  citations: z.array(
    z.object({
      label: z.string(),
      documentId: z.string().uuid(),
      chunkId: z.string().uuid(),
      fileName: z.string(),
      excerpt: z.string(),
      pageNumber: z.number().nullable(),
      contentUrl: z.string().startsWith("/api/"),
    }),
  ),
  inputTokens: z.number(),
  outputTokens: z.number(),
});
export type Scene = z.infer<typeof sceneSchema>;
export type Project = z.infer<typeof projectSchema>;
export type Capabilities = z.infer<typeof capabilitiesSchema>;
export type Document = z.infer<typeof documentSchema>;
export type Answer = z.infer<typeof answerSchema>;
export const isActive = (state: string) =>
  !["Ready", "Failed", "Canceled", "Uncertain", "NeedsReview"].includes(state);
