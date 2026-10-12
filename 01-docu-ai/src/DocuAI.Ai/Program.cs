using DocuAI.Ai.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<ContentUnderstandingOptions>(
    builder.Configuration.GetSection(ContentUnderstandingOptions.SectionName));
builder.Services.Configure<InternalApiOptions>(
    builder.Configuration.GetSection(InternalApiOptions.SectionName));
builder.Services.AddHttpClient("content-understanding", client =>
    client.Timeout = TimeSpan.FromMinutes(3));
builder.Services.AddScoped<ContentUnderstandingService>();
builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
await app.RunAsync();
