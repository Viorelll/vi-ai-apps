using ContractAI.Domain.DTOs;
using ContractAI.Domain.Services;

namespace ContractAI.Tests.Unit;

public sealed class ComparisonTests
{
    private static ClauseSource Source(string text) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1, text);

    [Fact]
    public void IdentifiesThirtyToFifteenWithoutRewritingText()
    {
        var l = Source("7. Beneficiarul plătește în 30 zile de la recepție.");
        var r = Source("9. Beneficiarul plătește în 15 zile de la recepție.");
        var p = Assert.Single(ClauseComparer.Compare([l], [r]));
        Assert.Equal("modified", p.Kind);
        Assert.Equal(l.Text, p.BeforeText);
        Assert.Equal(r.Text, p.AfterText);
    }

    [Fact]
    public void RenumberedAndMovedClausesRemainUnchanged()
    {
        var p = ClauseComparer.Compare(
            [Source("1. Plata are loc lunar."), Source("2. Contractul este confidențial.")],
            [Source("6. Contractul este confidențial."), Source("7. Plata are loc lunar.")]
        );
        Assert.All(p, x => Assert.Equal("unchanged", x.Kind));
    }

    [Fact]
    public void NegationChangeIsVisible()
    {
        var p = Assert.Single(
            ClauseComparer.Compare(
                [Source("Prestatorul poate divulga informațiile confidențiale.")],
                [Source("Prestatorul nu poate divulga informațiile confidențiale.")]
            )
        );
        Assert.Equal("modified", p.Kind);
        Assert.Contains("nu poate", p.AfterText!);
    }

    [Fact]
    public void ReportsUnmatchedAddedAndRemoved()
    {
        var p = ClauseComparer.Compare(
            [Source("Confidențialitate absolută.")],
            [Source("Asigurare obligatorie.")]
        );
        Assert.Contains(p, x => x.Kind == "added");
        Assert.Contains(p, x => x.Kind == "removed");
    }
}
