using Microsoft.EntityFrameworkCore;
using InvoiceAI.Contracts.Dto;
using InvoiceAI.Domain.Entities;
using InvoiceAI.Database.Persistence;
using InvoiceAI.Services.Abstractions;
using InvoiceAI.Services.Exceptions;
namespace InvoiceAI.Database.Repositories;

public sealed class ChatRepository(IDbContextFactory<AppDbContext> factory) : IChatRepository
{
    public async Task<(Guid Id, IReadOnlyList<ChatTurnDto> History)> GetHistoryAsync(Guid invoiceId, Guid? conversationId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        Conversation conversation;
        if (conversationId is Guid id)
            conversation = await db.Conversations.SingleOrDefaultAsync(x => x.Id == id && x.InvoiceId == invoiceId, ct) ?? throw new AppException("CONVERSATION_SCOPE_INVALID", 404);
        else
        {
            conversation = new()
            {
                InvoiceId = invoiceId
            };
            db.Conversations.Add(conversation);
            await db.SaveChangesAsync(ct);
        }
        var history = await db.Messages.Where(x => x.ConversationId == conversation.Id).OrderByDescending(x => x.CreatedAtUtc).Take(6).ToListAsync(ct);
        return (conversation.Id, history.OrderBy(x => x.CreatedAtUtc).Select(x => new ChatTurnDto(x.Role, x.Text)).ToArray());
    }
    public async Task SaveAsync(Guid conversationId, string question, ExplanationDto answer, IReadOnlyList<CitationDto> citations, int generation, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var message = new Message { ConversationId = conversationId, Role = "assistant", Text = answer.Summary, InputTokens = answer.InputTokens, OutputTokens = answer.OutputTokens };
        db.Messages.Add(new()
        {
            ConversationId = conversationId,
            Role = "user",
            Text = question
        });
        db.Messages.Add(message);
        db.Citations.AddRange(citations.Select(c => new Citation { MessageId = message.Id, DocumentId = c.DocumentId, SourceId = c.SourceId, Generation = generation, PageNumber = c.PageNumber, Excerpt = c.Excerpt }));
        await db.SaveChangesAsync(ct);
    }
}
