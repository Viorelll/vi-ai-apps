import { useEffect, useRef, useState } from "react";
import type { Proposal, Slot } from "./models/Voice";
import {
  confirmProposal,
  createSession,
  getSession,
  getSlots,
  rejectProposal,
  sendAudio,
  sendText,
} from "./services/api";

type Turn = { role: "assistant" | "user"; text: string };

export default function App() {
  const [session, setSession] = useState("");
  const [slots, setSlots] = useState<Slot[]>([]);
  const [proposal, setProposal] = useState<Proposal | null>(null);
  const [turns, setTurns] = useState<Turn[]>([]);
  const [text, setText] = useState("");
  const [busy, setBusy] = useState(false);
  const [recording, setRecording] = useState(false);
  const [error, setError] = useState("");
  const [audioUrl, setAudioUrl] = useState("");
  const recorder = useRef<MediaRecorder | null>(null);
  const chunks = useRef<Blob[]>([]);

  useEffect(() => {
    void (async () => {
      let savedId = localStorage.getItem("voiceai-session");
      if (savedId) {
        try {
          const saved = await getSession(savedId);
          setSession(saved.id);
          setTurns([
            ...saved.turns.flatMap((turn) => [
              { role: "user" as const, text: turn.transcript },
              { role: "assistant" as const, text: turn.reply },
            ]),
            ...saved.proposals
              .filter((item) => item.state === "Confirmed")
              .map(() => ({
                role: "assistant" as const,
                text: "Programarea a fost confirmată.",
              })),
          ]);
          setProposal(
            saved.proposals.find(
              (item) =>
                item.state === "Draft" &&
                new Date(item.expiresAtUtc) > new Date(),
            ) ?? null,
          );
          const latestAudio = [...saved.turns]
            .reverse()
            .find((turn) => turn.audioUrl)?.audioUrl;
          if (latestAudio) setAudioUrl(latestAudio);
        } catch (reason) {
          if (
            !(reason instanceof Error) ||
            reason.message !== "session_not_found"
          )
            throw reason;
          localStorage.removeItem("voiceai-session");
          savedId = null;
        }
      }
      if (!savedId) {
        const created = await createSession(
          Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC",
        );
        savedId = created.id;
        localStorage.setItem("voiceai-session", savedId);
        setSession(savedId);
      }
      setSlots(await getSlots());
    })().catch(showError);
  }, []);

  function showError(reason: unknown) {
    setError(reason instanceof Error ? reason.message : "A apărut o eroare.");
  }

  async function acceptTurn(result: Awaited<ReturnType<typeof sendText>>) {
    setTurns((current) => [
      ...current,
      { role: "user", text: result.transcript },
      { role: "assistant", text: result.text },
    ]);
    if (result.proposalId) {
      const state = await getSession(session);
      setProposal(
        state.proposals.find((item) => item.id === result.proposalId) ?? null,
      );
    }
    if (result.audioBase64 && result.audioContentType) {
      if (audioUrl) URL.revokeObjectURL(audioUrl);
      const bytes = Uint8Array.from(atob(result.audioBase64), (c) =>
        c.charCodeAt(0),
      );
      setAudioUrl(
        URL.createObjectURL(
          new Blob([bytes], { type: result.audioContentType }),
        ),
      );
    }
    setSlots(await getSlots());
  }

  useEffect(
    () => () => {
      if (audioUrl.startsWith("blob:")) URL.revokeObjectURL(audioUrl);
    },
    [audioUrl],
  );

  async function submitText(event: React.FormEvent) {
    event.preventDefault();
    if (!session || !text.trim()) return;
    setBusy(true);
    setError("");
    try {
      await acceptTurn(await sendText(session, text.trim()));
      setText("");
    } catch (reason) {
      showError(reason);
    } finally {
      setBusy(false);
    }
  }

  async function startRecording() {
    setError("");
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      const mime = MediaRecorder.isTypeSupported("audio/webm;codecs=opus")
        ? "audio/webm;codecs=opus"
        : "";
      const next = new MediaRecorder(
        stream,
        mime ? { mimeType: mime } : undefined,
      );
      chunks.current = [];
      next.ondataavailable = (event) => {
        if (event.data.size) chunks.current.push(event.data);
      };
      next.onstop = async () => {
        stream.getTracks().forEach((track) => track.stop());
        setRecording(false);
        setBusy(true);
        try {
          await acceptTurn(
            await sendAudio(
              session,
              new Blob(chunks.current, { type: next.mimeType || "audio/webm" }),
            ),
          );
        } catch (reason) {
          showError(reason);
        } finally {
          setBusy(false);
        }
      };
      recorder.current = next;
      next.start();
      setRecording(true);
    } catch (reason) {
      showError(reason);
    }
  }

  async function confirm() {
    if (!proposal) return;
    setBusy(true);
    setError("");
    try {
      await confirmProposal(proposal.id, proposal.revision);
      setTurns((current) => [
        ...current,
        { role: "assistant", text: "Programarea a fost confirmată." },
      ]);
      setProposal(null);
      setSlots(await getSlots());
    } catch (reason) {
      showError(reason);
    } finally {
      setBusy(false);
    }
  }

  async function reject() {
    if (!proposal) return;
    try {
      await rejectProposal(proposal.id);
      setProposal(null);
    } catch (reason) {
      showError(reason);
    }
  }

  return (
    <main className="page">
      <header className="topbar">
        <a className="brand" href="#top">
          <span className="brand-mark">V</span>
          <span>
            voice<span className="brand-light">ai</span>
          </span>
        </a>
        <span className="secure-label">
          <span className="pulse" /> Laborator local
        </span>
      </header>
      <section className="hero" id="top">
        <p className="eyebrow">PROGRAMĂRI, SIMPLIFICATE</p>
        <h1>
          Hai să găsim
          <br />
          <em>un moment</em> potrivit.
        </h1>
        <p className="hero-copy">
          Spune-mi când ți-ar conveni. Verific disponibilitatea și pregătesc o
          propunere pe care o confirmi doar dacă este potrivită.
        </p>
      </section>
      <section className="workspace">
        <div className="conversation-card">
          <div className="card-heading">
            <div>
              <span className="eyebrow">ASISTENTUL TĂU</span>
              <h2>Cu ce te pot ajuta?</h2>
            </div>
            <span className="online-dot">● Disponibil</span>
          </div>
          <div className="conversation" aria-live="polite">
            {turns.length === 0 && (
              <div className="welcome">
                <div className="sparkle">✳</div>
                <p>
                  Poți încerca:{" "}
                  <button
                    className="suggestion"
                    onClick={() => setText("Ai un loc liber mâine dimineață?")}
                  >
                    „Ai un loc liber mâine dimineață?”
                  </button>
                </p>
              </div>
            )}
            {turns.map((turn, index) => (
              <div
                className={`bubble ${turn.role}`}
                key={`${index}-${turn.text}`}
              >
                <span>{turn.role === "assistant" ? "VA" : "TU"}</span>
                <p>{turn.text}</p>
              </div>
            ))}
            {busy && (
              <div className="thinking">
                <i />
                <i />
                <i /> <span>Pregătesc răspunsul</span>
              </div>
            )}
          </div>
          {proposal && (
            <div className="proposal">
              <div className="proposal-top">
                <span className="proposal-icon">✦</span>
                <div>
                  <span className="eyebrow">PROPUNERE DE PROGRAMARE</span>
                  <h3>
                    {new Intl.DateTimeFormat("ro-RO", {
                      weekday: "long",
                      day: "numeric",
                      month: "long",
                    }).format(new Date(proposal.startUtc))}
                  </h3>
                </div>
              </div>
              <p className="proposal-time">
                {new Intl.DateTimeFormat("ro-RO", {
                  hour: "2-digit",
                  minute: "2-digit",
                  timeZoneName: "short",
                }).format(new Date(proposal.startUtc))}{" "}
                <span>· {proposal.resourceLabel}</span>
              </p>
              <p className="proposal-note">
                Programarea nu este rezervată până nu o confirmi.
              </p>
              <div className="proposal-actions">
                <button
                  className="primary"
                  disabled={busy}
                  onClick={() => void confirm()}
                >
                  Confirmă programarea <span>→</span>
                </button>
                <button className="text-button" onClick={() => void reject()}>
                  Nu, mulțumesc
                </button>
              </div>
            </div>
          )}
          {error && (
            <div className="error" role="alert">
              {error === "ai_not_configured"
                ? "Configurează endpoint-ul și cheia Foundry în fișierul .env pentru conversație."
                : error === "speech_not_configured"
                  ? "Configurează cheia și endpoint-ul Speech în .env pentru voce. Poți continua prin text."
                  : error}
            </div>
          )}
          {audioUrl && (
            <audio className="audio-player" controls autoPlay src={audioUrl} />
          )}
          <form
            className="composer"
            onSubmit={(event) => void submitText(event)}
          >
            <input
              value={text}
              onChange={(event) => setText(event.target.value)}
              placeholder="Scrie un mesaj..."
              aria-label="Mesaj"
              disabled={busy}
            />
            <button
              className={`mic ${recording ? "recording" : ""}`}
              type="button"
              aria-label={
                recording
                  ? "Oprește înregistrarea"
                  : "Înregistrează mesaj audio"
              }
              disabled={!session || busy}
              onClick={() =>
                recording ? recorder.current?.stop() : void startRecording()
              }
            >
              {recording ? "■" : "◉"}
            </button>
            <button
              className="send"
              type="submit"
              disabled={busy || !text.trim()}
              aria-label="Trimite mesaj"
            >
              ↑
            </button>
          </form>
          <p className="privacy">
            Fără cont, nume sau număr de telefon. Rezervarea rămâne în acest
            laborator.
          </p>
        </div>
        <aside className="slots-card">
          <div className="slots-head">
            <div>
              <span className="eyebrow">AGENDA DEMO</span>
              <h2>Locuri disponibile</h2>
            </div>
            <span className="count">{slots.length}</span>
          </div>
          <p className="slots-hint">Orele sunt afișate în fusul tău local.</p>
          <div className="slot-list">
            {slots.slice(0, 6).map((slot) => (
              <div className="slot" key={slot.id}>
                <span className="slot-date">
                  {new Intl.DateTimeFormat("ro-RO", {
                    weekday: "short",
                    day: "2-digit",
                    month: "short",
                  }).format(new Date(slot.startUtc))}
                </span>
                <span className="slot-time">
                  {new Intl.DateTimeFormat("ro-RO", {
                    hour: "2-digit",
                    minute: "2-digit",
                  }).format(new Date(slot.startUtc))}
                </span>
                <span className="slot-arrow">↗</span>
              </div>
            ))}
            {slots.length === 0 && (
              <p className="empty">Nu sunt locuri disponibile momentan.</p>
            )}
          </div>
          <div className="timezone">
            <span>◷</span>
            <div>
              <b>Fusul tău orar</b>
              <small>{Intl.DateTimeFormat().resolvedOptions().timeZone}</small>
            </div>
          </div>
        </aside>
      </section>
      <footer>
        <span>
          VOICEAI <span>•</span> DEMO LOCAL
        </span>
        <span>Asistent pentru programări</span>
      </footer>
    </main>
  );
}
