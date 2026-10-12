import { useEffect, useState } from "react";
import { api } from "./services/api";
import {
  isActive,
  type Capabilities,
  type Project,
  type Scene,
} from "./models/schemas";
import { ClipCard } from "./components/ClipCard";
import { DocumentsPanel } from "./components/DocumentsPanel";
const seed =
  "Un videoclip cinematic pentru o cafenea artizanală: aburul se ridică dintr-o ceașcă, lumină caldă de dimineață, detalii naturale și o atmosferă liniștită.";
export default function App() {
  const [brief, setBrief] = useState(seed),
    [project, setProject] = useState<Project>(),
    [projects, setProjects] = useState<Project[]>([]),
    [caps, setCaps] = useState<Capabilities>(),
    [scenes, setScenes] = useState<Scene[]>([]),
    [seconds, setSeconds] = useState(8),
    [size, setSize] = useState("1280x720"),
    [sceneId, setSceneId] = useState(""),
    [busy, setBusy] = useState(false),
    [error, setError] = useState(""),
    [ack, setAck] = useState(false),
    [dirty, setDirty] = useState(false);
  const active = project?.jobs.some((j) => isActive(j.state)) ?? false,
    locked = busy || active;
  function select(p: Project) {
    setDirty(false);
    setProject(p);
    setBrief(p.brief);
    setScenes(p.storyboard?.scenes ?? []);
    setSceneId(p.storyboard?.scenes[0]?.id ?? "");
  }
  useEffect(() => {
    const c = new AbortController();
    void Promise.all([api.capabilities(c.signal), api.list(c.signal)])
      .then(([cap, list]) => {
        setCaps(cap);
        setSeconds(cap.seconds.includes(8) ? 8 : cap.seconds[0]);
        setSize(cap.sizes[0]);
        setProjects(list);
        if (list[0]) select(list[0]);
      })
      .catch((e) => {
        if (!c.signal.aborted) setError(String(e));
      });
    return () => c.abort();
  }, []);
  useEffect(() => {
    if (!project) return;
    const c = new AbortController();
    let running = false;
    const t = setInterval(async () => {
      if (running) return;
      running = true;
      try {
        const p = await api.get(project.id, c.signal);
        setProject(p);
        if (
          p.storyboard &&
          (!project.storyboard ||
            project.jobs.some(
              (j) => j.kind === "Storyboard" && isActive(j.state),
            ))
        ) {
          setScenes(p.storyboard.scenes);
          setSceneId(p.storyboard.scenes[0].id);
        }
      } catch (e) {
        if (!c.signal.aborted) setError(String(e));
      } finally {
        running = false;
      }
    }, 1800);
    return () => {
      c.abort();
      clearInterval(t);
    };
  }, [project?.id, project?.storyboard === null, active]);
  async function run(action: () => Promise<void>) {
    setBusy(true);
    setError("");
    try {
      await action();
      setProjects(await api.list());
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  }
  async function refresh() {
    if (project) setProject(await api.get(project.id));
  }
  return (
    <main>
      <header>
        <a className="brand" href="/">
          ◈{" "}
          <span>
            VideoStudio<span className="accent">AI</span>
          </span>
        </a>
        <span className="pill">
          {caps?.mode === "Demo"
            ? "DEMO · fără apeluri cloud"
            : "FOUNDRY · VIDEO PREVIEW"}
        </span>
      </header>
      <section className="hero">
        <p className="eyebrow">DE LA IDEE LA CADRU</p>
        <h1>
          Povestea ta.
          <br />
          <span>În mișcare.</span>
        </h1>
        <p>
          Transformă un brief într-un scenariu, generează un clip și explorează
          variante prin remix.
        </p>
        {caps?.mode === "Demo" && (
          <p className="demo-note">
            Mod demonstrativ: scenariul este un șablon, iar clipurile sunt
            fișiere MP4 de test marcate DEMO.
          </p>
        )}
      </section>
      <div className="studio">
        <aside>
          <div className="section-title">
            <span className="number">01</span>
            <h2>Brief creativ</h2>
          </div>
          <label>
            Ce poveste vrei să spui?
            <textarea
              className="brief"
              value={brief}
              onChange={(e) => setBrief(e.target.value)}
              disabled={busy}
              maxLength={8000}
            />
          </label>
          <p className="muted">
            Descrie subiectul, atmosfera, lumina și mișcarea camerei.
          </p>
          <button
            disabled={busy || !brief.trim()}
            onClick={() =>
              void run(async () => {
                const p = await api.create(brief);
                select(p);
                await api.storyboard(p.id, p.revision);
                setProject(await api.get(p.id));
              })
            }
          >
            {busy ? "Se procesează…" : "Creează scenariul →"}
          </button>
          <div className="history">
            <h3>Proiecte recente</h3>
            {projects.map((p) => (
              <button
                className={`history-item ${p.id === project?.id ? "selected" : ""}`}
                key={p.id}
                disabled={busy}
                onClick={() =>
                  void run(async () => select(await api.get(p.id)))
                }
              >
                <span>{p.storyboard?.title ?? p.brief.slice(0, 50)}</span>
                <small>{p.state}</small>
              </button>
            ))}
          </div>
        </aside>
        <section className="workspace">
          <div className="section-title">
            <span className="number">02</span>
            <h2>Scenariu & generare</h2>
            {project && (
              <span className="pill status" aria-live="polite">
                {project.state}
              </span>
            )}
          </div>
          {error && (
            <div className="error" role="alert">
              {error}
              <button className="text-button" onClick={() => void run(refresh)}>
                Reîncarcă
              </button>
            </div>
          )}
          {!project && (
            <div className="empty">
              <span>✧</span>
              <h3>Primul cadru începe cu o idee.</h3>
              <p>Completează brief-ul și creează scenariul.</p>
            </div>
          )}
          {project && (
            <>
              <h3>{project.storyboard?.title ?? "Pregătim scenariul…"}</h3>
              {scenes.map((s, i) => (
                <article className="scene" key={s.id}>
                  <div className="row">
                    <strong>
                      Scena {i + 1} · {s.title}
                    </strong>
                    <small>{s.id}</small>
                  </div>
                  <label>
                    Prompt vizual
                    <textarea
                      aria-label={`Prompt scena ${i + 1}`}
                      value={s.prompt}
                      maxLength={4000}
                      disabled={locked}
                      onChange={(e) => {
                        setScenes(
                          scenes.map((x) =>
                            x.id === s.id
                              ? { ...x, prompt: e.target.value }
                              : x,
                          ),
                        );
                        setDirty(true);
                      }}
                    />
                  </label>
                </article>
              ))}
              {scenes.length > 0 && (
                <>
                  <div className="controls">
                    <label>
                      Durată
                      <select
                        value={seconds}
                        disabled={locked}
                        onChange={(e) => setSeconds(Number(e.target.value))}
                      >
                        {caps?.seconds.map((s) => (
                          <option key={s} value={s}>
                            {s} secunde
                          </option>
                        ))}
                      </select>
                    </label>
                    <label>
                      Format
                      <select
                        value={size}
                        disabled={locked}
                        onChange={(e) => setSize(e.target.value)}
                      >
                        {caps?.sizes.map((s) => (
                          <option key={s}>{s}</option>
                        ))}
                      </select>
                    </label>
                    <label>
                      Scena
                      <select
                        value={sceneId}
                        disabled={locked}
                        onChange={(e) => setSceneId(e.target.value)}
                      >
                        {scenes.map((s) => (
                          <option key={s.id} value={s.id}>
                            {s.title}
                          </option>
                        ))}
                      </select>
                    </label>
                  </div>
                  <div className="actions">
                    <button
                      className="secondary"
                      disabled={locked || scenes.some((s) => !s.prompt.trim())}
                      onClick={() =>
                        void run(async () => {
                          const confirmed = await api.confirm(
                            project.id,
                            project.revision,
                            scenes,
                          );
                          setProject(confirmed);
                          setDirty(false);
                        })
                      }
                    >
                      {project.confirmed && !dirty
                        ? "✓ Scenariu confirmat"
                        : "Confirmă scenariul"}
                    </button>
                    <button
                      disabled={
                        locked ||
                        dirty ||
                        !project.confirmed ||
                        !caps?.videoEnabled
                      }
                      onClick={() =>
                        void run(async () => {
                          await api.generate(
                            project.id,
                            project.revision,
                            sceneId,
                            seconds,
                            size,
                          );
                          await refresh();
                        })
                      }
                    >
                      Generează clip ✧
                    </button>
                  </div>
                </>
              )}
              <div className="jobs" aria-live="polite">
                {project.jobs.map((j) => (
                  <div className="job" key={j.id}>
                    <span className={isActive(j.state) ? "dot pulse" : "dot"} />
                    <span>
                      {j.kind} · {j.state}
                    </span>
                    {j.errorCode && <small>{j.errorCode}</small>}
                    {["Failed", "Uncertain"].includes(j.state) && (
                      <>
                        <label className="check">
                          {j.state === "Uncertain" && (
                            <>
                              <input
                                type="checkbox"
                                checked={ack}
                                onChange={(e) => setAck(e.target.checked)}
                              />
                              Înțeleg că relansarea poate genera un cost
                              suplimentar.
                            </>
                          )}
                        </label>
                        <button
                          className="secondary"
                          disabled={locked || (j.state === "Uncertain" && !ack)}
                          onClick={() =>
                            void run(async () => {
                              await api.retry(j.id, project.revision, ack);
                              setAck(false);
                              await refresh();
                            })
                          }
                        >
                          Relansează explicit
                        </button>
                      </>
                    )}
                  </div>
                ))}
              </div>
              {project.clips.length > 0 && (
                <>
                  <div className="section-title">
                    <span className="number">03</span>
                    <h2>Clipuri & variante</h2>
                  </div>
                  <div className="clips">
                    {project.clips.map((c) => (
                      <ClipCard
                        key={c.id}
                        clip={c}
                        busy={locked}
                        onRemix={(id, prompt) =>
                          void run(async () => {
                            await api.remix(id, project.revision, prompt);
                            await refresh();
                          })
                        }
                      />
                    ))}
                  </div>
                </>
              )}
            </>
          )}
        </section>
      </div>
      <DocumentsPanel />
      <footer>
        VideoStudioAI · un clip per job · fișiere păstrate local în MinIO · fără
        conturi
      </footer>
    </main>
  );
}
