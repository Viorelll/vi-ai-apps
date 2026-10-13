import { z } from "zod";
export const sourceSchema = z.object({
  id: z.string().uuid(),
  title: z.string(),
  objective: z.string(),
  text: z.string(),
  officialUrl: z.string().url(),
  checkedAtUtc: z.string(),
});
export const outputSchema = z.object({
  explanation: z.string(),
  score: z.number().nullable(),
  criteria: z.array(
    z.object({
      criterion: z.string(),
      score: z.number().min(0).max(1),
      feedback: z.string(),
    }),
  ),
  citations: z.array(
    z.object({ sourceId: z.string().uuid(), quote: z.string() }),
  ),
});
export const sessionSchema = z.object({
  id: z.string().uuid(),
  topic: z.string(),
  difficulty: z.number().int().min(1).max(3),
  state: z.enum([
    "Open",
    "Explaining",
    "ExerciseReady",
    "Assessing",
    "FeedbackReady",
    "Failed",
    "Stale",
  ]),
  revision: z.number().int(),
  explanation: outputSchema.nullable(),
  exercise: z
    .object({
      id: z.string().uuid(),
      kind: z.enum(["single_choice", "open_answer"]),
      prompt: z.string(),
      options: z.array(z.string()),
      difficulty: z.number(),
    })
    .nullable(),
  feedback: z
    .object({
      id: z.string().uuid(),
      requestId: z.string().uuid(),
      score: z.number().nullable(),
      feedback: outputSchema.nullable(),
    })
    .nullable(),
  errorCode: z.string().nullable(),
  demoMode: z.boolean(),
});
export type Session = z.infer<typeof sessionSchema>;
export type Source = z.infer<typeof sourceSchema>;
export type Output = z.infer<typeof outputSchema>;
