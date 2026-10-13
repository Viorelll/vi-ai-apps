namespace AccessReadAI.Contracts;

public sealed record GroundedRequest(string Question, IReadOnlyList<GroundedSource> Sources);
