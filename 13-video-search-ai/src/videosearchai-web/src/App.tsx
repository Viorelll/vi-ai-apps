import { useCallback, useEffect, useRef, useState } from "react";
import * as api from "./services/api";
import type { Video, Answer, Citation } from "./models/schemas";
import { VideoLibrary } from "./components/VideoLibrary";
import { VideoPlayer, type Playback } from "./components/VideoPlayer";
import { SearchResults } from "./components/SearchResults";
export default function App() {
  const [videos, setVideos] = useState<Video[]>([]);
  const [selected, setSelected] = useState<string[]>([]);
  const [question, setQuestion] = useState("");
  const [answer, setAnswer] = useState<Answer>();
  const [playback, setPlayback] = useState<Playback>();
  const [busy, setBusy] = useState(false);
  const [searching, setSearching] = useState(false);
  const [error, setError] = useState("");
  const [mode, setMode] = useState("");
  const [runId, setRunId] = useState<string>();
  const controller = useRef<AbortController | undefined>(undefined);
  const refresh = useCallback(async (signal?: AbortSignal) => {
    const loaded = await api.list(signal);
    setVideos(loaded);
    setSelected((current) =>
      current.filter((id) =>
        loaded.some((v) => v.id === id && v.state === "Ready"),
      ),
    );
  }, []);
  useEffect(() => {
    const c = new AbortController();
    controller.current = c;
    let timer: ReturnType<typeof setTimeout>;
    const tick = async () => {
      try {
        await refresh(c.signal);
      } catch (e) {
        if (!c.signal.aborted)
          setError(e instanceof Error ? e.message : "load_failed");
      }
      if (!c.signal.aborted) timer = setTimeout(tick, 1500);
    };
    void tick();
    void api
      .config(c.signal)
      .then((v) => setMode(v.mode))
      .catch(() => {});
    return () => {
      c.abort();
      clearTimeout(timer);
    };
  }, [refresh]);
  useEffect(() => {
    if (!runId) return;
    const c = new AbortController();
    let timer: ReturnType<typeof setTimeout>;
    const poll = async () => {
      try {
        const run = await api.loadRun(runId, c.signal);
        if (run.state === "Answered" && run.result) {
          setAnswer(run.result);
          setSearching(false);
          setRunId(undefined);
        } else if (run.state === "Failed" || run.state === "Stale") {
          setError(run.errorCode ?? "Sursele s-au schimbat. Repetă căutarea.");
          setAnswer(undefined);
          setSearching(false);
          setRunId(undefined);
        } else timer = setTimeout(poll, 1000);
      } catch (e) {
        if (!c.signal.aborted) {
          setError(e instanceof Error ? e.message : "search_failed");
          setSearching(false);
          setRunId(undefined);
        }
      }
    };
    void poll();
    return () => {
      c.abort();
      clearTimeout(timer);
    };
  }, [runId]);
  async function action(work: () => Promise<unknown>) {
    setBusy(true);
    setError("");
    try {
      await work();
      await refresh(controller.current?.signal);
    } catch (e) {
      if (!controller.current?.signal.aborted)
        setError(e instanceof Error ? e.message : "request_failed");
    } finally {
      setBusy(false);
    }
  }
  async function ask() {
    if (searching || !question.trim() || selected.length === 0) return;
    setSearching(true);
    setError("");
    setAnswer(undefined);
    try {
      const run = await api.search(
        question.trim(),
        selected,
        controller.current?.signal,
      );
      setRunId(run.searchRunId);
    } catch (e) {
      if (!controller.current?.signal.aborted)
        setError(e instanceof Error ? e.message : "search_failed");
      setSearching(false);
    }
  }
  function openCitation(c: Citation) {
    const v = videos.find((x) => x.id === c.videoId);
    if (!v || v.state !== "Ready") {
      setError("Sursa nu mai este disponibilă.");
      return;
    }
    setPlayback({
      videoId: c.videoId,
      title: v.title,
      startMs: c.startMs,
      nonce: Date.now(),
    });
  }
  return (
    <>
      <header>
        <div className="brand">
          <span className="brand-mark">▶</span>
          <div>
            <strong>
              VideoSearch<span>AI</span>
            </strong>
            <small>CAUTĂ ÎN VIDEO. GĂSEȘTE MOMENTUL.</small>
          </div>
        </div>
        <span className="mode-badge">
          {mode === "Demo"
            ? "Mod demo · date artificiale"
            : "Microsoft Foundry"}
        </span>
      </header>
      <main>
        <div className="intro">
          <p className="eyebrow">BIBLIOTECĂ VIDEO INTELIGENTĂ</p>
          <h1>
            Răspunsul e într-un clip.
            <br />
            <span>Ajungi direct la el.</span>
          </h1>
          <p>
            Încarcă tutoriale, selectează sursele și întreabă. Fiecare răspuns
            te duce la momentul citat.
          </p>
        </div>
        {mode === "Demo" && (
          <aside className="demo-note">
            Demo-ul procesează doar clipul artificial{" "}
            <code>seeds/postgresql-course.mp4</code>. Pentru videoclipurile
            tale, configurează modul Foundry.
          </aside>
        )}
        {error && (
          <div role="alert" className="error">
            {error}
            <button onClick={() => setError("")} aria-label="Închide eroarea">
              ×
            </button>
          </div>
        )}
        <div className="workspace">
          <aside>
            <label className={`upload-zone ${busy ? "disabled" : ""}`}>
              <span className="upload-icon">↑</span>
              <strong>{busy ? "Se încarcă…" : "Încarcă un tutorial"}</strong>
              <small>MP4 sau WebM · maximum 50 MiB / 30 minute</small>
              <input
                type="file"
                accept="video/mp4,video/webm"
                disabled={busy}
                aria-label="Încarcă video"
                onChange={(e) => {
                  const f = e.target.files?.[0];
                  e.target.value = "";
                  if (f) {
                    if (f.size > 50 * 1024 * 1024) {
                      setError("Fișierul depășește 50 MiB.");
                      return;
                    }
                    void action(() =>
                      api.upload(f, controller.current?.signal),
                    );
                  }
                }}
              />
            </label>
            <VideoLibrary
              videos={videos}
              selected={selected}
              busy={busy || searching}
              onSelect={(id) =>
                setSelected((s) =>
                  s.includes(id) ? s.filter((x) => x !== id) : [...s, id],
                )
              }
              onPlay={(v) =>
                setPlayback({
                  videoId: v.id,
                  title: v.title,
                  startMs: 0,
                  nonce: Date.now(),
                })
              }
              onIndex={(v) =>
                void action(async () => {
                  setAnswer(undefined);
                  await api.index(v.id, v.revision, controller.current?.signal);
                })
              }
              onDelete={(v) =>
                void action(async () => {
                  await api.remove(v.documentId, controller.current?.signal);
                  setAnswer(undefined);
                  if (playback?.videoId === v.id) setPlayback(undefined);
                })
              }
            />
          </aside>
          <div className="search-panel">
            <form
              className="question-box"
              onSubmit={(e) => {
                e.preventDefault();
                void ask();
              }}
            >
              <label htmlFor="question">Ce vrei să găsești?</label>
              <textarea
                id="question"
                placeholder="De exemplu: Cum creez un index pentru query în PostgreSQL?"
                maxLength={2000}
                value={question}
                onChange={(e) => setQuestion(e.target.value)}
                disabled={searching}
              />
              <div className="question-actions">
                <span>
                  {selected.length}{" "}
                  {selected.length === 1
                    ? "sursă selectată"
                    : "surse selectate"}
                </span>
                <button
                  className="primary"
                  disabled={
                    busy ||
                    searching ||
                    !question.trim() ||
                    selected.length === 0
                  }
                >
                  {searching
                    ? "Caută și verifică dovezile…"
                    : "Caută în clipuri ↗"}
                </button>
              </div>
            </form>
            <SearchResults answer={answer} onCitation={openCitation} />
            <VideoPlayer playback={playback} />
          </div>
        </div>
      </main>
      <footer>
        VideoSearchAI · Citări verificabile, timestamps din original · Fără
        conturi
      </footer>
    </>
  );
}
