const labels: Record<string, string> = {
  Uploaded: "Încărcat",
  Extracting: "Se procesează",
  NeedsReview: "Necesită revizuire",
  Validated: "Validat",
  Exported: "Exportat",
  Failed: "Eșuat",
  Stale: "Sursă eliminată",
};
export default function StatusBadge({ state }: { state: string }) {
  return (
    <span className={`status status-${state.toLowerCase()}`}>
      {labels[state] ?? state}
    </span>
  );
}
