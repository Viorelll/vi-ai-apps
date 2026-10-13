using AccessReadAI.Domain.Entities;

namespace AccessReadAI.Api.Models;

public static class ReadingView
{
    public static object From(Reading r) =>
        new
        {
            r.Id,
            r.DocumentId,
            r.State,
            r.Revision,
            r.SourceLanguage,
            r.TargetLanguage,
            r.ErrorCode,
            r.InputTokens,
            r.OutputTokens,
            blocks = r
                .Blocks.OrderBy(x => x.Ordinal)
                .Select(b => new
                {
                    b.Id,
                    b.SourceId,
                    b.Ordinal,
                    b.PageNumber,
                    b.Kind,
                    original = b.OriginalText,
                    b.NeedsReview,
                    b.Warning,
                    contentUrl = $"/api/documents/{r.DocumentId}/content#page={b.PageNumber}",
                    imageUrl = b.ImageObjectKey != null
                        ? $"/api/readings/{r.Id}/images/{b.Id}"
                        : null,
                    imageDescriptions = b.ImageDescriptions.Select(x => x.Text),
                    variants = b
                        .Variants.Where(v => !v.Stale)
                        .Select(v => new
                        {
                            v.Id,
                            v.Kind,
                            v.Language,
                            v.Text,
                            v.TextHash,
                            v.PromptVersion,
                            v.Deployment,
                            narration = v
                                .Narration.Where(n => !n.Stale && n.TextHash == v.TextHash)
                                .Select(n => new
                                {
                                    n.Id,
                                    n.Voice,
                                    n.ContentType,
                                    audioUrl = $"/api/readings/{r.Id}/audio/{n.Id}",
                                }),
                        }),
                }),
        };
}
