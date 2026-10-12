import { useEffect, useRef } from "react";
import { seek, timestamp } from "../services/time";
export type Playback = {
  videoId: string;
  title: string;
  startMs: number;
  nonce: number;
};
export function VideoPlayer({ playback }: { playback: Playback | undefined }) {
  const ref = useRef<HTMLVideoElement>(null);
  useEffect(() => {
    const player = ref.current;
    if (player && playback && player.readyState >= 1)
      seek(player, playback.startMs);
  }, [playback]);
  return (
    <section className="player-panel">
      <div className="panel-heading">
        <h2>Player original</h2>
        {playback && <span>{timestamp(playback.startMs)}</span>}
      </div>
      {playback ? (
        <>
          <video
            key={playback.videoId}
            ref={ref}
            controls
            preload="metadata"
            src={`/api/video/library/${playback.videoId}/content`}
            onLoadedMetadata={() =>
              ref.current && seek(ref.current, playback.startMs)
            }
            aria-label="Player video original"
          />
          <p className="player-caption">{playback.title}</p>
        </>
      ) : (
        <div className="player-empty">
          <span className="play-icon">▶</span>
          <p>Selectează un clip sau o citare</p>
          <small>Vei vedea momentul exact din fișierul original.</small>
        </div>
      )}
    </section>
  );
}
