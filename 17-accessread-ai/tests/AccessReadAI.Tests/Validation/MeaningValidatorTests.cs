using AccessReadAI.Services.Validation;

namespace AccessReadAI.Tests.Validation;

public sealed class MeaningValidatorTests
{
    [Theory]
    [InlineData("Nu depășiți 10 unități.", "Nu depășiți 10 unități.", true)]
    [InlineData("Nu depășiți 10 unități.", "Depășiți 10 unități.", false)]
    [InlineData("Nu depășiți 10 unități.", "Nu depășiți 11 unități.", false)]
    [InlineData("10 mg", "10 g", false)]
    [InlineData("1,5 kg", "1.5 kg", true)]
    [InlineData("10 și 10", "10", false)]
    [InlineData("Nu utilizați fără apă", "Nu utilizați apă", false)]
    [InlineData("Se păstrează 2 ore", "Se păstrează 2 ore", true)]
    [InlineData("5%", "5", false)]
    [InlineData("10 kg", "", false)]
    public void RejectsChangedNumbersNegationsAndUnits(string source, string target, bool valid) =>
        Assert.Equal(valid, MeaningValidator.IsSafe(source, target));

    [Fact]
    public void TranslationPreservesNumbersAndNegation()
    {
        Assert.True(
            MeaningValidator.IsSafe("Nu depășiți 10 unități.", "Do not exceed 10 units.", false)
        );
    }
}
