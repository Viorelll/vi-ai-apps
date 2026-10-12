namespace ImageAI.Infrastructure.Models;

public sealed record ImageInfo(int Width, int Height, bool HasAlpha, string ContentType);
