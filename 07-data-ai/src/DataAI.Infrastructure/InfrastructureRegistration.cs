using Amazon.Runtime;
using Amazon.S3;
using DataAI.Application.Abstractions;
using DataAI.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataAI.Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddDataAIInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(new BasicAWSCredentials(configuration["Storage:AccessKey"] ?? "dataai", configuration["Storage:SecretKey"] ?? "dataai-local-secret"), new AmazonS3Config { ServiceURL = configuration["Storage:Endpoint"], ForcePathStyle = true, AuthenticationRegion = "us-east-1" }));
        services.AddSingleton<IObjectStore, S3ObjectStore>();
        services.AddHttpClient<IAggregatePlanner, DataAI.Infrastructure.AiClients.DomainAiClient>(client => { client.BaseAddress = new Uri(configuration["Ai:BaseUrl"] ?? "http://ai:8080/"); client.Timeout = TimeSpan.FromSeconds(120); });
        return services;
    }
}
