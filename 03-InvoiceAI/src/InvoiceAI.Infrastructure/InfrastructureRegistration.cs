using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using InvoiceAI.Infrastructure.Ai;
using InvoiceAI.Infrastructure.Storage;
using InvoiceAI.Services.Abstractions;
namespace InvoiceAI.Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddObjectStorage(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<StorageOptions>().Bind(config.GetSection("Storage")).Validate(x => Uri.TryCreate(x.Endpoint, UriKind.Absolute, out _) && !string.IsNullOrWhiteSpace(x.Bucket), "Storage configuration invalid").ValidateOnStart();
        services.AddSingleton<IAmazonS3>(sp => { var o = sp.GetRequiredService<IOptions<StorageOptions>>().Value; return new AmazonS3Client(new BasicAWSCredentials(o.AccessKey, o.SecretKey), new AmazonS3Config { ServiceURL = o.Endpoint, ForcePathStyle = true, AuthenticationRegion = "us-east-1", Timeout = TimeSpan.FromSeconds(30) }); });
        services.AddSingleton<IObjectStore, S3ObjectStore>();
        return services;
    }
    public static IServiceCollection AddAiGateway(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<AiOptions>().Bind(config.GetSection("Ai")).ValidateOnStart();
        services.AddOptions<InternalOptions>().Bind(config.GetSection("Internal")).Validate(x => x.Key.Length >= 16, "Internal key needs 16 characters").ValidateOnStart();
        services.AddHttpClient<IAiGateway, HttpAiGateway>((sp, http) => { http.BaseAddress = new Uri(sp.GetRequiredService<IOptions<AiOptions>>().Value.BaseUrl); http.Timeout = TimeSpan.FromSeconds(60); http.DefaultRequestHeaders.Add("X-Internal-Key", sp.GetRequiredService<IOptions<InternalOptions>>().Value.Key); });
        return services;
    }
}
