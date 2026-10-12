using Amazon.Runtime;
using Amazon.S3;
using ImageAI.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ImageAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddObjectStorage(
        this IServiceCollection services,
        IConfiguration config
    )
    {
        services.Configure<StorageOptions>(config.GetSection("Storage"));
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var o = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            return new AmazonS3Client(
                new BasicAWSCredentials(o.AccessKey, o.SecretKey),
                new AmazonS3Config
                {
                    ServiceURL = o.Endpoint,
                    ForcePathStyle = true,
                    AuthenticationRegion = "us-east-1",
                }
            );
        });
        services.AddSingleton<ObjectStore>();
        return services;
    }
}
