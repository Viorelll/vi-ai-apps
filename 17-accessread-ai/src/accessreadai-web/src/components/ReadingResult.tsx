import type { Reading } from "../models/reading";
import { VariantPanel } from "./VariantPanel";
export function ReadingResult({
  reading,
  onCorrect,
  busy,
}: {
  reading: Reading;
  onCorrect: (id: string, text: string) => Promise<void>;
  busy: boolean;
}) {
  return (
    <section aria-labelledby="reading-title">
      <div className="section-heading">
        <div>
          <p className="eyebrow">LECTURĂ CU PROVENIENȚĂ</p>
          <h2 id="reading-title">Textul tău, mai ușor de parcurs</h2>
        </div>
        <a className="button quiet" href={`/api/readings/${reading.id}/export`}>
          Exportă JSON
        </a>
      </div>
      {reading.state === "NeedsReview" && (
        <p role="alert" className="notice">
          Verifică fragmentele marcate înainte de a folosi variantele. Audio nu
          este generat pentru rezultate incerte.
        </p>
      )}
      {reading.state === "Stale" && (
        <p role="alert">
          Documentul sursă a fost șters. Această lectură nu mai este actuală.
        </p>
      )}
      {reading.blocks.map((block) => (
        <article className="reading-block" key={block.id}>
          <header>
            <h3>
              Pagina {block.pageNumber} · Fragment {block.ordinal + 1}
            </h3>
            <a href={block.contentUrl} target="_blank" rel="noreferrer">
              Deschide sursa ↗
            </a>
          </header>
          {block.needsReview && (
            <p className="notice">
              Necesită verificare: {block.warning ?? "OCR neclar"}
            </p>
          )}
          {block.kind === "image" ? (
            <>
              <img
                className="source-image"
                src={block.imageUrl ?? undefined}
                alt={
                  block.imageDescriptions.join(". ") ||
                  "Imagine din document, descriere necunoscută"
                }
              />
              {block.imageDescriptions.map((text, i) => (
                <p key={i}>{text}</p>
              ))}
            </>
          ) : (
            <div className="text-columns">
              <section className="original" lang={reading.sourceLanguage}>
                <h4>Original</h4>
                <p>{block.original}</p>
              </section>
              {block.variants.map((variant) => (
                <VariantPanel
                  key={variant.id}
                  variant={variant}
                  onCorrect={onCorrect}
                  disabled={
                    busy || !["Ready", "NeedsReview"].includes(reading.state)
                  }
                />
              ))}
            </div>
          )}
        </article>
      ))}
    </section>
  );
}
