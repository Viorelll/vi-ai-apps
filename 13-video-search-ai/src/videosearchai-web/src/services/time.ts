export function timestamp(ms: number) {
  const seconds = Math.floor(ms / 1000);
  return `${Math.floor(seconds / 60)
    .toString()
    .padStart(2, "0")}:${(seconds % 60).toString().padStart(2, "0")}`;
}
export function seek(player: HTMLVideoElement, ms: number) {
  if (!Number.isFinite(ms) || ms < 0) return;
  player.currentTime = ms / 1000;
}
