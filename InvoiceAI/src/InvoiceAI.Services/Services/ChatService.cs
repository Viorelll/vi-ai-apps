using InvoiceAI.Contracts.Dto;
using InvoiceAI.Services.Abstractions;
using InvoiceAI.Services.Exceptions;
using InvoiceAI.Services.Mapping;
namespace InvoiceAI.Services.Services;

public sealed class ChatService(IInvoiceRepository invoices, IChatRepository chats, IAiGateway ai)
{
    public async Task<ChatResponse> AskAsync(ChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > 2000)
            throw new AppException("QUESTION_INVALID");
        var invoice = await invoices.GetAsync(request.InvoiceId, ct);
        if (invoice.Document.Status != "Ready" || invoice.State is "Uploaded" or "Extracting" or "Failed" or "Stale")
            throw new AppException("SOURCE_NOT_READY", 409);
        var (id, history) = await chats.GetHistoryAsync(invoice.Id, request.ConversationId, ct);
        var fields = invoice.Fields.Select(InvoiceMapper.Field).ToArray();
        var answer = await ai.ExplainAsync(new(invoice.Id, fields, invoice.Lines.Select(InvoiceMapper.Line).ToArray(), invoice.Issues.Select(InvoiceMapper.Issue).ToArray(), request.Question, history), ct);
        var current = await invoices.GetAsync(invoice.Id, ct);
        if (current.Document.Status != "Ready" || current.Revision != invoice.Revision || current.Document.ActiveGeneration != invoice.Document.ActiveGeneration)
            throw new AppException("SOURCE_STALE", 409);
        var sources = fields.SelectMany(x => x.Evidence).ToDictionary(x => x.SourceId);
        if (answer.SourceIds.Any(x => !sources.ContainsKey(x)))
            throw new AppException("CITATION_INVALID", 502);
        var citations = answer.SourceIds.Distinct().Select(x => { var s = sources[x]; return new CitationDto(x, s.DocumentId, invoice.Document.FileName, s.PageNumber, s.Text, $"/api/documents/{s.DocumentId}/content" + (s.PageNumber is int p ? $"#page={p}" : "")); }).ToArray();
        await chats.SaveAsync(id, request.Question, answer, citations, invoice.Document.ActiveGeneration, ct);
        return new(id, answer.Summary, citations, answer.InputTokens, answer.OutputTokens);
    }
}
