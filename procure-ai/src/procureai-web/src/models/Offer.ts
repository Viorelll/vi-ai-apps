export type Offer = {
  id: string;
  documentId: string;
  supplierLabel: string;
  currency: string | null;
  quantity: number | null;
  unit: string | null;
  net: number | null;
  shipping: number | null;
  leadDays: number | null;
  meetsRequirements: boolean | null;
  sourceIds: string[];
};
