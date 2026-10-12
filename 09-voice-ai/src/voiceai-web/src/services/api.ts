import type { Proposal, Slot, TurnResult } from "../models/Voice";

export type SessionState = {
  id: string;
  timeZoneId: string;
  turns: Array<{
    id: string;
    transcript: string;
    reply: string;
    audioUrl: string | null;
  }>;
  proposals: Proposal[];
};

async function checked<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(body.code ?? `Cererea a eșuat (${response.status})`);
  }
  return response.json() as Promise<T>;
}

export async function createSession(
  timeZoneId: string,
): Promise<{ id: string }> {
  return checked(
    await fetch("/api/voice/sessions", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ timeZoneId }),
    }),
  );
}
export async function getSlots(): Promise<Slot[]> {
  return checked(await fetch("/api/voice/slots"));
}
export async function getSession(sessionId: string): Promise<SessionState> {
  return checked(await fetch(`/api/voice/sessions/${sessionId}`));
}
export async function sendText(
  sessionId: string,
  text: string,
): Promise<TurnResult> {
  return checked(
    await fetch(`/api/voice/sessions/${sessionId}/turns`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ requestId: crypto.randomUUID(), text }),
    }),
  );
}
export async function sendAudio(
  sessionId: string,
  blob: Blob,
): Promise<TurnResult> {
  const form = new FormData();
  form.append("audio", blob, "recording.webm");
  form.append("requestId", crypto.randomUUID());
  return checked(
    await fetch(`/api/voice/sessions/${sessionId}/turns/audio`, {
      method: "POST",
      body: form,
    }),
  );
}
export async function confirmProposal(
  id: string,
  revision: number,
): Promise<void> {
  await checked(
    await fetch(`/api/voice/proposals/${id}/confirm`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ revision, idempotencyKey: crypto.randomUUID() }),
    }),
  );
}
export async function rejectProposal(id: string): Promise<void> {
  await checked(
    await fetch(`/api/voice/proposals/${id}/reject`, { method: "POST" }),
  );
}
