using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DocuAI.Application.Abstractions;

namespace DocuAI.Database.Persistence;

public static class DatabaseRegistration
{
    public static IServiceCollection AddDocuAiDatabase(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<DocuAiDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<DocumentRepository>();
        services.AddScoped<IDocumentRepository>(provider => provider.GetRequiredService<DocumentRepository>());
        services.AddScoped<IKnowledgeRepository>(provider => provider.GetRequiredService<DocumentRepository>());
        services.AddScoped<IIngestionRepository>(provider => provider.GetRequiredService<DocumentRepository>());
        return services;
    }
}
