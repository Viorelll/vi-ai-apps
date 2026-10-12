export interface Obligation {
  id: string;
  clauseId: string;
  partyText: string;
  actionText: string;
  relativeTerm: string | null;
  dueDate: string | null;
  evidenceJson: string;
}
