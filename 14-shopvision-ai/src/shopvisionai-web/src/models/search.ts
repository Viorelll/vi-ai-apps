import { z } from "zod";
export const observationSchema = z.object({
  sourceId: z.string(),
  kind: z.enum(["visible", "inferred", "unknown"]),
  attribute: z.string(),
  value: z.string(),
  region: z
    .object({
      x: z.number(),
      y: z.number(),
      width: z.number(),
      height: z.number(),
    })
    .nullable(),
});
export const candidateSchema = z.object({
  sku: z.string(),
  name: z.string(),
  category: z.string(),
  retrievalScore: z.number(),
  reason: z.string(),
  price: z.string(),
  currency: z.string(),
  quantity: z.number().int(),
  stockLabel: z.string(),
  capturedAtUtc: z.string(),
  sourceIds: z.array(z.string()),
  sheetUrl: z.string().startsWith("/api/visual/catalog/"),
});
export const searchSchema = z.object({
  id: z.uuid(),
  state: z.enum([
    "Created",
    "Inspecting",
    "Searching",
    "Completed",
    "NeedsInput",
    "Failed",
    "Stale",
  ]),
  revision: z.number().int(),
  catalogVersion: z.string(),
  errorCode: z.string().nullable(),
  inputTokens: z.number(),
  outputTokens: z.number(),
  imageUrl: z.string().startsWith("/api/documents/"),
  result: z
    .object({
      observations: z.array(observationSchema),
      candidates: z.array(candidateSchema),
      warning: z.string(),
      catalogVersion: z.string(),
      demonstration: z.boolean(),
    })
    .nullable(),
});
export type Search = z.infer<typeof searchSchema>;
export type Candidate = z.infer<typeof candidateSchema>;
