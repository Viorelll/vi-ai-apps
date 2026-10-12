using ShopVisionAI.Contracts.Dto;
using ShopVisionAI.Contracts.Validation;

namespace ShopVisionAI.Tests.Rules;

public sealed class VisionTests
{
    [Fact]
    public void UnknownSourceRejected() =>
        Assert.Throws<ArgumentException>(() =>
            VisionValidator.Validate(
                new(
                    "Completed",
                    [new("other", "visible", "color", "black", null)],
                    "model",
                    1,
                    1,
                    false
                ),
                "image:allowed"
            )
        );

    [Fact]
    public void InvalidRegionRejected() =>
        Assert.Throws<ArgumentException>(() =>
            VisionValidator.Validate(
                new(
                    "Completed",
                    [new("image:allowed", "visible", "color", "black", new(.8, .1, .5, .5))],
                    "model",
                    1,
                    1,
                    false
                ),
                "image:allowed"
            )
        );

    [Fact]
    public void MaterialNotVisible() =>
        Assert.Throws<ArgumentException>(() =>
            VisionValidator.Validate(
                new(
                    "Completed",
                    [new("image:allowed", "visible", "material", "plastic", null)],
                    "model",
                    1,
                    1,
                    false
                ),
                "image:allowed"
            )
        );

    [Fact]
    public void TokenBudgetEnforced() =>
        Assert.Throws<ArgumentException>(() =>
            VisionValidator.Validate(
                new("Completed", [], "model", 15000, 1, false),
                "image:allowed"
            )
        );
}
