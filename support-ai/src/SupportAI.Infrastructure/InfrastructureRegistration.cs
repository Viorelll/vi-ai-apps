using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportAI.Application.Abstractions;
using SupportAI.Infrastructure.Ai;
using SupportAI.Infrastructure.Storage;
namespace SupportAI.Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(new BasicAWSCredentials(config["Storage:AccessKey"], config["Storage:SecretKey"]), new AmazonS3Config
        {
            ServiceURL = config["Storage:Endpoint"] ?? "http://localhost:9000",
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1"
        }));
        services.AddSingleton<IObjectStore, S3ObjectStore>();
        return services;
    }
}
