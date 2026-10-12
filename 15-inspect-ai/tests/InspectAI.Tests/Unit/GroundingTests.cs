using InspectAI.Domain.DTOs;
using InspectAI.Domain.Models;
using InspectAI.Domain.Services;

namespace InspectAI.Tests.Unit;

public sealed class GroundingTests
{
    private readonly Guid photo = Guid.NewGuid(),
        chunk = Guid.NewGuid(),
        manual = Guid.NewGuid(),
        finding = Guid.NewGuid();
    private PhotoSource[] Photos => [new(photo, "originals/" + photo, "image/png", "HASH")];
    private ManualSource[] Sources =>
        [new(chunk, manual, 1, "Inspect the stain before cleaning.", 3, "HASH")];
    private InspectionResult Valid =>
        new(
            "Visible stain.",
            ["Internal state unknown."],
            [
                new(
                    finding,
                    photo,
                    "visible",
                    "Brown stain.",
                    null,
                    new("normalized", 0.1, 0.2, 0.4, 0.5)
                ),
            ],
            [
                new(
                    Guid.NewGuid(),
                    finding,
                    "Review cleaning instructions.",
                    [new(chunk, "Inspect the stain")]
                ),
            ]
        );

    [Fact]
    public void SeedHasPhotoAndManualEvidence() =>
        GroundingValidator.Validate(Valid, Photos, Sources);

    [Theory]
    [InlineData(-0.1, 0, 0.1, 0.1)]
    [InlineData(0, 0, 0, 0.1)]
    [InlineData(0.9, 0, 0.2, 0.1)]
    [InlineData(0, 0.9, 0.1, 0.2)]
    [InlineData(double.NaN, 0, 0.1, 0.1)]
    public void RejectsBadGeometry(double x, double y, double w, double h) =>
        Assert.Throws<AppException>(() =>
            GroundingValidator.ValidateRegion(new("normalized", x, y, w, h))
        );

    [Fact]
    public void RejectsPixelCoordinates() =>
        Assert.Throws<AppException>(() =>
            GroundingValidator.ValidateRegion(new("pixel", 0, 0, 1, 1))
        );

    [Fact]
    public void RejectsForeignPhoto()
    {
        var r = Valid;
        r = r with { Findings = [r.Findings[0] with { DocumentId = Guid.NewGuid() }] };
        Assert.Throws<AppException>(() => GroundingValidator.Validate(r, Photos, Sources));
    }

    [Fact]
    public void RejectsInventedQuote()
    {
        var r = Valid;
        r = r with { Checklist = [r.Checklist[0] with { Evidence = [new(chunk, "safe to use")] }] };
        Assert.Throws<AppException>(() => GroundingValidator.Validate(r, Photos, Sources));
    }

    [Fact]
    public void RejectsForeignManual() =>
        Assert.Throws<AppException>(() =>
            GroundingValidator.ValidateEvidence([new(Guid.NewGuid(), "Inspect")], Sources)
        );

    [Fact]
    public void InferenceRequiresReason()
    {
        var r = Valid;
        r = r with { Findings = [r.Findings[0] with { Kind = "inferred" }] };
        Assert.Throws<AppException>(() => GroundingValidator.Validate(r, Photos, Sources));
    }

    [Fact]
    public void ChecklistRequiresFinding()
    {
        var r = Valid;
        r = r with { Checklist = [r.Checklist[0] with { FindingId = Guid.NewGuid() }] };
        Assert.Throws<AppException>(() => GroundingValidator.Validate(r, Photos, Sources));
    }

    [Fact]
    public void DuplicateIdsRejected()
    {
        var r = Valid;
        r = r with { Findings = [r.Findings[0], r.Findings[0]] };
        Assert.Throws<AppException>(() => GroundingValidator.Validate(r, Photos, Sources));
    }

    [Fact]
    public void MissingEvidenceRejected() =>
        Assert.Throws<AppException>(() => GroundingValidator.ValidateEvidence([], Sources));

    [Fact]
    public void InternalDefectCannotBeAVisibleFinding()
    {
        var r = Valid;
        r = r with
        {
            Findings = [r.Findings[0] with { Description = "Confirmed internal corrosion" }],
        };
        Assert.Throws<AppException>(() => GroundingValidator.Validate(r, Photos, Sources));
    }
}
