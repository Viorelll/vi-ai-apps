import { useEffect, useRef, useState } from "react";
import type { Asset, Project, UploadedDocument } from "./models/schemas";
import { api } from "./services/api";
import { exportImage } from "./services/exportImage";
import { AssetGallery } from "./components/AssetGallery";
import { MaskEditor } from "./components/MaskEditor";
const working = (p: Project | null) =>
  p && ["Queued", "Generating", "Editing", "RetryWaiting"].includes(p.state);
export default function App() {
  const [brief, setBrief] = useState(
    "O fotografie publicitară premium pentru un parfum, lumină caldă, fundal minimalist și reflexii discrete.",
  );
  const [size, setSize] = useState("1024x1024");
  const [quality, setQuality] = useState("medium");
  const [banner, setBanner] = useState("Ofertă de laborator");
  const [project, setProject] = useState<Project | null>(null);
  const [history, setHistory] = useState<Project[]>([]);
  const [asset, setAsset] = useState<Asset | null>(null);
  const [product, setProduct] = useState<UploadedDocument | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [mode, setMode] = useState("");
  const activeId = useRef<string | null>(null);
  useEffect(() => {
    void api
      .list()
      .then(setHistory)
      .catch((e) => setError(e.message));
    void api
      .config()
      .then((c) => setMode(c.providerMode))
      .catch((e) => setError(e.message));
  }, []);
  useEffect(() => {
    activeId.current = project?.id ?? null;
    if (!working(project)) return;
    const controller = new AbortController();
    const timer = setInterval(() => {
      void api
        .get(project!.id, controller.signal)
        .then((p) => {
          if (activeId.current !== p.id) return;
          setProject(p);
          if (!working(p)) {
            setAsset(p.assets.at(-1) ?? null);
            void api
              .list()
              .then(setHistory)
              .catch(() => {});
          }
        })
        .catch((e) => {
          if (e.name !== "AbortError") setError(e.message);
        });
    }, 1200);
    return () => {
      clearInterval(timer);
      controller.abort();
    };
  }, [project?.id, project?.state]);
  const disabled = busy || !!working(project);
  async function perform(action: () => Promise<void>) {
    setError("");
    setBusy(true);
    try {
      await action();
    } catch (e) {
      setError(e instanceof Error ? e.message : "request_failed");
    } finally {
      setBusy(false);
    }
  }
  async function generate() {
    await perform(async () => {
      const p = await api.create(brief);
      setProject(p);
      setAsset(null);
      await api.action(p.id, "generate", {
        requestId: crypto.randomUUID(),
        revision: p.revision,
        size,
        quality,
        inputDocumentId: product?.id ?? null,
      });
      setProject(await api.get(p.id));
    });
  }
  async function edit(mask: Blob, prompt: string) {
    await perform(async () => {
      if (!project || !asset) return;
      const doc = await api.upload(mask, "mask.png");
      await api.action(project.id, "edits", {
        requestId: crypto.randomUUID(),
        revision: project.revision,
        inputAssetId: asset.id,
        maskDocumentId: doc.id,
        prompt,
        quality,
      });
      setProject(await api.get(project.id));
    });
  }
  async function variant() {
    await perform(async () => {
      if (!project || !asset) return;
      await api.action(project.id, "variants", {
        requestId: crypto.randomUUID(),
        revision: project.revision,
        assetId: asset.id,
        brief,
        quality,
      });
      setProject(await api.get(project.id));
    });
  }
  const parent = asset?.parentAssetId
    ? project?.assets.find((a) => a.id === asset.parentAssetId)
    : null;
  return (
    <>
      <header>
        <a className="brand" href="/">
          <span className="brand-icon">◈</span>Image<span>AI</span>
          <small>STUDIO</small>
        </a>
        <span className="header-tag">BRIEF → IMAGINE → VARIANTĂ</span>
        <div className="mode">
          <i />
          {mode === "Demo" ? "Laborator demonstrativ" : "Microsoft Foundry"}
        </div>
      </header>
      <main>
        <div className="intro">
          <p className="eyebrow">STUDIO PENTRU IMAGINI PUBLICITARE</p>
          <h1>
            Ideea ta. <em>Următoarea imagine.</em>
          </h1>
          <p>
            De la un brief la o compoziție vizuală. Generează, rafinează o
            regiune și păstrează fiecare versiune.
          </p>
        </div>
        {mode === "Demo" && (
          <div className="notice">
            Mod demonstrativ: imagini deterministe, fără apeluri sau costuri
            Azure. Pentru generare AI reală configurează Foundry.
          </div>
        )}
        {error && (
          <div className="error" role="alert">
            {error}
          </div>
        )}
        <div className="workspace">
          <aside className="panel controls">
            <div className="section-title">
              <h2>01 / Brief creativ</h2>
              <span>INPUT</span>
            </div>
            <label>
              Descrie imaginea
              <textarea
                rows={6}
                maxLength={4000}
                value={brief}
                onChange={(e) => setBrief(e.target.value)}
                placeholder="Produs, lumină, decor, stil vizual…"
              />
            </label>
            <p className="muted small">
              {brief.length} / 4000 · minimum 5 caractere
            </p>
            <label className="upload">
              Fotografie de produs
              <span>opțional · PNG / JPEG · max. 50 MiB</span>
              <input
                type="file"
                accept="image/png,image/jpeg"
                disabled={disabled}
                onChange={(e) => {
                  const file = e.target.files?.[0];
                  if (file)
                    void perform(async () =>
                      setProduct(await api.upload(file, file.name)),
                    );
                }}
              />
              {product && (
                <span className="filename">
                  ✓ {product.fileName}
                  <button
                    type="button"
                    onClick={() => setProduct(null)}
                    disabled={disabled}
                  >
                    Elimină
                  </button>
                </span>
              )}
            </label>
            <div className="field-row">
              <label>
                Format
                <select value={size} onChange={(e) => setSize(e.target.value)}>
                  <option value="1024x1024">Pătrat · 1:1</option>
                  <option value="1536x1024">Peisaj · 3:2</option>
                  <option value="1024x1536">Portret · 2:3</option>
                </select>
              </label>
              <label>
                Calitate
                <select
                  value={quality}
                  onChange={(e) => setQuality(e.target.value)}
                >
                  <option value="low">Draft</option>
                  <option value="medium">Standard</option>
                  <option value="high">Premium</option>
                </select>
              </label>
            </div>
            <button
              className="generate"
              disabled={disabled || brief.trim().length < 5}
              onClick={() => void generate()}
            >
              {disabled ? "Se procesează…" : "Generează imaginea"}
              <span>↗</span>
            </button>
            <div className="divider" />
            <label>
              Text exact pentru banner
              <input
                value={banner}
                maxLength={180}
                onChange={(e) => setBanner(e.target.value)}
              />
            </label>
            <p className="muted small">
              Textul se suprapune determinist la export.
            </p>
            {history.length > 0 && (
              <label>
                Proiecte salvate
                <select
                  value={project?.id ?? ""}
                  disabled={disabled}
                  onChange={(e) => {
                    const p = history.find((x) => x.id === e.target.value);
                    if (p) {
                      setProject(p);
                      setBrief(p.brief);
                      setAsset(p.assets.at(-1) ?? null);
                    }
                  }}
                >
                  <option value="">Alege un proiect</option>
                  {history.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.brief.slice(0, 45)} · {p.assets.length} variante
                    </option>
                  ))}
                </select>
              </label>
            )}
          </aside>
          <section className="panel results">
            <div className="section-title">
              <h2>02 / Pânza de lucru</h2>
              <span
                className={working(project) ? "status processing" : "status"}
              >
                {project?.state ?? "Pregătit"}
              </span>
            </div>
            {asset ? (
              <>
                <div className="preview">
                  <img src={api.content(asset.id)} alt="Imagine generată" />
                  <div className="preview-label">
                    {asset.width} × {asset.height} · PNG
                  </div>
                </div>
                <div className="button-row">
                  <button
                    className="secondary"
                    disabled={disabled}
                    onClick={() => void variant()}
                  >
                    Creează variantă din brief
                  </button>
                  <button
                    disabled={disabled}
                    onClick={() =>
                      void perform(() => exportImage(asset, banner))
                    }
                  >
                    Descarcă {banner ? "cu banner" : "PNG"} ↓
                  </button>
                </div>
                <AssetGallery
                  assets={project?.assets ?? []}
                  selected={asset.id}
                  onSelect={setAsset}
                />
                {parent && (
                  <details>
                    <summary>Compară cu originalul</summary>
                    <div className="comparison">
                      <figure>
                        <img src={api.content(parent.id)} alt="Înainte" />
                        <figcaption>Înainte</figcaption>
                      </figure>
                      <figure>
                        <img src={api.content(asset.id)} alt="După" />
                        <figcaption>După</figcaption>
                      </figure>
                    </div>
                  </details>
                )}
                <MaskEditor
                  key={asset.id}
                  asset={asset}
                  disabled={disabled}
                  onEdit={edit}
                />
              </>
            ) : (
              <div className={`empty ${disabled ? "loading" : ""}`}>
                <div className="empty-art">◈</div>
                <h3>
                  {disabled
                    ? "Construim imaginea ta"
                    : "Un spațiu pentru ideile tale"}
                </h3>
                <p>
                  {disabled
                    ? "Jobul este salvat. Revino din lista proiectelor după reîncărcare."
                    : "Scrie un brief și generează prima compoziție. Variantele vor apărea aici."}
                </p>
                <span>PNG · 1024 PX · VERSIUNI IMUTABILE</span>
              </div>
            )}
            {project && (
              <details
                className="runs"
                open={["Failed", "Uncertain"].includes(project.state)}
              >
                <summary>Istoric operațiuni · {project.runs.length}</summary>
                {project.runs.map((r) => (
                  <div key={r.id} className="run">
                    <strong>
                      {r.kind} · {r.state}
                    </strong>
                    <span>
                      {r.size} / {r.quality}
                    </span>
                    {r.errorCode && (
                      <p role="alert">
                        {r.errorCode}
                        {r.state === "Uncertain"
                          ? " — Apelul poate fi facturat. Verifică providerul înainte de a genera din nou."
                          : ""}
                      </p>
                    )}
                    <small>
                      {r.deployment ?? "Deployment în așteptare"} · usage:{" "}
                      {r.usageJson ?? "indisponibil"}
                    </small>
                  </div>
                ))}
              </details>
            )}
          </section>
        </div>
        <footer>
          ImageAI / Creative laboratory
          <span>Fără conturi. Fără campanii. Doar imagini și versiuni.</span>
        </footer>
      </main>
    </>
  );
}
