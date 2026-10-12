using Microsoft.Extensions.Options;
using VideoSearchAI.Ai.Options;
using VideoSearchAI.Contracts.Dto;

namespace VideoSearchAI.Ai.Providers.Extraction;

public sealed class DemoExtractor(IOptions<AiOptions> options)
{
    public ExtractionResult Extract(ExtractionRequest r)
    {
        if (
            string.IsNullOrEmpty(options.Value.DemoSeedHash)
            || r.Sha256 != options.Value.DemoSeedHash
            || Math.Abs(r.DurationMs - 180000) > 1000
        )
            throw new ProviderException("demo_requires_seed_video", 422);
        EvidenceDto[] evidence =
        [
            new(
                "demo:intro",
                0,
                30000,
                "Acesta este un curs artificial despre PostgreSQL și optimizarea interogărilor.",
                null,
                []
            ),
            new(
                "demo:index",
                70000,
                95000,
                "Un index PostgreSQL pentru query accelerează interogările WHERE. CREATE INDEX idx_orders_customer ON orders(customer_id); EXPLAIN ANALYZE verifică planul de execuție.",
                "Pe ecran apare comanda CREATE INDEX și un plan de execuție PostgreSQL.",
                [new(70000, "Comanda CREATE INDEX este afișată pe diapozitiv.")]
            ),
            new(
                "demo:finish",
                120000,
                160000,
                "Măsoară performanța și costul de scriere înainte să adaugi indexuri suplimentare.",
                null,
                []
            ),
        ];
        return new(
            "Succeeded",
            null,
            System.Text.Json.JsonSerializer.Serialize(
                new { provider = "artificial-fixture-v1", evidence }
            ),
            evidence
        );
    }
}
