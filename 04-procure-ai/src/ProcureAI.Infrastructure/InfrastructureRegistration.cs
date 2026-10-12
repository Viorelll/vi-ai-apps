using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProcureAI.Application.Abstractions;
using ProcureAI.Infrastructure.Storage;

namespace ProcureAI.Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddObjectStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(new BasicAWSCredentials(
            configuration["Storage:AccessKey"] ?? throw new InvalidOperationException("Storage:AccessKey required"),
            configuration["Storage:SecretKey"] ?? throw new InvalidOperationException("Storage:SecretKey required")),
            new AmazonS3Config { ServiceURL = configuration["Storage:Endpoint"], ForcePathStyle = true, AuthenticationRegion = "us-east-1" }));
        services.AddSingleton<IObjectStore, S3ObjectStore>();
        return services;
    }
}
