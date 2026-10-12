namespace ProcureAI.Application.Services;

using ProcureAI.Application.Abstractions;
using ProcureAI.Application.Exceptions;
using ProcureAI.Domain.DTOs;

public sealed class RunService(IProcurementRepository repository)
{
    public Task<ProcureAI.Domain.Entities.ProcurementRun> CreateAsync(CreateRunRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Brief) || request.Brief.Length > 8000 || request.Criteria is null ||
            request.Criteria.Currency.Length != 3 || request.Criteria.Currency.Any(c => c is < 'A' or > 'Z') ||
            request.Criteria.Quantity <= 0 || request.Criteria.Quantity > 1000000 ||
            string.IsNullOrWhiteSpace(request.Criteria.Unit) || request.Criteria.Unit.Length > 40 ||
            request.Criteria.MaxDays is <= 0 or > 3650 || request.BudgetTokens is < 1000 or > 50000)
            throw new AppException("invalid_requirements");
        return repository.CreateAsync(request, ct);
    }
}
