import type { Asset } from "../models/schemas";
import { api } from "../services/api";
export function AssetGallery({
  assets,
  selected,
  onSelect,
}: {
  assets: Asset[];
  selected: string | undefined;
  onSelect: (asset: Asset) => void;
}) {
  return (
    <div className="gallery">
      {assets.map((a, i) => (
        <button
          key={a.id}
          className={`asset-card ${a.id === selected ? "selected" : ""}`}
          onClick={() => onSelect(a)}
        >
          <img
            src={api.content(a.id)}
            alt={`Varianta ${i + 1}`}
            loading="lazy"
          />
          <span>
            V{i + 1} · {a.parentAssetId ? "Derivată" : "Original"}
            <small>
              {a.width} × {a.height}
            </small>
          </span>
        </button>
      ))}
    </div>
  );
}
