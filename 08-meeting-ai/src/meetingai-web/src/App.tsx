import { useRef, useState } from 'react';
import {
  AudioLines,
  Check,
  ChevronRight,
  CircleHelp,
  Download,
  FileAudio,
  LoaderCircle,
  Mic2,
  Pencil,
  Play,
  Plus,
  Sparkles,
  UploadCloud,
  WandSparkles,
} from 'lucide-react';
type Segment = {
  id: string;
  ordinal: number;
  startMs: number;
  endMs: number;
  speakerId: string | null;
  speakerLabel: string | null;
  text: string;
};
type Result = {
  id: string;
  title: string;
  state: string;
  summary: string;
  revision: number;
  transcriptRevision: number;
  segments: Segment[];
  decisions: { id: string; text: string; segmentIds: string[] }[];
  actionItems: {
    id: string;
    text: string;
    assigneeLabel: string | null;
    dueText: string | null;
    segmentIds: string[];
  }[];
};
const stamp = (ms: number) =>
  `${Math.floor(ms / 60000)}:${String(Math.floor(ms / 1000) % 60).padStart(2, '0')}`;
export default function App() {
  const [meeting, setMeeting] = useState<Result | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [editing, setEditing] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const player = useRef<HTMLAudioElement>(null);
  const [url, setUrl] = useState('');
  async function demo() {
    setBusy(true);
    setError('');
    try {
      const r = await fetch('/api/meetings/demo', { method: 'POST' });
      if (!r.ok) throw new Error('Nu am putut încărca demonstrația.');
      setMeeting(await r.json());
      setFile(null);
      if (url) URL.revokeObjectURL(url);
      setUrl('');
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  async function analyze() {
    if (!file) return;
    setBusy(true);
    setError('');
    try {
      const form = new FormData();
      form.append('file', file);
      const up = await fetch('/api/meetings', { method: 'POST', body: form });
      if (!up.ok) throw new Error((await up.json()).error ?? 'Încărcarea a eșuat.');
      const x = await up.json();
      const r = await fetch(`/api/meetings/${x.meetingId}/analyze`, { method: 'POST' });
      if (!r.ok) throw new Error('Analiza a eșuat. Verifică serviciile AI și stocarea.');
      setMeeting(await r.json());
      if (url.startsWith('blob:')) URL.revokeObjectURL(url);
      setUrl(`/api/meetings/${x.meetingId}/audio`);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  async function save() {
    if (!meeting) return;
    setBusy(true);
    try {
      const r = await fetch(`/api/meetings/${meeting.id}/transcript`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          revision: meeting.revision,
          segments: meeting.segments.map((s) => ({
            id: s.id,
            text: s.text,
            speakerLabel: s.speakerLabel,
          })),
        }),
      });
      if (!r.ok) throw new Error('Salvarea corecturilor a eșuat. Reîncarcă și încearcă din nou.');
      setMeeting(await r.json());
      setEditing(false);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  function choose(f?: File) {
    if (!f) return;
    if (url) URL.revokeObjectURL(url);
    setFile(f);
    setMeeting(null);
    setUrl(URL.createObjectURL(f));
  }
  const seek = (ms: number) => {
    if (player.current) {
      player.current.currentTime = ms / 1000;
      void player.current.play();
    }
  };
  return (
    <main className="shell">
      <header>
        <a className="brand" href="#">
          <span className="brand-icon">
            <AudioLines size={20} />
          </span>
          <span>
            meeting<span className="brand-accent">ai</span>
          </span>
        </a>
        <div className="header-right">
          <span className="saved">
            <i /> local, privat
          </span>
          <button className="help">
            <CircleHelp size={18} />
            <span>Ajutor</span>
          </button>
          <span className="avatar">M</span>
        </div>
      </header>
      <section className="hero">
        <div className="eyebrow">
          <span className="eyebrow-dot" /> NOTE INTELIGENTE PENTRU ȘEDINȚE
        </div>
        <h1>
          Ședința ta, <em>înțeleasă.</em>
        </h1>
        <p>
          Transcriere, decizii și acțiuni — cu fiecare concluzie legată de momentul din
          înregistrare.
        </p>
        <div className="trust">
          <span>
            <Check /> Fără cont
          </span>
          <span>
            <Check /> Datele rămân private
          </span>
          <span>
            <Check /> Surse verificabile
          </span>
        </div>
      </section>
      <section className="workspace">
        <div className="work-head">
          <div>
            <span className="step">SPAȚIUL TĂU DE LUCRU</span>
            <h2>Începe o ședință</h2>
          </div>
          <button className="demo" onClick={demo} disabled={busy}>
            <Sparkles size={16} /> Încearcă o demonstrație
          </button>
        </div>
        <label
          className={`drop ${file ? 'has-file' : ''}`}
          onDragOver={(e) => e.preventDefault()}
          onDrop={(e) => {
            e.preventDefault();
            choose(e.dataTransfer.files[0]);
          }}
        >
          <input
            hidden
            type="file"
            accept="audio/*,.mp4"
            onChange={(e) => choose(e.target.files?.[0])}
          />
          <span className="upload-icon">{file ? <FileAudio /> : <UploadCloud />}</span>
          <span className="drop-title">{file ? file.name : 'Trage înregistrarea aici'}</span>
          <span className="drop-hint">
            sau <u>alege un fișier</u> de pe dispozitiv
          </span>
          <span className="drop-meta">MP3, WAV, M4A, OGG · max. 50 MB</span>
        </label>
        {file && (
          <div className="file-actions">
            <span>
              <FileAudio size={17} />
              {(file.size / 1024 / 1024).toFixed(1)} MB
            </span>
            <button className="analyze" disabled={busy} onClick={analyze}>
              {busy ? <LoaderCircle className="spin" size={17} /> : <WandSparkles size={17} />}{' '}
              Analizează înregistrarea <ChevronRight size={17} />
            </button>
          </div>
        )}
        {error && <div className="error">{error}</div>}
        {meeting && (
          <article className="results">
            <div className="result-head">
              <div>
                <span className="step">REZULTATUL ȘEDINȚEI</span>
                <h2>{meeting.title}</h2>
                <span className="result-sub">
                  Transcript · {meeting.segments.length} momente · {meeting.state}
                </span>
              </div>
              <button
                className="export"
                onClick={() => window.location.assign(`/api/meetings/${meeting.id}/export`)}
              >
                <Download size={16} /> Exportă JSON
              </button>
            </div>
            {url && <audio ref={player} src={url} controls />}
            <p className="summary">{meeting.summary}</p>
            <div className="result-columns">
              <div className="transcript">
                <div className="section-title">
                  <span>
                    <Mic2 size={16} /> TRANSCRIPT
                  </span>
                  <button onClick={() => (editing ? save() : setEditing(true))}>
                    {editing ? (
                      <>
                        <Check size={14} /> Salvează
                      </>
                    ) : (
                      <>
                        <Pencil size={14} /> Corectează
                      </>
                    )}
                  </button>
                </div>
                {meeting.segments.map((s) => (
                  <div className="segment" key={s.id}>
                    <button className="time" onClick={() => seek(s.startMs)}>
                      <Play size={12} />
                      {stamp(s.startMs)}
                    </button>
                    <div>
                      <span className="speaker">{s.speakerLabel ?? s.speakerId ?? 'Vorbitor'}</span>
                      {editing && (
                        <input
                          aria-label="Eticheta vorbitorului"
                          value={s.speakerLabel ?? s.speakerId ?? 'Vorbitor'}
                          onChange={(e) =>
                            setMeeting({
                              ...meeting,
                              segments: meeting.segments.map((x) =>
                                x.id === s.id ? { ...x, speakerLabel: e.target.value } : x,
                              ),
                            })
                          }
                        />
                      )}
                      {editing ? (
                        <input
                          value={s.text}
                          onChange={(e) =>
                            setMeeting({
                              ...meeting,
                              segments: meeting.segments.map((x) =>
                                x.id === s.id ? { ...x, text: e.target.value } : x,
                              ),
                            })
                          }
                        />
                      ) : (
                        <p>{s.text}</p>
                      )}
                    </div>
                  </div>
                ))}
              </div>
              <aside>
                <div className="section-title">
                  <span>
                    <Sparkles size={16} /> DECIZII
                  </span>
                </div>
                {meeting.decisions.map((d) => (
                  <button
                    className="insight"
                    key={d.id}
                    onClick={() => {
                      const s = meeting.segments.find((x) => d.segmentIds.includes(x.id));
                      if (s) seek(s.startMs);
                    }}
                  >
                    <span className="insight-icon">
                      <Check size={15} />
                    </span>
                    <span>
                      {d.text}
                      <small>
                        <Play size={10} />{' '}
                        {stamp(
                          meeting.segments.find((s) => d.segmentIds.includes(s.id))?.startMs ?? 0,
                        )}{' '}
                        · sursă audio
                      </small>
                    </span>
                  </button>
                ))}
                <div className="section-title tasks-title">
                  <span>
                    <Check size={16} /> ACȚIUNI URMĂTOARE
                  </span>
                </div>
                {meeting.actionItems.map((a) => (
                  <button
                    className="insight task"
                    key={a.id}
                    onClick={() => {
                      const s = meeting.segments.find((x) => a.segmentIds.includes(x.id));
                      if (s) seek(s.startMs);
                    }}
                  >
                    <span className="checkbox" />
                    <span>
                      {a.text}
                      <small>
                        <Play size={10} />{' '}
                        {stamp(
                          meeting.segments.find((s) => a.segmentIds.includes(s.id))?.startMs ?? 0,
                        )}{' '}
                        · responsabil neatribuit
                      </small>
                    </span>
                  </button>
                ))}
              </aside>
            </div>
          </article>
        )}
        {!meeting && !file && (
          <div className="empty-hint">
            <span>
              <Plus size={16} />
            </span>{' '}
            Încarcă un fișier audio sau deschide o demonstrație pentru a începe
          </div>
        )}
      </section>
      <footer>
        <span>
          MeetingAI <b>·</b> Note clare, fără efort.
        </span>
        <span>
          Procesare locală ·{' '}
          <button
            onClick={() => {
              setMeeting(null);
              setFile(null);
            }}
          >
            Șterge sesiunea
          </button>
        </span>
      </footer>
    </main>
  );
}
