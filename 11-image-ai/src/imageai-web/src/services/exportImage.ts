import { wrapText } from "../utils/canvas";
import type { Asset } from "../models/schemas";
import { api } from "./api";
export async function exportImage(asset: Asset, text: string) {
  const image = new Image();
  image.src = api.content(asset.id);
  await image.decode();
  const canvas = document.createElement("canvas");
  canvas.width = asset.width;
  canvas.height = asset.height;
  const ctx = canvas.getContext("2d")!;
  ctx.drawImage(image, 0, 0);
  if (text) {
    const font = Math.round(asset.width * 0.047);
    ctx.font = `600 ${font}px system-ui, sans-serif`;
    const pad = Math.round(asset.width * 0.06);
    const lines = wrapText(ctx, text, asset.width - pad * 2);
    const lineHeight = font * 1.35;
    const height = lines.length * lineHeight + pad;
    ctx.fillStyle = "rgba(12,15,22,.82)";
    ctx.fillRect(0, asset.height - height, asset.width, height);
    ctx.fillStyle = "#ffffff";
    ctx.textBaseline = "top";
    lines.forEach((line, i) =>
      ctx.fillText(line, pad, asset.height - height + pad / 2 + i * lineHeight),
    );
  }
  const blob = await new Promise<Blob>((resolve, reject) =>
    canvas.toBlob(
      (b) => (b ? resolve(b) : reject(new Error("export_failed"))),
      "image/png",
    ),
  );
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `imageai-${asset.id}${text ? "-banner" : ""}.png`;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
