import { useState } from "react";
import type { Project } from "../models/schemas";
export function ClipCard({
  clip,
  busy,
  onRemix,
}: {
  clip: Project["clips"][number];
  busy: boolean;
  onRemix: (id: string, prompt: string) => void;
}) {
  const [prompt, setPrompt] = useState(
    "Schimbă paleta în tonuri calde de apus.",
  );
  return (
    <article className="clip-card">
      <video
        controls
        preload="metadata"
        src={clip.contentUrl}
        aria-label="Clip generat"
      />
      <div className="clip-body">
        <div className="row">
          <strong>
            {clip.sourceClipId ? "Variantă remix" : "Clip original"}
          </strong>
          <span className="pill">
            {clip.durationMs / 1000}s · {clip.width}×{clip.height}
          </span>
        </div>
        <p className="muted">{clip.prompt}</p>
        <a
          className="download"
          href={clip.contentUrl}
          download={`clip-${clip.id}.mp4`}
        >
          ↓ Descarcă MP4
        </a>
        <label>
          Instrucțiuni remix
          <textarea
            value={prompt}
            maxLength={4000}
            onChange={(e) => setPrompt(e.target.value)}
            disabled={busy}
          />
        </label>
        <button
          className="secondary"
          disabled={busy || !prompt.trim()}
          onClick={() => onRemix(clip.id, prompt)}
        >
          Creează remix
        </button>
        <details>
          <summary>Proveniență</summary>
          <p>Deployment: {clip.deployment}</p>
          <p className="hash">SHA-256: {clip.sha256}</p>
          <p>Clip sursă: {clip.sourceClipId ?? "—"}</p>
        </details>
      </div>
    </article>
  );
}
