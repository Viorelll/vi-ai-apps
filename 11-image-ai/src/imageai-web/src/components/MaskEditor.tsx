import { useEffect, useRef, useState } from "react";
import type { Asset } from "../models/schemas";
import { api } from "../services/api";
import { canvasPoint } from "../utils/canvas";
type Props = {
  asset: Asset;
  disabled: boolean;
  onEdit: (mask: Blob, prompt: string) => Promise<void>;
};
export function MaskEditor({ asset, disabled, onEdit }: Props) {
  const canvas = useRef<HTMLCanvasElement>(null);
  const previous = useRef<{ x: number; y: number } | null>(null);
  const [brush, setBrush] = useState(48);
  const [prompt, setPrompt] = useState(
    "Schimbă fundalul cu un decor elegant, păstrând produsul.",
  );
  const [painted, setPainted] = useState(false);
  function reset() {
    const c = canvas.current;
    if (!c) return;
    const ctx = c.getContext("2d")!;
    ctx.globalCompositeOperation = "source-over";
    ctx.fillStyle = "white";
    ctx.fillRect(0, 0, c.width, c.height);
    setPainted(false);
    previous.current = null;
  }
  useEffect(reset, [asset.id]);
  async function submit() {
    const blob = await new Promise<Blob | null>((resolve) =>
      canvas.current!.toBlob(resolve, "image/png"),
    );
    if (blob) await onEdit(blob, prompt);
  }
  return (
    <section className="editor">
      <div className="section-title">
        <h3>Editează o regiune</h3>
        <span>PNG · alpha</span>
      </div>
      <p className="muted">
        Desenează pe fundal. Zonele transparente vor fi editate; restul rămâne
        protejat de mască.
      </p>
      <div
        className="mask-stage"
        style={{ aspectRatio: `${asset.width}/${asset.height}` }}
      >
        <img src={api.content(asset.id)} alt="Imagine pentru editare" />
        <canvas
          ref={canvas}
          width={asset.width}
          height={asset.height}
          aria-label="Mască de editare"
          onPointerDown={(e) => {
            if (disabled) return;
            e.currentTarget.setPointerCapture(e.pointerId);
            const p = canvasPoint(
              e.clientX,
              e.clientY,
              e.currentTarget.getBoundingClientRect(),
              asset.width,
              asset.height,
            );
            previous.current = p;
            const ctx = e.currentTarget.getContext("2d")!;
            ctx.globalCompositeOperation = "destination-out";
            ctx.beginPath();
            ctx.arc(p.x, p.y, brush / 2, 0, Math.PI * 2);
            ctx.fill();
            setPainted(true);
          }}
          onPointerMove={(e) => {
            if (!previous.current || disabled) return;
            const p = canvasPoint(
              e.clientX,
              e.clientY,
              e.currentTarget.getBoundingClientRect(),
              asset.width,
              asset.height,
            );
            const ctx = e.currentTarget.getContext("2d")!;
            ctx.globalCompositeOperation = "destination-out";
            ctx.lineCap = "round";
            ctx.lineWidth = brush;
            ctx.beginPath();
            ctx.moveTo(previous.current.x, previous.current.y);
            ctx.lineTo(p.x, p.y);
            ctx.stroke();
            previous.current = p;
          }}
          onPointerUp={() => (previous.current = null)}
          onPointerCancel={() => (previous.current = null)}
        />
      </div>
      <label>
        Mărimea pensulei: {brush}px
        <input
          type="range"
          min="10"
          max="180"
          value={brush}
          onChange={(e) => setBrush(+e.target.value)}
        />
      </label>
      <label>
        Instrucțiuni de editare
        <textarea
          value={prompt}
          onChange={(e) => setPrompt(e.target.value)}
          maxLength={4000}
        />
      </label>
      <div className="button-row">
        <button
          type="button"
          className="secondary"
          onClick={reset}
          disabled={disabled}
        >
          Resetează masca
        </button>
        <button
          type="button"
          disabled={disabled || !painted || prompt.trim().length < 5}
          onClick={() => void submit()}
        >
          Aplică editarea
        </button>
      </div>
      <p className="muted small">
        Masca ghidează modelul; fidelitatea pixelilor nu este garantată de
        Foundry. Originalul este păstrat separat.
      </p>
    </section>
  );
}
