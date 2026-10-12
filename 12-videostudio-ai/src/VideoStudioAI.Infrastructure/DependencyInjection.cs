using Amazon.Runtime;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VideoStudioAI.Database;
using VideoStudioAI.Infrastructure.Http;
using VideoStudioAI.Infrastructure.Media;
using VideoStudioAI.Infrastructure.Storage;
using VideoStudioAI.Infrastructure.Workers;
using VideoStudioAI.Services;
using VideoStudioAI.Services.Interfaces;

namespace VideoStudioAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddVideoStudio(
        this IServiceCollection services,
        IConfiguration config,
        bool worker = true
    )
    {
        services.AddDbContextFactory<AppDbContext>(o =>
            o.UseNpgsql(config.GetConnectionString("App"))
        );
        services.AddHttpClient<IAiGateway, AiGatewayClient>(http =>
        {
            http.BaseAddress = new Uri(config["Ai:Endpoint"] ?? "http://localhost:5081/");
            http.Timeout = TimeSpan.FromSeconds(45);
            http.DefaultRequestHeaders.Add(
                "X-Internal-Key",
                config["Ai:InternalKey"] ?? "dummy-internal-key-replace-me"
            );
        });
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
        services.AddSingleton<IMediaValidator, MediaValidator>();
        services.AddScoped<ProjectService>();
        services.AddScoped<DocumentService>();
        services.AddTransient<JobLeaseService>();
        services.AddTransient<VideoJobProcessor>();
        services.AddTransient<IngestionProcessor>();
        if (worker)
            services.AddHostedService<DurableWorker>();
        return services;
    }
}
