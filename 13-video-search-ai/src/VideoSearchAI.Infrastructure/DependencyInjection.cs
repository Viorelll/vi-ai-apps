using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VideoSearchAI.Infrastructure.Storage;

namespace VideoSearchAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddStorage(
        this IServiceCollection services,
        IConfiguration config
    )
    {
        services
            .AddOptions<StorageOptions>()
            .Bind(config.GetSection("Storage"))
            .Validate(
                o =>
                    Uri.TryCreate(o.Endpoint, UriKind.Absolute, out _)
                    && o.Bucket.Length > 0
                    && o.AccessKey.Length > 0
                    && o.SecretKey.Length > 0,
                "storage_configuration_missing"
            )
            .ValidateOnStart();
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
