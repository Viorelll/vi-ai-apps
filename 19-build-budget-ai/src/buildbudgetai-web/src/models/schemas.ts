import { z } from "zod";
const decimal = z.string().regex(/^-?\d+(\.\d+)?$/);
export const evidenceSchema = z.object({
  sourceId: z.string(),
  documentId: z.string().uuid(),
  pageNumber: z.number().nullable(),
  text: z.string(),
});
const lineSchema = z.object({
  id: z.string().uuid(),
  materialLabel: z.string(),
  unit: z.string(),
  price: decimal,
  transport: decimal.nullable(),
  packQuantity: decimal.nullable(),
  confirmed: z.boolean(),
  evidence: z.array(evidenceSchema),
});
export const budgetSchema = z.object({
  id: z.string().uuid(),
  revision: z.number(),
  totalDecimal: decimal,
  currency: z.string(),
  incomplete: z.boolean(),
  stale: z.boolean(),
  lines: z.array(
    z.object({
      needId: z.string().uuid(),
      offerLineId: z.string().uuid(),
      material: z.string(),
      unit: z.string(),
      adjustedQuantity: decimal,
      unitPrice: decimal.nullable(),
      totalDecimal: decimal.nullable(),
      formula: z.string(),
      unknowns: z.array(z.string()),
    }),
  ),
});
export const planSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  currency: z.string(),
  state: z.string(),
  revision: z.number(),
  needs: z.array(
    z.object({
      id: z.string().uuid(),
      label: z.string(),
      unit: z.string(),
      quantity: decimal,
      wastePercent: decimal,
    }),
  ),
  offers: z.array(
    z.object({
      id: z.string().uuid(),
      supplierLabel: z.string(),
      currency: z.string(),
      taxBasis: z.string(),
      documentId: z.string().uuid().nullable(),
      lines: z.array(lineSchema),
    }),
  ),
  sources: z.array(
    z.object({ documentId: z.string().uuid(), role: z.string() }),
  ),
  evidence: z.array(evidenceSchema),
  budget: budgetSchema.nullable(),
});
export const documentSchema = z.object({
  id: z.string().uuid(),
  fileName: z.string(),
  contentType: z.string(),
  status: z.string(),
  sizeBytes: z.number(),
  errorCode: z.string().nullable(),
});
export const chatSchema = z.object({
  conversationId: z.string().uuid(),
  summary: z.string(),
  unknowns: z.array(z.string()),
  citations: z.array(evidenceSchema),
  deployment: z.string(),
});
export type Plan = z.infer<typeof planSchema>;
export type Evidence = z.infer<typeof evidenceSchema>;
export type Document = z.infer<typeof documentSchema>;
export type Chat = z.infer<typeof chatSchema>;
