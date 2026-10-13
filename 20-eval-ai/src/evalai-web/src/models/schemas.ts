import { z } from "zod";
export const datasetSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  version: z.string(),
  caseCount: z.number().int(),
});
export const variantSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  deploymentAlias: z.string(),
  promptVersion: z.string(),
});
export const variantsSchema = z.object({
  mode: z.enum(["Demo", "Live"]),
  variants: z.array(variantSchema),
});
const usage = z.object({
  input: z.number().nonnegative(),
  output: z.number().nonnegative(),
  deployment: z.string(),
  simulated: z.boolean(),
});
const cost = z.object({
  amount: z.string().nullable(),
  currency: z.string().nullable(),
  rateVersion: z.string().nullable(),
  knownPartial: z.boolean(),
});
export const reportSchema = z.object({
  id: z.string().uuid(),
  datasetId: z.string().uuid(),
  state: z.string(),
  revision: z.number().int(),
  errorCode: z.string().nullable(),
  stale: z.boolean(),
  budgetTokens: z.number(),
  usedTokens: z.number(),
  possibleRepeatedCloudCost: z.boolean(),
  baselineCompatible: z.boolean(),
  costNote: z.string(),
  progress: z.object({ completed: z.number(), total: z.number() }),
  summaries: z.array(
    z.object({
      variantId: z.string(),
      variant: z.string(),
      deployment: z.string(),
      total: z.number(),
      passed: z.number(),
      failed: z.number(),
      deterministicFailed: z.number(),
      completed: z.number(),
      judgeMean: z.number().nullable(),
      p50Ms: z.number(),
      p95Ms: z.number(),
      regression: z.boolean().nullable(),
      checksDelta: z.number().nullable(),
    }),
  ),
  results: z.array(
    z.object({
      id: z.string(),
      variant: z.string(),
      caseName: z.string(),
      state: z.string(),
      errorCode: z.string().nullable(),
      output: z.unknown().nullable(),
      checks: z
        .object({
          passed: z.boolean(),
          jsonFieldsPassed: z.boolean(),
          citationsPassed: z.boolean(),
          errors: z.array(z.string()),
        })
        .nullable(),
      judge: z
        .object({
          score: z.number(),
          reason: z.string(),
          rubricVersion: z.string(),
        })
        .nullable(),
      candidateUsage: usage.nullable(),
      judgeUsage: usage.nullable(),
      candidateLatencyMs: z.number(),
      candidateCost: cost,
      judgeCost: cost,
    }),
  ),
});
export const documentSchema = z.object({
  id: z.string().uuid(),
  fileName: z.string(),
  status: z.string(),
  errorCode: z.string().nullable(),
});
export const chunkSchema = z.object({
  id: z.string().uuid(),
  text: z.string(),
  pageNumber: z.number().nullable(),
  generation: z.number(),
  contentUrl: z.string(),
});
export const chatSchema = z.object({
  conversationId: z.string(),
  answer: z.string(),
  citations: z.array(
    z.object({
      chunkId: z.string(),
      documentId: z.string(),
      text: z.string(),
      fileName: z.string(),
      pageNumber: z.number().nullable(),
    }),
  ),
});
export type Dataset = z.infer<typeof datasetSchema>;
export type Variant = z.infer<typeof variantSchema>;
export type Report = z.infer<typeof reportSchema>;
export type Document = z.infer<typeof documentSchema>;
export type Chunk = z.infer<typeof chunkSchema>;
export type Chat = z.infer<typeof chatSchema>;
