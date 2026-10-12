import { z } from "zod";
export const assetSchema = z.object({
  id: z.uuid(),
  runId: z.uuid(),
  parentAssetId: z.uuid().nullable(),
  width: z.number().int().positive(),
  height: z.number().int().positive(),
  sha256: z.string(),
  createdAtUtc: z.string(),
});
export const runSchema = z.object({
  id: z.uuid(),
  kind: z.string(),
  state: z.string(),
  errorCode: z.string().nullable(),
  prompt: z.string(),
  inputAssetId: z.uuid().nullable(),
  inputDocumentId: z.uuid().nullable(),
  maskDocumentId: z.uuid().nullable(),
  size: z.string(),
  quality: z.string(),
  deployment: z.string().nullable(),
  usageJson: z.string().nullable(),
});
export const projectSchema = z.object({
  id: z.uuid(),
  brief: z.string(),
  state: z.string(),
  revision: z.number().int(),
  assets: z.array(assetSchema),
  runs: z.array(runSchema),
});
export const documentSchema = z.object({
  id: z.uuid(),
  fileName: z.string(),
  contentType: z.string(),
  sha256: z.string(),
  sizeBytes: z.number(),
  width: z.number(),
  height: z.number(),
  hasAlpha: z.boolean(),
  status: z.string(),
});
export type Asset = z.infer<typeof assetSchema>;
export type Project = z.infer<typeof projectSchema>;
export type UploadedDocument = z.infer<typeof documentSchema>;
