import { useState } from "react";
import type { Variant } from "../models/reading";
export function VariantPanel({
  variant,
  onCorrect,
  disabled,
}: {
  variant: Variant;
  onCorrect: (id: string, text: string) => Promise<void>;
  disabled: boolean;
}) {
  const [editing, setEditing] = useState(false);
  const [text, setText] = useState(variant.text);
  return (
    <section className="variant" lang={variant.language}>
      <h4>
        {variant.kind === "translated" ? "Traducere" : "Simplificare"}{" "}
        <span>{variant.language.toUpperCase()}</span>
      </h4>
      <p>{variant.text}</p>
      {variant.narration.map((segment) => (
        <div key={segment.id}>
          <audio
            controls
            preload="none"
            aria-label={`Ascultă ${variant.kind} ${variant.language}`}
            src={segment.audioUrl}
          />
          <p className="audio-note">Voce: {segment.voice}</p>
        </div>
      ))}
      {editing ? (
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            await onCorrect(variant.id, text);
            setEditing(false);
          }}
        >
          <label htmlFor={`edit-${variant.id}`}>Corectează varianta</label>
          <textarea
            id={`edit-${variant.id}`}
            value={text}
            maxLength={3000}
            onChange={(e) => setText(e.target.value)}
          />
          <button disabled={disabled}>Salvează și regenerează audio</button>
          <button
            type="button"
            className="quiet"
            onClick={() => setEditing(false)}
          >
            Renunță
          </button>
        </form>
      ) : (
        <button
          className="quiet"
          disabled={disabled}
          onClick={() => {
            setText(variant.text);
            setEditing(true);
          }}
        >
          Corectează textul
        </button>
      )}
    </section>
  );
}
