using Amazon.Runtime;
using Amazon.S3;
using ContractAI.Infrastructure.Abstractions;
using ContractAI.Infrastructure.Ai;
using ContractAI.Infrastructure.Extraction;
using ContractAI.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContractAI.Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddStorage(
        this IServiceCollection services,
        IConfiguration config
    )
    {
        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
            new BasicAWSCredentials(config["Storage:AccessKey"], config["Storage:SecretKey"]),
            new AmazonS3Config
            {
                ServiceURL = config["Storage:Endpoint"],
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
            }
        ));
        services.AddSingleton<IObjectStore, S3ObjectStore>();
        return services;
    }

    public static IServiceCollection AddGatewayClient(
        this IServiceCollection services,
        IConfiguration config
    )
    {
        services
            .AddHttpClient<IAiClient, DomainAiClient>(http =>
            {
                http.BaseAddress = new Uri(config["Ai:BaseUrl"] ?? "http://localhost:5101/");
                http.DefaultRequestHeaders.Add("X-Internal-Key", config["Internal:Key"]);
                http.Timeout = TimeSpan.FromSeconds(65);
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { AllowAutoRedirect = false }
            );
        return services;
    }

    public static IServiceCollection AddProviders(this IServiceCollection services)
    {
        services
            .AddHttpClient<FoundryClient>(http => http.Timeout = TimeSpan.FromSeconds(60))
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { AllowAutoRedirect = false }
            );
        services
            .AddHttpClient<DocumentExtractor>(http => http.Timeout = TimeSpan.FromSeconds(60))
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { AllowAutoRedirect = false }
            );
        return services;
    }
}
