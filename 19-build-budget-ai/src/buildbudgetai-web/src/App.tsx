import { useEffect, useState } from "react";
import { z } from "zod";
import { action, request } from "./services/api";
import {
  budgetSchema,
  chatSchema,
  documentSchema,
  planSchema,
  type Chat,
  type Document,
  type Plan,
} from "./models/schemas";
import { SourceCard } from "./components/SourceCard";
const draftSchema = z.object({
  supplierLabel: z.string().nullable(),
  currency: z.string().nullable(),
  taxBasis: z.string().nullable(),
  provider: z.string(),
  lines: z.array(
    z.object({
      materialLabel: z.string().nullable(),
      unit: z.string().nullable(),
      price: z.string().nullable(),
      transport: z.string().nullable(),
      packQuantity: z.string().nullable(),
      sourceIds: z.array(z.string()),
    }),
  ),
});
const units = ["m", "m²", "m³", "kg", "buc", "pachet"];
export default function App() {
  const [draft, setDraft] = useState<z.infer<typeof draftSchema> | null>(null);
  const [plans, setPlans] = useState<{ id: string; name: string }[]>([]);
  const [plan, setPlan] = useState<Plan | null>(null);
  const [docs, setDocs] = useState<Document[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [mode, setMode] = useState("");
  const [name, setName] = useState("Casa mea");
  const [currency, setCurrency] = useState("EUR");
  const [label, setLabel] = useState("Izolație");
  const [unit, setUnit] = useState("m²");
  const [quantity, setQuantity] = useState("100");
  const [waste, setWaste] = useState("10");
  const [supplier, setSupplier] = useState("Materiale Demo A");
  const [price, setPrice] = useState("5");
  const [transport, setTransport] = useState("20");
  const [pack, setPack] = useState("");
  const [tax, setTax] = useState("Included");
  const [confirmed, setConfirmed] = useState(true);
  const [source, setSource] = useState("");
  const [selected, setSelected] = useState<Record<string, string>>({});
  const [question, setQuestion] = useState(
    "Explică bugetul și ce informații lipsesc.",
  );
  const [chat, setChat] = useState<Chat | null>(null);
  async function refresh(id?: string) {
    setPlans(
      await request(
        "/api/build/plans",
        z.array(z.object({ id: z.string(), name: z.string() })),
      ),
    );
    setDocs(await request("/api/documents", z.array(documentSchema)));
    if (id) setPlan(await request(`/api/build/plans/${id}`, planSchema));
  }
  async function run(fn: () => Promise<unknown>) {
    setBusy(true);
    setError("");
    try {
      await fn();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Operația a eșuat");
    } finally {
      setBusy(false);
    }
  }
  useEffect(() => {
    void run(async () => {
      await refresh();
      setMode(
        (await request("/api/health", z.object({ mode: z.string() }))).mode,
      );
    });
  }, []);
  useEffect(() => {
    if (!docs.some((d) => ["Uploaded", "Processing"].includes(d.status)))
      return;
    const timer = setInterval(() => {
      void refresh(plan?.id).catch((e) => setError(String(e)));
    }, 2000);
    return () => clearInterval(timer);
  }, [docs, plan?.id]);
  async function create() {
    const p = await request(
      "/api/build/plans",
      z.object({ id: z.string(), revision: z.number() }),
      "POST",
      { name, currency },
    );
    setChat(null);
    setDraft(null);
    setSelected({});
    await refresh(p.id);
    return p;
  }
  async function addNeed() {
    if (!plan) return;
    setPlan(
      await request(`/api/build/plans/${plan.id}/needs`, planSchema, "PUT", {
        revision: plan.revision,
        needs: [
          ...plan.needs.map((n) => ({
            label: n.label,
            unit: n.unit,
            quantity: n.quantity,
            wastePercent: n.wastePercent,
          })),
          { label, unit, quantity, wastePercent: waste },
        ],
      }),
    );
  }
  async function addOffer() {
    if (!plan) return;
    const evidence = source
      ? plan.evidence.filter((e) => e.documentId === source).slice(0, 1)
      : [];
    setPlan(
      await request(`/api/build/plans/${plan.id}/offers`, planSchema, "POST", {
        revision: plan.revision,
        supplierLabel: supplier,
        currency,
        taxBasis: tax,
        documentId: source || null,
        lines: [
          {
            materialLabel: label,
            unit,
            price,
            transport: transport || null,
            packQuantity: pack || null,
            confirmed,
            evidence,
          },
        ],
      }),
    );
  }
  async function demo() {
    const p = await create();
    let state = await request(
      `/api/build/plans/${p.id}/needs`,
      planSchema,
      "PUT",
      {
        revision: 0,
        needs: [
          {
            label: "Izolație",
            unit: "m²",
            quantity: "100",
            wastePercent: "10",
          },
        ],
      },
    );
    state = await request(
      `/api/build/plans/${p.id}/offers`,
      planSchema,
      "POST",
      {
        revision: state.revision,
        supplierLabel: "Materiale Demo A",
        currency,
        taxBasis: "Included",
        documentId: null,
        lines: [
          {
            materialLabel: "Izolație",
            unit: "m²",
            price: "5",
            transport: "20",
            packQuantity: null,
            confirmed: true,
            evidence: [],
          },
        ],
      },
    );
    const selection = {
      needId: state.needs[0].id,
      offerLineId: state.offers[0].lines[0].id,
    };
    await request(`/api/build/plans/${p.id}/budget`, budgetSchema, "POST", {
      revision: state.revision,
      selectedOfferLines: [selection],
    });
    setSelected({ [selection.needId]: selection.offerLineId });
    await refresh(p.id);
  }
  return (
    <>
      <header>
        <div className="brand">
          <span className="mark">B</span>
          <div>
            BuildBudget<span>AI</span>
            <small>CONSTRUIEȘTE CU DATE CLARE</small>
          </div>
        </div>
        <span className="mode">
          {mode === "Demo" ? "● Mod demonstrativ" : "● Microsoft Foundry"}
        </span>
      </header>
      <main>
        <section className="intro">
          <div>
            <p className="eyebrow">DE LA OFERTE LA DECIZII</p>
            <h1>
              O casă începe cu
              <br />
              <em>un buget bine construit.</em>
            </h1>
            <p>
              Compară materiale pe aceeași bază. Calcule exacte, surse vizibile
              și necunoscute explicite.
            </p>
          </div>
          <button
            className="secondary"
            disabled={busy}
            onClick={() => void run(demo)}
          >
            Încarcă exemplul · 570 EUR ↗
          </button>
        </section>
        {error && (
          <div role="alert" className="error">
            {error}{" "}
            <button onClick={() => void run(() => refresh(plan?.id))}>
              Reîncarcă datele
            </button>
          </div>
        )}
        {busy && (
          <p role="status" className="working">
            Se procesează…
          </p>
        )}
        <section className="panel plan-bar">
          <div>
            <label>
              Nume plan
              <input
                value={name}
                onChange={(e) => setName(e.target.value)}
                maxLength={200}
              />
            </label>
          </div>
          <label>
            Monedă
            <select
              value={currency}
              onChange={(e) => setCurrency(e.target.value)}
            >
              <option>EUR</option>
              <option>RON</option>
              <option>MDL</option>
              <option>USD</option>
            </select>
          </label>
          <button disabled={busy} onClick={() => void run(create)}>
            Plan nou +
          </button>
          <label>
            Planuri salvate
            <select
              value={plan?.id ?? ""}
              onChange={(e) => {
                setChat(null);
                setDraft(null);
                setSelected({});
                void run(() => refresh(e.target.value));
              }}
            >
              <option value="">Selectează un plan</option>
              {plans.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          </label>
        </section>
        <div className="workspace">
          <aside>
            <section className="panel">
              <h2>
                <span>01</span> Documente & surse
              </h2>
              <p className="hint">
                PDF, PNG, JPEG sau TXT · maximum 50 MiB. PDF și imaginile
                necesită extractor Azure.
              </p>
              <label className="upload">
                ＋ Încarcă o ofertă sau o imagine
                <input
                  aria-label="Încarcă document"
                  type="file"
                  accept=".pdf,.png,.jpg,.jpeg,.txt"
                  disabled={busy}
                  onChange={(e) => {
                    const file = e.target.files?.[0];
                    if (file)
                      void run(async () => {
                        const form = new FormData();
                        form.append("file", file);
                        const d = await request(
                          "/api/documents",
                          z.object({
                            documentId: z.string(),
                            status: z.string(),
                          }),
                          "POST",
                          form,
                        );
                        if (plan)
                          await request(
                            `/api/build/plans/${plan.id}/sources`,
                            planSchema,
                            "POST",
                            {
                              revision: plan.revision,
                              documentId: d.documentId,
                              role: "offer",
                            },
                          );
                        await refresh(plan?.id);
                      });
                    e.target.value = "";
                  }}
                />
              </label>
              <div className="documents">
                {docs.length === 0 ? (
                  <p className="empty">Documentele tale vor apărea aici.</p>
                ) : (
                  docs.map((d) => (
                    <article key={d.id}>
                      <strong>{d.fileName}</strong>
                      <span className={`badge ${d.status}`}>{d.status}</span>
                      {d.errorCode && <p className="hint">{d.errorCode}</p>}
                      <div className="doc-actions">
                        <a
                          href={`/api/documents/${d.id}/content`}
                          target="_blank"
                          rel="noreferrer"
                        >
                          Original ↗
                        </a>
                        {plan &&
                          !plan.sources.some((s) => s.documentId === d.id) && (
                            <button
                              disabled={busy}
                              onClick={() =>
                                void run(async () => {
                                  await request(
                                    `/api/build/plans/${plan.id}/sources`,
                                    planSchema,
                                    "POST",
                                    {
                                      revision: plan.revision,
                                      documentId: d.id,
                                      role: "offer",
                                    },
                                  );
                                  await refresh(plan.id);
                                })
                              }
                            >
                              Atașează
                            </button>
                          )}
                        {d.status === "Failed" && (
                          <button
                            disabled={busy}
                            onClick={() =>
                              void run(async () => {
                                await action(
                                  `/api/documents/${d.id}/retry`,
                                  "POST",
                                );
                                await refresh(plan?.id);
                              })
                            }
                          >
                            Reîncearcă
                          </button>
                        )}
                        <button
                          disabled={busy}
                          onClick={() =>
                            void run(async () => {
                              await action(`/api/documents/${d.id}`, "DELETE");
                              await refresh(plan?.id);
                            })
                          }
                        >
                          Șterge
                        </button>
                      </div>
                    </article>
                  ))
                )}
              </div>
            </section>
            <section className="note">
              <h3>O comparație corectă</h3>
              <p>
                Unitatea, moneda și baza fiscală trebuie să coincidă.
                Transportul lipsă rămâne necunoscut.
              </p>
              <p>
                Fotografiile oferă context, nu măsurători sau garanții
                structurale.
              </p>
            </section>
          </aside>
          <div className="content">
            <section className="panel">
              <h2>
                <span>02</span> Necesar & oferte{" "}
                {plan && (
                  <small>
                    {plan.name} · rev. {plan.revision}
                  </small>
                )}
              </h2>
              {!plan ? (
                <p className="empty">
                  Creează un plan sau încearcă exemplul pentru a începe.
                </p>
              ) : (
                <>
                  <div className="form-grid">
                    <label>
                      Material
                      <input
                        value={label}
                        onChange={(e) => setLabel(e.target.value)}
                      />
                    </label>
                    <label>
                      Unitate
                      <select
                        value={unit}
                        onChange={(e) => setUnit(e.target.value)}
                      >
                        {units.map((u) => (
                          <option key={u}>{u}</option>
                        ))}
                      </select>
                    </label>
                    <label>
                      Cantitate
                      <input
                        type="number"
                        min="0"
                        step="any"
                        value={quantity}
                        onChange={(e) => setQuantity(e.target.value)}
                      />
                    </label>
                    <label>
                      Pierdere %
                      <input
                        type="number"
                        min="0"
                        max="100"
                        step="any"
                        value={waste}
                        onChange={(e) => setWaste(e.target.value)}
                      />
                    </label>
                  </div>
                  <button disabled={busy} onClick={() => void run(addNeed)}>
                    Adaugă necesar
                  </button>
                  <hr />
                  <button
                    className="secondary"
                    disabled={busy || !plan.evidence.length}
                    onClick={() =>
                      void run(async () => {
                        setDraft(
                          await request(
                            `/api/build/plans/${plan.id}/extract`,
                            draftSchema,
                            "POST",
                          ),
                        );
                      })
                    }
                  >
                    Extrage câmpurile ofertelor ↗
                  </button>
                  {draft && (
                    <div className="draft">
                      <p className="hint">
                        Propuneri neconfirmate · {draft.provider}
                      </p>
                      {draft.lines.map((l, i) => (
                        <div key={i}>
                          <b>{l.materialLabel ?? "Material necunoscut"}</b> ·{" "}
                          {l.price ?? "Preț necunoscut"}{" "}
                          {draft.currency ?? "Monedă necunoscută"}/
                          {l.unit ?? "?"}
                          <button
                            className="secondary"
                            onClick={() => {
                              setLabel(l.materialLabel ?? "");
                              setUnit(l.unit ?? "m²");
                              setPrice(l.price ?? "");
                              setTransport(l.transport ?? "");
                              setPack(l.packQuantity ?? "");
                              setSupplier(draft.supplierLabel ?? "");
                              setCurrency(draft.currency ?? "EUR");
                              setTax(draft.taxBasis ?? "Unknown");
                              setConfirmed(false);
                              setSource(
                                plan.evidence.find((e) =>
                                  l.sourceIds.includes(e.sourceId),
                                )?.documentId ?? "",
                              );
                            }}
                          >
                            Verifică și completează
                          </button>
                        </div>
                      ))}
                    </div>
                  )}
                  <div className="form-grid">
                    <label>
                      Furnizor
                      <input
                        value={supplier}
                        onChange={(e) => setSupplier(e.target.value)}
                      />
                    </label>
                    <label>
                      Preț / unitate
                      <input
                        type="number"
                        min="0"
                        step="any"
                        value={price}
                        onChange={(e) => setPrice(e.target.value)}
                      />
                    </label>
                    <label>
                      Transport
                      <input
                        placeholder="Necunoscut"
                        type="number"
                        min="0"
                        step="any"
                        value={transport}
                        onChange={(e) => setTransport(e.target.value)}
                      />
                    </label>
                    <label>
                      Bucăți / pachet
                      <input
                        placeholder="Opțional"
                        type="number"
                        min="0"
                        step="any"
                        value={pack}
                        onChange={(e) => setPack(e.target.value)}
                      />
                    </label>
                    <label>
                      Taxe
                      <select
                        value={tax}
                        onChange={(e) => setTax(e.target.value)}
                      >
                        <option value="Included">Incluse</option>
                        <option value="Excluded">Excluse</option>
                        <option value="Unknown">Necunoscute</option>
                      </select>
                    </label>
                    <label>
                      Sursă
                      <select
                        value={source}
                        onChange={(e) => setSource(e.target.value)}
                      >
                        <option value="">Input manual confirmat</option>
                        {docs
                          .filter(
                            (d) =>
                              d.status === "Ready" &&
                              plan.sources.some((s) => s.documentId === d.id),
                          )
                          .map((d) => (
                            <option key={d.id} value={d.id}>
                              {d.fileName}
                            </option>
                          ))}
                      </select>
                    </label>
                  </div>
                  <label className="checkbox">
                    <input
                      type="checkbox"
                      checked={confirmed}
                      onChange={(e) => setConfirmed(e.target.checked)}
                    />{" "}
                    Confirm prețul, unitatea, moneda și taxele
                  </label>
                  <button
                    className="secondary"
                    disabled={busy}
                    onClick={() => void run(addOffer)}
                  >
                    Salvează oferta
                  </button>
                  <p className="hint">
                    Datele extrase se verifică în sursa de mai jos și se
                    introduc explicit înainte de confirmare.
                  </p>
                  {plan.evidence.map((e) => (
                    <SourceCard key={e.sourceId} source={e} />
                  ))}
                  <div className="table-scroll">
                    <table>
                      <thead>
                        <tr>
                          <th>Material / necesar</th>
                          {plan.offers.map((o) => (
                            <th key={o.id}>
                              {o.supplierLabel}
                              <small>
                                {o.currency} · {o.taxBasis}
                              </small>
                            </th>
                          ))}
                          <th>Alegerea ta</th>
                        </tr>
                      </thead>
                      <tbody>
                        {plan.needs.map((n) => (
                          <tr key={n.id}>
                            <td>
                              <strong>{n.label}</strong>
                              <small>
                                {n.quantity} {n.unit} + {n.wastePercent}%
                                pierdere
                              </small>
                            </td>
                            {plan.offers.map((o) => (
                              <td key={o.id}>
                                {o.lines
                                  .filter(
                                    (l) =>
                                      l.materialLabel.toLowerCase() ===
                                      n.label.toLowerCase(),
                                  )
                                  .map((l) => (
                                    <div key={l.id}>
                                      {l.price} {o.currency}/{l.unit}
                                      <small>
                                        Transport: {l.transport ?? "necunoscut"}{" "}
                                        ·{" "}
                                        {l.confirmed
                                          ? "confirmat"
                                          : "neconfirmat"}
                                      </small>
                                      <small>
                                        {o.documentId
                                          ? "Document atașat"
                                          : "Input manual"}
                                      </small>
                                    </div>
                                  ))}
                              </td>
                            ))}
                            <td>
                              <select
                                aria-label={`Ofertă pentru ${n.label}`}
                                value={selected[n.id] ?? ""}
                                onChange={(e) =>
                                  setSelected({
                                    ...selected,
                                    [n.id]: e.target.value,
                                  })
                                }
                              >
                                <option value="">Selectează</option>
                                {plan.offers.flatMap((o) =>
                                  o.lines
                                    .filter(
                                      (l) =>
                                        l.materialLabel.toLowerCase() ===
                                        n.label.toLowerCase(),
                                    )
                                    .map((l) => (
                                      <option key={l.id} value={l.id}>
                                        {o.supplierLabel} · {l.price}/{l.unit}
                                      </option>
                                    )),
                                )}
                              </select>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                  <button
                    disabled={busy || !Object.values(selected).some(Boolean)}
                    onClick={() =>
                      void run(async () => {
                        await request(
                          `/api/build/plans/${plan.id}/budget`,
                          budgetSchema,
                          "POST",
                          {
                            revision: plan.revision,
                            selectedOfferLines: Object.entries(selected)
                              .filter(([, v]) => v)
                              .map(([needId, offerLineId]) => ({
                                needId,
                                offerLineId,
                              })),
                          },
                        );
                        await refresh(plan.id);
                      })
                    }
                  >
                    Calculează bugetul →
                  </button>
                </>
              )}
            </section>
            <section className="panel budget">
              <h2>
                <span>03</span> Bugetul tău
              </h2>
              {plan?.budget ? (
                <>
                  <div className="total">
                    <div>
                      <p>
                        {plan.budget.incomplete
                          ? "Subtotal cunoscut"
                          : "Total confirmat"}
                      </p>
                      <strong data-testid="budget-total">
                        {plan.budget.totalDecimal}
                        <small> {plan.budget.currency}</small>
                      </strong>
                    </div>
                    <span className="badge">
                      {plan.budget.stale
                        ? "Învechit · recalculează"
                        : plan.budget.incomplete
                          ? "Incomplet"
                          : "BudgetReady"}
                    </span>
                  </div>
                  {plan.budget.lines.map((l) => (
                    <div className="formula" key={l.needId}>
                      <b>{l.material}</b>
                      <p>{l.formula}</p>
                      {l.unknowns.map((u) => (
                        <span className="unknown" key={u}>
                          {u}
                        </span>
                      ))}
                    </div>
                  ))}
                  <a
                    className="export"
                    href={`/api/build/plans/${plan.id}/export`}
                  >
                    Descarcă bugetul JSON cu surse ↓
                  </a>
                </>
              ) : (
                <p className="empty">
                  Alege ofertele și calculează. Modelul AI nu stabilește
                  prețurile.
                </p>
              )}
            </section>
            <section className="panel">
              <h2>
                <span>04</span> Explicații cu surse
              </h2>
              <div className="ask">
                <textarea
                  aria-label="Întrebare"
                  value={question}
                  maxLength={2000}
                  onChange={(e) => setQuestion(e.target.value)}
                />
                <button
                  disabled={busy || !plan}
                  onClick={() =>
                    void run(async () => {
                      setChat(
                        await request("/api/chat", chatSchema, "POST", {
                          planId: plan!.id,
                          conversationId: chat?.conversationId ?? null,
                          question,
                        }),
                      );
                    })
                  }
                >
                  Întreabă →
                </button>
              </div>
              {chat && (
                <div className="answer">
                  <p>{chat.summary}</p>
                  {chat.unknowns.map((u) => (
                    <p className="hint" key={u}>
                      • {u}
                    </p>
                  ))}
                  {chat.citations.map((e) => (
                    <SourceCard key={e.sourceId} source={e} />
                  ))}
                  <small>{chat.deployment}</small>
                </div>
              )}
            </section>
          </div>
        </div>
        <footer>
          BuildBudgetAI · Date confirmate. Ipoteze explicite. Decizii informate.
        </footer>
      </main>
    </>
  );
}
