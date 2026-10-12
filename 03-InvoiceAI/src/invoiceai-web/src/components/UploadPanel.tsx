import { useState } from "react";
export default function UploadPanel({
  onUpload,
  busy,
}: {
  onUpload: (file: File, kind: string) => Promise<void>;
  busy: boolean;
}) {
  const [kind, setKind] = useState("invoice");
  return (
    <section className="upload">
      <label htmlFor="kind">Tip document</label>
      <select
        id="kind"
        value={kind}
        disabled={busy}
        onChange={(e) => setKind(e.target.value)}
      >
        <option value="invoice">Factură</option>
        <option value="receipt">Bon fiscal</option>
      </select>
      <input
        id="file"
        type="file"
        accept=".pdf,.png,.jpg,.jpeg,.tif,.tiff"
        disabled={busy}
        onChange={async (e) => {
          const f = e.target.files?.[0];
          const input = e.currentTarget;
          if (f) {
            await onUpload(f, kind);
            input.value = "";
          }
        }}
      />
      <p className="muted">PDF sau imagine · maximum 50 MiB</p>
    </section>
  );
}
