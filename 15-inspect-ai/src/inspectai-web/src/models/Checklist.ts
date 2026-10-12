export type Checklist = {
  id: string;
  findingId: string;
  text: string;
  evidenceJson: string;
  status: "proposed" | "noted";
};
