using Amazon.Runtime;
using Amazon.S3;
using DocuAI.Application.Abstractions;
using DocuAI.Infrastructure.Ai;
using DocuAI.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocuAI.Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddDocuAiInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FoundryOptions>(configuration.GetSection(FoundryOptions.SectionName));

        services.AddHttpClient("foundry", client => client.Timeout = TimeSpan.FromSeconds(60));
        services.AddHttpClient("ai-gateway", client =>
        {
            client.BaseAddress = new Uri(configuration["Ai:BaseUrl"] ?? "http://localhost:8081/");
            client.Timeout = TimeSpan.FromMinutes(3);
        });

        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
            new BasicAWSCredentials(
                configuration["Storage:AccessKey"] ?? "docuai",
                configuration["Storage:SecretKey"] ?? "docuai-password"),
            new AmazonS3Config
            {
                ServiceURL = configuration["Storage:Endpoint"] ?? "http://localhost:9000",
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1"
            }));

        services.AddSingleton<IObjectStore, S3ObjectStore>();
        services.AddTransient<IAiExtractionClient, HttpAiExtractionClient>();
        services.AddTransient<IAnswerGenerator, FoundryAnswerGenerator>();
        return services;
    }
}
