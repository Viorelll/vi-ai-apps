using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModerateAI.Domain.Interfaces;

namespace ModerateAI.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddStorage(
        this IServiceCollection services,
        IConfiguration config
    )
    {
        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
            new BasicAWSCredentials(
                config["Storage:AccessKey"]
                    ?? throw new InvalidOperationException("Storage key missing"),
                config["Storage:SecretKey"]
                    ?? throw new InvalidOperationException("Storage secret missing")
            ),
            new AmazonS3Config
            {
                ServiceURL =
                    config["Storage:Endpoint"]
                    ?? throw new InvalidOperationException("Storage endpoint missing"),
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
            }
        ));
        services.AddSingleton<IObjectStore, S3ObjectStore>();
        return services;
    }
}
