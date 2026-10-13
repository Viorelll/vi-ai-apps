import { z } from "zod";
export const summarySchema = z.object({
  id: z.string().uuid(),
  state: z.string(),
  revision: z.number(),
  stale: z.boolean().optional(),
  createdAtUtc: z.string(),
});
export const resultSchema = summarySchema.extend({
  stale: z.boolean(),
  errorCode: z.string().nullable(),
  explanation: z.string().nullable(),
  text: z.string().nullable(),
  imageDocumentId: z.string().uuid().nullable(),
  analysis: z
    .object({
      provider: z.string(),
      apiVersion: z.string(),
      outputScale: z.string(),
      complete: z.boolean(),
      inputTokens: z.number(),
      outputTokens: z.number(),
      scores: z.array(
        z.object({ category: z.string(), severity: z.number().int() }),
      ),
      rawUrl: z.string(),
    })
    .nullable(),
  policy: z
    .object({
      decision: z.enum(["allow", "review", "block"]),
      policyVersion: z.string(),
      decisionHash: z.string(),
      reasons: z.array(z.string()),
    })
    .nullable(),
  reviews: z.array(
    z.object({
      id: z.string().uuid(),
      expectedRevision: z.number(),
      decision: z.enum(["allow", "block"]),
      note: z.string().nullable(),
      createdAtUtc: z.string(),
    }),
  ),
});
export type ItemResult = z.infer<typeof resultSchema>;
export type ItemSummary = z.infer<typeof summarySchema>;
export const healthSchema = z.object({
  status: z.literal("ok"),
  mode: z.enum(["Demo", "Foundry"]),
  policyVersion: z.string(),
});
