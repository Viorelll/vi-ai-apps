export default function SourceRegion({ json }: { json: string }) {
  try {
    const region = JSON.parse(json).boundingRegion;
    const points: number[] = region.polygon;
    if (
      !Array.isArray(points) ||
      points.length < 4 ||
      !points.every(Number.isFinite)
    )
      return null;
    const xs = points.filter((_, n) => n % 2 === 0),
      ys = points.filter((_, n) => n % 2 === 1);
    const format = (n: number) => n.toFixed(2);
    return (
      <small className="muted">
        Regiune OCR · x {format(Math.min(...xs))}–{format(Math.max(...xs))}, y{" "}
        {format(Math.min(...ys))}–{format(Math.max(...ys))}
      </small>
    );
  } catch {
    return null;
  }
}
