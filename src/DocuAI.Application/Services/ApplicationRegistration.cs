using DocuAI.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DocuAI.Application;

public static class ApplicationRegistration
{
    public static IServiceCollection AddDocuAiApplication(this IServiceCollection services)
    {
        services.AddScoped<DocumentService>();
        services.AddScoped<ChatService>();
        services.AddHostedService<DocumentIngestionWorker>();
        return services;
    }
}
