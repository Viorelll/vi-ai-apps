using System.Text;
namespace SupportAI.Application.Services;

public sealed class PolicySeeder(DocumentService documents)
{
    public const string Policy = """
 # Politică de suport — versiunea 1, activă pentru laborator
 Comenzile LAB sunt date artificiale. Starea comenzii se citește din snapshot, niciodată din această politică.
 Un retur poate fi propus numai pentru o comandă Delivered, în cel mult 30 de zile de la livrare, conform eligibilității calculate de adaptor.
 Pentru produse deteriorate se pregătește un tichet de verificare. Cere fotografii sau dovezi relevante; acestea nu stabilesc cauza defectului și nu garantează acceptarea garanției.
 Tichetul și returul sunt exclusiv locale în demonstrație. Nu există scrieri în CRM, ERP, plăți sau rambursări.
 Execută o acțiune locală numai după confirmarea explicită a argumentelor de către utilizator. Propunerile expiră după 30 de minute.
 """;
    public async Task SeedAsync(CancellationToken ct)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Policy));
        await documents.UploadAsync("support-policy.md", stream, ct, policy: true);
    }
}
