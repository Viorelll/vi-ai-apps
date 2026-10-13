namespace AccessReadAI.Contracts;

public sealed record CorrectVariantRequest(long Revision, Guid VariantId, string Text);
