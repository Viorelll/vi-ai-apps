using AccessReadAI.Infrastructure.Storage;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AccessReadAI.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddObjectStorage(
        this IServiceCollection services,
        IConfiguration c
    )
    {
        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
            new BasicAWSCredentials(c["Storage:AccessKey"], c["Storage:SecretKey"]),
            new AmazonS3Config
            {
                ServiceURL = c["Storage:Endpoint"],
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
            }
        ));
        services.AddSingleton<ObjectStore>();
        services.AddSingleton<IObjectStore>(sp => sp.GetRequiredService<ObjectStore>());
        return services;
    }
}
