import type { Output, Source } from "../models/contracts";
export default function Evidence({
  output,
  sources,
}: {
  output: Output;
  sources: Source[];
}) {
  return (
    <div className="evidence">
      {output.citations.map((c, i) => {
        const s = sources.find((x) => x.id === c.sourceId);
        return (
          <blockquote key={`${c.sourceId}-${i}`}>
            <p>{c.quote}</p>
            {s && (
              <a href={s.officialUrl} target="_blank" rel="noreferrer">
                {s.title} ↗
              </a>
            )}
            {s && (
              <small>
                {" "}
                Verificat: {new Date(s.checkedAtUtc).toLocaleDateString("ro")}
              </small>
            )}
          </blockquote>
        );
      })}
    </div>
  );
}
