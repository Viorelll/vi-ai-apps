using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SupportAI.Application.Services;
using SupportAI.Domain.DTOs;
using SupportAI.Domain.Entities;
using SupportAI.Domain.Models;
using Xunit;
namespace SupportAI.IntegrationTests;

public sealed class WorkflowTests(LaboratoryFixture lab) : IClassFixture<LaboratoryFixture>
{
    private SupportService Support() => new(lab.Factory, new SeedOrderReader());
    private ActionService Actions() => new(lab.Factory, new SeedOrderReader());
    private DurableWorker Worker() => new(lab.Factory, lab.Storage, new FakeAiGateway(), new SeedOrderReader(), NullLogger<DurableWorker>.Instance);
    private async Task<ActionProposal> Proposal(string kind = "ticket")
    {
        var support = Support();
        var item = await support.CreateAsync(new("LAB-1001"), default);
        await support.SendAsync(item.Id, new("Damaged", Guid.NewGuid()), default);
        await using var db = lab.Factory.CreateDbContext();
        var conversation = await db.Conversations.SingleAsync(x => x.CaseId == item.Id);
        var message = await db.Messages.SingleAsync(x => x.ConversationId == conversation.Id);
        var p = new ActionProposal { CaseId = item.Id, MessageId = message.Id, Kind = kind, ArgumentsJson = "{\"summary\":\"Issue\"}", PayloadHash = "test-hash" };
        db.Add(p);
        await db.SaveChangesAsync();
        return p;
    }
    [Fact]
    public async Task ConfirmIsIdempotentEvenWhenConcurrent()
    {
        var p = await Proposal();
        var request = new ConfirmAction(0, p.PayloadHash, Guid.NewGuid().ToString());
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Actions().ConfirmAsync(p.Id, request, default)));
        Assert.Single(results.Select(x => x.Id).Distinct());
        await using var db = lab.Factory.CreateDbContext();
        Assert.Equal(1, await db.ActionExecutions.CountAsync(x => x.ProposalId == p.Id));
    }
    [Fact]
    public async Task RejectsExpiredOrModifiedProposal()
    {
        var p = await Proposal();
        Assert.Equal(409, (await Assert.ThrowsAsync<SupportException>(() => Actions().ConfirmAsync(p.Id, new(1, "tampered", "key"), default))).Status);
        await using var db = lab.Factory.CreateDbContext();
        var live = await db.ActionProposals.SingleAsync(x => x.Id == p.Id);
        live.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<SupportException>(() => Actions().ConfirmAsync(p.Id, new(0, p.PayloadHash, "key"), default));
    }
    [Fact]
    public async Task IdempotencyKeyCannotBeReusedForAnotherProposal()
    {
        var a = await Proposal();
        var b = await Proposal();
        var key = Guid.NewGuid().ToString();
        await Actions().ConfirmAsync(a.Id, new(0, a.PayloadHash, key), default);
        await Assert.ThrowsAsync<SupportException>(() => Actions().ConfirmAsync(b.Id, new(0, b.PayloadHash, key), default));
    }
    [Fact]
    public async Task DuplicateMessageCreatesOneJob()
    {
        var item = await Support().CreateAsync(new("LAB-1001"), default);
        var request = new SendMessage("Status", Guid.NewGuid());
        var jobs = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Support().SendAsync(item.Id, request, default)));
        Assert.Single(jobs.Select(x => x.JobId).Distinct());
        await Assert.ThrowsAsync<SupportException>(() => Support().SendAsync(item.Id, request with { Text = "changed" }, default));
    }
    [Fact]
    public async Task UploadIngestCiteDeleteAndRestartPersistence()
    {
        var docs = new DocumentService(lab.Factory, lab.Storage);
        var text = "Support evidence " + Guid.NewGuid();
        var upload = await docs.UploadAsync("evidence.txt", new MemoryStream(Encoding.UTF8.GetBytes(text)), default);
        var duplicate = await docs.UploadAsync("duplicate.txt", new MemoryStream(Encoding.UTF8.GetBytes(text)), default);
        Assert.True(duplicate.Deduplicated);
        Assert.Equal(upload.DocumentId, duplicate.DocumentId);
        for (var i = 0; i < 30 && (await docs.GetAsync(upload.DocumentId, default)).Status != "Ready"; i++)
            await Worker().ProcessOneAsync(default);
        var document = await docs.GetAsync(upload.DocumentId, default);
        Assert.Equal("Ready", document.Status);
        await using (var normalized = await lab.Storage.OpenAsync($"artifacts/{document.Id}/support-v1/normalized.json", default))
        {
            using var reader = new StreamReader(normalized);
            Assert.Contains(text, await reader.ReadToEndAsync());
        }
        await using (var original = await lab.Storage.OpenAsync(document.ObjectKey, default))
        {
            using var reader = new StreamReader(original);
            Assert.Equal(text, await reader.ReadToEndAsync());
        }
        var item = await Support().CreateAsync(new("LAB-1001"), default);
        await Support().AttachAsync(item.Id, document.Id, default);
        await Support().SendAsync(item.Id, new("Damaged", Guid.NewGuid()), default);
        CaseDto result = await Support().GetAsync(item.Id, default);
        for (var i = 0; i < 30 && result.Messages.All(x => x.Role != "assistant"); i++)
        {
            await Worker().ProcessOneAsync(default);
            result = await Support().GetAsync(item.Id, default);
        }
        var answer = Assert.Single(result.Messages, x => x.Role == "assistant");
        Assert.Contains(answer.Citations, x => x.DocumentId == document.Id);
        Assert.Single(result.Proposals);
        // A new context/service/worker uses only retained database and S3 state.
        var restored = await new SupportService(lab.Factory, new SeedOrderReader()).GetAsync(item.Id, default);
        Assert.Equal(result.Messages.Count, restored.Messages.Count);
        await docs.DeleteAsync(document.Id, default);
        var stale = await Support().GetAsync(item.Id, default);
        Assert.True(stale.Messages.Single(x => x.Id == answer.Id).Stale);
        Assert.Equal("Expired", stale.Proposals.Single().State);
        for (var i = 0; i < 30; i++)
        if (!await Worker().ProcessOneAsync(default))
            break;
        await Assert.ThrowsAsync<SupportException>(() => docs.GetAsync(document.Id, default));
    }
    [Fact]
    public async Task ReclaimsExpiredLeaseAndOnlyRunsOneWorker()
    {
        var docs = new DocumentService(lab.Factory, lab.Storage);
        var upload = await docs.UploadAsync("recover.txt", new MemoryStream(Encoding.UTF8.GetBytes("Recovery " + Guid.NewGuid())), default);
        await using (var db = lab.Factory.CreateDbContext())
        {
            var job = await db.IngestionJobs.SingleAsync(x => x.Id == upload.JobId);
            job.Status = "Running";
            job.LeaseToken = Guid.NewGuid();
            job.LeaseUntilUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }
        for (var i = 0; i < 30 && (await docs.GetAsync(upload.DocumentId, default)).Status != "Ready"; i++)
            await Task.WhenAll(Worker().ProcessOneAsync(default), Worker().ProcessOneAsync(default));
        await using var check = lab.Factory.CreateDbContext();
        Assert.Equal("Complete", (await check.IngestionJobs.SingleAsync(x => x.Id == upload.JobId)).Status);
        Assert.Equal(1, await check.DocumentChunks.CountAsync(x => x.DocumentId == upload.DocumentId));
    }
    [Fact] public async Task UnknownOrderDoesNotCreateCase() => await Assert.ThrowsAsync<SupportException>(() => Support().CreateAsync(new("LAB-UNKNOWN"), default));
    [Fact]
    public async Task NewEvidenceCannotChangeRunningCaseAndInvalidatesOldDraft()
    {
        var docs = new DocumentService(lab.Factory, lab.Storage);
        var first = await docs.UploadAsync("first.txt", new MemoryStream(Encoding.UTF8.GetBytes("First " + Guid.NewGuid())), default);
        var second = await docs.UploadAsync("second.txt", new MemoryStream(Encoding.UTF8.GetBytes("Second " + Guid.NewGuid())), default);
        for (var i = 0; i < 30 && (await docs.GetAsync(second.DocumentId, default)).Status != "Ready"; i++)
            await Worker().ProcessOneAsync(default);
        var item = await Support().CreateAsync(new("LAB-1001"), default);
        await Support().AttachAsync(item.Id, first.DocumentId, default);
        await Support().SendAsync(item.Id, new("Damaged", Guid.NewGuid()), default);
        Assert.Equal("case_busy", (await Assert.ThrowsAsync<SupportException>(() => Support().AttachAsync(item.Id, second.DocumentId, default))).Code);
        var result = await Support().GetAsync(item.Id, default);
        for (var i = 0; i < 30 && result.Messages.All(m => m.Role != "assistant"); i++)
        {
            await Worker().ProcessOneAsync(default);
            result = await Support().GetAsync(item.Id, default);
        }
        await Support().AttachAsync(item.Id, second.DocumentId, default);
        var updated = await Support().GetAsync(item.Id, default);
        Assert.True(updated.Messages.Single(m => m.Role == "assistant").Stale);
        Assert.Equal("Expired", Assert.Single(updated.Proposals).State);
        Assert.True(updated.Revision > result.Revision);
    }
    [Fact]
    public async Task ImageExtractionStoresRawAndNormalizedEvidence()
    {
        var docs = new DocumentService(lab.Factory, lab.Storage);
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a7E0AAAAASUVORK5CYII=");
        var upload = await docs.UploadAsync("photo.png", new MemoryStream(png), default);
        for (var i = 0; i < 30 && (await docs.GetAsync(upload.DocumentId, default)).Status != "Ready"; i++)
            await Worker().ProcessOneAsync(default);
        Assert.Equal("Ready", (await docs.GetAsync(upload.DocumentId, default)).Status);
        await using var raw = await lab.Storage.OpenAsync($"artifacts/{upload.DocumentId}/support-v1/raw.json", default);
        using var reader = new StreamReader(raw);
        Assert.Contains("Succeeded", await reader.ReadToEndAsync());
        await docs.DeleteAsync(upload.DocumentId, default);
        for (var i = 0; i < 30; i++)
        if (!await Worker().ProcessOneAsync(default))
            break;
    }

}
